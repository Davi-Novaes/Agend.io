using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.GetPlatformDashboardMetrics;

public sealed record GetPlatformDashboardMetricsQuery : IQuery<PlatformDashboardMetrics>;

public sealed record SignupMonthPoint(string Month, int Count);

public sealed record PlatformDashboardMetrics(
    int TotalTenants,
    int ActiveTenants,
    int NewTenantsThisMonth,
    int TrialingCount,
    int ActiveSubscriptionsCount,
    int PastDueCount,
    int CanceledCount,
    decimal Mrr,
    string MrrCurrency,
    IReadOnlyList<SignupMonthPoint> NewTenantsBySeriesMonth);
