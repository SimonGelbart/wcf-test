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
            var readScope = Environment.GetEnvironmentVariable("SOAP_TEST_READ_SCOPE") ?? "greeting.read";
            var writeScope = Environment.GetEnvironmentVariable("SOAP_TEST_WRITE_SCOPE") ?? "greeting.write";
            var authOnlyScope = Environment.GetEnvironmentVariable("SOAP_TEST_AUTH_ONLY_SCOPE") ?? "";
            var audience = Environment.GetEnvironmentVariable("SOAP_TEST_AUDIENCE");

            RequireHttps(baseUrl, "SOAP_TEST_BASE_URL");
            RequireHttps(tokenUrl, "SOAP_TEST_TOKEN_URL");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var tokens = new TokenClient(http, tokenUrl, clientId, clientSecret, audience);
            var soap = new SoapClient(http, baseUrl);

            var readToken = await tokens.RequestTokenAsync(readScope);
            if (!TokenClient.HasScope(readToken, "greeting.read") || TokenClient.HasScope(readToken, "greeting.write"))
                throw new InvalidOperationException("The read JWT must contain greeting.read but not greeting.write.");

            var writeToken = await tokens.RequestTokenAsync(writeScope);
            if (!TokenClient.HasScope(writeToken, "greeting.write") || TokenClient.HasScope(writeToken, "greeting.read"))
                throw new InvalidOperationException("The write JWT must contain greeting.write but not greeting.read.");

            var authOnlyToken = await tokens.RequestTokenAsync(authOnlyScope);
            if (TokenClient.HasScope(authOnlyToken, "greeting.read") || TokenClient.HasScope(authOnlyToken, "greeting.write"))
                throw new InvalidOperationException("The JWT-only token contains a greeting scope. Set SOAP_TEST_AUTH_ONLY_SCOPE to another scope (or configure the server's default scopes).");

            await soap.ExpectGreetingAsync("PublicGreeting.svc", "IPublicGreetingService", "GreetPublic", null);
            Console.WriteLine("PASS public operation without a token");

            await soap.ExpectGreetingAsync("Greeting.svc", "IGreetingService", "GreetAuthenticated", authOnlyToken);
            Console.WriteLine("PASS authenticated operation with JWT lacking greeting.read");

            await soap.ExpectGreetingAsync("Greeting.svc", "IGreetingService", "Greet", readToken);
            Console.WriteLine("PASS scoped operation with greeting.read");

            await soap.ExpectDeniedAsync("Greeting.svc", "IGreetingService", "Greet", authOnlyToken);
            Console.WriteLine("PASS scoped operation denies JWT lacking greeting.read");

            await soap.ExpectDeniedAsync("Greeting.svc", "IGreetingService", "Greet", writeToken);
            Console.WriteLine("PASS read operation denies write-only JWT");

            await soap.ExpectDeniedAsync("Greeting.svc", "IGreetingService", "UpdateGreeting", readToken, "salutation");
            Console.WriteLine("PASS write operation denies read-only JWT");

            string? previous = null;
            try
            {
                previous = await soap.UpdateGreetingAsync(writeToken, "IntegrationWelcome");
                await soap.ExpectGreetingAsync("Greeting.svc", "IGreetingService", "Greet", readToken, "IntegrationWelcome");
                Console.WriteLine("PASS write operation changes the greeting read by a read-scoped JWT");
            }
            finally
            {
                if (previous is not null) await soap.UpdateGreetingAsync(writeToken, previous);
            }

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
