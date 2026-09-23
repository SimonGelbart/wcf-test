using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWcfJwtSoap;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
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
