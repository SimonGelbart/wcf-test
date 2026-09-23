# .NET 10 CoreWCF SOAP service with JWT

This is a `BasicHttpBinding` SOAP 1.1 service hosted in ASP.NET Core. It accepts an OAuth/OIDC access token in the HTTP `Authorization: Bearer <token>` header. The configured HTTPS issuer publishes signing keys through OpenID Connect discovery; this project validates signature, issuer, audience, and expiration. It does not issue tokens.

## Configure and run

Install the .NET 10 SDK. Point the service at your existing HTTPS OIDC issuer and set the expected API audience (the token's `aud` claim):

```bash
cd CoreWcfJwtSoap
export Jwt__Authority='https://your-issuer.example.com/'
export Jwt__Audience='your-api-audience'
dotnet dev-certs https --trust
dotnet restore
dotnet run --launch-profile CoreWcfJwtSoap
```

On Windows PowerShell, set the variables with `$env:Jwt__Authority = '...'` and `$env:Jwt__Audience = '...'`.

The three SOAP 1.1 endpoints use HTTPS:

| Endpoint | Operation | Requirement |
| --- | --- | --- |
| `/Services/Greeting.svc` | `Greet` | Valid JWT with `greeting.read` scope |
| `/Services/AuthenticatedGreeting.svc` | `GreetAuthenticated` | Valid JWT; no scope required |
| `/Services/PublicGreeting.svc` | `GreetPublic` | No token required |

Each WSDL is available by appending `?wsdl` to its HTTPS address, for example `https://localhost:7184/Services/PublicGreeting.svc?wsdl`. WSDL metadata may be publicly readable even for protected operations.

## Call with an access token

Obtain an access token from your configured issuer with `aud` equal to `Jwt__Audience` and the `greeting.read` scope. The `Greet` SOAP action is `urn:example:greeting:v1/IGreetingService/Greet`.

```bash
curl -i 'https://localhost:7184/Services/Greeting.svc' \
  -H 'Content-Type: text/xml; charset=utf-8' \
  -H 'SOAPAction: "urn:example:greeting:v1/IGreetingService/Greet"' \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  --data-binary @request.xml
```

Create `request.xml` with:

```xml
<?xml version="1.0" encoding="utf-8"?>
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <Greet xmlns="urn:example:greeting:v1">
      <name>Ada</name>
    </Greet>
  </s:Body>
</s:Envelope>
```

To call the authenticated endpoint with a token that has no `greeting.read` scope, change the URL to `/Services/AuthenticatedGreeting.svc`, the SOAP action to `urn:example:greeting:v1/IAuthenticatedGreetingService/GreetAuthenticated`, and the body element to `<GreetAuthenticated xmlns="urn:example:greeting:v1">`. Keep the `Authorization` header.

To call the public endpoint, change the URL to `/Services/PublicGreeting.svc`, the SOAP action to `urn:example:greeting:v1/IPublicGreetingService/GreetPublic`, and the body element to `<GreetPublic xmlns="urn:example:greeting:v1">`. Omit the `Authorization` header. In both cases, retain the `<name>...</name>` child and the matching closing body element.

For generated WCF clients, use `BasicHttpBinding` with transport security and set the outbound HTTP `Authorization` header using `HttpRequestMessageProperty` inside an `OperationContextScope`. Refresh the token before it expires. Configure the client endpoint for HTTPS; do not put a bearer token in the SOAP body or URL.

## Scope authorization per operation

`Program.cs` defines the `GreetingRead` policy. It accepts `greeting.read` in a `scope` or `scp` claim, including a space-separated list such as `"scope": "greeting.read profile"`. The implementation method uses `[Authorize(Policy = "GreetingRead")]`; a valid token without this scope is denied. Define another named policy for each distinct permission and apply it to the corresponding service implementation method. For example:

```csharp
options.AddPolicy("GreetingWrite", new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
    .RequireAuthenticatedUser()
    .RequireAssertion(context => context.User.Claims.Any(claim =>
        (claim.Type is "scope" or "scp") &&
        claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("greeting.write", StringComparer.Ordinal)))
    .Build());

[Authorize(Policy = "GreetingWrite")]
public void UpdateGreeting(string value) { /* your implementation */ }
```

Keep scope names aligned with your issuer's access tokens. This sample checks a whole scope value, so `greeting.read.all` does not grant `greeting.read`. Keep issuer credentials outside source control.

`AuthenticatedGreetingService.GreetAuthenticated` uses plain `[Authorize]`, which applies the default policy requiring a valid JWT. `PublicGreetingService` is a separate contract with no authorization attribute and an HTTPS transport binding with `ClientCredentialType.None`. CoreWCF does not support using `[AllowAnonymous]` to expose a method on a protected service.

## Continuous integration

The GitHub Actions workflow in `.github/workflows/build.yml` restores and builds the project on pushes to `main` and pull requests. It does not need an issuer because it only compiles the service.
