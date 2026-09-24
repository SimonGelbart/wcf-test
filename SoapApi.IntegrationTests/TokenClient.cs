using System.Text.Json;

internal sealed class TokenClient(HttpClient http, string tokenUrl, string clientId, string clientSecret, string? audience)
{
    public async Task<string> RequestTokenAsync(string scope)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };
        if (!string.IsNullOrWhiteSpace(scope)) fields["scope"] = scope;
        if (!string.IsNullOrWhiteSpace(audience)) fields["audience"] = audience;

        using var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(fields));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token endpoint returned HTTP {(int)response.StatusCode} (requested scope: '{scope}').");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var accessToken) || accessToken.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(accessToken.GetString()) ||
            !root.TryGetProperty("token_type", out var tokenType) ||
            !string.Equals(tokenType.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Token endpoint did not return a bearer access_token.");

        return accessToken.GetString()!;
    }

    // Inspect only the test token's scope claims. The SOAP service validates signature, issuer and audience.
    public static bool HasScope(string jwt, string requiredScope)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) throw new InvalidOperationException("Authentication server returned a non-JWT access token.");
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            foreach (var claimName in new[] { "scope", "scp" })
            {
                if (!document.RootElement.TryGetProperty(claimName, out var claim)) continue;
                if (claim.ValueKind == JsonValueKind.String &&
                    claim.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(requiredScope, StringComparer.Ordinal))
                    return true;
                if (claim.ValueKind == JsonValueKind.Array && claim.EnumerateArray().Any(value =>
                        value.ValueKind == JsonValueKind.String && value.GetString() == requiredScope))
                    return true;
            }
            return false;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Authentication server returned an invalid JWT payload.");
        }
    }
}
