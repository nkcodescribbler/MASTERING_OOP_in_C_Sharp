// Chapter 4 — Constructors, Object Initialisation & Self-Reference
// Run with: dotnet run --project Chapter04_ConstructorsInitialisationSelfReference

using OOPBook.Chapter04_ConstructorsInitialisationSelfReference;

Section1B_FourConstructorTypes();
Section3_CommonMistakes();
Section4_1_ConstructorChaining();
Section4_2_ConstructorAccessibility();
Section4_3_SelfReference();
Section4_4And4_5_RequiredAndInit();
Section4_6_OutParameter();
Section4_7_PrimaryConstructors();
Section4_8_InitialiserVsConstructor();
Section5_CaseStudy();

static void Section1B_FourConstructorTypes()
{
    Header("Section 1B — The Four Constructor Types");

    var diag = new GridDiagnostics(); // default constructor
    Console.WriteLine($"Diagnostics started at {diag.StartedAt:O}");

    var zone = ZoneRegistry.KnownZones["North-7"]; // static constructor already ran
    Console.WriteLine($"Known zone: {zone.ZoneCode}");

    var original = new GridPermit1B("P-001", "North-7", 500);
    var copy = new GridPermit1B(original); // copy constructor
    Console.WriteLine($"Original: {original.PermitId}, Copy: {copy.PermitId}, same MaxLoad={copy.MaxLoad == original.MaxLoad}");
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var twoStep = new GridZoneTwoStep(); // invalid state exists between 'new' and assignment
    twoStep.ZoneCode = "North-7";
    twoStep.IsActive = true;
    Console.WriteLine($"Two-step zone: {twoStep.ZoneCode}");

    var singleStep = new GridZoneSingleStep("North-7"); // valid the moment 'new' returns
    Console.WriteLine($"Single-step zone: {singleStep.ZoneCode}");

    try
    {
        _ = new PowerSubstationValidating("A-01", "North");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught (virtual call in base ctor): {ex.Message}");
    }

    var repo = new InMemorySimplePermitRepository();
    var report = ZoneReport.LoadFor("North-7", repo); // loading owned by the factory method, not the constructor
    Console.WriteLine($"Zone report loaded {report.Permits.Count} permit(s).");
}

static void Section4_1_ConstructorChaining()
{
    Header("Section 4.1 — Constructor Chaining");

    var permit1 = new GridPermitChained("P-001", "North-7", 500);          // requestedBy = "system"
    var permit2 = new GridPermitChained("P-002", "North-7", 500, "Admin"); // explicit requestedBy
    Console.WriteLine($"{permit1.PermitId} requestedBy={permit1.RequestedBy}");
    Console.WriteLine($"{permit2.PermitId} requestedBy={permit2.RequestedBy}");

    var substation = new PowerSubstationChained("SUB-01", "North-7", 250.0);
    Console.WriteLine($"Substation {substation.AssetId} capacity {substation.MaxCapacityMW} MW");
}

static void Section4_2_ConstructorAccessibility()
{
    Header("Section 4.2 — Constructor Accessibility");

    var viaFactory = GridPermitFactoryMade.CreateNew("P-001", "North-7", 500); // via factory
    var draft = GridPermitFactoryMade.CreateDraft("East-4");
    Console.WriteLine($"{viaFactory.PermitId}: {viaFactory.CurrentStatus}, draft {draft.PermitId}: {draft.CurrentStatus}");
    // var bad = new GridPermitFactoryMade(...); // CS0122 — constructor is private

    var internalFactory = new PermitFactoryInternal();
    var internalPermit = internalFactory.Create("P-777", "North-7"); // same assembly — allowed
    Console.WriteLine($"Internal-ctor permit: {internalPermit.PermitId}");
    // new GridAssetProtectedCtor("A-01", "North-7"); // cannot instantiate an abstract class
}

static void Section4_3_SelfReference()
{
    Header("Section 4.3 — Self-Reference: this");

    var zone = new GridZoneThis("North-7", true);
    Console.WriteLine($"{zone.ZoneCode}, active={zone.IsActive}");

    var monitor = new ZoneMonitor();
    var subscribing = new GridZoneSubscribing("North-7");
    subscribing.Subscribe(monitor); // 'this' passed only after construction
    Console.WriteLine($"Registered after safe construction: {monitor.RegisteredCount}");

    var unsafeZone = new GridZoneUnsafe("North-7", monitor); // 'this' escapes mid-construction
    Console.WriteLine($"Unsafe zone constructed. ZoneCode ended up as: {unsafeZone.ZoneCode}");

    var safeZone = GridZoneSafe.CreateAndRegister("East-4", monitor); // factory ensures full construction first
    Console.WriteLine($"Safe zone via factory: {safeZone.ZoneCode}, total registered={monitor.RegisteredCount}");

    var config = new ZoneConfigBuilder() // fluent chain — each call returns 'this'
        .WithZoneCode("North-7")
        .WithMaxAlerts(3)
        .Build();
    Console.WriteLine($"Built config: {config.ZoneCode}, maxAlerts={config.MaxAlertCount}");
}

static void Section4_4And4_5_RequiredAndInit()
{
    Header("Section 4.4/4.5 — required Members & init-Only Setters");

    var config = new ZoneConfiguration
    {
        ZoneCode = "North-7",
        MaxAlertCount = 5,
        IsMonitored = true
    };
    Console.WriteLine($"{config.ZoneCode}: maxAlerts={config.MaxAlertCount}");
    // var bad = new ZoneConfiguration { MaxAlertCount = 5, IsMonitored = true }; // CS9035 — ZoneCode missing

    var alert = new GridAlertEvent
    {
        AlertId = "ALT-0042",
        ZoneCode = "North-7",
        Severity = 3
    };
    Console.WriteLine($"{alert.AlertId} severity={alert.Severity}");
    // alert.Severity = 5; // CS8852 — cannot set init property after object creation

    var critical = new CriticalAlertEvent("ALT-0099", "East-4") { EscalationPath = "on-call-tier-2" };
    Console.WriteLine($"{critical.AlertId} escalates to {critical.EscalationPath}");
}

static void Section4_6_OutParameter()
{
    Header("Section 4.6 — The out Parameter");

    string rawInput = "north-7";
    if (ZoneParser.TryParseZoneCode(rawInput, out string parsedCode))
    {
        var zone = new GridZone(parsedCode);
        Console.WriteLine($"Zone created: {zone.ZoneCode}");
    }

    bool isValid = ZoneParser.TryParseZoneCode(rawInput, out _); // discard
    Console.WriteLine($"isValid (discarded value): {isValid}");

    var registry = new PermitRegistry();
    registry.RegisterZone(new ZoneConfiguration { ZoneCode = "North-7", MaxAlertCount = 5, IsMonitored = true });
    if (registry.TryGetZone("North-7", out var found))
        Console.WriteLine($"MaxAlerts: {found!.MaxAlertCount}");
}

static void Section4_7_PrimaryConstructors()
{
    Header("Section 4.7 — Primary Constructors (C# 12)");

    var repo = new InMemorySimplePermitRepository();
    var validator = new PassthroughZoneValidator();

    var traditional = new PermitServiceTraditional(repo, validator);
    var primary = new PermitServicePrimaryCtor(repo, validator);

    Console.WriteLine(traditional.CreatePermit("P-201", "North-7", 300).PermitId);
    Console.WriteLine(primary.CreatePermit("P-202", "North-7", 300).PermitId);
}

static void Section4_8_InitialiserVsConstructor()
{
    Header("Section 4.8 — Object Initialisers vs Constructors");

    var dto = new GridPermitDto // object initialiser — a mutable DTO with all-optional setters
    {
        PermitId = "P-001",
        ZoneCode = "North-7",
        MaxLoad = 500,
        RequestedBy = "Admin"
    };
    Console.WriteLine($"DTO: {dto.PermitId} requestedBy={dto.RequestedBy}");

    // Combining both: required fields via the factory's parameters, optional
    // (RequestedBy) supplied by the caller with a sensible default otherwise.
    var permit = GridPermit.CreateNew("P-003", "North-7", 500, requestedBy: "OperatorJane");
    Console.WriteLine($"Combined: {permit.PermitId}, requestedBy={permit.RequestedBy}");
}

static void Section5_CaseStudy()
{
    Header("Section 5 — Case Study: Permit & Zone Configuration Construction");

    var registry = new PermitRegistry();
    registry.RegisterZone(new ZoneConfiguration
    {
        ZoneCode = "North-7",
        MaxAlertCount = 5,
        IsMonitored = true,
        Description = "North commercial district — high density"
    });

    var service = new PermitService(registry, new ConsoleLogger<PermitService>());
    var permit = service.IssuePermit("P-001", "North-7", 350);
    Console.WriteLine($"Created: {permit.PermitId} | Zone: {permit.ZoneCode} | Status: {permit.Status}");

    if (registry.TryGetPermit("P-001", out var found))
        Console.WriteLine($"Found: {found!.PermitId} | Load: {found.MaxLoad} MW");

    // These all fail — never silently:
    try { registry.IssuePermit("P-002", "South-Nonexistent", 100); }
    catch (InvalidOperationException ex) { Console.WriteLine($"Caught: {ex.Message}"); }

    try { GridPermit.CreateNew("", "North-7", 500); }
    catch (ArgumentException ex) { Console.WriteLine($"Caught: {ex.Message}"); }

    try { GridPermit.CreateNew("P-003", "North-7", -1); }
    catch (ArgumentOutOfRangeException ex) { Console.WriteLine($"Caught: {ex.Message}"); }
    // new ZoneConfiguration { MaxAlertCount = 5, IsMonitored = true }; // CS9035: missing ZoneCode
    // new GridPermit("P-001", "North-7", 500, ...);                    // CS0122: constructor is private
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
