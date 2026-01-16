using MediatR;
using SharedKernel;

namespace BuildingBlocks.CQRS;

/// <summary>
/// Query handler arayüzü.
/// Query'ler read-only olmalı ve state değiştirmemelidir.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
