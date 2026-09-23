namespace CoreWcfJwtSoap;

public sealed class PublicGreetingService : IPublicGreetingService
{
    public string GreetPublic(string name) => $"Hello, {name}!";
}
