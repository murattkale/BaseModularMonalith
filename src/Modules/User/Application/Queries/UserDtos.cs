namespace User.Application.Queries;

/// <summary>
/// User read DTO - sadece gerekli alanlar (minimal allocation).
/// </summary>
public readonly record struct UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    long Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>
/// User list DTO - daha az alan (bandwidth optimization).
/// </summary>
public readonly record struct UserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    long Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>
/// Login result DTO.
/// </summary>
public readonly record struct LoginResultDto(
    Guid UserId,
    string Email,
    string FullName,
    string Token);
