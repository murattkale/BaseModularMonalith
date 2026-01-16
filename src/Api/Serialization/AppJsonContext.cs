using System.Text.Json.Serialization;
using User.Application.Queries;
using User.Api;
using SharedKernel;
using User.Domain.Events;

namespace Api.Serialization;

/// <summary>
/// Source-generated JSON context for extreme performance.
/// Reflection-free serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserListItemDto))]
[JsonSerializable(typeof(PagedResult<UserListItemDto>))]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(LoginResultDto))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(UpdateUserRequest))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(UserCreatedEvent))]
[JsonSerializable(typeof(UserLoggedInEvent))]
[JsonSerializable(typeof(UserPasswordChangedEvent))]
[JsonSerializable(typeof(UserProfileUpdatedEvent))]
[JsonSerializable(typeof(UserDeactivatedEvent))]
[JsonSerializable(typeof(UserActivatedEvent))]
[JsonSerializable(typeof(UserDeletedEvent))]
public partial class AppJsonContext : JsonSerializerContext
{
}
