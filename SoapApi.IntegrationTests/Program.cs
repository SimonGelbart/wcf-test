internal static class Program
{
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
            var tokens = new TokenClient(http, tokenUrl, clientId, clientSecret, audience);
            var soap = new SoapClient(http, baseUrl);

            var scopedToken = await tokens.RequestTokenAsync(scopedScope);
            if (!TokenClient.HasScope(scopedToken, "greeting.read"))
                throw new InvalidOperationException("The scoped access token is a JWT without greeting.read. Configure the authentication server to issue that scope.");

            var authOnlyToken = await tokens.RequestTokenAsync(authOnlyScope);
            if (TokenClient.HasScope(authOnlyToken, "greeting.read"))
                throw new InvalidOperationException("The JWT-only token contains greeting.read. Set SOAP_TEST_AUTH_ONLY_SCOPE to a scope that does not grant it (or configure the server's default scopes).");

            await soap.ExpectGreetingAsync("PublicGreeting.svc", "IPublicGreetingService", "GreetPublic", null);
            Console.WriteLine("PASS public operation without a token");

            await soap.ExpectGreetingAsync("Greeting.svc", "IGreetingService", "GreetAuthenticated", authOnlyToken);
            Console.WriteLine("PASS authenticated operation with JWT lacking greeting.read");

            await soap.ExpectGreetingAsync("Greeting.svc", "IGreetingService", "Greet", scopedToken);
            Console.WriteLine("PASS scoped operation with greeting.read");

            await soap.ExpectDeniedAsync("Greeting.svc", "IGreetingService", "Greet", authOnlyToken);
            Console.WriteLine("PASS scoped operation denies JWT lacking greeting.read");

            await soap.ExpectDeniedAsync("Greeting.svc", "IGreetingService", "GreetAuthenticated", null);
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
}
