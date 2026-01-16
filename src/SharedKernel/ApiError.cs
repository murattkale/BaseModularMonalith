namespace SharedKernel;

/// <summary>
/// API hata yanıtı.
/// Standartlaştırılmış hata formatı sağlar.
/// </summary>
public sealed record ApiError(string Code, string Message, string? TraceId = null);
