// Chapter 3 — Access Modifiers & Assembly Boundaries
// Run with: dotnet run --project Chapter03_AccessModifiersAssemblyBoundaries
//
// This chapter is fundamentally about compile-time boundaries BETWEEN
// assemblies. A single console project is one assembly, so the book's
// "this fails in a different assembly" examples are reproduced as comments
// (exactly as the book itself presents the inaccessible lines) rather than
// as code that would need a second project reference to prove the point.

using OOPBook.Chapter03_AccessModifiersAssemblyBoundaries;

Section1A_AccessModifiers();
Section3_CommonMistakes();
Section5_1And5_2_FieldAndPropertyAccess();
Section5_4_SealedClass();
Section6_CaseStudy();

// ---------------------------------------------------------------------
// Section 1A — access modifiers enforce design boundaries at compile time
// ---------------------------------------------------------------------
static void Section1A_AccessModifiers()
{
    Header("Section 1A — Access Modifiers");

    var zone = new GridZone("North-7");
    zone.RecordAlert();               // public method
    int count = zone.AlertCount;      // public property
    // zone._alertCount = 0;          // CS0122 — inaccessible due to protection level
    Console.WriteLine($"{zone.ZoneCode}: {count} alert(s), active={zone.IsActive}");

    var substation = new PowerSubstation("SUB-N7-01", "North-7");
    substation.PerformInspection();
    Console.WriteLine(substation.GetStatus());
}

// ---------------------------------------------------------------------
// Section 3 — Common Mistakes
// ---------------------------------------------------------------------
static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake 1 — public mutable fields, no validation
    var loose = new CommonMistakes.GridPermitUnprotected { PermitId = null, ZoneCount = -99 };
    Console.WriteLine($"Unprotected permit accepted invalid data: ZoneCount={loose.ZoneCount}");

    // Fix — private fields + constructor validation
    var validated = new CommonMistakes.GridPermitValidated("PMT-001", 3);
    Console.WriteLine($"Validated permit: {validated.PermitId}, zones={validated.ZoneCount}");
    try
    {
        _ = new CommonMistakes.GridPermitValidated("", 3);
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Caught expected validation error: {ex.Message}");
    }

    // Mistake 2 — protected field lets a derived class bypass business rules
    var bad = new CommonMistakes.MonitoredGridZoneBad();
    bad.RecordAlert();
    bad.RecordAlert();
    bad.ForceReset(); // bypasses whatever invariant RecordAlert() was meant to enforce
    Console.WriteLine("MonitoredGridZoneBad.ForceReset() bypassed the alert count with no validation.");

    // The fix for the same problem: canonical GridZone above uses a private
    // field + protected read-only gateway, so no subclass can reset state directly.
}

// ---------------------------------------------------------------------
// Section 5.1 / 5.2 — field access patterns & property access asymmetry
// ---------------------------------------------------------------------
static void Section5_1And5_2_FieldAndPropertyAccess()
{
    Header("Section 5.1/5.2 — Field & Property Access Patterns");

    var zone = new GridZone("East-4");
    zone.UpdateThreshold(150.0); // goes through the private setter — validation runs
    Console.WriteLine($"Threshold set to {zone.LoadThresholdMw}");

    try
    {
        zone.UpdateThreshold(-10);
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Console.WriteLine($"Caught expected validation error: {ex.Message}");
    }

    // zone.IsActive = false;  // compile error — set is private
    // zone.AlertCount = 0;    // compile error — set is private

    var calc = new ZoneStatusCalculator(); // fine — same assembly
    Console.WriteLine($"Alert level at 165 MW: {calc.CalculateLevel(165.0)}");
}

// ---------------------------------------------------------------------
// Section 5.4 — sealed class
// ---------------------------------------------------------------------
static void Section5_4_SealedClass()
{
    Header("Section 5.4 — sealed Class");

    // GridPermit (declared in the Section 6 case study, below) is sealed:
    // public class ExtendedPermit : GridPermit { }
    // CS0509: cannot derive from sealed type 'GridPermit'

    var substation = new PowerSubstation("SUB-E4-01", "East-4");
    substation.TakeOffline();
    Console.WriteLine(substation.GetStatus()); // sealed override — no further subclass can override this again
}

// ---------------------------------------------------------------------
// Section 6 — Case Study: UrbanGrid Multi-Layer Visibility Design
// ---------------------------------------------------------------------
static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Multi-Layer Visibility");

    // UrbanGrid.Domain — internal services, only reachable inside this assembly
    IPermitRepository repository = new InMemoryPermitRepository();
    var validator = new PermitValidator();
    var approvalService = new PermitApprovalService(repository, validator);

    var permit = approvalService.CreateAndApprove("PMT-100", "North-7", zoneCount: 4);
    Console.WriteLine($"Created & approved: {permit.PermitId}, status={permit.Status}");

    // UrbanGrid.Api — only the public surface is reachable from here
    var api = new PermitApiLayer(repository);
    Console.WriteLine(api.Get(permit.PermitId));
    Console.WriteLine(api.Revoke(permit.PermitId));
    Console.WriteLine(api.Get(permit.PermitId));

    // BLOCKED at compile time from the Api layer:
    //   permit.Approve()               — internal
    //   new GridPermit(...)            — internal constructor
    //   new PermitApprovalService(...) — internal class
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
