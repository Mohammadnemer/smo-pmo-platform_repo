using Microsoft.EntityFrameworkCore;

namespace SmoPmo.Smo;

/// <summary>
/// Dev-only demo data: a full Strategy → Perspective → Objective → KPI (with trend
/// measurements) → Initiative tree, with a deliberately mixed RAG spread, so F4's
/// scorecard has real data to render against the live API instead of an empty tenant.
///
/// Only ever called from Program.cs behind an <c>IsDevelopment()</c> check — never runs
/// against a real tenant. Idempotent: a strategy already existing for the current tenant
/// (set by the caller before this runs) means a prior run already seeded it.
/// </summary>
public static class SmoDemoSeed
{
    public static async Task SeedAsync(SmoDbContext db, CancellationToken ct = default)
    {
        if (await db.Strategies.AnyAsync(ct))
        {
            return;
        }

        var strategy = new Strategy
        {
            Id = Guid.NewGuid(),
            Name = "Digital Transformation Strategy 2026–2028",
            NameAr = "استراتيجية التحول الرقمي ٢٠٢٦–٢٠٢٨",
            Vision = "Become the region's most trusted integrated strategy-to-execution platform.",
            Mission = "Connect strategic intent to funded, delivered work with full visibility end to end.",
            HorizonStart = new DateOnly(2026, 1, 1),
            HorizonEnd = new DateOnly(2028, 12, 31)
        };
        db.Strategies.Add(strategy);

        // Tracks each seeded objective's id by name so the cause-effect edges below (added
        // after the tree exists) can reference them without a second round trip.
        var objectivesByName = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var perspectiveDef in Perspectives)
        {
            var perspective = new Perspective
            {
                Id = Guid.NewGuid(),
                StrategyId = strategy.Id,
                Name = perspectiveDef.Name,
                NameAr = perspectiveDef.NameAr,
                DisplayOrder = perspectiveDef.DisplayOrder
            };
            db.Perspectives.Add(perspective);

            foreach (var objectiveDef in perspectiveDef.Objectives)
            {
                var objective = new Objective
                {
                    Id = Guid.NewGuid(),
                    PerspectiveId = perspective.Id,
                    Name = objectiveDef.Name,
                    NameAr = objectiveDef.NameAr,
                    TargetState = objectiveDef.TargetState,
                    DisplayOrder = objectiveDef.DisplayOrder
                };
                db.Objectives.Add(objective);
                objectivesByName[objectiveDef.Name] = objective.Id;

                foreach (var kpiDef in objectiveDef.Kpis)
                {
                    var hasMeasurements = kpiDef.Trend.Length > 0;
                    var kpi = new Kpi
                    {
                        Id = Guid.NewGuid(),
                        ObjectiveId = objective.Id,
                        Name = kpiDef.Name,
                        NameAr = kpiDef.NameAr,
                        Unit = kpiDef.Unit,
                        Direction = kpiDef.Direction,
                        Frequency = kpiDef.Frequency,
                        Baseline = kpiDef.Baseline,
                        Target = kpiDef.Target,
                        Actual = hasMeasurements ? kpiDef.Trend[^1] : null,
                        ActualAsOf = hasMeasurements ? DateTimeOffset.UtcNow : null
                    };
                    db.Kpis.Add(kpi);

                    for (var i = 0; i < kpiDef.Trend.Length; i++)
                    {
                        var month = i + 1;
                        db.KpiMeasurements.Add(new KpiMeasurement
                        {
                            Id = Guid.NewGuid(),
                            KpiId = kpi.Id,
                            PeriodStart = new DateOnly(2026, month, 1),
                            PeriodEnd = new DateOnly(2026, month, DateTime.DaysInMonth(2026, month)),
                            Value = kpiDef.Trend[i]
                        });
                    }
                }

                if (objectiveDef.Initiative is { } initiativeDef)
                {
                    db.Initiatives.Add(new Initiative
                    {
                        Id = Guid.NewGuid(),
                        ObjectiveId = objective.Id,
                        Name = initiativeDef.Name,
                        NameAr = initiativeDef.NameAr,
                        Sponsor = initiativeDef.Sponsor,
                        Budget = initiativeDef.Budget,
                        BudgetCurrency = "USD",
                        ExpectedBenefit = initiativeDef.ExpectedBenefit,
                        StartDate = initiativeDef.StartDate,
                        EndDate = initiativeDef.EndDate,
                        Status = initiativeDef.Status
                    });
                }
            }
        }

        // Cause-effect edges for the strategy map (F5) — the classic Kaplan/Norton bottom-up
        // BSC chain: Learning & Growth feeds Internal Process, which feeds Customer, which
        // feeds Financial. Every seeded objective carries at least one edge so the map has
        // no isolated nodes.
        foreach (var (sourceName, targetName) in CauseEffectEdges)
        {
            db.ObjectiveLinks.Add(new ObjectiveLink
            {
                Id = Guid.NewGuid(),
                SourceObjectiveId = objectivesByName[sourceName],
                TargetObjectiveId = objectivesByName[targetName]
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly (string Source, string Target)[] CauseEffectEdges =
    [
        ("Increase employee training hours", "Reduce project delivery time"),
        ("Improve digital tool adoption", "Improve quality / defect rate"),
        ("Reduce project delivery time", "Improve customer satisfaction"),
        ("Improve quality / defect rate", "Improve customer satisfaction"),
        ("Improve quality / defect rate", "Improve gross margin"),
        ("Improve customer satisfaction", "Increase recurring revenue"),
        ("Expand into new markets", "Increase recurring revenue")
    ];

    private sealed record KpiDef(
        string Name, string NameAr, string Unit, KpiDirection Direction, KpiFrequency Frequency,
        decimal? Baseline, decimal? Target, decimal[] Trend);

    private sealed record InitiativeDef(
        string Name, string NameAr, string Sponsor, decimal Budget, string ExpectedBenefit,
        DateOnly StartDate, DateOnly EndDate, string Status);

    private sealed record ObjectiveDef(
        string Name, string NameAr, string TargetState, int DisplayOrder,
        KpiDef[] Kpis, InitiativeDef? Initiative = null);

    private sealed record PerspectiveDef(string Name, string NameAr, int DisplayOrder, ObjectiveDef[] Objectives);

    // Achievement percentages (and so RAG bands) below use each KPI's default thresholds
    // (green >= 95%, amber >= 80%). Spread across the tree: 4 green, 4 amber, 3 red, 1
    // not-set (no target/actual yet) — enough variety for the scorecard's RAG rendering
    // and roll-up counts to be worth looking at.
    private static readonly PerspectiveDef[] Perspectives =
    [
        new PerspectiveDef("Financial", "المالية", 0,
        [
            new ObjectiveDef(
                "Increase recurring revenue", "زيادة الإيرادات المتكررة",
                "20% YoY growth in ARR by 2028", 0,
                [
                    new KpiDef("ARR Growth Rate", "معدل نمو الإيرادات السنوية المتكررة", "%",
                        KpiDirection.HigherIsBetter, KpiFrequency.Quarterly, 8m, 20m,
                        [10m, 12m, 14m, 16m, 18m, 19.4m]),
                    new KpiDef("Net Revenue Retention", "صافي معدل الاحتفاظ بالإيرادات", "%",
                        KpiDirection.HigherIsBetter, KpiFrequency.Quarterly, 100m, 110m,
                        [95m, 98m, 90m, 92m, 89m, 88m])
                ],
                new InitiativeDef(
                    "Enterprise Upsell Program", "برنامج توسيع الحسابات الكبرى",
                    "VP Sales", 250000m, "Drive ARR growth via expansion deals into existing accounts.",
                    new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Active")),
            new ObjectiveDef(
                "Improve gross margin", "تحسين هامش الربح الإجمالي",
                "Gross margin above 55% by 2027", 1,
                [
                    new KpiDef("Gross Margin", "هامش الربح الإجمالي", "%",
                        KpiDirection.HigherIsBetter, KpiFrequency.Monthly, 42m, 55m,
                        [45m, 44m, 42m, 40m, 39m, 38m])
                ])
        ]),
        new PerspectiveDef("Customer", "العملاء", 1,
        [
            new ObjectiveDef(
                "Improve customer satisfaction", "تحسين رضا العملاء",
                "Net Promoter Score above 50 by 2027", 0,
                [
                    new KpiDef("Net Promoter Score", "صافي نقاط الترويج", "score",
                        KpiDirection.HigherIsBetter, KpiFrequency.Quarterly, 28m, 50m,
                        [30m, 35m, 38m, 42m, 46m, 49m]),
                    new KpiDef("Customer Churn Rate", "معدل تسرب العملاء", "%",
                        KpiDirection.LowerIsBetter, KpiFrequency.Monthly, 9m, 5m,
                        [8m, 7.5m, 7m, 6.8m, 6.5m, 6.2m])
                ],
                new InitiativeDef(
                    "Customer Success Revamp", "إعادة هيكلة نجاح العملاء",
                    "Head of CS", 120000m, "Cut time-to-value and proactively catch at-risk accounts.",
                    new DateOnly(2026, 2, 1), new DateOnly(2026, 11, 30), "Active")),
            new ObjectiveDef(
                "Expand into new markets", "التوسع في أسواق جديدة",
                "Enter 3 new GCC markets by 2028", 1,
                [
                    new KpiDef("New Markets Entered", "الأسواق الجديدة المستهدفة", "markets",
                        KpiDirection.HigherIsBetter, KpiFrequency.Annual, 0m, 3m,
                        [0m, 0m, 0m, 1m, 1m, 1m])
                ])
        ]),
        new PerspectiveDef("Internal Process", "العمليات الداخلية", 2,
        [
            new ObjectiveDef(
                "Reduce project delivery time", "تقليل زمن تسليم المشاريع",
                "Average project cycle time under 90 days", 0,
                [
                    new KpiDef("Average Cycle Time", "متوسط زمن الدورة", "days",
                        KpiDirection.LowerIsBetter, KpiFrequency.Monthly, 130m, 90m,
                        [120m, 115m, 108m, 102m, 98m, 95m])
                ],
                new InitiativeDef(
                    "Delivery Process Standardization", "توحيد معايير عملية التسليم",
                    "PMO Director", 80000m, "Common stage-gate templates across all delivery teams.",
                    new DateOnly(2026, 1, 15), new DateOnly(2026, 7, 31), "Approved")),
            new ObjectiveDef(
                "Improve quality / defect rate", "تحسين معدل الجودة",
                "Defect escape rate below 2%", 1,
                [
                    new KpiDef("Defect Escape Rate", "معدل الأخطاء الفائتة", "%",
                        KpiDirection.LowerIsBetter, KpiFrequency.Monthly, 5m, 2m,
                        [4m, 3.5m, 3m, 2.5m, 2m, 1.8m]),
                    new KpiDef("First-Time-Right Rate", "معدل الأداء الصحيح من أول مرة", "%",
                        KpiDirection.HigherIsBetter, KpiFrequency.Quarterly, 70m, null, [])
                ])
        ]),
        new PerspectiveDef("Learning & Growth", "التعلم والنمو", 3,
        [
            new ObjectiveDef(
                "Increase employee training hours", "زيادة ساعات تدريب الموظفين",
                "40 training hours per employee per year", 0,
                [
                    new KpiDef("Training Hours per Employee", "ساعات التدريب لكل موظف", "hours",
                        KpiDirection.HigherIsBetter, KpiFrequency.Annual, 15m, 40m,
                        [18m, 22m, 25m, 28m, 31m, 33m])
                ]),
            new ObjectiveDef(
                "Improve digital tool adoption", "تحسين تبني الأدوات الرقمية",
                "90% of staff actively using the core platform", 1,
                [
                    new KpiDef("Platform Adoption Rate", "معدل تبني المنصة", "%",
                        KpiDirection.HigherIsBetter, KpiFrequency.Monthly, 50m, 90m,
                        [60m, 68m, 75m, 82m, 87m, 91m]),
                    new KpiDef("Support Ticket Resolution Time", "زمن حل تذاكر الدعم", "hours",
                        KpiDirection.LowerIsBetter, KpiFrequency.Monthly, 65m, 24m,
                        [60m, 55m, 50m, 46m, 42m, 40m])
                ],
                new InitiativeDef(
                    "Unified Platform Rollout", "نشر المنصة الموحدة",
                    "CIO", 300000m, "Retire legacy point tools in favor of one adopted platform.",
                    new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28), "Draft"))
        ])
    ];
}
