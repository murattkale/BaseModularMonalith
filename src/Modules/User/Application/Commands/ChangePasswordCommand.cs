using BuildingBlocks.CQRS;

namespace User.Application.Commands;

/// <summary>
/// Şifre değiştirme command'ı.
/// </summary>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : ICommand;
