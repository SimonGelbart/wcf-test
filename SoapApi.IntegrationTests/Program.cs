using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

internal static class Program
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Contract = "urn:example:greeting:v1";

    private static async Task<int> Main()
    {
        try
        {
            var baseUrl = Required("SOAP_TEST_BASE_URL").TrimEnd('/');
            var tokenUrl = Required("SOAP_TEST_TOKEN_URL");
            var clientId = Required("SOAP_TEST_CLIENT_ID");
            var clientSecret = Required("SOAP_TEST_CLIENT_SECRET");
            var scopedScope = Environment.GetEnvironmentVariable("SOAP_TEST_SCOPED_SCOPE") ?? "greeting.read";
            var authOnlyScope = Environment.GetEnvironmentVariable("SOAP_TEST_AUTH_ONLY_SCOPE") ?? "";
            var audience = Environment.GetEnvironmentVariable("SOAP_TEST_AUDIENCE");

            RequireHttps(baseUrl, "SOAP_TEST_BASE_URL");
            RequireHttps(tokenUrl, "SOAP_TEST_TOKEN_URL");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var scopedToken = await RequestToken(http, tokenUrl, clientId, clientSecret, scopedScope, audience);
            if (!HasScope(scopedToken, "greeting.read"))
                throw new InvalidOperationException("The scoped access token is a JWT without greeting.read. Configure the authentication server to issue that scope.");

            var authOnlyToken = await RequestToken(http, tokenUrl, clientId, clientSecret, authOnlyScope, audience);
            if (HasScope(authOnlyToken, "greeting.read"))
                throw new InvalidOperationException("The JWT-only token contains greeting.read. Set SOAP_TEST_AUTH_ONLY_SCOPE to a scope that does not grant it (or configure the server's default scopes).");

            var protectedUrl = baseUrl + "/Services/Greeting.svc";
            var publicUrl = baseUrl + "/Services/PublicGreeting.svc";

            await ExpectGreeting(http, publicUrl, "IPublicGreetingService", "GreetPublic", null);
            Console.WriteLine("PASS public operation without a token");

            await ExpectGreeting(http, protectedUrl, "IGreetingService", "GreetAuthenticated", authOnlyToken);
            Console.WriteLine("PASS authenticated operation with JWT lacking greeting.read");

            await ExpectGreeting(http, protectedUrl, "IGreetingService", "Greet", scopedToken);
            Console.WriteLine("PASS scoped operation with greeting.read");

            await ExpectDenied(http, protectedUrl, "IGreetingService", "Greet", authOnlyToken);
            Console.WriteLine("PASS scoped operation denies JWT lacking greeting.read");

            await ExpectDenied(http, protectedUrl, "IGreetingService", "GreetAuthenticated", null);
            Console.WriteLine("PASS authenticated operation denies anonymous caller");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"FAIL {error.Message}");
            return 1;
        }
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {name} before running the integration tests.");

    private static void RequireHttps(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{name} must be an absolute HTTPS URL.");
    }

    private static async Task<string> RequestToken(HttpClient http, string tokenUrl, string clientId,
        string clientSecret, string scope, string? audience)
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

    // Inspect only the test token's scope claims. The SOAP service validates its signature, issuer and audience.
    private static bool HasScope(string jwt, string requiredScope)
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

    private static async Task ExpectGreeting(HttpClient http, string url, string contract, string operation, string? token)
    {
        using var response = await SendSoap(http, url, contract, operation, token);
        var xml = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{operation} returned HTTP {(int)response.StatusCode}.");

        var body = XDocument.Parse(xml).Root?.Element(Soap + "Body");
        if (body?.Element(Soap + "Fault") is not null)
            throw new InvalidOperationException($"{operation} returned a SOAP fault.");
        var result = body?.Element(Contract + (operation + "Response"))?.Element(Contract + (operation + "Result"))?.Value;
        if (result is null || !result.Contains("IntegrationTest", StringComparison.Ordinal))
            throw new InvalidOperationException($"{operation} returned an unexpected SOAP result.");
    }

    private static async Task ExpectDenied(HttpClient http, string url, string contract, string operation, string? token)
    {
        using var response = await SendSoap(http, url, contract, operation, token);
        var xml = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return;

        var body = XDocument.Parse(xml).Root?.Element(Soap + "Body");
        if (body?.Element(Soap + "Fault") is not null) return;
        throw new InvalidOperationException($"{operation} unexpectedly succeeded without its required credential.");
    }

    private static Task<HttpResponseMessage> SendSoap(HttpClient http, string url, string contract,
        string operation, string? token)
    {
        var envelope = new XDocument(new XElement(Soap + "Envelope",
            new XElement(Soap + "Body",
                new XElement(Contract + operation, new XElement(Contract + "name", "IntegrationTest")))));
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"urn:example:greeting:v1/{contract}/{operation}\"");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return SendAndDisposeRequest(http, request);
    }

    private static async Task<HttpResponseMessage> SendAndDisposeRequest(HttpClient http, HttpRequestMessage request)
    {
        using (request) return await http.SendAsync(request);
    }
}
