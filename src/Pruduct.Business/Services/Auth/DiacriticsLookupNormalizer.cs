using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Pruduct.Business.Services.Auth;

public sealed class DiacriticsLookupNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name) => NormalizeValue(name);

    public string? NormalizeEmail(string? email) => NormalizeValue(email);

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }
}
