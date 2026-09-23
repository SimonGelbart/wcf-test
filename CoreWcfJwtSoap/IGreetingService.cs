using CoreWCF;

namespace CoreWcfJwtSoap;

[ServiceContract(Namespace = "urn:example:greeting:v1")]
public interface IGreetingService
{
    [OperationContract]
    string Greet(string name);

    [OperationContract]
    string GreetAuthenticated(string name);
}
