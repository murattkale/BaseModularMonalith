namespace SharedKernel;

/// <summary>
/// Idempotency (tekrar önleme) kaydı.
/// </summary>
public sealed class IdempotentRequest
{
    /// <summary>
    /// Talebin benzersiz kimliği.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// İşlem adı.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kaydın oluşturulma zamanı.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
