// Chapter 13 — Static Members & Extension Methods
// Run with: dotnet run --project Chapter13_StaticMembersExtensionMethods

using OOPBook.Chapter13_StaticMembersExtensionMethods;

Section1A_StaticMembers();
Section1B_StaticClass();
Section1C_ExtensionMethods();
Section2A_StaticFactoryMethods();
Section2B_UtilityDesign();
Section3_CommonMistakes();
Section5_1_PureFunctionUtility();
Section5_2_StaticConstructors();
Section5_3_ExtensionDiscovery();
Section5_3b_StaticAbstractInterfaceMembers();
Section5_4_ThreadSafety();
Section5_5_DecisionGuide();
await Section6_CaseStudy();

static void Section1A_StaticMembers()
{
    Header("Section 1A — What Is a Static Member");

    var zone = new GridZone("Z001", "North-7");
    zone.Activate();                                   // instance method — called on the object
    bool valid = GridZone.IsValidZoneCode("North-7");   // static method — called on the type
    // bool x = zone.IsValidZoneCode("North-7");        // CS0176 — use the type name
    Console.WriteLine($"Valid: {valid}, ZoneCount: {GridZone.ZoneCount}");
}

static void Section1B_StaticClass()
{
    Header("Section 1B — What Is a Static Class");

    double kw = GridMath.MegawattsToKilowatts(42.7); // 42700.0
    Console.WriteLine(kw);
    // new GridMath(); // compile error — static class cannot be instantiated
}

static void Section1C_ExtensionMethods()
{
    Header("Section 1C — What Is an Extension Method");

    var reading = new SensorReading("North-7", 112.3, DateTime.UtcNow);
    bool alert = reading.IsAlertLevel(100.0);   // looks like an instance call
    string disp = reading.ToDisplayString();
    Console.WriteLine($"{disp}, alert={alert}");

    // Equivalent explicit call (compiled form — no performance difference):
    bool alertExplicit = SensorReadingExtensions.IsAlertLevel(reading, 100.0);
    Console.WriteLine($"Explicit call: {alertExplicit}");
}

static void Section2A_StaticFactoryMethods()
{
    Header("Section 2A — When Static Members Earn Their Place");

    var permit = GridPermit.ForInstallation("P-001", "North-7");
    Console.WriteLine($"{permit.Id}: {permit.Type}, expires {permit.ExpiryDate:d}");
}

static void Section2B_UtilityDesign()
{
    Header("Section 2B — Utility Design");

    if (ZoneCodeParser.TryParse("North-7", out var region, out var number))
        Console.WriteLine($"Parsed: region={region}, number={number}");
    Console.WriteLine($"IsValidFormat('bad-format-x'): {ZoneCodeParser.IsValidFormat("bad-format-x")}");
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var zone = new GridZone("Z002", "east-4");
    Console.WriteLine(zone.GetDisplayCode());          // instance method — reads instance state
    Console.WriteLine(GridZone.FormatCode(" south-1")); // static method — takes input explicitly, no instance state

    // Missing `using UrbanGrid.Domain.Extensions;` -> CS1061: reading.ToDisplayString() would not resolve.
    // Fixed by importing the namespace the extension class lives in — already done via the top-level `using`.

    SensorReading? maybeNull = null;
    Console.WriteLine(maybeNull.IsAlertLevel(100.0)); // false — explicit null guard inside the extension, no NullReferenceException
}

static void Section5_1_PureFunctionUtility()
{
    Header("Section 5.1 — Static Classes and Static Methods");

    Console.WriteLine(SensorAnalysis.ClassifyReading(160.0));           // Critical
    Console.WriteLine(SensorAnalysis.NormaliseToPercentage(84, 100));   // 84
    var readings = new[]
    {
        new SensorReading("North-7", 42.7, DateTime.UtcNow),
        new SensorReading("North-7", 88.3, DateTime.UtcNow),
    };
    Console.WriteLine(SensorAnalysis.PeakReading(readings)); // 88.3
}

static void Section5_2_StaticConstructors()
{
    Header("Section 5.2 — Static Constructors and Initialisation Order");

    Console.WriteLine(ZoneRegistry.IsCritical("North-7"));
    Console.WriteLine(ZoneRegistry.GetRegion("East-4"));

    Console.WriteLine($"BadOrder.Full  = '{BadOrder.Full}'");  // "/Platform" — Base was null when Full initialised
    Console.WriteLine($"GoodOrder.Full = '{GoodOrder.Full}'"); // "UrbanGrid/Platform" — Base declared first
}

static void Section5_3_ExtensionDiscovery()
{
    Header("Section 5.3 — Extension Methods: Declaration and Discovery");

    var permit = GridPermit.ForEmergency("P-002", "North-7");
    Console.WriteLine(permit.ToStatusSummary());

    IGridEntity entity = permit;
    Console.WriteLine($"{entity.ToAuditKey()}, inZone(North-7)={entity.IsInZone("North-7")}");
}

static void Section5_3b_StaticAbstractInterfaceMembers()
{
    Header("Section 5.3b — Static Abstract Interface Members (C# 11)");

    var total = GridMeasureHelper.SumReadings(new[] { new GridPower(42.7m), new GridPower(88.3m), new GridPower(55.0m) });
    Console.WriteLine(total.Megawatts); // 186.0
}

static void Section5_4_ThreadSafety()
{
    Header("Section 5.4 — Static Fields: Thread Safety");

    _ = new GridPermitCounterUnsafe("P-100", "North-7");
    _ = new GridPermitCounterUnsafe("P-101", "North-7");
    Console.WriteLine($"Unsafe counter (fine single-threaded, risky concurrently): {GridPermitCounterUnsafe.TotalIssued}");

    GridPermitCounterSafe.Reset();
    _ = new GridPermitCounterSafe("P-200", "North-7");
    _ = new GridPermitCounterSafe("P-201", "North-7");
    Console.WriteLine($"Safe counter (Interlocked, no race condition): {GridPermitCounterSafe.TotalIssued}");
}

static void Section5_5_DecisionGuide()
{
    Header("Section 5.5 — Decision Guide: Static vs Instance vs Extension");

    var beforeLevel = GridAlertClassifierBefore.ClassifyByTime(80.0); // non-deterministic — depends on DateTime.UtcNow
    Console.WriteLine($"Before (uses real 'now'): {beforeLevel}");

    var afterLevel = GridAlertClassifierAfter.ClassifyByTime(80.0, new DateTime(2024, 6, 1, 23, 0, 0, DateTimeKind.Utc));
    Console.WriteLine($"After (deterministic, testable): {afterLevel}"); // hour = 23 -> Critical
}

static async Task Section6_CaseStudy()
{
    Header("Section 6 — Case Study: From Static Sprawl to Clean Design");

    // "Before" — GridHelpers anti-pattern (SendAlert is present but not invoked; see comments in Models.cs).
    var permit = GridPermit.ForEmergency("P-300", "North-7");
    GridHelpers.ProcessPermit(permit);
    Console.WriteLine($"GridHelpers.PermitsProcessed (shared mutable state): {GridHelpers.PermitsProcessed}");

    // "After" — decomposed, injectable, testable.
    var notifier = new ConsoleAlertNotifier(); // stands in for SmtpAlertNotifier — no real network call needed to demo the design
    var processor = new PermitProcessor(notifier);

    var expiredPermit = GridPermit.ForEmergency("P-301", "North-7"); // 7-day validity, but we'll check "in the future" to force expiry logic
    await processor.ProcessAsync(expiredPermit); // not expired yet — no alert
    Console.WriteLine($"ProcessedCount: {processor.ProcessedCount}");
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
