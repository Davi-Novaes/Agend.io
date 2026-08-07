using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Upload de logo por enquanto e disco local (ver LocalFileStorage) — sem
/// modulo de Storage completo ainda. Cobre o essencial: formato/tamanho sao
/// validados, o arquivo fica de fato servivel, e so o Owner pode trocar.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TenantLogoTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    // Menor PNG valido possivel (1x1 transparente) — bytes reais, nao um mock.
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    [Fact]
    public async Task Owner_Can_Upload_A_Logo_And_Then_Fetch_It_Back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, slug, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var uploadResponse = await UploadLogoAsync(client, accessToken, TinyPng, "image/png", cancellationToken);
        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var logoUrl = uploadBody.GetProperty("logoUrl").GetString()!;
        logoUrl.ShouldStartWith("/uploads/tenant-logos/");

        var tenantResponse = await client.GetAsync($"/api/tenants/by-slug/{slug}", cancellationToken);
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        tenantBody.GetProperty("logoUrl").GetString().ShouldBe(logoUrl);

        // O arquivo precisa estar de fato acessivel — nao so o campo no banco.
        var fileResponse = await client.GetAsync(logoUrl, cancellationToken);
        fileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fileResponse.Content.ReadAsByteArrayAsync(cancellationToken)).ShouldBe(TinyPng);
    }

    [Fact]
    public async Task Uploading_A_File_Larger_Than_2MB_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, _, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var oversized = new byte[2 * 1024 * 1024 + 1];
        var response = await UploadLogoAsync(client, accessToken, oversized, "image/png", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uploading_A_Non_Image_File_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, _, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await UploadLogoAsync(client, accessToken, "nao sou uma imagem"u8.ToArray(), "text/plain", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Request_To_Upload_Logo_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/tenants/logo", content, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> UploadLogoAsync(
        HttpClient client, string accessToken, byte[] fileBytes, string contentType, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", "logo");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tenants/logo") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<(Guid TenantId, string Slug, string AccessToken)> CreateTenantWithOwnerAndLoginAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        var slug = $"tenant-{Guid.NewGuid():N}";

        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug,
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, slug, loginBody.GetProperty("accessToken").GetString()!);
    }
}
