namespace SharedKernel;

/// <summary>
/// Tüm domain entity'leri için temel sınıf.
/// Kimlik karşılaştırması, domain event desteği ve soft delete sağlar.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Varlığın benzersiz kimliği.
    /// </summary>
    public Guid Id { get; protected init; }

    /// <summary>
    /// Varlığın oluşturulma tarihi (UTC).
    /// </summary>
    public DateTime CreatedAt { get; protected init; } = DateTime.UtcNow;

    /// <summary>
    /// Varlığın son güncellenme tarihi (UTC).
    /// </summary>
    public DateTime? UpdatedAt { get; protected set; }
    
    /// <summary>
    /// Soft delete için silinme tarihi.
    /// Null ise kayıt aktif, değer varsa silinmiş demektir.
    /// </summary>
    public DateTime? DeletedAt { get; protected set; }

    /// <summary>
    /// Kayıt silinmiş mi? (soft delete kontrolü)
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>
    /// Bekleyen domain event'leri (henüz dispatch edilmemiş).
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Yeni bir domain event ekler.
    /// Event'ler SaveChanges sonrası in-memory olarak dispatch edilir.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Tüm domain event'leri temizler.
    /// Dispatch işleminden sonra çağrılmalıdır.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Entity'yi soft delete olarak işaretler.
    /// </summary>
    public virtual void MarkAsDeleted()
    {
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft delete'i geri alır.
    /// </summary>
    public virtual void Restore()
    {
        DeletedAt = null;
    }

    /// <summary>
    /// Belirtilen nesnenin mevcut varlığa eşit olup olmadığını belirler.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    /// <summary>
    /// Varlık için hash kodunu döner.
    /// </summary>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// İki varlığın eşitliğini kontrol eder.
    /// </summary>
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// İki varlığın eşitsizliğini kontrol eder.
    /// </summary>
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
