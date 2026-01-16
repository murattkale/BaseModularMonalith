using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SharedKernel;

/// <summary>
/// Result pattern implementasyonu - Readonly Struct ile Zero Allocation.
/// </summary>
public readonly struct Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// İşlemin başarılı olup olmadığını belirtir.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// İşlemin başarısız olup olmadığını belirtir.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// İşlem başarısız ise hata detaylarını döner.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Başarılı bir sonuç oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Success() => new(true, Error.None);
    
    /// <summary>
    /// Belirtilen hata ile başarısız bir sonuç oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Değer içeren başarılı bir sonuç oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    
    /// <summary>
    /// Değer içermesi beklenen ancak başarısız olan bir sonuç oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

/// <summary>
/// Generic Result - Readonly Struct.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(T? value, bool isSuccess, Error error)
    {
        _value = value;
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// İşlemin başarılı olup olmadığını belirtir.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// İşlemin başarısız olup olmadığını belirtir.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// İşlem başarısız ise hata detaylarını döner.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Başarılı sonuçtaki değere erişir. Başarısız ise hata fırlatır.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Başarısız sonucun değerine erişilemez. Hata: {Error.Code}");

    /// <summary>
    /// Belirtilen değer ile başarılı bir sonuç oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Success(T value) => new(value, true, Error.None);
    
    /// <summary>
    /// Belirtilen hata ile başarısız bir sonuç oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T> Failure(Error error) => new(default, false, error);

    /// <summary>
    /// Değeri otomatik olarak başarılı bir sonuca dönüştürür.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T>(T value) => Success(value);
}

/// <summary>
/// Hata detaylarını içeren record.
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>Hata yok.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
    /// <summary>Null değer hatası.</summary>
    public static readonly Error NullValue = new("Error.NullValue", "Null değer sağlandı.");
    /// <summary>Bulunamadı hatası.</summary>
    public static readonly Error NotFound = new("Error.NotFound", "İstenen kaynak bulunamadı.");
    /// <summary>Doğrulama hatası.</summary>
    public static readonly Error Validation = new("Error.Validation", "Doğrulama hatası oluştu.");
    /// <summary>Çakışma hatası.</summary>
    public static readonly Error Conflict = new("Error.Conflict", "Çakışma hatası oluştu.");
    /// <summary>Yetkisiz erişim hatası.</summary>
    public static readonly Error Unauthorized = new("Error.Unauthorized", "Yetkisiz erişim.");
    /// <summary>Erişim yasaklandı hatası.</summary>
    public static readonly Error Forbidden = new("Error.Forbidden", "Erişim yasaklandı.");
}
