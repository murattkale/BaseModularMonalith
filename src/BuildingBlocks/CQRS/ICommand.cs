using MediatR;
using SharedKernel;

namespace BuildingBlocks.CQRS;

/// <summary>
/// Tüm command'lar (yazma işlemleri) için işaret arayüzü.
/// Command'lar state değiştirir ve EF Core ile explicit transaction kullanır.
/// </summary>
public interface ICommand : IRequest<Result>;

/// <summary>
/// Değer döndüren command arayüzü.
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
