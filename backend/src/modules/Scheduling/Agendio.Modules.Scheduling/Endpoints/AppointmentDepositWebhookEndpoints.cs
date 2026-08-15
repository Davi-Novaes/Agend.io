using System.Security.Cryptography;
using System.Text;
using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Payments;
using Agendio.Modules.Scheduling.Application.ProcessAppointmentDepositWebhook;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Scheduling.Endpoints;

/// <summary>
/// Webhook DEDICADO ao sinal de agendamento — separado do webhook de assinatura
/// da plataforma (/api/webhooks/asaas, modulo Billing), com segredo proprio.
/// Nunca envolve JWT/rota com tenantId: o tenant e reconstruido a partir do
/// externalReference que a propria Agendio embutiu ao criar a cobranca.
/// </summary>
public sealed class AppointmentDepositWebhookEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/webhooks/asaas-appointment-deposits", async (
            HttpRequest httpRequest, AppointmentDepositWebhookPayload payload, IOptions<PaymentGatewayOptions> options,
            IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var receivedToken = httpRequest.Headers["asaas-access-token"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(receivedToken),
                    Encoding.UTF8.GetBytes(options.Value.AppointmentDepositWebhookSecret)))
            {
                return Results.Unauthorized();
            }

            var referenceParts = (payload.Payment.ExternalReference ?? string.Empty).Split(':', 2);
            if (referenceParts.Length != 2
                || !Guid.TryParse(referenceParts[0], out var tenantId)
                || !Guid.TryParse(referenceParts[1], out var depositId))
            {
                // externalReference nao e nosso (ou veio corrompido) — nada a
                // processar, mas devolve 200 para a Asaas nao ficar reenviando.
                return Results.Ok();
            }

            var command = new ProcessAppointmentDepositWebhookCommand(tenantId, depositId, payload.Event);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.Ok() : result.Error.ToProblemResult();
        })
        .AllowAnonymous()
        .WithTags("Scheduling")
        .WithName("AsaasAppointmentDepositWebhook")
        .WithSummary("Recebe eventos de pagamento da Asaas para sinais de agendamento (Fase 16).");
    }

    private sealed record AppointmentDepositWebhookPayload(string Event, AppointmentDepositWebhookPayment Payment);

    private sealed record AppointmentDepositWebhookPayment(string Id, string Status, string? ExternalReference);
}
