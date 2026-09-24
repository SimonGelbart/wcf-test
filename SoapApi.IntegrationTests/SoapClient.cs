using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

internal sealed class SoapClient(HttpClient http, string baseUrl)
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Contract = "urn:example:greeting:v1";

    public async Task ExpectGreetingAsync(string endpoint, string contract, string operation, string? token)
    {
        using var response = await SendAsync(endpoint, contract, operation, token);
        var xml = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{operation} returned HTTP {(int)response.StatusCode}.");

        var body = XDocument.Parse(xml).Root?.Element(Soap + "Body");
        if (body?.Element(Soap + "Fault") is not null)
            throw new InvalidOperationException($"{operation} returned a SOAP fault.");
        var result = body?.Element(Contract + (operation + "Response"))?.Element(Contract + (operation + "Result"))?.Value;
        if (result is null || !result.Contains("IntegrationTest", StringComparison.Ordinal))
            throw new InvalidOperationException($"{operation} returned an unexpected SOAP result.");
    }

    public async Task ExpectDeniedAsync(string endpoint, string contract, string operation, string? token)
    {
        using var response = await SendAsync(endpoint, contract, operation, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return;

        var xml = await response.Content.ReadAsStringAsync();
        // CoreWCF may report an authorization failure as an HTTP 500 SOAP fault.
        if (response.StatusCode == HttpStatusCode.InternalServerError &&
            response.Content.Headers.ContentType?.MediaType is "text/xml" or "application/soap+xml")
        {
            var fault = XDocument.Parse(xml).Root?.Element(Soap + "Body")?.Element(Soap + "Fault");
            var reason = fault?.Value;
            if (reason is not null && new[] { "access is denied", "not authorized", "unauthorized", "forbidden", "authorization", "authentication" }
                    .Any(text => reason.Contains(text, StringComparison.OrdinalIgnoreCase)))
                return;
        }

        throw new InvalidOperationException($"{operation} returned HTTP {(int)response.StatusCode} instead of an authorization denial.");
    }

    private async Task<HttpResponseMessage> SendAsync(string endpoint, string contract, string operation, string? token)
    {
        var envelope = new XDocument(new XElement(Soap + "Envelope",
            new XElement(Soap + "Body",
                new XElement(Contract + operation, new XElement(Contract + "name", "IntegrationTest")))));
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/Services/" + endpoint)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"urn:example:greeting:v1/{contract}/{operation}\"");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request);
    }
}
