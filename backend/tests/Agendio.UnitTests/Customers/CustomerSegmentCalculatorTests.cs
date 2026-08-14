using Agendio.Modules.Customers.Application.ListCustomers;
using Agendio.Modules.Scheduling.Contracts;

namespace Agendio.UnitTests.Customers;

public class CustomerSegmentCalculatorTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    private const decimal NoVipThreshold = decimal.MaxValue;

    [Fact]
    public void Customer_With_No_Stats_Should_Be_Novo()
    {
        var segment = CustomerSegmentCalculator.Calculate(null, NowUtc, NoVipThreshold);

        segment.ShouldBe(CustomerSegment.Novo);
    }

    [Fact]
    public void Customer_With_Zero_Completed_Visits_Should_Be_Novo()
    {
        var stats = new CustomerVisitStatsLookupResult(Guid.NewGuid(), TotalVisits: 0, LastVisitAtUtc: null, NoShowCount: 0, TotalSpent: 0m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, NoVipThreshold);

        segment.ShouldBe(CustomerSegment.Novo);
    }

    [Fact]
    public void Customer_With_Two_Or_More_No_Shows_Should_Be_NoShow_Even_If_Recently_Visited()
    {
        var stats = new CustomerVisitStatsLookupResult(
            Guid.NewGuid(), TotalVisits: 3, LastVisitAtUtc: NowUtc.AddDays(-1), NoShowCount: 2, TotalSpent: 100m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, NoVipThreshold);

        segment.ShouldBe(CustomerSegment.NoShow);
    }

    [Fact]
    public void Customer_Whose_Last_Visit_Was_More_Than_90_Days_Ago_Should_Be_Inativo()
    {
        var stats = new CustomerVisitStatsLookupResult(
            Guid.NewGuid(), TotalVisits: 5, LastVisitAtUtc: NowUtc.AddDays(-91), NoShowCount: 0, TotalSpent: 500m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, NoVipThreshold);

        segment.ShouldBe(CustomerSegment.Inativo);
    }

    [Fact]
    public void Customer_Whose_Last_Visit_Was_Between_45_And_90_Days_Ago_Should_Be_EmRisco()
    {
        var stats = new CustomerVisitStatsLookupResult(
            Guid.NewGuid(), TotalVisits: 5, LastVisitAtUtc: NowUtc.AddDays(-60), NoShowCount: 0, TotalSpent: 500m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, NoVipThreshold);

        segment.ShouldBe(CustomerSegment.EmRisco);
    }

    [Fact]
    public void Customer_With_Enough_Visits_And_Spend_Above_The_Vip_Threshold_Should_Be_Vip()
    {
        var stats = new CustomerVisitStatsLookupResult(
            Guid.NewGuid(), TotalVisits: 5, LastVisitAtUtc: NowUtc.AddDays(-1), NoShowCount: 0, TotalSpent: 1000m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, vipSpendThreshold: 900m);

        segment.ShouldBe(CustomerSegment.Vip);
    }

    [Fact]
    public void Customer_With_High_Spend_But_Too_Few_Visits_Should_Not_Be_Vip()
    {
        var stats = new CustomerVisitStatsLookupResult(
            Guid.NewGuid(), TotalVisits: 4, LastVisitAtUtc: NowUtc.AddDays(-1), NoShowCount: 0, TotalSpent: 1000m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, vipSpendThreshold: 900m);

        segment.ShouldBe(CustomerSegment.Recorrente);
    }

    [Fact]
    public void Recently_Visiting_Customer_Below_The_Vip_Threshold_Should_Be_Recorrente()
    {
        var stats = new CustomerVisitStatsLookupResult(
            Guid.NewGuid(), TotalVisits: 3, LastVisitAtUtc: NowUtc.AddDays(-10), NoShowCount: 0, TotalSpent: 150m);

        var segment = CustomerSegmentCalculator.Calculate(stats, NowUtc, NoVipThreshold);

        segment.ShouldBe(CustomerSegment.Recorrente);
    }

    [Fact]
    public void Vip_Threshold_Should_Be_Unreachable_When_The_Sample_Is_Too_Small()
    {
        var statsWithVisits = Enumerable.Range(0, 3)
            .Select(_ => new CustomerVisitStatsLookupResult(Guid.NewGuid(), TotalVisits: 10, LastVisitAtUtc: NowUtc, NoShowCount: 0, TotalSpent: 10_000m))
            .ToList();

        var threshold = CustomerSegmentCalculator.CalculateVipSpendThreshold(statsWithVisits);

        threshold.ShouldBe(decimal.MaxValue);
    }

    [Fact]
    public void Vip_Threshold_Should_Be_The_80th_Percentile_Of_Spend()
    {
        // 5 clientes, gasto 100..500 — p80 cai no maior valor (500) com essa amostra pequena.
        var statsWithVisits = Enumerable.Range(1, 5)
            .Select(i => new CustomerVisitStatsLookupResult(Guid.NewGuid(), TotalVisits: 10, LastVisitAtUtc: NowUtc, NoShowCount: 0, TotalSpent: i * 100m))
            .ToList();

        var threshold = CustomerSegmentCalculator.CalculateVipSpendThreshold(statsWithVisits);

        threshold.ShouldBe(500m);
    }
}
