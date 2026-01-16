using MediatR;
using SharedKernel;

namespace BuildingBlocks.CQRS;

/// <summary>
/// Tüm query'ler (okuma işlemleri) için işaret arayüzü.
/// Query'ler yüksek performans için Dapper kullanır.
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
