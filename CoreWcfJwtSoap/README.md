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

SOAP address: `https://localhost:7184/Services/Greeting.svc`. WSDL: `https://localhost:7184/Services/Greeting.svc?wsdl`. The WSDL may be publicly readable; each SOAP operation requires a valid access token.

## Call with an access token

Obtain an access token from your configured issuer with `aud` equal to `Jwt__Audience`. The `Greet` SOAP action is `urn:example:greeting:v1/IGreetingService/Greet`.

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

For generated WCF clients, use `BasicHttpBinding` with transport security and set the outbound HTTP `Authorization` header using `HttpRequestMessageProperty` inside an `OperationContextScope`. Refresh the token before it expires. Configure the client endpoint for HTTPS; do not put a bearer token in the SOAP body or URL.

Replace `GreetingService` and `IGreetingService` with your own contract. Add a policy to `AddAuthorization` and use `[Authorize(Policy = "Name")]` for claim or scope restrictions. Keep issuer credentials outside source control.
