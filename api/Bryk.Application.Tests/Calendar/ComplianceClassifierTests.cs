using Bryk.Application.Calendar;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Calendar;

public class ComplianceClassifierTests
{
    // Fixed today so the past/future boundary tests are deterministic (ADR-0008 §1 — "today" is
    // DateOnly.FromDateTime(DateTime.UtcNow) in production; tests pass it in explicitly).
    private static readonly DateOnly Today = new(2026, 6, 19);

    // ── grey / red boundary (no completion) ──

    [Fact]
    public void FuturePlanned_NoCompletion_IsGrey()
    {
        Classify(ScheduledDate: Today.AddDays(1), HasCompletion: false).Should().Be(ComplianceBucket.Grey);
    }

    [Fact]
    public void TodayPlanned_NoCompletion_IsGrey()
    {
        // The day isn't over — today stays grey until UTC midnight rolls past (ADR-0008 §1).
        Classify(ScheduledDate: Today, HasCompletion: false).Should().Be(ComplianceBucket.Grey);
    }

    [Fact]
    public void PastPlanned_NoCompletion_IsRed()
    {
        Classify(ScheduledDate: Today.AddDays(-1), HasCompletion: false).Should().Be(ComplianceBucket.Red);
    }

    // ── ratio boundaries with a planned load ──

    [Fact]
    public void TodayPlanned_CompletionRatio1_IsGreen()
    {
        Classify(ScheduledDate: Today, plannedLoad: 100m, completedLoad: 100m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Green);
    }

    [Fact]
    public void PastPlanned_Ratio08_IsGreen_BoundaryInclusive()
    {
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 100m, completedLoad: 80m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Green);
    }

    [Fact]
    public void PastPlanned_Ratio12_IsGreen_BoundaryInclusive()
    {
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 100m, completedLoad: 120m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Green);
    }

    [Fact]
    public void PastPlanned_Ratio079_IsYellow()
    {
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 100m, completedLoad: 79m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Yellow);
    }

    [Fact]
    public void PastPlanned_Ratio121_IsYellow_UpperTail()
    {
        // (1.2, ∞) is yellow, not red — the upper tail of the yellow band (ADR-0008 §1).
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 100m, completedLoad: 121m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Yellow);
    }

    [Fact]
    public void PastPlanned_Ratio05_IsYellow_BoundaryInclusive()
    {
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 100m, completedLoad: 50m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Yellow);
    }

    [Fact]
    public void PastPlanned_Ratio049_IsRed()
    {
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 100m, completedLoad: 49m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Red);
    }

    // ── null-load fallback chain ──

    [Fact]
    public void NullPlannedLoad_PlannedDurationSet_DurationRatio1_IsGreen()
    {
        Classify(
            ScheduledDate: Today.AddDays(-1),
            plannedLoad: null,
            plannedDurationSeconds: 3600,
            completedLoad: null,
            completedDurationSeconds: 3600,
            HasCompletion: true)
        .Should().Be(ComplianceBucket.Green);
    }

    [Fact]
    public void NullPlannedLoad_NullPlannedDuration_CompletionExists_IsGreen_TailBranch()
    {
        // Neither planned load nor duration → completion is green (the tail of the fallback chain).
        Classify(
            ScheduledDate: Today.AddDays(-1),
            plannedLoad: null,
            plannedDurationSeconds: null,
            completedLoad: null,
            completedDurationSeconds: null,
            HasCompletion: true)
        .Should().Be(ComplianceBucket.Green);
    }

    [Fact]
    public void PlannedLoadZero_CompletionExists_IsGreen_NoDivByZero()
    {
        // Planned 0 + completed anything → green (degenerate, don't div-by-zero).
        Classify(ScheduledDate: Today.AddDays(-1), plannedLoad: 0m, completedLoad: 50m, HasCompletion: true)
            .Should().Be(ComplianceBucket.Green);
    }

    [Fact]
    public void NullPlannedLoad_PlannedDurationSet_NoCompletion_Past_IsRed()
    {
        // The 0.0-ratio path: planned duration set but no completion (past) → red.
        Classify(
            ScheduledDate: Today.AddDays(-1),
            plannedLoad: null,
            plannedDurationSeconds: 3600,
            completedLoad: null,
            completedDurationSeconds: null,
            HasCompletion: false)
        .Should().Be(ComplianceBucket.Red);
    }

    // ── helpers ──

    // Wraps the record to keep the per-test call sites readable; nulls default the irrelevant fields.
    private static ComplianceBucket Classify(
        DateOnly ScheduledDate,
        decimal? plannedLoad = null,
        int? plannedDurationSeconds = null,
        decimal? completedLoad = null,
        int? completedDurationSeconds = null,
        bool HasCompletion = true)
    {
        var input = new ComplianceInput(
            ScheduledDate: ScheduledDate,
            PlannedLoad: plannedLoad,
            PlannedDurationSeconds: plannedDurationSeconds,
            CompletedLoad: completedLoad,
            CompletedDurationSeconds: completedDurationSeconds,
            HasCompletion: HasCompletion,
            Today: Today);
        return ComplianceClassifier.Classify(input);
    }
}
