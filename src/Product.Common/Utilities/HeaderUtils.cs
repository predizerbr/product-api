using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace Product.Common.Utilities;

public static class HeaderUtils
{
    public static string? ExtractSignatureFromHeaders(string? headers)
    //Aqui facilita a extração do header de assinatura de webhooks
    {
        if (string.IsNullOrWhiteSpace(headers))
            return null;

        var parts = headers.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var keys = new[]
        {
            "x-hub-signature",
            "x-hub-signature-256",
            "x-callback-signature",
            "x-mercadopago-signature",
            "x-mp-signature",
            "signature",
            "x-signature",
        };

        foreach (var p in parts)
        {
            var idx = p.IndexOf(':');
            if (idx <= 0)
                continue;
            var k = p.Substring(0, idx).Trim();
            var v = p.Substring(idx + 1).Trim();
            if (keys.Any(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase)))
                return v;
        }

        return null;
    }

    public static string FormatHeaders(IEnumerable<KeyValuePair<string, StringValues>> headers)
    {
        if (headers is null)
            return string.Empty;

        try
        {
            return string.Join(
                ";",
                headers.Select(static h => $"{h.Key}:{string.Join(',', h.Value!)}")
            );
        }
        catch
        {
            return string.Empty;
        }
    }
}
