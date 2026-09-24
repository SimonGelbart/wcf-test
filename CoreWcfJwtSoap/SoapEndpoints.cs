using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;

namespace CoreWcfJwtSoap;

internal static class SoapEndpoints
{
    public static void MapGreetingEndpoints(this IServiceBuilder services)
    {
        services.AddService<GreetingService>();
        services.AddServiceEndpoint<GreetingService, IGreetingService>(
            HttpsBinding(HttpClientCredentialType.InheritedFromHost), "/Services/Greeting.svc");

        // Anonymous operations live on a distinct contract and endpoint.
        services.AddService<PublicGreetingService>();
        services.AddServiceEndpoint<PublicGreetingService, IPublicGreetingService>(
            HttpsBinding(HttpClientCredentialType.None), "/Services/PublicGreeting.svc");
    }

    private static BasicHttpBinding HttpsBinding(HttpClientCredentialType credentialType) => new()
    {
        Security = new BasicHttpSecurity
        {
            Mode = BasicHttpSecurityMode.Transport,
            Transport = new HttpTransportSecurity { ClientCredentialType = credentialType }
        }
    };
}
