using BuildingBlocks.CQRS;
using SharedKernel;

namespace User.Application.Commands;

/// <summary>
/// Kullanıcı oluşturma isteği (Command).
/// </summary>
/// <param name="RequestId">İsteğin benzersiz kimliği (Idempotency için).</param>
/// <param name="Email">Kullanıcının e-posta adresi.</param>
/// <param name="Password">Kullanıcının şifresi.</param>
/// <param name="FirstName">Kullanıcının adı.</param>
/// <param name="LastName">Kullanıcının soyadı.</param>
public sealed record CreateUserCommand(
    Guid RequestId,
    string Email,
    string Password,
    string FirstName,
    string LastName) : ICommand<Guid>, IIdempotentCommand, IAuditable;
