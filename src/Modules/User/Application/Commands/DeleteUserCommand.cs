using BuildingBlocks.CQRS;

namespace User.Application.Commands;

/// <summary>
/// User silme command'ı (soft delete).
/// </summary>
public sealed record DeleteUserCommand(Guid Id) : ICommand;
