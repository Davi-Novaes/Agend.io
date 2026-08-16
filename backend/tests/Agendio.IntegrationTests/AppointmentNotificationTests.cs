using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Confirmacao e lembretes (Sprint 5). O teste de confirmacao passa pelo
/// pipeline inteiro de verdade (agendar -> Hangfire -> AppointmentNotificationJobs
/// -> SMTP -> MailHog). Lembrete de verdade exigiria esperar 24h/2h, entao os
/// testes de lembrete chamam o job diretamente (mesmo metodo que o Hangfire
/// chamaria) para provar a regra mais importante do design: um lembrete so sai
/// se o horario dele ainda bater com o agendamento atual — sem isso, remarcar
/// ou cancelar deixaria um lembrete errado escapar.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class AppointmentNotificationTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Scheduling_An_Appointment_Sends_A_Real_Confirmation_Email_Via_Smtp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId, customerEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var found = await PollMailHogForAsync(customerEmail, cancellationToken);
        found.ShouldBeTrue("o e-mail de confirmacao deveria ter chegado no MailHog.");
    }

    [Fact]
    public async Task Reminder_Job_Sends_An_Email_When_The_Appointment_Still_Matches_The_Expected_Time()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        var (customerId, resourceId, serviceId, customerEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var startAtUtc = DateTimeOffset.UtcNow.AddDays(1);
        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        using var scope = fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<AppointmentNotificationJobs>();
        await jobs.SendReminderEmailAsync(tenantId, appointmentId, startAtUtc, ReminderLeadTime.TwentyFourHours, cancellationToken);

        var found = await PollMailHogForAsync(customerEmail, cancellationToken, subjectContains: "Lembrete");
        found.ShouldBeTrue("o lembrete deveria ter chegado no MailHog quando o horario esperado bate com o agendamento.");
    }

    [Fact]
    public async Task Reminder_Job_Is_A_No_Op_When_The_Appointment_Was_Rescheduled_Since_It_Was_Queued()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        var (customerId, resourceId, serviceId, customerEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var originalStartAtUtc = DateTimeOffset.UtcNow.AddDays(1);
        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = originalStartAtUtc, notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        // Remarca para outro horario — um job de lembrete que ja estava na fila
        // para o horario ORIGINAL nao pode mais disparar.
        var newStartAtUtc = originalStartAtUtc.AddDays(1);
        var rescheduleResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/reschedule", new { newStartAtUtc }, cancellationToken);
        rescheduleResponse.EnsureSuccessStatusCode();

        // Remarcar agora tambem enfileira seu proprio aviso por e-mail (Fase 6)
        // — espera ele chegar antes de medir a contagem "antes", senao a chegada
        // assincrona desse aviso poderia ser confundida com o envio indevido
        // que este teste esta verificando que NAO acontece.
        var rescheduleNoticeArrived = await PollMailHogForAsync(customerEmail, cancellationToken, subjectContains: "remarcado");
        rescheduleNoticeArrived.ShouldBeTrue("o aviso de remarcacao deveria ter chegado no MailHog.");

        var countBefore = await CountMailHogMessagesForAsync(customerEmail, cancellationToken);

        using var scope = fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<AppointmentNotificationJobs>();
        await jobs.SendReminderEmailAsync(tenantId, appointmentId, originalStartAtUtc, ReminderLeadTime.TwentyFourHours, cancellationToken);

        // Da um tempo para um envio indevido (se houvesse) chegar, depois confirma que nada mudou.
        await Task.Delay(500, cancellationToken);
        var countAfter = await CountMailHogMessagesForAsync(customerEmail, cancellationToken);

        countAfter.ShouldBe(countBefore);
    }

    [Fact]
    public async Task Reminder_Job_Is_A_No_Op_When_The_24h_Reminder_Is_Disabled_For_The_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        var (customerId, resourceId, serviceId, customerEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var disableResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/reminder-settings",
            new { reminder24hEnabled = false, reminder2hEnabled = true, postServiceThankYouEnabled = true },
            cancellationToken);
        disableResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var startAtUtc = DateTimeOffset.UtcNow.AddDays(1);
        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc, notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        // Confirmacao (Scheduled) nao e afetada pelo toggle — so confirma que
        // ela chegou antes de testar o lembrete, pra nao confundir "24h
        // desligado" com "nada sai desse tenant".
        var confirmationArrived = await PollMailHogForAsync(customerEmail, cancellationToken, subjectContains: "confirmado");
        confirmationArrived.ShouldBeTrue("a confirmacao deveria ter chegado normalmente — so o lembrete 24h esta desligado.");
        var countBefore = await CountMailHogMessagesForAsync(customerEmail, cancellationToken);

        using var scope = fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<AppointmentNotificationJobs>();
        await jobs.SendReminderEmailAsync(tenantId, appointmentId, startAtUtc, ReminderLeadTime.TwentyFourHours, cancellationToken);

        await Task.Delay(500, cancellationToken);
        var countAfter = await CountMailHogMessagesForAsync(customerEmail, cancellationToken);

        countAfter.ShouldBe(countBefore);
    }

    [Fact]
    public async Task Completed_Notification_Is_A_No_Op_When_Post_Service_Thank_You_Is_Disabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        var (customerId, resourceId, serviceId, customerEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var disableResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/reminder-settings",
            new { reminder24hEnabled = true, reminder2hEnabled = true, postServiceThankYouEnabled = false },
            cancellationToken);
        disableResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddMinutes(5), notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken);
        var completeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken);
        completeResponse.EnsureSuccessStatusCode();

        var countBefore = await CountMailHogMessagesForAsync(customerEmail, cancellationToken);

        using var scope = fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<AppointmentNotificationJobs>();
        await jobs.SendCompletedNotificationAsync(tenantId, appointmentId, cancellationToken);

        await Task.Delay(500, cancellationToken);
        var countAfter = await CountMailHogMessagesForAsync(customerEmail, cancellationToken);

        countAfter.ShouldBe(countBefore, "o obrigado pos-atendimento nao deveria ter sido enviado com o toggle desligado.");
    }

    [Fact]
    public async Task Confirmation_Email_Is_Recorded_In_The_Notification_History()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerId, resourceId, serviceId, customerEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var appointmentId = createBody.GetProperty("id").GetGuid();

        var confirmationArrived = await PollMailHogForAsync(customerEmail, cancellationToken, subjectContains: "confirmado");
        confirmationArrived.ShouldBeTrue();

        // O envio real acontece via Hangfire (fora da requisicao); da um
        // instante pro job persistir o NotificationLogEntry antes de consultar.
        var historyResponse = await PollNotificationHistoryForAsync(client, accessToken, appointmentId, cancellationToken);
        historyResponse.ShouldNotBeNull("o historico deveria ter um registro do e-mail de confirmacao enviado.");

        historyResponse!.Value.GetProperty("channel").GetString().ShouldBe("Email");
        historyResponse.Value.GetProperty("trigger").GetString().ShouldBe("Scheduled");
        historyResponse.Value.GetProperty("status").GetString().ShouldBe("Sent");
        historyResponse.Value.GetProperty("customerName").GetString().ShouldBe("Cliente Notificacao");
    }

    [Fact]
    public async Task Notification_History_Can_Be_Filtered_By_Customer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (customerAId, resourceAId, serviceAId, customerAEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);
        var (customerBId, resourceBId, serviceBId, customerBEmail) = await SetUpBookingPrerequisitesAsync(client, accessToken, cancellationToken);

        await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId = customerAId, resourceId = resourceAId, serviceId = serviceAId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId = customerBId, resourceId = resourceBId, serviceId = serviceBId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);

        (await PollMailHogForAsync(customerAEmail, cancellationToken, subjectContains: "confirmado")).ShouldBeTrue();
        (await PollMailHogForAsync(customerBEmail, cancellationToken, subjectContains: "confirmado")).ShouldBeTrue();

        JsonElement? filteredBody = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(
                client, accessToken, $"/api/appointments/notifications?page=1&pageSize=20&customerId={customerAId}", cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (body.GetProperty("items").GetArrayLength() > 0)
            {
                filteredBody = body;
                break;
            }

            await Task.Delay(300, cancellationToken);
        }

        filteredBody.ShouldNotBeNull("o historico filtrado por cliente deveria ter pelo menos um registro.");
        var items = filteredBody!.Value.GetProperty("items").EnumerateArray().ToList();
        items.ShouldNotBeEmpty();
        items.ShouldAllBe(item => item.GetProperty("customerId").GetGuid() == customerAId);
    }

    private static async Task<JsonElement?> PollNotificationHistoryForAsync(
        HttpClient client, string accessToken, Guid appointmentId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(
                client, accessToken, "/api/appointments/notifications?page=1&pageSize=20", cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var match = body.GetProperty("items").EnumerateArray()
                .FirstOrDefault(item => item.GetProperty("appointmentId").GetGuid() == appointmentId);

            if (match.ValueKind != JsonValueKind.Undefined)
            {
                return match;
            }

            await Task.Delay(300, cancellationToken);
        }

        return null;
    }

    private async Task<bool> PollMailHogForAsync(string mustContain, CancellationToken cancellationToken, string? subjectContains = null)
    {
        using var mailHogClient = new HttpClient { BaseAddress = new Uri(fixture.MailHogApiBaseUrl) };
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var messagesJson = await mailHogClient.GetStringAsync("/api/v2/messages", cancellationToken);
            if (messagesJson.Contains(mustContain, StringComparison.OrdinalIgnoreCase)
                && (subjectContains is null || messagesJson.Contains(subjectContains, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }

    private async Task<int> CountMailHogMessagesForAsync(string mustContain, CancellationToken cancellationToken)
    {
        using var mailHogClient = new HttpClient { BaseAddress = new Uri(fixture.MailHogApiBaseUrl) };
        var messagesJson = await mailHogClient.GetStringAsync("/api/v2/messages", cancellationToken);
        var document = JsonDocument.Parse(messagesJson);

        return document.RootElement.GetProperty("items").EnumerateArray()
            .Count(item => item.GetRawText().Contains(mustContain, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(Guid CustomerId, Guid ResourceId, Guid ServiceId, string CustomerEmail)> SetUpBookingPrerequisitesAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var customerEmail = $"cliente-notif-{Guid.NewGuid():N}@example.com";
        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente Notificacao", email = customerEmail }, cancellationToken);
        var customerBody = await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = customerBody.GetProperty("id").GetGuid();

        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Sala de Atendimento", type = "Room", capacity = 1, description = (string?)null },
            cancellationToken);
        var resourceBody = await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Consulta", description = (string?)null, durationMinutes = 30, price = 80m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var serviceBody = await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceId = serviceBody.GetProperty("id").GetGuid();

        return (customerId, resourceId, serviceId, customerEmail);
    }

    private async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (_, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        return accessToken;
    }

    private async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsyncWithId(HttpClient client, CancellationToken cancellationToken)
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

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
