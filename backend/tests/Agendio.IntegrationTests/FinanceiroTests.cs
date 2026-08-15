using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o fluxo automatico (agendamento concluido -> conta a receber + comissao
/// via FinancialIntegrationEventConsumer, segundo consumidor de RabbitMQ do
/// projeto — ver Sprint 7/BillingTests.cs para o primeiro), os lancamentos
/// manuais, o fluxo de caixa e o isolamento cruzado de tenant.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class FinanceiroTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Completing_An_Appointment_Without_Commission_Rule_Should_Only_Create_A_Receivable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, resourceId, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, cancellationToken);

        var receivable = await PollAsync(
            tenantId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        receivable.ShouldNotBeNull();
        receivable.Status.ShouldBe(AccountReceivableStatus.Pending);
        receivable.Amount.Amount.ShouldBe(45.90m);

        // O consumidor cria receita + (se houver regra) comissao na MESMA
        // chamada, antes do SaveChanges — se a receita ja chegou e nao ha regra
        // configurada para este recurso, a comissao nunca vai chegar.
        await using var scope = fixture.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(TenantId.From(tenantId));
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceiroDbContext>();
        var payable = await dbContext.AccountsPayable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken);
        payable.ShouldBeNull();

        _ = resourceId;
    }

    [Fact]
    public async Task Completing_An_Appointment_With_Active_Commission_Rule_Should_Create_Receivable_And_Commission_Payable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var commissionResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/financeiro/comissoes/{resourceId}",
            new { calculationType = "Percentage", value = 20m }, cancellationToken);
        commissionResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var appointmentId = await ScheduleConfirmStartCompleteAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(client, accessToken, cancellationToken);

        var receivable = await PollAsync(
            tenantId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);
        receivable.ShouldNotBeNull();

        var payable = await PollAsync(
            tenantId, dbContext => dbContext.AccountsPayable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        payable.ShouldNotBeNull();
        payable.Category.ShouldBe(ExpenseCategory.Commission);
        payable.ResourceId.ShouldBe(resourceId);
        payable.Amount.Amount.ShouldBe(9.18m); // 20% de 45.90
    }

    [Fact]
    public async Task Duplicate_SourceAppointmentId_Should_Be_Rejected_By_The_Unique_Index()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, cancellationToken);

        await PollAsync(
            tenantId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        // Mesma protecao que torna o consumidor idempotente sob redelivery do
        // RabbitMQ: mesmo que o codigo esquecesse de checar antes de inserir, o
        // indice unico parcial (ver AccountReceivableConfiguration) barra no banco.
        await using var scope = fixture.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(TenantId.From(tenantId));
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceiroDbContext>();

        var duplicate = AccountReceivable.Create(
            TenantId.From(tenantId), "Duplicata", Money.Create(10m).Value, DateOnly.FromDateTime(DateTime.UtcNow), appointmentId).Value;
        dbContext.AccountsReceivable.Add(duplicate);

        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Owner_Can_Mark_A_Receivable_As_Received_And_A_Payable_As_Paid()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, cancellationToken);

        var receivable = await PollAsync(
            tenantId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);
        receivable.ShouldNotBeNull();

        var receiveResponse = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-receber/{receivable.Id.Value}/receber", new { }, cancellationToken);
        receiveResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Uma segunda confirmacao (ja Received, nao mais Pending) precisa falhar.
        var secondAttempt = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-receber/{receivable.Id.Value}/receber", new { }, cancellationToken);
        secondAttempt.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Owner_Can_Create_A_Manual_Expense()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/financeiro/contas-a-pagar",
            new { description = "Aluguel de agosto", amount = 1200m, dueDate = DateOnly.FromDateTime(DateTime.UtcNow), category = "Rent" },
            cancellationToken);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/financeiro/contas-a-pagar", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Cash_Flow_Summary_Should_Aggregate_Received_And_Paid_Amounts_In_The_Period()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, _, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, accessToken, cancellationToken);

        var receivable = await PollAsync(
            tenantId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);
        receivable.ShouldNotBeNull();

        await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-receber/{receivable.Id.Value}/receber", new { }, cancellationToken);

        var createPayableResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/financeiro/contas-a-pagar",
            new { description = "Insumos", amount = 30m, dueDate = DateOnly.FromDateTime(DateTime.UtcNow), category = "Supplies" },
            cancellationToken);
        var createPayableBody = await createPayableResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var payableId = createPayableBody.GetProperty("id").GetGuid();

        await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-pagar/{payableId}/pagar", new { }, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summaryResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/financeiro/fluxo-de-caixa?from={AuthorizedRequestHelpers.Iso(today.AddDays(-1))}&to={AuthorizedRequestHelpers.Iso(today.AddDays(1))}", cancellationToken);
        summaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var summary = await summaryResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        summary.GetProperty("totalReceived").GetDecimal().ShouldBe(45.90m);
        summary.GetProperty("totalPaid").GetDecimal().ShouldBe(30m);
        summary.GetProperty("netBalance").GetDecimal().ShouldBe(15.90m);
    }

    [Fact]
    public async Task Commission_Report_Should_Aggregate_Pending_And_Paid_Amounts_By_Professional()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(client, accessToken, cancellationToken);

        var (customerId, resourceAId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);
        (await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/financeiro/comissoes/{resourceAId}", new { calculationType = "Percentage", value = 20m }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var appointmentAId = await ScheduleConfirmStartCompleteAsync(client, accessToken, customerId, resourceAId, serviceId, cancellationToken);

        var resourceBResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources", new { name = "Barbeiro 2", type = "Person", capacity = 1, description = (string?)null },
            cancellationToken);
        var resourceBId = (await resourceBResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        (await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/financeiro/comissoes/{resourceBId}", new { calculationType = "Percentage", value = 10m }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var appointmentBId = await ScheduleConfirmStartCompleteAsync(client, accessToken, customerId, resourceBId, serviceId, cancellationToken);

        var payableA = await PollAsync(
            tenantId, dbContext => dbContext.AccountsPayable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentAId, cancellationToken),
            cancellationToken);
        payableA.ShouldNotBeNull();
        var payableB = await PollAsync(
            tenantId, dbContext => dbContext.AccountsPayable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentBId, cancellationToken),
            cancellationToken);
        payableB.ShouldNotBeNull();

        // A recebe pagamento confirmado, B fica pendente — o relatorio precisa
        // refletir os dois estados separadamente, por profissional.
        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/financeiro/contas-a-pagar/{payableA.Id.Value}/pagar", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reportResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken,
            $"/api/financeiro/comissoes/relatorio?from={AuthorizedRequestHelpers.Iso(today.AddDays(-1))}&to={AuthorizedRequestHelpers.Iso(today.AddDays(1))}",
            cancellationToken);
        reportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entries = await reportResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        entries.GetArrayLength().ShouldBe(2);

        var entryA = entries.EnumerateArray().Single(e => e.GetProperty("resourceId").GetGuid() == resourceAId);
        entryA.GetProperty("resourceName").GetString().ShouldBe("Barbeiro 1");
        entryA.GetProperty("pendingAmount").GetDecimal().ShouldBe(0m);
        entryA.GetProperty("paidAmount").GetDecimal().ShouldBe(9.18m); // 20% de 45.90
        entryA.GetProperty("totalAmount").GetDecimal().ShouldBe(9.18m);

        var entryB = entries.EnumerateArray().Single(e => e.GetProperty("resourceId").GetGuid() == resourceBId);
        entryB.GetProperty("resourceName").GetString().ShouldBe("Barbeiro 2");
        entryB.GetProperty("pendingAmount").GetDecimal().ShouldBe(4.59m); // 10% de 45.90
        entryB.GetProperty("paidAmount").GetDecimal().ShouldBe(0m);
        entryB.GetProperty("totalAmount").GetDecimal().ShouldBe(4.59m);
    }

    [Fact]
    public async Task Commission_Report_For_A_Period_With_No_Commissions_Should_Return_An_Empty_List()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Agendamento concluido sem CommissionRule configurada: gera receita,
        // nunca comissao (ver teste analogo de fluxo de caixa acima).
        await CreateAndCompleteAppointmentAsync(client, accessToken, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reportResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken,
            $"/api/financeiro/comissoes/relatorio?from={AuthorizedRequestHelpers.Iso(today.AddDays(-1))}&to={AuthorizedRequestHelpers.Iso(today.AddDays(1))}",
            cancellationToken);
        reportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entries = await reportResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        entries.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Commission_Report_Is_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantAId = await GetTenantIdFromTokenAsync(client, tenantAToken, cancellationToken);
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, tenantAToken, cancellationToken);
        (await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, tenantAToken, $"/api/financeiro/comissoes/{resourceId}", new { calculationType = "Percentage", value = 20m }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var appointmentId = await ScheduleConfirmStartCompleteAsync(client, tenantAToken, customerId, resourceId, serviceId, cancellationToken);
        await PollAsync(
            tenantAId, dbContext => dbContext.AccountsPayable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reportResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken,
            $"/api/financeiro/comissoes/relatorio?from={AuthorizedRequestHelpers.Iso(today.AddDays(-1))}&to={AuthorizedRequestHelpers.Iso(today.AddDays(1))}",
            cancellationToken);
        reportResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var entries = await reportResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        entries.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_Or_Act_On_A_Receivable_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, _, appointmentId, tenantAId) = await CreateAndCompleteAppointmentAsync(client, tenantAToken, cancellationToken);

        var receivable = await PollAsync(
            tenantAId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);
        receivable.ShouldNotBeNull();

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var crossTenantAttempt = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, tenantBToken, $"/api/financeiro/contas-a-receber/{receivable.Id.Value}/receber", new { }, cancellationToken);
        crossTenantAttempt.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var tenantBList = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, "/api/financeiro/contas-a-receber", cancellationToken);
        var tenantBListBody = await tenantBList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        tenantBListBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task A_Staff_Member_Should_Not_Be_Able_To_Manage_Sensitive_Financeiro_Actions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var ownerToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (_, resourceId, appointmentId, tenantId) = await CreateAndCompleteAppointmentAsync(client, ownerToken, cancellationToken);

        var receivable = await PollAsync(
            tenantId, dbContext => dbContext.AccountsReceivable.SingleOrDefaultAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken),
            cancellationToken);
        receivable.ShouldNotBeNull();

        var createPayableResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerToken, "/api/financeiro/contas-a-pagar",
            new { description = "Insumos", amount = 30m, dueDate = DateOnly.FromDateTime(DateTime.UtcNow), category = "Supplies" },
            cancellationToken);
        var payableId = (await createPayableResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var staffToken = await InviteAndLoginStaffAsync(client, ownerToken, cancellationToken);

        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, staffToken, $"/api/financeiro/contas-a-receber/{receivable.Id.Value}/receber", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, staffToken, $"/api/financeiro/contas-a-pagar/{payableId}/pagar", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, staffToken, $"/api/financeiro/contas-a-pagar/{payableId}/cancelar", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, staffToken, $"/api/financeiro/comissoes/{resourceId}",
            new { calculationType = "Percentage", value = 10m }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await AuthorizedRequestHelpers.DeleteAuthorizedAsync(
            client, staffToken, $"/api/financeiro/comissoes/{resourceId}", cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Nada mudou: nenhuma tentativa de Staff teve efeito.
        var receivableAfter = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, ownerToken, "/api/financeiro/contas-a-receber", cancellationToken);
        var receivableAfterBody = await receivableAfter.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        receivableAfterBody.GetProperty("items")[0].GetProperty("status").GetString().ShouldBe("Pending");
    }

    private static async Task<string> InviteAndLoginStaffAsync(HttpClient client, string ownerAccessToken, CancellationToken cancellationToken)
    {
        var staffEmail = $"staff-{Guid.NewGuid():N}@example.com";
        var inviteResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerAccessToken, "/api/team/invitations", new { email = staffEmail, role = "Staff" }, cancellationToken);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = inviteBody.GetProperty("token").GetString()!;

        await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept", new { fullName = "Funcionario", password = Password }, cancellationToken);

        var tenantId = await GetTenantIdFromTokenAsync(client, ownerAccessToken, cancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = staffEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }

    private async Task<(Guid CustomerId, Guid ResourceId, Guid AppointmentId, Guid TenantId)> CreateAndCompleteAppointmentAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var (customerId, resourceId, serviceId) = await CreateBookingPrerequisitesAsync(client, accessToken, cancellationToken);
        var appointmentId = await ScheduleConfirmStartCompleteAsync(client, accessToken, customerId, resourceId, serviceId, cancellationToken);
        var tenantId = await GetTenantIdFromTokenAsync(client, accessToken, cancellationToken);

        return (customerId, resourceId, appointmentId, tenantId);
    }

    private async Task<Guid> ScheduleConfirmStartCompleteAsync(
        HttpClient client, string accessToken, Guid customerId, Guid resourceId, Guid serviceId, CancellationToken cancellationToken)
    {
        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/confirm", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // O outbox de Scheduling so drena pro RabbitMQ no proximo tick do job
        // recorrente (a cada minuto) — forcar o drain aqui evita o teste ter
        // que esperar ate 60s de verdade pelo consumidor do Financeiro.
        await ForceDrainSchedulingOutboxAsync(cancellationToken);

        return appointmentId;
    }

    private async Task ForceDrainSchedulingOutboxAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor<SchedulingDbContext>>();
        await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);
    }

    /// <summary>Poll com retry curto: FinancialIntegrationEventConsumer processa a mensagem de forma assincrona apos o drain do outbox.</summary>
    private async Task<T?> PollAsync<T>(
        Guid tenantId, Func<FinanceiroDbContext, Task<T?>> query, CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(TenantId.From(tenantId));
            var dbContext = scope.ServiceProvider.GetRequiredService<FinanceiroDbContext>();

            var result = await query(dbContext);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(250, cancellationToken);
        }

        return null;
    }

    private static async Task<Guid> GetTenantIdFromTokenAsync(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        // O JWT ja carrega tenant_id, mas o teste nao decodifica token — em vez
        // disso reusa a mesma rota que a UI usaria para descobrir "quem sou eu"
        // hoje nao existe /api/me, entao lemos de uma consulta que exige tenant
        // (lista de membros da equipe so tem o Owner criado no setup).
        var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/team/members", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        // /api/team/members nao devolve tenantId — decodifica o JWT (sem validar
        // assinatura, so para ler a claim, seguro aqui porque e o proprio
        // access token que o teste acabou de receber do backend real).
        var payloadSegment = accessToken.Split('.')[1];
        var padded = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');
        var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        var payload = JsonDocument.Parse(payloadJson).RootElement;

        _ = body;
        return payload.GetProperty("tenant_id").GetGuid();
    }

    private static async Task<(Guid CustomerId, Guid ResourceId, Guid ServiceId)> CreateBookingPrerequisitesAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente de Teste" }, cancellationToken);
        var customerBody = await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = customerBody.GetProperty("id").GetGuid();

        // Person (nao Room/Equipment): so profissionais sao elegiveis a comissao.
        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Barbeiro 1", type = "Person", capacity = 1, description = (string?)null },
            cancellationToken);
        var resourceBody = await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Corte de Cabelo", description = (string?)null, durationMinutes = 30, price = 45.90m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var serviceBody = await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceId = serviceBody.GetProperty("id").GetGuid();

        return (customerId, resourceId, serviceId);
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
