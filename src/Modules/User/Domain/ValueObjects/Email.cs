using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace User.Domain.ValueObjects;

/// <summary>
/// Email Değer Nesnesi (Value Object) - Değişmez (immutable), doğrulanmış, sıfır bellek maliyetli karşılaştırma.
/// </summary>
public readonly partial struct Email : IEquatable<Email>
{
    // Compile-time regex (source generator)
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    private readonly string _value;

    private Email(string value) => _value = value.ToLowerInvariant();

    /// <summary>
    /// Email adresinin tam metin değeri.
    /// </summary>
    public string Value => _value;

    /// <summary>
    /// Email oluşturur - validation ile.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCreate(ReadOnlySpan<char> email, [NotNullWhen(true)] out Email? result)
    {
        result = null;
        
        if (email.IsEmpty || email.Length > 254)
            return false;

        // .NET 7+ IsMatch(ReadOnlySpan<char>) kullanımı
        if (!EmailRegex().IsMatch(email))
            return false;

        result = new Email(new string(email));
        return true;
    }

    /// <summary>
    /// Email oluşturur - exception fırlatır.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Email Create(string email)
    {
        if (!TryCreate(email.AsSpan(), out var result))
            throw new ArgumentException("Geçersiz email formatı.", nameof(email));
        
        return result.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Email other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Email other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public override string ToString() => _value;

    /// <summary> İki email adresinin eşitliğini kontrol eder. </summary>
    public static bool operator ==(Email left, Email right) => left.Equals(right);
    /// <summary> İki email adresinin eşitsizliğini kontrol eder. </summary>
    public static bool operator !=(Email left, Email right) => !left.Equals(right);

    public static implicit operator string(Email email) => email._value;
}
