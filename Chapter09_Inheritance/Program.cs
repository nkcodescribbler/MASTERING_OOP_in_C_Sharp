// Chapter 9 — Inheritance
// Run with: dotnet run --project Chapter09_Inheritance

using OOPBook.Chapter09_Inheritance;

Section1_SixKeywords();
Section3_CommonMistakes();
Section5_4_FragileBaseClass();
Section5_5_SealedMethods();
Section5_6_CovariantReturns();
Section6_CaseStudy();

static void Section1_SixKeywords()
{
    Header("Section 1 — The Six Keywords: virtual, override, new, base, abstract, sealed");

    var transformer = new PowerTransformer("TRFR-001", "Main", "NW-01", 250.0, 132.0, 33.0);
    var breaker = new CircuitBreaker("CB-001", "Feeder A", "NW-01", 800.0);
    Console.WriteLine(transformer.GetStatusSummary()); // override — full replacement
    Console.WriteLine(breaker.GetStatusSummary());     // override — extends via base.GetStatusSummary()

    // new — hiding, not overriding
    GridAssetHidingDemo a = new CircuitBreakerHidingDemo();
    CircuitBreakerHidingDemo cb = new CircuitBreakerHidingDemo();
    Console.WriteLine(a.GetStatusSummary());  // "Base summary" — resolved by declared type
    Console.WriteLine(cb.GetStatusSummary()); // "Breaker summary" — resolved by declared type
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake 1 — inheriting for reuse when the "is-a" test fails
    var report = new ReportGenerator("RPT-1");
    try { report.GetHealth(); }
    catch (NotSupportedException ex) { Console.WriteLine($"Caught: {ex.Message}"); }

    var goodGenerator = new ZoneAssetReportGenerator(new GridAsset[]
    {
        new PowerTransformer("TRFR-001", "Main", "NW-01", 250.0, 132.0, 33.0)
    });
    Console.WriteLine($"Composition-based generator: {string.Join(" | ", goodGenerator.Summaries)}");

    // Mistake 2 — skipping the base implementation vs. properly extending it
    var zone = new GridZoneStub();
    var bad = new HighVoltageAssetBad();
    bad.RegisterWithZone(zone); // silently skips the capacity check
    Console.WriteLine($"Bad registration completed with no validation output above.");

    var good = new HighVoltageAssetGood();
    good.RegisterWithZone(zone); // runs base validation, then extends

    // Mistake 3 — virtual dispatch from a constructor
    var dangerous = new BatteryBankDangerousCtor("BATT-DGR", capacityKWh: 500);
    Console.WriteLine($"InitialHealthSnapshot captured before _capacityKWh was set: {dangerous.InitialHealthSnapshot}");
    Console.WriteLine($"GetHealth() called now (after construction) is accurate: {dangerous.GetHealth()}");

    // Mistake 4 — `new` hides a non-virtual method; access via the base type misses the override
    GridSensor sensor = new TemperatureSensor("TEMP-001");
    Console.WriteLine(sensor.GetStatusSummary()); // "[TEMP-001] Sensor" — NOT the temperature!
    Console.WriteLine(((TemperatureSensor)sensor).GetStatusSummary()); // "[TEMP-001] Temp: 0.0°C" — via the derived type
}

static void Section5_4_FragileBaseClass()
{
    Header("Section 5.4 — The Fragile Base Class Problem");

    var fragile = new MonitoredTransformerFragile();
    fragile.GetHealth();
    Console.WriteLine($"Fragile design (V1): CheckCount after one call = {fragile.ObservedCheckCount}"); // 2 — GetHealth override + base

    var stable = new MonitoredTransformerStable();
    stable.IsHealthy();
    Console.WriteLine($"Stable design: CheckCount after one call = {stable.ObservedCheckCount}"); // predictable — 1
}

static void Section5_5_SealedMethods()
{
    Header("Section 5.5 — sealed Methods and Classes");

    var log = new ConsoleLogger<MonitoredGridAssetSealed>();
    var monitored = new MonitoredPowerTransformer("TRFR-M01", "Monitored Main", "NW-01", log);
    Console.WriteLine(monitored.GetStatusSummary()); // sealed override — logs, then returns base summary
    // public class SmartBatteryBank : BatteryBank { } // CS0509 — BatteryBank is sealed
}

static void Section5_6_CovariantReturns()
{
    Header("Section 5.6 — Covariant Return Types (C# 9+)");

    PowerTransformer t = new PowerTransformer("TRFR-001", "Main", "NW-01", 250.0, 132.0, 33.0);
    PowerTransformer cpy = t.Clone(); // no cast — type is known precisely
    GridAsset ac = t.Clone();          // also works — covariant type satisfies the base contract
    Console.WriteLine($"Cloned: {cpy.Name}, base-typed reference name: {ac.Name}");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: UrbanGrid Asset Hierarchy in Production");

    var assets = new List<GridAsset>
    {
        new PowerTransformer("TRFR-001", "Main Transformer", "NW-01", 250.0, 132.0, 33.0),
        new PowerTransformer("TRFR-002", "Backup Transformer", "NW-01", 180.0, 132.0, 33.0),
        new CircuitBreaker("CB-001", "Feeder A Breaker", "NW-01", 800.0),
        new CircuitBreaker("CB-002", "Feeder B Breaker", "NW-01", 600.0),
        new SolarPanel("SP-001", "Rooftop Array 1", "NW-01", 120.0, 240),
        new BatteryBank("BATT-001", "Energy Storage A", "NW-01", 500.0, 150.0),
    };

    ((PowerTransformer)assets[0]).RecordLoad(240.0);   // 96% — Critical
    ((CircuitBreaker)assets[2]).Trip();                // Tripped — Critical
    ((SolarPanel)assets[4]).RecordOutput(98.0);        // Healthy
    ((BatteryBank)assets[5]).SetStateOfCharge(45.0);   // 9% SoC — Critical

    var service = new ZoneInspectionService(assets.AsReadOnly(), new ConsoleLogger<ZoneInspectionService>());
    var report = service.InspectZone("NW-01");

    foreach (var result in report.Results)
        Console.WriteLine($"{result.Summary}  <- {result.Health}");

    Console.WriteLine($"Total: {report.TotalAssets}, Critical: {report.CriticalCount}, Warning: {report.WarningCount}, Healthy: {report.HealthyCount}");

    var simulationCopies = service.CloneForSimulation("NW-01");
    Console.WriteLine($"Cloned {simulationCopies.Count} asset(s) for simulation — originals untouched.");
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
