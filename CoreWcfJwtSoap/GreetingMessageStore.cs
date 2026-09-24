namespace CoreWcfJwtSoap;

// Demo state shared by service instances; changes are lost when the process restarts.
public sealed class GreetingMessageStore
{
    private string _salutation = "Hello";

    public string Salutation => Volatile.Read(ref _salutation);

    public string Update(string salutation)
    {
        if (string.IsNullOrWhiteSpace(salutation) || salutation.Length > 80)
            throw new ArgumentException("Salutation must contain 1 to 80 characters.", nameof(salutation));

        return Interlocked.Exchange(ref _salutation, salutation.Trim());
    }
}
