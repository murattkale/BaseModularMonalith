using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace User.Domain.ValueObjects;

/// <summary>
/// Şifrelenmiş Parola Değer Nesnesi - PBKDF2-SHA512 kullanarak zaman tabanlı saldırılara (timing attack) karşı güvenli karşılaştırma sağlar.
/// </summary>
public readonly struct HashedPassword : IEquatable<HashedPassword>
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    // ÜRETİM: Xeon Platinum performansı ve güvenlik dengesi için önerilen değer
    private const int Iterations = 10_000;

    private readonly byte[] _hash;
    private readonly byte[] _salt;

    private HashedPassword(byte[] hash, byte[] salt)
    {
        _hash = hash;
        _salt = salt;
    }

    /// <summary>
    /// Yeni parolayı şifreler (İşlemci yoğunluklu çalışma thread pool'a aktarılır).
    /// </summary>
    public static async ValueTask<HashedPassword> CreateAsync(string password, CancellationToken ct = default)
    {
        return await Task.Run(() => CreateSync(password.AsSpan()), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Senkron versiyon - sadece internal kullanım için.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashedPassword Create(ReadOnlySpan<char> password)
    {
        return CreateSync(password);
    }

    private static HashedPassword CreateSync(ReadOnlySpan<char> password)
    {
        Span<byte> salt = stackalloc byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var hash = DeriveKey(password, salt);
        return new HashedPassword(hash, salt.ToArray());
    }

    /// <summary>
    /// DB'den yükler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashedPassword FromStorage(byte[] hash, byte[] salt)
    {
        return new HashedPassword(hash, salt);
    }

    /// <summary>
    /// Parolayı doğrular - sabit zamanlı karşılaştırma (timing attack koruması).
    /// İşlemci yoğunluklu çalışma thread pool'ere aktarılır.
    /// </summary>
    public async ValueTask<bool> VerifyAsync(string password, CancellationToken ct = default)
    {
        var salt = _salt;
        var hash = _hash;
        return await Task.Run(() =>
        {
            var computedHash = DeriveKey(password.AsSpan(), salt);
            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Senkron doğrulama - geriye uyumluluk için.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Verify(ReadOnlySpan<char> password)
    {
        var computedHash = DeriveKey(password, _salt);
        return CryptographicOperations.FixedTimeEquals(computedHash, _hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] DeriveKey(ReadOnlySpan<char> password, ReadOnlySpan<byte> salt)
    {
        byte[] hash = new byte[HashSize];
        Rfc2898DeriveBytes.Pbkdf2(password, salt, hash, Iterations, HashAlgorithmName.SHA512);
        return hash;
    }

    /// <summary> Şifrelenmiş veri (hash). </summary>
    public byte[] Hash => _hash;
    /// <summary> Şifreleme sırasında kullanılan rastgele veri (salt). </summary>
    public byte[] Salt => _salt;

    public bool Equals(HashedPassword other) =>
        CryptographicOperations.FixedTimeEquals(_hash, other._hash) &&
        CryptographicOperations.FixedTimeEquals(_salt, other._salt);

    public override bool Equals(object? obj) => obj is HashedPassword other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_hash?.Length ?? 0, _salt?.Length ?? 0);

    public static bool operator ==(HashedPassword left, HashedPassword right) => left.Equals(right);
    public static bool operator !=(HashedPassword left, HashedPassword right) => !left.Equals(right);
}
