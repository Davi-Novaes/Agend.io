using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// CRUD de produtos e movimentacao manual de estoque (entrada/saida). Diferente
/// do Financeiro, tudo aqui e sincrono dentro do proprio modulo — sem outbox/
/// consumidor — entao os testes nao precisam de poll-with-retry.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class EstoqueTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Create_A_Product()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/estoque/produtos",
            new { name = "Xampu", sku = "SKU-1", description = "Xampu revenda", quantityInStock = 10, minimumStock = 2, salePrice = 29.9m, currency = "BRL" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var productId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}", cancellationToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("name").GetString().ShouldBe("Xampu");
        getBody.GetProperty("quantityInStock").GetInt32().ShouldBe(10);
        getBody.GetProperty("minimumStock").GetInt32().ShouldBe(2);
        getBody.GetProperty("salePrice").GetDecimal().ShouldBe(29.9m);
        getBody.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Registering_An_Entry_Movement_Should_Increase_QuantityInStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var productId = await CreateProductAsync(client, accessToken, quantityInStock: 10, minimumStock: 2, cancellationToken);

        var movementResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}/movimentacoes",
            new { type = "Entry", quantity = 5, reason = "Purchase", notes = (string?)null, occurredAtUtc = (DateTimeOffset?)null },
            cancellationToken);
        movementResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var product = await GetProductAsync(client, accessToken, productId, cancellationToken);
        product.GetProperty("quantityInStock").GetInt32().ShouldBe(15);
    }

    [Fact]
    public async Task Registering_An_Exit_Movement_Should_Decrease_QuantityInStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var productId = await CreateProductAsync(client, accessToken, quantityInStock: 10, minimumStock: 2, cancellationToken);

        var movementResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}/movimentacoes",
            new { type = "Exit", quantity = 4, reason = "Sale", notes = "Venda avulsa", occurredAtUtc = (DateTimeOffset?)null },
            cancellationToken);
        movementResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var product = await GetProductAsync(client, accessToken, productId, cancellationToken);
        product.GetProperty("quantityInStock").GetInt32().ShouldBe(6);
    }

    [Fact]
    public async Task Registering_An_Exit_Larger_Than_Balance_Should_Be_Rejected_And_Not_Create_A_Movement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var productId = await CreateProductAsync(client, accessToken, quantityInStock: 5, minimumStock: 1, cancellationToken);

        var movementResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}/movimentacoes",
            new { type = "Exit", quantity = 10, reason = "Sale", notes = (string?)null, occurredAtUtc = (DateTimeOffset?)null },
            cancellationToken);
        movementResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var product = await GetProductAsync(client, accessToken, productId, cancellationToken);
        product.GetProperty("quantityInStock").GetInt32().ShouldBe(5);

        var movementsResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/estoque/movimentacoes?productId={productId}", cancellationToken);
        var movementsBody = await movementsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        movementsBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Listing_Products_With_LowStockOnly_Should_Only_Return_Products_At_Or_Below_MinimumStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        await CreateProductAsync(client, accessToken, "Produto Baixo", quantityInStock: 1, minimumStock: 5, cancellationToken);
        await CreateProductAsync(client, accessToken, "Produto Ok", quantityInStock: 10, minimumStock: 2, cancellationToken);

        var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/estoque/produtos?lowStockOnly=true", cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
        body.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Produto Baixo");
    }

    [Fact]
    public async Task Listing_Stock_Movements_Should_Filter_By_Product_Type_Reason_And_Period()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var productId = await CreateProductAsync(client, accessToken, quantityInStock: 20, minimumStock: 2, cancellationToken);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}/movimentacoes",
            new { type = "Entry", quantity = 5, reason = "Purchase", notes = (string?)null, occurredAtUtc = (DateTimeOffset?)null },
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}/movimentacoes",
            new { type = "Exit", quantity = 3, reason = "Loss", notes = "Quebrou", occurredAtUtc = (DateTimeOffset?)null },
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var byType = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/estoque/movimentacoes?productId={productId}&type=Exit", cancellationToken);
        var byTypeBody = await byType.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        byTypeBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
        byTypeBody.GetProperty("items")[0].GetProperty("reason").GetString().ShouldBe("Loss");

        var byReason = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/estoque/movimentacoes?productId={productId}&reason=Purchase", cancellationToken);
        var byReasonBody = await byReason.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        byReasonBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
        byReasonBody.GetProperty("items")[0].GetProperty("type").GetString().ShouldBe("Entry");

        // DateOnly.ToString() usa a cultura corrente do processo (pt-BR aqui,
        // "dd/MM/yyyy") mas o model binding do ASP.NET espera formato invariante
        // ("yyyy-MM-dd") — sem Iso() a interpolacao quebra sempre que o dia > 12.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var byPeriod = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken,
            $"/api/estoque/movimentacoes?productId={productId}&from={Iso(today.AddDays(-1))}&to={Iso(today.AddDays(1))}", cancellationToken);
        var byPeriodBody = await byPeriod.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        byPeriodBody.GetProperty("totalCount").GetInt32().ShouldBe(2);

        var outsidePeriod = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken,
            $"/api/estoque/movimentacoes?productId={productId}&from={Iso(today.AddDays(-30))}&to={Iso(today.AddDays(-10))}",
            cancellationToken);
        var outsidePeriodBody = await outsidePeriod.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        outsidePeriodBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Owner_Can_Update_And_Toggle_Active_Status_Of_A_Product()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var productId = await CreateProductAsync(client, accessToken, quantityInStock: 10, minimumStock: 2, cancellationToken);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}",
            new { name = "Xampu 500ml", sku = "SKU-2", description = (string?)null, minimumStock = 3, salePrice = (decimal?)null, currency = (string?)null },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var statusResponse = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/estoque/produtos/{productId}/status", new { isActive = false }, cancellationToken);
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var product = await GetProductAsync(client, accessToken, productId, cancellationToken);
        product.GetProperty("name").GetString().ShouldBe("Xampu 500ml");
        product.GetProperty("minimumStock").GetInt32().ShouldBe(3);
        product.GetProperty("quantityInStock").GetInt32().ShouldBe(10);
        product.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_Or_Act_On_A_Product_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var productId = await CreateProductAsync(client, tenantAToken, quantityInStock: 10, minimumStock: 2, cancellationToken);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var crossTenantGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/estoque/produtos/{productId}", cancellationToken);
        crossTenantGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantMovement = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantBToken, $"/api/estoque/produtos/{productId}/movimentacoes",
            new { type = "Entry", quantity = 1, reason = "Adjustment", notes = (string?)null, occurredAtUtc = (DateTimeOffset?)null },
            cancellationToken);
        crossTenantMovement.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var tenantBList = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/estoque/produtos", cancellationToken);
        var tenantBListBody = await tenantBList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        tenantBListBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<Guid> CreateProductAsync(
        HttpClient client, string accessToken, int quantityInStock, int minimumStock, CancellationToken cancellationToken) =>
        await CreateProductAsync(client, accessToken, "Xampu", quantityInStock, minimumStock, cancellationToken);

    private static async Task<Guid> CreateProductAsync(
        HttpClient client, string accessToken, string name, int quantityInStock, int minimumStock, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/estoque/produtos",
            new { name, sku = (string?)null, description = (string?)null, quantityInStock, minimumStock, salePrice = (decimal?)null, currency = (string?)null },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetProductAsync(
        HttpClient client, string accessToken, Guid productId, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/estoque/produtos/{productId}", cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
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

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
