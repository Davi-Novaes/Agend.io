using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Fase 22 — Assistente Agend.io. FakeAiChatClient (ver esse arquivo) substitui
/// o provedor de IA real: pede a ferramenta get_inventory_summary e ecoa o
/// resultado na resposta final, o suficiente pra provar que o loop de
/// tool-calling do handler chega ate os lookups reais (Financeiro/Estoque/
/// Scheduling) e volta com dado de verdade, sem custar dinheiro nem depender
/// de rede externa.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class AssistantTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Asking_A_Question_Should_Return_An_Answer_Using_Real_Tenant_Data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        await CreateProductAsync(client, accessToken, "Xampu", quantityInStock: 40, minimumStock: 5, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quantos produtos ativos eu tenho?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("activeProductCount");
        answer.ShouldContain("\"activeProductCount\":1");
    }

    [Fact]
    public async Task Asking_Without_Authentication_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/assistant/ask", new { question = "Quanto faturei esse mes?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Asking_With_A_Blank_Question_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "   " }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assistant_Data_Is_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        await CreateProductAsync(client, tenantAToken, "Produto do tenant A", quantityInStock: 10, minimumStock: 1, cancellationToken);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantBToken, "/api/assistant/ask", new { question = "Quantos produtos ativos eu tenho?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();

        // O tenant B nao tem nenhum produto — a resposta nao pode refletir o
        // produto cadastrado pelo tenant A.
        answer.ShouldContain("\"activeProductCount\":0");
    }

    [Fact]
    public async Task Asking_Without_Ai_Provider_Configured_Should_Return_A_Graceful_Error()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var unconfiguredFactory = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("AiAssistant:ApiKey", string.Empty));
        using var client = unconfiguredFactory.CreateClient();

        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quanto faturei esse mes?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    private static async Task<Guid> CreateProductAsync(
        HttpClient client, string accessToken, string name, int quantityInStock, int minimumStock, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/estoque/produtos", new { name, quantityInStock, minimumStock }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = $"tenant-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
