// Chapter 10 — Polymorphism & Pattern Matching
// Run with: dotnet run --project Chapter10_PolymorphismPatternMatching

using OOPBook.Chapter10_PolymorphismPatternMatching;

Section1A_CompileTimePolymorphism();
Section1B_CompileTimeVsRuntime();
Section1C_VirtualOverrideAbstractContract();
Section3_CommonMistakes();
Section5_2_IsAsTypeTesting();
Section5_3_SwitchExpressions();
Section5_4_PropertyPatterns();
Section5_5_ListPatterns();
Section5_7_CovariantReturns();
Section6_CaseStudy();

static void Section1A_CompileTimePolymorphism()
{
    Header("Section 1A — Compile-Time Polymorphism (Overloading)");

    var logger = new GridAlertLogger();
    logger.Log("Voltage spike in North-7");                                   // overload 1
    logger.Log(new GridAlert(GridAlertLevel.Critical, "Transformer fault"));  // overload 2
    logger.Log(GridAlertLevel.Critical, "Fault");                             // overload 3

    var assets = new List<GridAsset>
    {
        new PowerSubstation("SUB-N7-01", "North-7", 112.3, isOnline: true),
        new GridSensor("SEN-N7-001", "North-7", isCalibrated: true),
        new PowerSubstation("SUB-E4-01", "East-4", 87.6, isOnline: true),
        new GridSensor("SEN-S1-001", "South-1", isCalibrated: false)
    };
    foreach (var asset in assets)
        Console.WriteLine(asset.GenerateReport()); // runtime polymorphism — virtual dispatch
}

static void Section1B_CompileTimeVsRuntime()
{
    Header("Section 1B — Compile-Time vs Runtime Dispatch");

    var logger = new GridAssetLogger();

    GridAsset baseRef = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);
    PowerSubstation subRef = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);

    logger.Log(baseRef); // compiler sees GridAsset -> Overload A
    logger.Log(subRef);  // compiler sees PowerSubstation -> Overload B

    Console.WriteLine(baseRef.GenerateReport()); // CLR sees PowerSubstation at runtime either way
    Console.WriteLine(subRef.GenerateReport());
}

static void Section1C_VirtualOverrideAbstractContract()
{
    Header("Section 1C — virtual / override / abstract Contract");

    GridAsset asset = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);
    Console.WriteLine(asset.GetIdentity()); // non-virtual — fixed behaviour for every asset
    Console.WriteLine(asset.GetAlertLevel());
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake: no override — the base's weak default silently ships to production.
    GridAssetWeakDefault relay = new SolarRelayNoOverride("REL-N7-01");
    Console.WriteLine(relay.GenerateReport()); // "Asset REL-N7-01 — no detail available" — wrong data, no error signal

    // Mistake: `new` hides the base method instead of overriding it.
    GridAssetVirtualReport asset1 = new GridSensorHidingReport("SEN-N7-001");
    GridSensorHidingReport asset2 = new GridSensorHidingReport("SEN-N7-001");
    Console.WriteLine(asset1.GenerateReport()); // base class version
    Console.WriteLine(asset2.GenerateReport()); // GridSensorHidingReport version

    // Mistake: type-switch antipattern vs polymorphism.
    var switcher = new GridAssetSwitcher();
    GridAsset substation = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);
    Console.WriteLine(switcher.GetAssetSummaryWrong(substation));
    Console.WriteLine(switcher.GetAssetSummaryCorrect(substation));

    // Mistake: switch expression with no default arm risks a SwitchExpressionException.
    GridAlertLevel level = GridAlertLevel.Critical;
    string label = level switch
    {
        GridAlertLevel.Normal => "All clear",
        GridAlertLevel.High => "Elevated alert",
        GridAlertLevel.Critical => "Emergency",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unrecognised alert level — update this switch")
    };
    Console.WriteLine(label);

    // Mistake: general pattern arm placed before a more specific one — the specific arm is unreachable.
    // (Shown as commentary — a truly unreachable arm is a compile warning/error in C#, so it can't be
    // reproduced as running code here.) Correct ordering — most specific first:
    GridAsset sample = new PowerSubstation("SUB-X", "Zone-X", 150, true);
    string summary = sample switch
    {
        PowerSubstation sub when sub.LoadMw > 100 => "High load substation",
        PowerSubstation sub => $"Substation: {sub.AssetId}",
        GridSensor sen => $"Sensor: {sen.AssetId}",
        _ => $"Asset: {sample.AssetId}"
    };
    Console.WriteLine(summary);
}

static void Section5_2_IsAsTypeTesting()
{
    Header("Section 5.2 — is, as, and Type Testing");

    GridAsset asset = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);

    if (asset is PowerSubstation substation) // declaration pattern — test and assign in one step
        Console.WriteLine(substation.LoadMw);

    var sensor = asset as GridSensor; // returns null if the cast fails
    if (sensor != null) Console.WriteLine(sensor.IsCalibrated);

    if (asset is GridSensor sen) // preferred — replaces the as + null check
        Console.WriteLine(sen.IsCalibrated);
    else
        Console.WriteLine("Not a GridSensor.");
}

static void Section5_3_SwitchExpressions()
{
    Header("Section 5.3 — Switch Expressions and Type Patterns");

    var router = new GridAlertRouter();
    Console.WriteLine(router.RouteAlert(new PowerSubstation("SUB-N7-01", "North-7", 112.3, true)));
    Console.WriteLine(router.RouteAlert(new GridSensor("SEN-N7-001", "North-7", false)));
}

static void Section5_4_PropertyPatterns()
{
    Header("Section 5.4 — Property Patterns and Relational Patterns");

    var classifier = new GridAssetClassifier();
    Console.WriteLine(classifier.ClassifyAsset(new PowerSubstation("SUB-N7-01", "North-7", 112.3, true)));
    Console.WriteLine(classifier.ClassifyAsset(new GridSensor("SEN-N7-001", "North-7", false)));
}

static void Section5_5_ListPatterns()
{
    Header("Section 5.5 — List Patterns (C# 11+)");

    var analyser = new GridSensorAnalyser();
    Console.WriteLine(analyser.EvaluateReadings(Array.Empty<double>()));
    Console.WriteLine(analyser.EvaluateReadings(new[] { 42.0 }));
    Console.WriteLine(analyser.EvaluateReadings(new[] { 20.0, 60.0 }));
    Console.WriteLine(analyser.EvaluateReadings(new[] { 30.0, 45.0, 110.0 }));
    Console.WriteLine(analyser.EvaluateReadings(new[] { 10.0, 20.0, 30.0, 40.0 }));
}

static void Section5_7_CovariantReturns()
{
    Header("Section 5.7 — Covariant Return Types");

    GridAsset baseRef = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);
    PowerSubstation subRef = new PowerSubstation("SUB-N7-01", "North-7", 112.3, true);

    GridAsset cloned1 = baseRef.Clone();  // GridAsset — base contract
    PowerSubstation cloned2 = subRef.Clone(); // PowerSubstation directly — no cast
    Console.WriteLine($"{cloned1.AssetId} / {cloned2.LoadMw}");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Alert Processing & Asset Classification");

    var assets = new List<GridAsset>
    {
        new PowerSubstation("SUB-N7-01", "North-7", 112.3, isOnline: true),
        new PowerSubstation("SUB-E4-01", "East-4", 45.1, isOnline: true),
        new PowerSubstation("SUB-S1-01", "South-1", 0.0, isOnline: false),
        new GridSensor("SEN-N7-001", "North-7", isCalibrated: false),
        new GridSensor("SEN-E4-001", "East-4", isCalibrated: true),
        new ZoneMonitor("MON-S1-01", "South-1", isOperational: false)
    };

    var oldProcessor = new GridEventProcessor(); // "the problem" — still works, but doesn't scale
    Console.WriteLine($"Old processor (first asset): {oldProcessor.ClassifyAlert(assets[0])}");

    new GridControlCentre().ProcessAll(assets); // "the redesign" — polymorphism + pattern matching
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
