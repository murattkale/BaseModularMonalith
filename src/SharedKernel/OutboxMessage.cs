namespace SharedKernel;

/// <summary>
/// Domain event'lerin DB transaction ile güvenli kaydedilmesi için kullanılan Outbox modeli.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Mesajın benzersiz kimliği.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Olayın (event) tipi.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Olayın serileştirilmiş içeriği (JSON).
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Oluşturulma zamanı (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// İşlenme zamanı (UTC). Null ise henüz işlenmemiştir.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>
    /// İşleme sırasında oluşan hata mesajı.
    /// </summary>
    public string? Error { get; set; }
}
