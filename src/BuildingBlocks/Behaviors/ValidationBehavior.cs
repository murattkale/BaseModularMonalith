using FluentValidation;
using MediatR;
using SharedKernel;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// Fail-fast doğrulama için MediatR pipeline behavior'ı.
/// Handler çalışmadan önce tüm validator'ları çalıştırır.
/// Exception fırlatmak yerine Result.Failure döndürür (performans için).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest>[] _validators;
    
    // Tip bilgisi cache - her request'te reflection çalışmasın
    private static readonly bool IsResultType;
    private static readonly bool IsGenericResultType;
    private static readonly Func<Error, TResponse>? CreateFailureResult;

    static ValidationBehavior()
    {
        var responseType = typeof(TResponse);
        IsResultType = responseType == typeof(Result);
        
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            IsGenericResultType = true;
            var resultType = responseType.GetGenericArguments()[0];
            
            // Expression Trees ile compiled delegate oluştur (Invoke maliyetinden kurtuluruz)
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
    /// ValidationBehavior sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="validators">Kullanılacak doğrulayıcılar listesi.</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        // Bir kez diziye dönüştür - tekrarlı sayma ve Count() çağrılarını önler
        _validators = validators as IValidator<TRequest>[] ?? validators.ToArray();
    }

    /// <summary>
    /// İsteği doğrular ve hatalar varsa bir başarısızlık sonucu döner.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // 1. Validator yoksa hızlı çık
        if (_validators.Length == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        // 2. Tüm validator'ları paralel çalıştır (pre-allocated array)
        var validationTasks = new Task<FluentValidation.Results.ValidationResult>[_validators.Length];
        for (var i = 0; i < _validators.Length; i++)
        {
            validationTasks[i] = _validators[i].ValidateAsync(context, cancellationToken);
        }

        var validationResults = await Task.WhenAll(validationTasks);

        // 3. Hata var mı kontrolü (Hızlı tarama - early exit)
        var hasError = false;
        for (var j = 0; j < validationResults.Length; j++)
        {
            if (!validationResults[j].IsValid)
            {
                hasError = true;
                break;
            }
        }

        if (!hasError)
            return await next();

        // 4. Hataları topla (Sadece hata varsa bu maliyete giriyoruz)
        var sb = new System.Text.StringBuilder();
        for (var j = 0; j < validationResults.Length; j++)
        {
            var result = validationResults[j];
            if (result.IsValid) continue;

            var errors = result.Errors;
            for (var k = 0; k < errors.Count; k++)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(errors[k].ErrorMessage);
            }
        }

        var error = new Error("Validation.Failed", sb.ToString());

        // Cache'lenmiş tip bilgisi ile hızlı dönüş
        if (IsResultType)
        {
            return (TResponse)(object)Result.Failure(error);
        }

        if (IsGenericResultType && CreateFailureResult is not null)
        {
            return CreateFailureResult(error);
        }

        // Result tipi değilse exception fırlat
        var allFailures = validationResults.SelectMany(r => r.Errors).ToList();
        throw new ValidationException(allFailures);
    }
}
