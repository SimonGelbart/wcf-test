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

### Authorization server through a proxy

If the SOAP host reaches the issuer through an outbound HTTP proxy, set its URL before starting the service:

```bash
export Jwt__Proxy__Url='http://proxy.example.com:8080'
# Only for a proxy that requires username/password authentication:
export Jwt__Proxy__Username='proxy-user'
export Jwt__Proxy__Password='proxy-password'
```

Use `Jwt__Proxy__Url` alone for an unauthenticated proxy. The credentials must be supplied together through configuration or environment variables; do not embed them in the URL or commit them to `appsettings.json`. The proxy is used by JWT bearer authentication to fetch OpenID Connect metadata and signing keys from `Jwt__Authority`. Token validation still checks the issuer, audience, lifetime, and signature, and the SOAP client still sends `Authorization: Bearer <token>` to this service over HTTPS. Proxy settings do not route incoming SOAP requests or obtain tokens for clients.

The two SOAP 1.1 endpoints use HTTPS:

| Endpoint | Operation | Requirement |
| --- | --- | --- |
| `/Services/Greeting.svc` | `Greet` | Valid JWT with `greeting.read` scope |
| `/Services/Greeting.svc` | `UpdateGreeting` | Valid JWT with `greeting.write` scope |
| `/Services/Greeting.svc` | `GreetAuthenticated` | Valid JWT; no scope required |
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

To call `GreetAuthenticated` with a token that has no `greeting.read` scope, keep the URL `/Services/Greeting.svc`, change the SOAP action to `urn:example:greeting:v1/IGreetingService/GreetAuthenticated`, and change the body element to `<GreetAuthenticated xmlns="urn:example:greeting:v1">`. Keep the `Authorization` header.

To update the salutation, send a JWT with `greeting.write` to the same URL using SOAP action `urn:example:greeting:v1/IGreetingService/UpdateGreeting` and this body:

```xml
<s:Body xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <UpdateGreeting xmlns="urn:example:greeting:v1">
    <salutation>Welcome</salutation>
  </UpdateGreeting>
</s:Body>
```

The operation returns the previous salutation. The current salutation is shared in memory by service instances and resets to `Hello` when the process restarts; replace `GreetingMessageStore` with persistent storage for a real application.

To call the public endpoint, change the URL to `/Services/PublicGreeting.svc`, the SOAP action to `urn:example:greeting:v1/IPublicGreetingService/GreetPublic`, and the body element to `<GreetPublic xmlns="urn:example:greeting:v1">`. Omit the `Authorization` header. In both cases, retain the `<name>...</name>` child and the matching closing body element.

For generated WCF clients, use `BasicHttpBinding` with transport security and set the outbound HTTP `Authorization` header using `HttpRequestMessageProperty` inside an `OperationContextScope`. Refresh the token before it expires. Configure the client endpoint for HTTPS; do not put a bearer token in the SOAP body or URL.

## Scope authorization per operation

`AuthenticationSetup.cs` registers `GreetingRead` and `GreetingWrite` through one `ScopePolicy` helper. Each matches its exact scope in a `scope` or `scp` claim, including a space-separated list such as `"scope": "greeting.read greeting.write"`. The service methods use `[Authorize(Policy = GreetingPolicies.Read)]` and `[Authorize(Policy = GreetingPolicies.Write)]`. A read-only token cannot call `UpdateGreeting`, and a write-only token cannot call `Greet`.

Keep scope names aligned with your issuer's access tokens. This sample checks a whole scope value, so `greeting.read.all` does not grant `greeting.read`. Add a new named policy using `ScopePolicy` for each distinct permission. Keep issuer credentials outside source control.

`GreetingService.GreetAuthenticated` uses plain `[Authorize]`, which applies the default policy requiring a valid JWT. It shares the contract and endpoint with the scope-protected `Greet` operation. `PublicGreetingService` is a separate contract with no authorization attribute and an HTTPS transport binding with `ClientCredentialType.None`. CoreWCF does not support using `[AllowAnonymous]` to expose a method on a protected service.

## Continuous integration

The GitHub Actions workflow in `.github/workflows/build.yml` restores and builds the project on pushes to `main` and pull requests. It does not need an issuer because it only compiles the service.
