using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Agendio.IntegrationTests;

internal static class AuthorizedRequestHelpers
{
    public static Task<HttpResponseMessage> PostAuthorizedAsync(
        HttpClient client, string accessToken, string path, object payload, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Post, accessToken, path, payload, cancellationToken);

    public static Task<HttpResponseMessage> PutAuthorizedAsync(
        HttpClient client, string accessToken, string path, object payload, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Put, accessToken, path, payload, cancellationToken);

    public static Task<HttpResponseMessage> GetAuthorizedAsync(
        HttpClient client, string accessToken, string path, CancellationToken cancellationToken) =>
        SendAuthorizedAsync(client, HttpMethod.Get, accessToken, path, payload: null, cancellationToken);

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
