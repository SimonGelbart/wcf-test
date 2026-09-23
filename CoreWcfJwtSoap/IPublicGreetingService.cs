using CoreWCF;

namespace CoreWcfJwtSoap;

[ServiceContract(Namespace = "urn:example:greeting:v1")]
public interface IPublicGreetingService
{
    [OperationContract]
    string GreetPublic(string name);
}
