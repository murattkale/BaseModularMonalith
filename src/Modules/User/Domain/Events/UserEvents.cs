using SharedKernel;

namespace User.Domain.Events;

/// <summary>
/// User domain events.
/// </summary>
public sealed record UserCreatedEvent(Guid UserId, string Email) : DomainEventBase;
public sealed record UserLoggedInEvent(Guid UserId, string Email) : DomainEventBase;
public sealed record UserPasswordChangedEvent(Guid UserId) : DomainEventBase;
public sealed record UserProfileUpdatedEvent(Guid UserId) : DomainEventBase;
public sealed record UserDeactivatedEvent(Guid UserId) : DomainEventBase;
public sealed record UserActivatedEvent(Guid UserId) : DomainEventBase;
public sealed record UserDeletedEvent(Guid UserId, string Email) : DomainEventBase;
