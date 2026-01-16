using System.Text.RegularExpressions;

namespace SharedKernel;

/// <summary>
/// Kişisel verilerin loglanmadan önce maskelenmesini sağlayan yardımcı sınıf.
/// </summary>
public static class PiiScrubber
{
    private static readonly Regex EmailRegex = new(@"[^@\s]+@[^@\s]+\.[^@\s]+", RegexOptions.Compiled);

    /// <summary>
    /// Email adreslerini m***@domain.com şeklinde maskeler.
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return string.Empty;

        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***";

        var name = parts[0];
        var domain = parts[1];

        if (name.Length <= 1) return "*@" + domain;

        return name[0] + new string('*', name.Length - 1) + "@" + domain;
    }

    /// <summary>
    /// Tüm string içindeki email'leri bulup maskeler.
    /// </summary>
    public static string MaskAllEmails(string input)
    {
        return EmailRegex.Replace(input, match => MaskEmail(match.Value));
    }
}
