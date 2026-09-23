using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWcfJwtSoap;
using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Trust tokens from an existing OpenID Connect issuer.
var authority = builder.Configuration["Jwt:Authority"];
var audience = builder.Configuration["Jwt:Audience"];
if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuerUri) || issuerUri.Scheme != Uri.UriSchemeHttps ||
    string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Set Jwt:Authority to your HTTPS token issuer and Jwt:Audience to this API's audience.");
}

// Only outbound issuer discovery and signing-key requests use this proxy.
var proxyUrl = builder.Configuration["Jwt:Proxy:Url"];
var proxyUsername = builder.Configuration["Jwt:Proxy:Username"];
var proxyPassword = builder.Configuration["Jwt:Proxy:Password"];
WebProxy? issuerProxy = null;
if (!string.IsNullOrWhiteSpace(proxyUrl))
{
    if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri) ||
        (proxyUri.Scheme != Uri.UriSchemeHttp && proxyUri.Scheme != Uri.UriSchemeHttps) ||
        !string.IsNullOrEmpty(proxyUri.UserInfo))
        throw new InvalidOperationException("Jwt:Proxy:Url must be an absolute HTTP(S) URL without credentials.");

    if (string.IsNullOrWhiteSpace(proxyUsername) != string.IsNullOrWhiteSpace(proxyPassword))
        throw new InvalidOperationException("Set both Jwt:Proxy:Username and Jwt:Proxy:Password, or neither.");

    issuerProxy = new WebProxy(proxyUri);
    if (!string.IsNullOrWhiteSpace(proxyUsername))
        issuerProxy.Credentials = new NetworkCredential(proxyUsername, proxyPassword);
}
else if (!string.IsNullOrWhiteSpace(proxyUsername) || !string.IsNullOrWhiteSpace(proxyPassword))
{
    throw new InvalidOperationException("Set Jwt:Proxy:Url when supplying proxy credentials.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
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
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("GreetingRead", new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireAssertion(context => context.User.Claims.Any(claim =>
            (claim.Type is "scope" or "scp") &&
            claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("greeting.read", StringComparer.Ordinal)))
        .Build());
});

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.UseAuthentication();

// CoreWCF applies each operation's authorization policy on this protected endpoint.
app.UseServiceModel(services =>
{
    services.AddService<GreetingService>();
    services.AddServiceEndpoint<GreetingService, IGreetingService>(new BasicHttpBinding
    {
        Security = new BasicHttpSecurity
        {
            Mode = BasicHttpSecurityMode.Transport,
            Transport = new HttpTransportSecurity
            {
                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
            }
        }
    }, "/Services/Greeting.svc");

    // This distinct contract has no [Authorize] operation and accepts anonymous callers.
    services.AddService<PublicGreetingService>();
    services.AddServiceEndpoint<PublicGreetingService, IPublicGreetingService>(new BasicHttpBinding
    {
        Security = new BasicHttpSecurity
        {
            Mode = BasicHttpSecurityMode.Transport,
            Transport = new HttpTransportSecurity
            {
                ClientCredentialType = HttpClientCredentialType.None
            }
        }
    }, "/Services/PublicGreeting.svc");
});

var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
metadata.HttpsGetEnabled = true;

// Place authorization after CoreWCF so endpoint routing fallback policies do not intercept SOAP.
app.UseAuthorization();
app.Run();
