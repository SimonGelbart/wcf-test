using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CoreWcfJwtSoap;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = configuration["Jwt:Authority"];
        var audience = configuration["Jwt:Audience"];
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuerUri) || issuerUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Set Jwt:Authority to your HTTPS token issuer and Jwt:Audience to this API's audience.");

        var issuerProxy = CreateIssuerProxy(configuration);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                // Keep JWT claim names such as "sub" and "scope" intact.
                options.MapInboundClaims = false;
                if (issuerProxy is not null)
                {
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        Proxy = issuerProxy,
                        UseProxy = true
                    };
                }
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true
                };
            });
        return services;
    }

    public static IServiceCollection AddGreetingAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(GreetingPolicies.Read, ScopePolicy(GreetingPolicies.ReadScope));
            options.AddPolicy(GreetingPolicies.Write, ScopePolicy(GreetingPolicies.WriteScope));
        });
        return services;
    }

    private static AuthorizationPolicy ScopePolicy(string scope) =>
        new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.Claims.Any(claim =>
                (claim.Type is "scope" or "scp") &&
                claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(scope, StringComparer.Ordinal)))
            .Build();

    private static WebProxy? CreateIssuerProxy(IConfiguration configuration)
    {
        // Only outbound issuer discovery and signing-key requests use this proxy.
        var proxyUrl = configuration["Jwt:Proxy:Url"];
        var username = configuration["Jwt:Proxy:Username"];
        var password = configuration["Jwt:Proxy:Password"];
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Set Jwt:Proxy:Url when supplying proxy credentials.");
            return null;
        }

        if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri) ||
            (proxyUri.Scheme != Uri.UriSchemeHttp && proxyUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(proxyUri.UserInfo))
            throw new InvalidOperationException("Jwt:Proxy:Url must be an absolute HTTP(S) URL without credentials.");

        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set both Jwt:Proxy:Username and Jwt:Proxy:Password, or neither.");

        var proxy = new WebProxy(proxyUri);
        if (!string.IsNullOrWhiteSpace(username))
            proxy.Credentials = new NetworkCredential(username, password);
        return proxy;
    }
}
