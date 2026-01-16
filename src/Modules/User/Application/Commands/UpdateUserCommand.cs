using BuildingBlocks.CQRS;

namespace User.Application.Commands;

/// <summary>
/// User profil güncelleme command'ı.
/// </summary>
public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName) : ICommand;
