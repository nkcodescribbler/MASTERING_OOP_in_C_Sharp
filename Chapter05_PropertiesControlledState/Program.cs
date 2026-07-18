// Chapter 5 — Properties & Controlled State
// Run with: dotnet run --project Chapter05_PropertiesControlledState

using System.Text.Json;
using OOPBook.Chapter05_PropertiesControlledState;

Section1_FieldVsProperty();
Section3_CommonMistakes();
Section5_1_AutoPropertiesAndDefaults();
Section5_2And5_5_ComputedAndAsymmetricAccess();
Section5_3And5_4_InitAndRequired();
Section5_6_WhyFrameworksPreferProperties();
Section5_7_PropertyVsMethod();
Section5_8AndWiring_IndexerAndRegistry();
Section6_CaseStudy();

static void Section1_FieldVsProperty()
{
    Header("Section 1 — What Is a Property?");

    var withField = new PowerZoneField();
    withField.Name = "";      // nothing stops this
    withField.Name = null;    // nothing stops this either
    Console.WriteLine("Field version accepted blank/null with no validation.");

    var withProperty = new PowerZoneWithProperty();
    withProperty.Name = "North Zone";
    Console.WriteLine($"Property version: {withProperty.Name}");
    try { withProperty.Name = ""; }
    catch (ArgumentException ex) { Console.WriteLine($"Caught: {ex.Message}"); }
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var board = new AlertBoard();
    board.AddAlert(new GridAlert { Message = "Load spike detected" });
    Console.WriteLine($"Alerts (read-only view): {board.Alerts.Count}");
    // board.Alerts.Add(...); // would not compile — IReadOnlyList has no Add
}

static void Section5_1_AutoPropertiesAndDefaults()
{
    Header("Section 5.1 — Auto-Properties and Backing Fields");

    var sensor = new GridSensor(); // SensorId defaults to string.Empty, not null
    Console.WriteLine($"SensorId defaults to: '{sensor.SensorId}'");

    var zone = new PowerZoneDefaults();
    Console.WriteLine($"Defaults — active={zone.IsActive}, tags={zone.Tags.Count}, status={zone.Status}");
}

static void Section5_2And5_5_ComputedAndAsymmetricAccess()
{
    Header("Section 5.2/5.5 — Computed Properties & Asymmetric Access");

    var zone = new PowerZone("ALPHA-7", "Alpha Zone 7", capacity: 5000.0);
    zone.Activate();
    zone.CurrentLoad = 4200.0; // internal set — same assembly, allowed
    Console.WriteLine(zone.StatusSummary); // computed from CurrentLoad/Capacity

    try
    {
        _ = new PowerZone("BETA-3", "Beta Zone 3", capacity: -1); // Capacity's private setter validates
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }
}

static void Section5_3And5_4_InitAndRequired()
{
    Header("Section 5.3/5.4 — init-Only & required Properties");

    var permit = new GridPermit
    {
        PermitId = "P-2024-0042",
        ZoneCode = "ALPHA-7",
        IssuedAt = DateTime.UtcNow,
        ExpiryDate = DateTime.UtcNow.AddYears(1)
    };
    Console.WriteLine(permit.Summary);
    // permit.PermitId = "P-NEW"; // compile error — init-only property

    // The compiler enforces required members are all set:
    // var incomplete = new GridPermit { PermitId = "X", IssuedAt = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow };
    // CS9035: Required member 'GridPermit.ZoneCode' must be set
}

static void Section5_6_WhyFrameworksPreferProperties()
{
    Header("Section 5.6 — Why Frameworks Prefer Properties");

    var permit = new GridPermit
    {
        PermitId = "P-001",
        ZoneCode = "ALPHA-7",
        IssuedAt = DateTime.UtcNow,
        ExpiryDate = DateTime.UtcNow.AddYears(1)
    };

    string json = JsonSerializer.Serialize(permit);
    Console.WriteLine(json); // only public properties are serialised
}

static void Section5_7_PropertyVsMethod()
{
    Header("Section 5.7 — Property vs Method");

    var lookup = new PermitLookupService();
    // Validate() is a method, not a property — its name signals it may do real work.
    Console.WriteLine($"Validate('P-2024-0042', expired=false): {lookup.Validate("P-2024-0042", isExpired: false)}");
}

static void Section5_8AndWiring_IndexerAndRegistry()
{
    Header("Section 5.8 & Wiring It Together — Indexer");

    var registry = new ZoneRegistry();
    registry["ALPHA-7"] = new PowerZone("ALPHA-7", "Alpha Zone 7", capacity: 5000.0);
    registry["BETA-3"] = new PowerZone("BETA-3", "Beta Zone 3", capacity: 3500.0);

    PowerZone? zone = registry["ALPHA-7"]; // null if not found
    Console.WriteLine(zone?.ZoneLabel ?? "Not found");
    Console.WriteLine($"Registry count: {registry.Count}");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Zone Status & Permit Validity");

    var registry = new ZoneRegistry();
    var alpha = new PowerZone("ALPHA-7", "Alpha Zone 7", capacity: 5000.0);
    var beta = new PowerZone("BETA-3", "Beta Zone 3", capacity: 3500.0);

    alpha.Activate();
    alpha.CurrentLoad = 4200.0; // internal set — same assembly
    beta.Activate();
    beta.CurrentLoad = 3600.0;  // over capacity

    registry.Register(alpha);
    registry.Register(beta);

    Console.WriteLine(registry["ALPHA-7"]?.StatusSummary); // [ALPHA-7] Near Capacity — 84.0%
    Console.WriteLine(registry["BETA-3"]?.StatusSummary);  // [BETA-3] OVERLOADED — 102.9%

    Console.WriteLine($"Active zones: {registry.ActiveZones.Count()}");

    var permit = new GridPermit
    {
        PermitId = "P-2024-0042",
        ZoneCode = "ALPHA-7",
        IssuedAt = DateTime.UtcNow,
        ExpiryDate = DateTime.UtcNow.AddYears(2),
        ApprovedBy = "ops-team",
        Notes = "Annual renewal"
    };

    Console.WriteLine(permit.Summary);
    Console.WriteLine(permit.IsValid);   // true
    Console.WriteLine(permit.IsExpired); // false
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
