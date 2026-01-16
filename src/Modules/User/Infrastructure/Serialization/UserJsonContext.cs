using System.Text.Json.Serialization;
using User.Domain.Events;
using User.Application.Queries;

namespace User.Infrastructure.Serialization;

/// <summary>
/// User modülü için özel JSON context (Source Generated).
/// Native AOT ve yüksek performans için gereklidir.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UserCreatedEvent))]
[JsonSerializable(typeof(UserLoggedInEvent))]
[JsonSerializable(typeof(UserPasswordChangedEvent))]
[JsonSerializable(typeof(UserProfileUpdatedEvent))]
[JsonSerializable(typeof(UserDeactivatedEvent))]
[JsonSerializable(typeof(UserActivatedEvent))]
[JsonSerializable(typeof(UserDeletedEvent))]
[JsonSerializable(typeof(UserDto))]
public partial class UserJsonContext : JsonSerializerContext
{
}
