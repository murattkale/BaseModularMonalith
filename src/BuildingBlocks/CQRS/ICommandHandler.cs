using MediatR;
using SharedKernel;

namespace BuildingBlocks.CQRS;

/// <summary>
/// Değer döndürmeyen command handler arayüzü.
/// </summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>
/// Değer döndüren command handler arayüzü.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
