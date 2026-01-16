namespace User.Domain.ValueObjects;

/// <summary>
/// Bitwise yetki sistemi.
/// Her yetki bir bit'i temsil eder. 
/// Bu sayede tek bir int ile 32, long ile 64 farklı yetki tutulabilir.
/// </summary>
[Flags]
public enum UserRoles : long
{
    None = 0,
    User = 1 << 0,          // 1
    Admin = 1 << 1,         // 2
    Moderator = 1 << 2,     // 4
    SuperAdmin = 1 << 62,   // Çok yüksek yetki
    
    // Kompleks yetki setleri
    All = -1                // Tüm bitler 1
}
