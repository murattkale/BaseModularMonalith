namespace SharedKernel;

/// <summary>
/// Sayfalanmış sonuç wrapper'ı.
/// </summary>
/// <typeparam name="T">Sonuç tipi.</typeparam>
/// <param name="Items">Kayıt listesi.</param>
/// <param name="TotalCount">Toplam kayıt sayısı.</param>
/// <param name="Page">Mevcut sayfa.</param>
/// <param name="PageSize">Sayfa boyutu.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Toplam sayfa sayısı.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    /// <summary>
    /// Sonraki sayfa var mı?
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
    
    /// <summary>
    /// Önceki sayfa var mı?
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
