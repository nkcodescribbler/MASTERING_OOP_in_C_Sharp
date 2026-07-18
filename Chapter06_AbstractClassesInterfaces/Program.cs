// Chapter 6 — Abstract Classes & Interfaces
// Run with: dotnet run --project Chapter06_AbstractClassesInterfaces

using OOPBook.Chapter06_AbstractClassesInterfaces;

Section2_AbstractionBoundaryAndDI();
Section3_CommonMistakes();
await Section5_1_AbstractClassesInAction();
await Section5_2_InterfaceImplementation();
await Section5_3_DefaultInterfaceMembers();
Section5_5_CovarianceAndContravariance();
await Section5_7_InterfaceReferencesAtRuntime();
await Section6_CaseStudy();

static void Section2_AbstractionBoundaryAndDI()
{
    Header("Section 2 — The Abstraction Boundary & Dependency Inversion");

    var assets = new GridAssetBase[]
    {
        new PowerSubstation("SUB-01", "ALPHA-7", outputMW: 80.0),
        new SolarFarm("SOL-01", "ALPHA-7", peakMW: 40.0),
    };
    ShutdownCriticalAssets(assets); // works with any GridAssetBase subtype

    var emailService = new ZoneAlertService(new EmailNotifier()); // loosely coupled — swap the channel freely
    var smsService = new ZoneAlertService(new SmsNotifier(apiKey: "key-001"));
    Console.WriteLine("ZoneAlertService constructed with two different notifier implementations.");
}

static void ShutdownCriticalAssets(IEnumerable<GridAssetBase> assets)
{
    foreach (var asset in assets.Where(a => a.GetHealthStatus() == AssetHealthStatus.Critical))
    {
        asset.Shutdown();
        Console.WriteLine($"{asset.AssetLabel} — shut down (critical health status)");
    }
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // GridAssetBase asset = new GridAssetBase(...); // CS0144 — cannot instantiate an abstract class
    // public class BrokenSubstation : GridAssetBase {} // CS0534 — missing required overrides

    var sensorDriver = new SensorDriver(); // fat-interface anti-pattern: forced to implement everything
    Console.WriteLine($"SensorDriver.GetHealthStatus(): {sensorDriver.GetHealthStatus()} (implements capabilities it doesn't own, too)");
    try
    {
        sensorDriver.CanReach("ops@urbangrid.io");
    }
    catch (NotImplementedException)
    {
        Console.WriteLine("Caught NotImplementedException — CanReach() was never meaningfully implementable here.");
    }
}

static async Task Section5_1_AbstractClassesInAction()
{
    Header("Section 5.1 — Abstract Classes and Abstract Methods");

    var substation = new PowerSubstation("SUB-01", "ALPHA-7", outputMW: 80.0);
    var solar = new SolarFarm("SOL-01", "ALPHA-7", peakMW: 40.0);
    var wind = new WindTurbine("WIN-01", "ALPHA-7", ratedMW: 30.0);

    foreach (GridAssetBase asset in new GridAssetBase[] { substation, solar, wind })
        Console.WriteLine($"{asset.AssetLabel}: {asset.ReadCurrentOutput():F1} MW, health={asset.GetHealthStatus()}");

    substation.Shutdown(); // overridden — clears load, then delegates to base via base.Shutdown()
    Console.WriteLine($"After shutdown: {substation.ReadCurrentOutput():F1} MW");
    await Task.CompletedTask;
}

static async Task Section5_2_InterfaceImplementation()
{
    Header("Section 5.2 — Interface Declaration and Implementation");

    var service = new ZoneAlertService(new SmsNotifier(apiKey: "key-001"));
    await service.RaiseAlertAsync("ALPHA-7", AlertLevel.Warning, "Output near threshold");
}

static async Task Section5_3_DefaultInterfaceMembers()
{
    Header("Section 5.3 — Default Interface Members (C# 8+)");

    IAlertNotifier email = new EmailNotifier();
    var messages = new[]
    {
        new AlertMessage("ALPHA-7", AlertLevel.Info, "Batch message 1"),
        new AlertMessage("ALPHA-7", AlertLevel.Info, "Batch message 2"),
    };
    await email.NotifyBatchAsync(messages); // uses the interface's default implementation

    IAlertNotifier push = new PushNotifier();
    await push.NotifyBatchAsync(messages); // PushNotifier overrides the default with a bulk endpoint
}

static void Section5_5_CovarianceAndContravariance()
{
    Header("Section 5.5 — Covariance & Contravariance");

    IEnumerable<SolarFarm> solarFarms = new List<SolarFarm> { new SolarFarm("SOL-01", "ALPHA-7", 40.0) };
    IEnumerable<GridAssetBase> allAssets = solarFarms; // covariant assignment — IEnumerable<out T>
    Console.WriteLine($"Covariant read: {allAssets.Count()} asset(s) via IEnumerable<GridAssetBase>");

    IAssetReader<SolarFarm> solarReader = new SolarFarmReader();
    IAssetReader<GridAssetBase> genericReader = solarReader; // covariant — IAssetReader<out TAsset>
    Console.WriteLine($"Covariant custom interface: {genericReader.ReadAll().Count()} asset(s)");

    Action<GridAssetBase> logAny = asset => Console.WriteLine($"logAny: {asset.AssetLabel}");
    Action<SolarFarm> logSolar = logAny; // contravariant assignment — Action<in T>
    logSolar(new SolarFarm("SOL-02", "BETA-3", 25.0));

    IAssetProcessor<GridAssetBase> generalProcessor = new GridAssetProcessor();
    IAssetProcessor<SolarFarm> solarProcessor = generalProcessor; // contravariant — IAssetProcessor<in TAsset>
    solarProcessor.Process(new SolarFarm("SOL-03", "GAMMA-1", 15.0));
}

static async Task Section5_7_InterfaceReferencesAtRuntime()
{
    Header("Section 5.7 — Interface References at Runtime");

    IAlertNotifier notifier = new SmsNotifier(apiKey: "key-001"); // upcast — implicit, zero runtime cost
    // 'notifier' exposes only IAlertNotifier members here — SmsNotifier-specific
    // members like UpdateApiKey are not visible through this reference, by design.

    var notifiers = new List<IAlertNotifier>
    {
        new SmsNotifier("key-001"),
        new EmailNotifier(),
        new PushNotifier() // added later — List<IAlertNotifier> needed no change
    };

    var alert = new AlertMessage("ALPHA-7", AlertLevel.Warning, "Output near threshold");
    var ct = CancellationToken.None;

    foreach (var n in notifiers)
    {
        if (n.CanReach("ops@urbangrid.io")) // fan-out: fires for every channel that can reach
            await n.NotifyAsync(alert, ct);
    }

    // Safe downcasts:
    if (notifier is SmsNotifier sms)
        sms.UpdateApiKey("new-key"); // SmsNotifier-specific member

    var tokens = new List<string> { "device:abc-123", "device:xyz-456" };
    PushNotifier? push = notifiers[2] as PushNotifier;
    if (push is not null)
        push.SetDeviceTokens(tokens);

    // Unsafe — only when certain of the concrete type:
    SmsNotifier smsUnsafe = (SmsNotifier)notifier;
    Console.WriteLine("Unsafe cast succeeded because 'notifier' really was an SmsNotifier.");
}

static async Task Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Asset Monitoring & Alert Pipeline");

    // Compose the monitor — zero knowledge of concrete types after construction
    var monitor = new ZoneOutputMonitor(thresholdMW: 100.0);

    monitor.RegisterAsset(new PowerSubstation("SUB-01", "ALPHA-7", outputMW: 80.0));
    monitor.RegisterAsset(new SolarFarm("SOL-01", "ALPHA-7", peakMW: 40.0));
    monitor.RegisterAsset(new WindTurbine("WIN-01", "ALPHA-7", ratedMW: 30.0));

    monitor.RegisterNotifier(new EmailNotifier());
    monitor.RegisterNotifier(new SmsNotifier(apiKey: "key-001"));

    await monitor.EvaluateZoneAsync("ALPHA-7");
    // Output (daytime, all healthy): [ALPHA-7] OK — 128.0 MW across 3 asset(s)
    //   SUB 80.0 + SOL 40x0.75=30.0 + WIN 30x0.60=18.0 (varies with time of day)
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
