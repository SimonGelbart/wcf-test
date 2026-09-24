# SOAP integration runner

This .NET 10 console project calls the real SOAP service and requests three JWT access tokens from an OAuth 2.0 authentication server using `client_credentials`. It tests all four operations and verifies that read and write permissions remain separate. It exits nonzero on failure. No token or client secret is printed.

Start `CoreWcfJwtSoap` with `Jwt__Authority` and `Jwt__Audience` configured for the same issuer. Register a test client at your issuer that can obtain a read-only token, a write-only token, and a token with neither greeting scope. Supply the issuer's HTTPS token endpoint and client credentials:

```bash
export SOAP_TEST_BASE_URL='https://localhost:7184'
export SOAP_TEST_TOKEN_URL='https://your-issuer.example.com/oauth2/token'
export SOAP_TEST_CLIENT_ID='your-test-client-id'
export SOAP_TEST_CLIENT_SECRET='your-test-client-secret'
export SOAP_TEST_READ_SCOPE='greeting.read'
export SOAP_TEST_WRITE_SCOPE='greeting.write'
export SOAP_TEST_AUTH_ONLY_SCOPE='profile'
# Set this only when your token endpoint requires an audience parameter:
export SOAP_TEST_AUDIENCE='your-api-audience'
dotnet run --project SoapApi.IntegrationTests/SoapApi.IntegrationTests.csproj
```

`SOAP_TEST_AUTH_ONLY_SCOPE` may be omitted if the server issues a JWT when no scope is requested. The runner verifies that the read token has only `greeting.read`, the write token has only `greeting.write`, and the JWT-only token has neither. Configure different scopes or test clients if your issuer grants extra scopes by default. The `SOAP_TEST_AUDIENCE` token request parameter is optional, but the resulting token's `aud` must still match the API's `Jwt__Audience`.

The runner uses form fields for client credentials. If your issuer requires HTTP Basic client authentication or another token flow, adapt `RequestTokenAsync` in `TokenClient.cs`. Trust your local HTTPS development certificate with `dotnet dev-certs https --trust` before running. Do not commit credentials or access tokens.

Checks: public call without a token; authenticated call with a JWT lacking greeting scopes; read and write calls with their respective JWTs; cross-scope calls denied; authenticated call denied without a JWT. The runner writes a temporary salutation, verifies that a read sees it, and restores the previous value.
