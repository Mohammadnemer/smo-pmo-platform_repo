using SmoPmo.Smo;
using Xunit;

namespace Smo.Tests;

public sealed class KpiEvaluationTests
{
    [Theory]
    // Higher is better: default bands are green ≥ 95%, amber ≥ 80%.
    [InlineData(100, 96, KpiDirection.HigherIsBetter, RagStatus.Green, 96)]
    [InlineData(100, 95, KpiDirection.HigherIsBetter, RagStatus.Green, 95)]
    [InlineData(100, 94, KpiDirection.HigherIsBetter, RagStatus.Amber, 94)]
    [InlineData(100, 80, KpiDirection.HigherIsBetter, RagStatus.Amber, 80)]
    [InlineData(100, 79, KpiDirection.HigherIsBetter, RagStatus.Red, 79)]
    [InlineData(100, 0, KpiDirection.HigherIsBetter, RagStatus.Red, 0)]
    // Lower is better: under target is over-achievement.
    [InlineData(5, 4, KpiDirection.LowerIsBetter, RagStatus.Green, 125)]
    [InlineData(5, 5, KpiDirection.LowerIsBetter, RagStatus.Green, 100)]
    [InlineData(5, 6, KpiDirection.LowerIsBetter, RagStatus.Amber, 83.33)]
    [InlineData(5, 10, KpiDirection.LowerIsBetter, RagStatus.Red, 50)]
    public void EvaluatesBandsFromTargetAndActual(
        decimal target,
        decimal actual,
        KpiDirection direction,
        RagStatus expectedStatus,
        decimal expectedAchievement)
    {
        var result = KpiEvaluation.Evaluate(target, actual, direction, 95m, 80m);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedAchievement, result.AchievementPercent);
    }

    [Fact]
    public void AKpiWithNoActualHasNoRagStatus()
    {
        var result = KpiEvaluation.Evaluate(target: 100m, actual: null, KpiDirection.HigherIsBetter, 95m, 80m);

        Assert.Equal(RagStatus.NotSet, result.Status);
        Assert.Null(result.AchievementPercent);
    }

    [Fact]
    public void AKpiWithNoTargetHasNoRagStatus()
    {
        var result = KpiEvaluation.Evaluate(target: null, actual: 42m, KpiDirection.HigherIsBetter, 95m, 80m);

        Assert.Equal(RagStatus.NotSet, result.Status);
        Assert.Null(result.AchievementPercent);
    }

    [Fact]
    public void CustomBandsAreRespected()
    {
        var result = KpiEvaluation.Evaluate(target: 100m, actual: 70m, KpiDirection.HigherIsBetter,
            greenThresholdPercent: 70m, amberThresholdPercent: 50m);

        Assert.Equal(RagStatus.Green, result.Status);
    }

    [Theory]
    // Zero targets and zero actuals would divide by zero; they are answered, not thrown.
    [InlineData(0, 10, KpiDirection.HigherIsBetter, RagStatus.Green)]
    [InlineData(0, 10, KpiDirection.LowerIsBetter, RagStatus.Red)]
    [InlineData(10, 0, KpiDirection.LowerIsBetter, RagStatus.Green)]
    public void ZeroTargetsAndActualsDoNotBlowUp(
        decimal target,
        decimal actual,
        KpiDirection direction,
        RagStatus expectedStatus)
    {
        var result = KpiEvaluation.Evaluate(target, actual, direction, 95m, 80m);

        Assert.Equal(expectedStatus, result.Status);
    }
}
