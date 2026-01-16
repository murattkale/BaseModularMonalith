using BuildingBlocks.CQRS;
using MediatR;
using SharedKernel;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// IIdempotentCommand arayüzünü uygulayan isteklerde tekrar önleme (Idempotency) sağlar.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly TResponse? CachedSuccessResponse;
    private static readonly Func<TResponse>? CreateSuccessResult;
    private static readonly Func<Error, TResponse>? CreateFailureResult;
    private readonly IIdempotencyService _idempotencyService;

    /// <summary>
    /// IdempotencyBehavior sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="idempotencyService">Idempotency kontrolü yapan servis.</param>
    public IdempotencyBehavior(IIdempotencyService idempotencyService)
    {
        _idempotencyService = idempotencyService;
    }

    static IdempotencyBehavior()
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            CachedSuccessResponse = (TResponse)(object)Result.Success();
            return;
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = responseType.GetGenericArguments()[0];
            
            // Expression Trees for Success
            var successMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(Result.Success), [resultType]);

            if (successMethod is not null)
            {
                var defaultValue = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                var body = System.Linq.Expressions.Expression.Call(null, successMethod, System.Linq.Expressions.Expression.Constant(defaultValue, resultType));
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<TResponse>>(body);
                CreateSuccessResult = lambda.Compile();
                CachedSuccessResponse = CreateSuccessResult();
            }

            // Expression Trees for Failure
            var errorParam = System.Linq.Expressions.Expression.Parameter(typeof(Error), "error");
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(Result.Failure), [typeof(Error)]);

            if (failureMethod is not null)
            {
                var body = System.Linq.Expressions.Expression.Call(null, failureMethod, errorParam);
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<Error, TResponse>>(body, errorParam);
                CreateFailureResult = lambda.Compile();
            }
        }
    }

    /// <summary>
    /// İstek işlenmeden önce mükerrer kontrolü yapar.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Eğer idempotent bir command değilse direkt devam et
        if (request is not IIdempotentCommand idempotentRequest)
        {
            return await next();
        }

        if (idempotentRequest.RequestId == Guid.Empty)
        {
            return await next();
        }

        // İstek daha önce işlenmiş mi?
        if (await _idempotencyService.ExistsAsync(idempotentRequest.RequestId, cancellationToken))
        {
            if (CachedSuccessResponse is not null)
            {
                return CachedSuccessResponse;
            }

            // Fallback: Reflection yerine compiled delegate'leri kullan
            if (typeof(TResponse) == typeof(Result))
            {
                return (TResponse)(object)Result.Success();
            }

            if (CreateSuccessResult is not null)
            {
                return CreateSuccessResult();
            }
            
            // Hata durumu (Conflict)
            if (typeof(TResponse) == typeof(Result))
            {
                return (TResponse)(object)Result.Failure(Error.Conflict);
            }

            if (CreateFailureResult is not null)
            {
                return CreateFailureResult(Error.Conflict);
            }

            throw new InvalidOperationException("Idempotency error: TResponse type is not supported.");
        }

        // İşlemi kaydet
        await _idempotencyService.CreateAsync(
            idempotentRequest.RequestId, 
            typeof(TRequest).Name, 
            cancellationToken);

        return await next();
    }
}
