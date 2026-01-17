using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace User.Domain.ValueObjects;

/// <summary>
/// Şifrelenmiş Parola Değer Nesnesi - Argon2id kullanarak GPU/ASIC tabanlı saldırılara karşı maksimum güvenlik sağlar.
/// Modern OWASP standartlarına uygundur.
/// </summary>
public readonly struct HashedPassword : IEquatable<HashedPassword>
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Argon2id Parametreleri (OWASP Safe Defaults - 2024/2025)
    // 64 MB Memory, 4 Iterations, 4 Parallelism
    private const int MemorySize = 65536; 
    private const int Iterations = 4;
    private const int Parallelism = 4;

    private readonly byte[] _hash;
    private readonly byte[] _salt;

    private HashedPassword(byte[] hash, byte[] salt)
    {
        _hash = hash;
        _salt = salt;
    }

    /// <summary>
    /// Yeni parolayı şifreler (Yoğun işlem, Thread Pool'da çalıştırılır).
    /// </summary>
    public static async ValueTask<HashedPassword> CreateAsync(string password, CancellationToken ct = default)
    {
        return await Task.Run(() => CreateSync(password), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Senkron versiyon - sadece test/internal kullanım için.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashedPassword Create(ReadOnlySpan<char> password)
    {
        return CreateSync(password.ToString());
    }

    private static HashedPassword CreateSync(string password)
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var hash = ComputeArgon2id(password, salt);
        return new HashedPassword(hash, salt);
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
    /// Parolayı doğrular.
    /// </summary>
    public async ValueTask<bool> VerifyAsync(string password, CancellationToken ct = default)
    {
        var salt = _salt;
        var hash = _hash;

        return await Task.Run(() =>
        {
            var computedHash = ComputeArgon2id(password, salt);
            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Senkron doğrulama.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Verify(ReadOnlySpan<char> password)
    {
        var computedHash = ComputeArgon2id(password.ToString(), _salt);
        return CryptographicOperations.FixedTimeEquals(computedHash, _hash);
    }

    private static byte[] ComputeArgon2id(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.DegreeOfParallelism = Parallelism;
        argon2.Iterations = Iterations;
        argon2.MemorySize = MemorySize;

        return argon2.GetBytes(HashSize);
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
