using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
    public async Task Asking_How_To_Navigate_Should_Answer_Instantly_Without_Ai_Provider()
    {
        // Fast-path (NavigationIntentRule) responde sem chamar IA nenhuma --
        // por isso funciona mesmo com o provedor de IA desligado, diferente do
        // teste "Asking_Without_Ai_Provider_Configured" abaixo (pergunta de
        // dado, sempre precisa do LLM).
        var cancellationToken = TestContext.Current.CancellationToken;
        using var unconfiguredFactory = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("AiAssistant:ApiKey", string.Empty));
        using var client = unconfiguredFactory.CreateClient();

        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Como cadastro um cliente?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("Clientes");
        body.GetProperty("suggestedRoute").GetString().ShouldBe("/clientes");
    }

    [Fact]
    public async Task Asking_Customer_Count_Should_Use_Fast_Path_With_Real_Data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        await CreateCustomerAsync(client, accessToken, "Cliente Um", cancellationToken);
        await CreateCustomerAsync(client, accessToken, "Cliente Dois", cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quantos clientes eu tenho?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("2 clientes ativos");
    }

    [Fact]
    public async Task Asking_Low_Stock_Products_Should_List_Product_Names()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        await CreateProductAsync(client, accessToken, "Xampu quase acabando", quantityInStock: 2, minimumStock: 5, cancellationToken);
        await CreateProductAsync(client, accessToken, "Condicionador com estoque cheio", quantityInStock: 40, minimumStock: 5, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Meu estoque tem algum produto acabando?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("Xampu quase acabando");
        answer.ShouldNotContain("Condicionador com estoque cheio");
    }

    [Fact]
    public async Task Asking_Inactive_Customers_Should_List_Customers_Never_Contacted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Cliente recem-criado nunca foi contatado (LastContactedAtUtc null) —
        // conta como inativo desde sempre, entao aparece pra qualquer corte de dias.
        await CreateCustomerAsync(client, accessToken, "Cliente Sumido", cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quais clientes estao inativos ha 60 dias?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("Cliente Sumido");
    }

    [Fact]
    public async Task Assistant_Query_Log_Should_Be_Isolated_Between_Tenants_Via_Row_Level_Security()
    {
        // Fase 3: AssistantQueryLogEntry e a primeira entidade do modulo
        // Assistant com DbContext/RLS proprios -- prova que o isolamento vale
        // pra ela tambem, nao so pros lookups de outros modulos (ja cobertos
        // por Assistant_Data_Is_Isolated_Between_Tenants acima).
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (tenantAId, tokenA) = await CreateTenantWithOwnerAndLoginReturningTenantIdAsync(client, cancellationToken);
        var askResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tokenA, "/api/assistant/ask", new { question = "Como cadastro um cliente?" }, cancellationToken);
        askResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (tenantBId, _) = await CreateTenantWithOwnerAndLoginReturningTenantIdAsync(client, cancellationToken);

        await using var ownerConnection = new NpgsqlConnection(fixture.DatabaseOwnerConnectionString);
        await ownerConnection.OpenAsync(cancellationToken);
        await using var findCommand = new NpgsqlCommand(
            "SELECT id FROM assistant.assistant_query_log WHERE tenant_id = @tenantId LIMIT 1", ownerConnection);
        findCommand.Parameters.AddWithValue("tenantId", tenantAId);
        var logEntryId = (Guid?)await findCommand.ExecuteScalarAsync(cancellationToken);
        logEntryId.ShouldNotBeNull("o fast-path de navegacao deveria ter gravado uma entrada de auditoria pro tenant A.");

        // Conecta como a role de RUNTIME (NOBYPASSRLS), ancora explicitamente no
        // tenant B e tenta ler a entrada de log do tenant A pelo Id exato — RLS
        // tem que barrar, nao so o Global Query Filter do EF Core.
        await using var appConnection = new NpgsqlConnection(fixture.DatabaseAppConnectionString);
        await appConnection.OpenAsync(cancellationToken);
        await using (var setTenantCommand = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenantId, false)", appConnection))
        {
            setTenantCommand.Parameters.AddWithValue("tenantId", tenantBId.ToString());
            await setTenantCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var selectCommand = new NpgsqlCommand(
            "SELECT id FROM assistant.assistant_query_log WHERE id = @id", appConnection);
        selectCommand.Parameters.AddWithValue("id", logEntryId!.Value);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);

        (await reader.ReadAsync(cancellationToken)).ShouldBeFalse(
            "RLS deveria impedir o tenant B de enxergar uma entrada de log de auditoria do assistente pertencente ao tenant A.");
    }

    [Fact]
    public async Task Asking_Appointment_Count_Top_Service_And_Revenue_Should_Use_Fast_Path_After_Completing_An_Appointment()
    {
        // Fase 5: as 3 novas regras que dependem de agendamento concluido
        // (AppointmentCountIntentRule, TopServiceIntentRule, RevenueIntentRule)
        // num unico fluxo -- montar o cenario (cliente/recurso/servico/
        // agendamento completo) e caro, reaproveitar entre as 3 perguntas.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente de Teste" }, cancellationToken);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Barbeiro 1", type = "Person", capacity = 1, description = (string?)null },
            cancellationToken);
        var resourceId = (await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Corte de Cabelo", description = (string?)null, durationMinutes = 30, price = 45.90m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var serviceId = (await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        // +5min (nao +1 dia): precisa ficar no futuro (Appointment.StartInThePast)
        // mas ainda no MESMO dia UTC de "hoje" -- os asserts abaixo pedem
        // "hoje" (contagem) e "esse mes" (topico/faturamento, que inclui hoje
        // como limite superior).
        var createAppointmentResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddMinutes(5), notes = (string?)null },
            cancellationToken);
        createAppointmentResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var appointmentId = (await createAppointmentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/confirm", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Contagem e servico-top vem direto de Scheduling -- ja disponiveis
        // sem esperar nada assincrono.
        var countResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quantos agendamentos tenho hoje?" }, cancellationToken);
        countResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var countAnswer = (await countResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("answer").GetString();
        countAnswer.ShouldNotBeNullOrWhiteSpace();
        countAnswer.ShouldContain("Voce tem 1 agendamento hoje");

        var topServiceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Qual servico mais vendeu esse mes?" }, cancellationToken);
        topServiceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var topServiceAnswer = (await topServiceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("answer").GetString();
        topServiceAnswer.ShouldNotBeNullOrWhiteSpace();
        topServiceAnswer.ShouldContain("Corte de Cabelo");

        // Faturamento depende do consumidor assincrono do Financeiro (evento de
        // integracao via RabbitMQ) -- drena o outbox e confirma o recebimento
        // antes de perguntar, mesmo padrao de FinanceiroTests.
        var tenantId = GetTenantIdFromToken(accessToken);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor<SchedulingDbContext>>();
            await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);
        }

        Guid? receivableId = null;
        for (var attempt = 0; attempt < 20 && receivableId is null; attempt++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(TenantId.From(tenantId));
            var financeiroDbContext = scope.ServiceProvider.GetRequiredService<FinanceiroDbContext>();
            var receivable = await financeiroDbContext.AccountsReceivable.AsNoTracking()
                .SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken);
            if (receivable is not null)
            {
                receivableId = receivable.Id.Value;
                break;
            }
            await Task.Delay(250, cancellationToken);
        }
        receivableId.ShouldNotBeNull();

        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-receber/{receivableId}/receber", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var revenueResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quanto faturei esse mes?" }, cancellationToken);
        revenueResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var revenueAnswer = (await revenueResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("answer").GetString();
        revenueAnswer.ShouldNotBeNullOrWhiteSpace();
        revenueAnswer.ShouldContain("45,90");
    }

    // Decodifica o JWT sem validar assinatura, so pra ler a claim tenant_id --
    // seguro aqui porque e o proprio access token que o teste acabou de
    // receber do backend real (mesmo padrao de FinanceiroTests).
    private static Guid GetTenantIdFromToken(string accessToken)
    {
        var payloadSegment = accessToken.Split('.')[1];
        var padded = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');
        var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        return JsonDocument.Parse(payloadJson).RootElement.GetProperty("tenant_id").GetGuid();
    }

    [Fact]
    public async Task Asking_Expenses_Should_Use_Fast_Path_With_Real_Paid_Amount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/financeiro/contas-a-pagar",
            new { description = "Aluguel", amount = 500m, dueDate = DateOnly.FromDateTime(DateTime.UtcNow), category = "Rent" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payableId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-pagar/{payableId}/pagar", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Quanto gastei esse mes?" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("500,00");
    }

    [Fact]
    public async Task Asking_Something_Outside_Fast_Path_Coverage_Without_Ai_Provider_Should_Return_A_Graceful_Message()
    {
        // Fase 5: sem chave de IA configurada e nenhuma regra de fast-path
        // batendo, o assistente nao falha mais com 500 -- responde de forma
        // graciosa (regra do produto: nunca inventar, avisar quando nao sabe).
        var cancellationToken = TestContext.Current.CancellationToken;

        using var unconfiguredFactory = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("AiAssistant:ApiKey", string.Empty));
        using var client = unconfiguredFactory.CreateClient();

        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/assistant/ask", new { question = "Me conte uma piada sobre gatos" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var answer = body.GetProperty("answer").GetString();
        answer.ShouldNotBeNullOrWhiteSpace();
        answer.ShouldContain("Ainda nao sei responder");
    }

    private static async Task<Guid> CreateCustomerAsync(
        HttpClient client, string accessToken, string fullName, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }

    private async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginReturningTenantIdAsync(
        HttpClient client, CancellationToken cancellationToken)
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
