using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Agendio.IntegrationTests;

internal static class AuthorizedRequestHelpers
{
    // DateOnly.ToString() usa a cultura corrente do processo (pt-BR neste ambiente ->
    // dd/MM/yyyy), mas o model binding de query string do ASP.NET para DateOnly? espera
    // formato invariante (yyyy-MM-dd) -- interpolar DateOnly direto numa URL de teste
    // quebra sempre que o dia do mes for maior que 12.
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static Task<HttpResponseMessage> PostAuthorizedAsync(
        HttpClient client, string accessToken, string path, object payload, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Post, accessToken, path, payload, cancellationToken);

    public static Task<HttpResponseMessage> PutAuthorizedAsync(
        HttpClient client, string accessToken, string path, object payload, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Put, accessToken, path, payload, cancellationToken);

    public static Task<HttpResponseMessage> PatchAuthorizedAsync(
        HttpClient client, string accessToken, string path, object payload, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Patch, accessToken, path, payload, cancellationToken);

    public static Task<HttpResponseMessage> GetAuthorizedAsync(
        HttpClient client, string accessToken, string path, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Get, accessToken, path, payload: null, cancellationToken);

    public static Task<HttpResponseMessage> DeleteAuthorizedAsync(
        HttpClient client, string accessToken, string path, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Delete, accessToken, path, payload: null, cancellationToken);

    private static Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client, HttpMethod method, string accessToken, string path, object? payload, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request, cancellationToken);
    }
}
