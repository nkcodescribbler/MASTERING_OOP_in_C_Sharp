// Chapter 14 — Composition & Dependency Injection
// Run with: dotnet run --project Chapter14_CompositionDependencyInjection

using Microsoft.Extensions.DependencyInjection;
using OOPBook.Chapter14_CompositionDependencyInjection;

Section1_CompositionAndInversion();
Section3_CommonMistakes();
Section5_1_ConstructorInjection();
Section5_2_DiContainer();
Section5_3_ServiceLifetimes();
Section5_5_AntiPatterns();
Section6_CaseStudy();

static void Section1_CompositionAndInversion()
{
    Header("Section 1 — Composition, Dependency Inversion, Inversion of Control");

    var monitor = new GridZoneMonitorSingleDependency(new SmsAlertNotifier()); // given from outside — composition, not inheritance
    monitor.CheckVoltage(420.0);

    IGridAlertNotifier swapped = new PushAlertNotifier(); // another implementation — no change to the monitor required
    new GridZoneMonitorSingleDependency(swapped).CheckVoltage(420.0);
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var tightlyCoupled = new GridZoneMonitorTightlyCoupled(); // BAD — permanently married to SmsAlertNotifier
    tightlyCoupled.CheckVoltage(420.0);

    var services = new ServiceCollection();
    services.AddTransient<IGridAlertNotifier, SmsAlertNotifier>();
    services.AddScoped<IGridPermitRepository, InMemoryPermitRepository>();
    services.AddSingleton<GridPermitReportCache>();
    using var provider = services.BuildServiceProvider(validateScopes: true);

    var locator = new GridPermitServiceLocator(provider); // BAD — dependencies hidden inside the method body
    locator.ApprovePermit(new GridPermit("P-900", "North-7"));

    var reportCache = provider.GetRequiredService<GridPermitReportCache>(); // Singleton
    reportCache.RefreshZone("North-7"); // creates + disposes its own short-lived scope internally — safe

    var withOptionalSetter = new GridZoneMonitorOptionalSetter(); // BAD — Notifier never set
    withOptionalSetter.CheckVoltage(420.0); // silently does nothing — no error, no alert
    Console.WriteLine("GridZoneMonitorOptionalSetter.CheckVoltage ran with no notifier set — nothing happened, silently.");
}

static void Section5_1_ConstructorInjection()
{
    Header("Section 5.1 — Constructor Injection");

    var monitor = new GridZoneMonitor(new SmsAlertNotifier(), new SimulatedSensorReader(fixedVoltage: 421.0));
    monitor.MonitorZone("ZONE-7"); // all mandatory dependencies declared in the constructor — nothing hidden

    try
    {
        _ = new GridZoneMonitor(null!, new SimulatedSensorReader());
    }
    catch (ArgumentNullException ex)
    {
        Console.WriteLine($"Caught: {ex.ParamName} — null guard fires at construction, not at first use.");
    }
}

static void Section5_2_DiContainer()
{
    Header("Section 5.2 — .NET 8 Built-In DI Container");

    var services = new ServiceCollection();
    services.AddTransient<IGridAlertNotifier, SmsAlertNotifier>();
    services.AddScoped<IGridPermitRepository, InMemoryPermitRepository>();
    services.AddSingleton<IGridSensorReader>(new SimulatedSensorReader(fixedVoltage: 421.0));
    services.AddScoped<GridZoneMonitor>();
    services.AddScoped<GridPermitService>();
    services.AddScoped<IGridPermitAuditService, ConsolePermitAuditService>();

    // Composite notifier — every channel under its own concrete type; the ONLY
    // registration under IGridAlertNotifier itself is the composite factory.
    var compositeServices = new ServiceCollection();
    compositeServices.AddTransient<SmsAlertNotifier>();
    compositeServices.AddTransient<PushAlertNotifier>();
    compositeServices.AddTransient<IGridAlertNotifier>(sp => new CompositeAlertNotifier(new IGridAlertNotifier[]
    {
        sp.GetRequiredService<SmsAlertNotifier>(),
        sp.GetRequiredService<PushAlertNotifier>()
    }));

    using var provider = compositeServices.BuildServiceProvider();
    var composite = provider.GetRequiredService<IGridAlertNotifier>();
    composite.Send("+1555OPS0001", "Composite fan-out test"); // fires on both SMS and Push
}

static void Section5_3_ServiceLifetimes()
{
    Header("Section 5.3 — Service Lifetimes: Transient, Scoped, Singleton");

    var services = new ServiceCollection();
    services.AddTransient<SmsAlertNotifier>(); // new instance every resolution
    services.AddScoped<InMemoryPermitRepository>(); // one instance per scope
    services.AddSingleton<SimulatedSensorReader>(); // one instance for the whole provider

    using var provider = services.BuildServiceProvider();

    var t1 = provider.GetRequiredService<SmsAlertNotifier>();
    var t2 = provider.GetRequiredService<SmsAlertNotifier>();
    Console.WriteLine($"Transient — same instance? {ReferenceEquals(t1, t2)}"); // false

    var s1 = provider.GetRequiredService<SimulatedSensorReader>();
    var s2 = provider.GetRequiredService<SimulatedSensorReader>();
    Console.WriteLine($"Singleton — same instance? {ReferenceEquals(s1, s2)}"); // true

    using (var scope1 = provider.CreateScope())
    using (var scope2 = provider.CreateScope())
    {
        var scoped1 = scope1.ServiceProvider.GetRequiredService<InMemoryPermitRepository>();
        var scoped1Again = scope1.ServiceProvider.GetRequiredService<InMemoryPermitRepository>();
        var scoped2 = scope2.ServiceProvider.GetRequiredService<InMemoryPermitRepository>();
        Console.WriteLine($"Scoped — same instance within one scope? {ReferenceEquals(scoped1, scoped1Again)}"); // true
        Console.WriteLine($"Scoped — same instance across scopes?    {ReferenceEquals(scoped1, scoped2)}");      // false
    }
}

static void Section5_5_AntiPatterns()
{
    Header("Section 5.5 — Anti-Patterns: Service Locator and Bastard Injection");

    var good = new GridPermitService(new SmsAlertNotifier(), new InMemoryPermitRepository(), new ConsolePermitAuditService());
    good.ApprovePermit(new GridPermit("P-901", "North-7")); // GOOD — every dependency visible in the constructor signature

    var bastard = new GridZoneMonitorBastardInjection(); // BAD — default ctor hardcodes SmsAlertNotifier + SimulatedSensorReader
    Console.WriteLine("GridZoneMonitorBastardInjection() constructed — looks injectable, but the parameterless ctor defeats the purpose.");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: SMS Provider Swap & Test Isolation");

    // Before — tightly coupled, untestable.
    var before = new GridAlertServiceBefore();
    before.SendZoneAlert("ZONE-7", "Overvoltage at 421V");

    // After — composed via interfaces; the provider choice lives in the composition root.
    var settings = new TwilioSettings(); // in a real app, bound from configuration via IOptions<T>
    IGridAlertNotifier notifier = new TwilioAlertNotifier(settings);
    // IGridAlertNotifier notifier = new AwsSnsAlertNotifier(...); // swap: one line (reference-only in this project)
    IGridOperatorRegistry registry = new InMemoryOperatorRegistry();
    var service = new GridAlertService(notifier, registry);
    service.SendZoneAlert("ZONE-7", "Overvoltage at 421V");

    // Step 5 — unit test without infrastructure (manual assertions; no test framework needed to run this file).
    var fakeNotifier = new FakeAlertNotifier();
    var testService = new GridAlertService(fakeNotifier, new FakeOperatorRegistry());
    testService.SendZoneAlert("ZONE-7", "Overvoltage at 421V");

    Assert("exactly one message sent", fakeNotifier.Sent.Count == 1);
    Assert("recipient resolved via the fake registry", fakeNotifier.Sent[0].Phone == "+1555999001");
    Assert("message mentions the zone", fakeNotifier.Sent[0].Message.Contains("ZONE-7"));

    try
    {
        _ = new GridAlertService(null!, new FakeOperatorRegistry());
        Assert("null notifier throws at construction", false);
    }
    catch (ArgumentNullException)
    {
        Assert("null notifier throws at construction", true);
    }
}

static void Assert(string description, bool condition) =>
    Console.WriteLine(condition ? $"  PASS — {description}" : $"  FAIL — {description}");

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
