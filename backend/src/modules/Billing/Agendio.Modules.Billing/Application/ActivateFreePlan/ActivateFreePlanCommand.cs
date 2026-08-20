using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.ActivateFreePlan;

/// <summary>
/// Ativa o plano Free para o tenant autenticado atual — sem CPF/CNPJ, sem
/// tocar a Asaas (mesma logica da escolha de plano Free no onboarding, so que
/// pelo Bearer token normal em vez do token de onboarding). Cobre tanto a
/// primeira escolha de plano quanto reassinar Free depois de cancelar
/// (BL-23/BL-33, docs/BACKLOG.md) — antes disto, a unica forma de "assinar
/// Free" fora do onboarding era o formulario de plano PAGO, que pedia CPF/CNPJ
/// e tentava criar uma assinatura de R$0 na Asaas.
/// </summary>
public sealed record ActivateFreePlanCommand : ICommand;
