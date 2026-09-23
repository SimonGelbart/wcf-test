using CoreWCF;

namespace CoreWcfJwtSoap;

[ServiceContract(Namespace = "urn:example:greeting:v1")]
public interface IAuthenticatedGreetingService
{
    [OperationContract]
    string GreetAuthenticated(string name);
}
