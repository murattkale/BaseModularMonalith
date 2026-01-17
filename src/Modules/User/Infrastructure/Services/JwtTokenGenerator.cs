using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using User.Application.Commands;

namespace User.Infrastructure.Services;

/// <summary>
/// High-performance JWT token generator.
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;
    private readonly int _expirationMinutes;

    // Static cache - avoid creating per request
    private static readonly JwtSecurityTokenHandler TokenHandler = new()
    {
        SetDefaultTimesOnTokenCreation = false
    };

    private static readonly string[] RoleNames = Enum.GetNames<User.Domain.ValueObjects.UserRoles>();
    private static readonly User.Domain.ValueObjects.UserRoles[] RoleValues = Enum.GetValues<User.Domain.ValueObjects.UserRoles>();

    public JwtTokenGenerator(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        _issuer = jwtSection["Issuer"] ?? "BaseModularMonolith";
        _audience = jwtSection["Audience"] ?? "BaseModularMonolith";
        _expirationMinutes = int.TryParse(jwtSection["ExpirationMinutes"], out var exp) ? exp : 60;

        // RSA support
        var publicKeyPath = jwtSection["PublicKeyPath"];
        var privateKeyPath = jwtSection["PrivateKeyPath"]; // Private key sadece token üretmek için gereklidir (Auth sunucusu)
        
        // PRODUCTION: RSA Private Key ile imzala
        if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
        {
             var rsa = System.Security.Cryptography.RSA.Create();
             rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
             _signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        }
        else 
        {
            // DEVELOPMENT: Simetrik anahtar fallback (veya otomatik RSA üretimi yapılabilir)
            // Not: Production'da kesinlikle RSA kullanılmalı.
            var secretKey = jwtSection["SecretKey"] 
                ?? throw new InvalidOperationException("JWT SecretKey or PrivateKeyPath not configured.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            
            // Eğer config'de RS256 isteniyorsa ama key yoksa hata fırlatılabilir veya HMAC ile devam edilir.
            // Burada HMAC-SHA256 ile devam ediyoruz.
            _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        }
    }

    public string GenerateToken(Guid userId, string email, string fullName, long roles)
    {
        var nowOffset = DateTimeOffset.UtcNow;
        var now = nowOffset.UtcDateTime;
        var nowUnix = nowOffset.ToUnixTimeSeconds();
        var expires = now.AddMinutes(_expirationMinutes);

        // Pre-allocate list capacity (5 default claims + roles) to avoid resizing
        var claims = new List<Claim>(10)
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, nowUnix.ToString(), ClaimValueTypes.Integer64)
        };

        // Bitwise rolleri Claims'e ekle (Cached enum names avoid reflection parsing)
        for (int i = 0; i < RoleNames.Length; i++)
        {
            var roleName = RoleNames[i];
            if (roleName == "None" || roleName == "All") continue;
            
            var roleValue = RoleValues[i];
            if ((roles & (long)roleValue) == (long)roleValue)
            {
                claims.Add(new Claim(ClaimTypes.Role, roleName));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials);

        return TokenHandler.WriteToken(token);
    }
}
