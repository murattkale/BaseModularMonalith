namespace SharedKernel;

/// <summary>
/// Sistemdeki kritik işlemlerin kalıcı günlüğü.
/// </summary>
public sealed class AuditLog
{
    /// <summary>
    /// Log kaydının benzersiz kimliği.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// İşlemi yapan kullanıcının kimliği.
    /// </summary>
    public string UserId { get; init; } = "Anonymous";

    /// <summary>
    /// Gerçekleştirilen işlem adı.
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// İşleme ait detaylı bilgiler (JSON formatında olabilir).
    /// </summary>
    public string Details { get; init; } = string.Empty;

    /// <summary>
    /// İşlemin yapıldığı IP adresi.
    /// </summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>
    /// İşlemin yapıldığı tarayıcı/istemci bilgisi.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;

    /// <summary>
    /// Kaydın oluşturulma zamanı (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
