# SOAP integration runner

This .NET 10 console project calls the real SOAP service and requests two JWT access tokens from an OAuth 2.0 authentication server using `client_credentials`. It tests all three operations and verifies that credentials lacking the required permission are denied. It exits nonzero on failure. No token or client secret is printed.

Start `CoreWcfJwtSoap` with `Jwt__Authority` and `Jwt__Audience` configured for the same issuer. Register a test client at your issuer that can obtain a token with `greeting.read` and a token without it. Supply the issuer's HTTPS token endpoint and client credentials:

```bash
export SOAP_TEST_BASE_URL='https://localhost:7184'
export SOAP_TEST_TOKEN_URL='https://your-issuer.example.com/oauth2/token'
export SOAP_TEST_CLIENT_ID='your-test-client-id'
export SOAP_TEST_CLIENT_SECRET='your-test-client-secret'
export SOAP_TEST_SCOPED_SCOPE='greeting.read'
export SOAP_TEST_AUTH_ONLY_SCOPE='profile'
# Set this only when your token endpoint requires an audience parameter:
export SOAP_TEST_AUDIENCE='your-api-audience'
dotnet run --project SoapApi.IntegrationTests/SoapApi.IntegrationTests.csproj
```

`SOAP_TEST_AUTH_ONLY_SCOPE` may be omitted if the server issues a JWT when no scope is requested. The runner checks that this second token does **not** contain `greeting.read`; configure a different scope or client if the issuer grants it by default. The `SOAP_TEST_AUDIENCE` token request parameter is optional, but the resulting token's `aud` must still match the API's `Jwt__Audience`.

The runner uses form fields for client credentials. If your issuer requires HTTP Basic client authentication or another token flow, adapt `RequestToken` in `Program.cs`. Trust your local HTTPS development certificate with `dotnet dev-certs https --trust` before running. Do not commit credentials or access tokens.

Checks: public call without a token; authenticated call with the JWT lacking `greeting.read`; scoped call with a JWT containing `greeting.read`; scoped call denied without that scope; authenticated call denied without a JWT.
