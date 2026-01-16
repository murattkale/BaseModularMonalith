using MediatR;
using Microsoft.Extensions.Logging;
using BuildingBlocks.CQRS;
using System.Runtime.CompilerServices;
using SharedKernel;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// Statik metadata cache. Reflection maliyetini sıfıra indirir.
/// </summary>
internal static class RequestMetadata<TRequest>
{
    public static readonly bool IsCommand = typeof(ICommand).IsAssignableFrom(typeof(TRequest));
    public static readonly string Name = typeof(TRequest).Name;
}

/// <summary>
/// Transaction yönetimi için MediatR pipeline behavior'ı.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// TransactionBehavior sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="unitOfWork">İş birimi (Unit of Work) yönetimi sağlar.</param>
    /// <param name="logger">Loglama işlemlerini gerçekleştirir.</param>
    public TransactionBehavior(IUnitOfWork unitOfWork, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Command türündeki istekleri bir veritabanı işlemi (transaction) içinde sarar.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Sıfır maliyetli metadata kontrolü
        if (!RequestMetadata<TRequest>.IsCommand)
        {
            return await next();
        }

        return await _unitOfWork.ExecuteStrategyAsync(async ct =>
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(ct);
                
                var response = await next();
                
                await _unitOfWork.CommitTransactionAsync(ct);

                return response;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                _logger.LogError(ex, "[Transaction Başarısız] {RequestName}", RequestMetadata<TRequest>.Name);
                throw;
            }
        }, cancellationToken);
    }
}
