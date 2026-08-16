using System.Globalization;
using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Platform.Application.GetPlatformDashboardMetrics;

public sealed class GetPlatformDashboardMetricsQueryHandler(
    ITenantAdministrationService tenantAdministrationService, IBillingAdministrationService billingAdministrationService, IClock clock)
    : IQueryHandler<GetPlatformDashboardMetricsQuery, PlatformDashboardMetrics>
{
    // Semelhante padrao YYYY-MM ja usado em GetCashFlowSummaryQueryHandler
    // (Financeiro) — mesma convencao pro frontend formatar o mes.
    public async Task<Result<PlatformDashboardMetrics>> Handle(
        GetPlatformDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await tenantAdministrationService.ListAllAsync(cancellationToken);
        var subscriptions = await billingAdministrationService.ListAllAsync(cancellationToken);

        var nowUtc = clock.UtcNow;
        var currentMonthStart = new DateTimeOffset(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var totalTenants = tenants.Count;
        var activeTenants = tenants.Count(t => t.IsActive);
        var newTenantsThisMonth = tenants.Count(t => t.CreatedAtUtc >= currentMonthStart);

        var trialingCount = subscriptions.Count(s => s.Status == "Trialing");
        var activeSubscriptionsCount = subscriptions.Count(s => s.Status == "Active");
        var pastDueCount = subscriptions.Count(s => s.Status == "PastDue");
        var canceledCount = subscriptions.Count(s => s.Status == "Canceled");

        var activeSubscriptions = subscriptions.Where(s => s.Status == "Active").ToList();
        var mrr = activeSubscriptions.Sum(s => s.PlanPriceAmount);
        var mrrCurrency = activeSubscriptions.Count > 0 ? activeSubscriptions[0].PlanCurrency : "BRL";

        var seriesStart = currentMonthStart.AddMonths(-5);
        var newTenantsBySeriesMonth = Enumerable.Range(0, 6)
            .Select(offset => seriesStart.AddMonths(offset))
            .Select(monthStart => new SignupMonthPoint(
                FormatMonth(monthStart),
                tenants.Count(t => t.CreatedAtUtc.Year == monthStart.Year && t.CreatedAtUtc.Month == monthStart.Month)))
            .ToList();

        var metrics = new PlatformDashboardMetrics(
            totalTenants, activeTenants, newTenantsThisMonth,
            trialingCount, activeSubscriptionsCount, pastDueCount, canceledCount,
            mrr, mrrCurrency, newTenantsBySeriesMonth);

        return Result.Success(metrics);
    }

    private static string FormatMonth(DateTimeOffset month) =>
        string.Create(CultureInfo.InvariantCulture, $"{month.Year:D4}-{month.Month:D2}");
}
