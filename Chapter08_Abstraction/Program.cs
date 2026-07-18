// Chapter 8 — Abstraction: Concept, Strategies & Decision Guide
// Run with: dotnet run --project Chapter08_Abstraction

using OOPBook.Chapter08_Abstraction;

await Section2_FlexibilityAndTestability();
Section3_CommonMistakes();
await Section5_1_InterfacesAndExplicitImplementation();
Section5_3_PrematureAbstraction();
await Section5_5_LeakyAbstractions();
await Section6_CaseStudy();

static async Task Section2_FlexibilityAndTestability()
{
    Header("Section 2 — Flexibility & Testability");

    var fake = new FakeAlertChannel();
    var dispatcher = new AlertDispatcher(new IAlertChannel[] { fake }, new ConsoleLogger<AlertDispatcher>());
    var message = new AlertMessage("A-1", "ALPHA-7", AlertSeverity.Warning, "Load approaching capacity",
        "+15551234567", "ops@urbangrid.io", "device-token-1", DateTime.UtcNow);

    await dispatcher.DispatchAsync(message);
    Console.WriteLine($"Fake channel recorded {fake.SentMessages.Count} message(s) — no real network call was made.");
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var zones = new List<GridZone> { new GridZone { ZoneId = "ALPHA-7", Status = ZoneStatus.Active } };
    IGridZoneService service = new GridZoneService(zones); // interface mirrors the one implementation exactly — no benefit
    Console.WriteLine($"IGridZoneService.GetById: {service.GetById("ALPHA-7")?.ZoneId}");
}

static async Task Section5_1_InterfacesAndExplicitImplementation()
{
    Header("Section 5.1 — Abstraction Through Interfaces");

    IZoneDataSource dataSource = new InMemoryZoneDataSource(
        new[] { new GridZone { ZoneId = "ALPHA-7", Status = ZoneStatus.Active, CurrentLoadMW = 80, CapacityMW = 100 } });
    var zone = await dataSource.GetByIdAsync("ALPHA-7");
    Console.WriteLine($"IZoneDataSource.GetByIdAsync: {zone?.ZoneId}");

    // Explicit interface implementation — same object, two contract views
    var recorder = new ZoneActivityRecorder(new ConsoleEventStream(), new ConsoleAuditStore());
    IZoneEventSink eventSink = recorder;
    IZoneAuditLog auditLog = (IZoneAuditLog)eventSink; // same object, different contract view

    eventSink.Record("Zone NW-01 load updated");    // -> IZoneEventSink.Record
    auditLog.Record("Permit PRM-2024-001 approved"); // -> IZoneAuditLog.Record
}

static void Section5_3_PrematureAbstraction()
{
    Header("Section 5.3 — When NOT to Abstract");

    var zone = new GridZone { ZoneId = "ALPHA-7", CurrentLoadMW = 84, CapacityMW = 100 };

    IZoneLoadCalculator calculator = new ZoneLoadCalculator(); // before: interface adds no value, one implementation
    Console.WriteLine($"Via interface: {calculator.Calculate(zone):F1}%");

    Console.WriteLine($"Direct call:   {ZoneLoadMetrics.LoadPercentage(zone):F1}%"); // after: no abstraction overhead
}

static async Task Section5_5_LeakyAbstractions()
{
    Header("Section 5.5 — Leaky Abstractions and How to Detect Them");

    IZoneDataSource dataSource = new InMemoryZoneDataSource(Array.Empty<GridZone>());

    try
    {
        _ = await dataSource.GetByIdAsync("MISSING") ?? throw new DataSourceUnavailableException("Zone lookup failed.");
    }
    catch (DataSourceUnavailableException)
    {
        // Clean abstraction — the contract specifies a domain exception; callers
        // never need to know or catch an infrastructure-specific type such as
        // SqlException (the book's "leaky" example: `catch (SqlException ex) when (ex.Number == 1205)`).
        Console.WriteLine("Caught DataSourceUnavailableException — no SQL-specific type ever crossed the abstraction boundary.");
    }
}

static async Task Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Alert Notification & Zone Reporting");

    // Part 1 — Alert Notification: manual composition root (no DI container package needed)
    var smsChannel = new SmsAlertChannel(new SmsGatewayClient(), new ConsoleLogger<SmsAlertChannel>());
    var emailChannel = new EmailAlertChannel(new FakeEmailClient(), new ConsoleLogger<EmailAlertChannel>());
    var pushChannel = new PushNotificationChannel(new FakePushProvider(), new ConsoleLogger<PushNotificationChannel>());

    var dispatcher = new AlertDispatcher(
        new IAlertChannel[] { smsChannel, pushChannel, emailChannel },
        new ConsoleLogger<AlertDispatcher>());

    var criticalAlert = new AlertMessage("A-100", "ALPHA-7", AlertSeverity.Critical, "Overload detected",
        "+15551234567", "ops@urbangrid.io", "device-token-1", DateTime.UtcNow);
    await dispatcher.DispatchAsync(criticalAlert); // fans out to SMS + Push (both CanDeliver Critical)

    // Part 2 — Zone Reporting: abstract Template Method, two interchangeable formats
    var operatorRef = new GridOperator { Name = "Morgan Lee" };
    var zone = new GridZone
    {
        ZoneId = "ALPHA-7",
        Status = ZoneStatus.Active,
        CurrentLoadMW = 84,
        CapacityMW = 100,
        AssignedOperator = operatorRef
    };
    var dataSource = new InMemoryZoneDataSource(new[] { zone });

    ZoneHealthReporter csvReporter = new CsvZoneHealthReporter(dataSource, new ConsoleLogger<ZoneHealthReporter>());
    var csvReport = await csvReporter.GenerateAsync("ALPHA-7");
    Console.WriteLine($"CSV report sections: {string.Join(" | ", csvReport.Sections.Select(s => s.Name))}");

    ZoneHealthReporter htmlReporter = new HtmlZoneHealthReporter(dataSource, new ConsoleLogger<ZoneHealthReporter>());
    var htmlReport = await htmlReporter.GenerateAsync("ALPHA-7"); // swap the reporter — AlertDispatcher-style caller needs zero changes
    Console.WriteLine($"HTML report sections: {string.Join(" | ", htmlReport.Sections.Select(s => s.Name))}");

    try
    {
        await csvReporter.GenerateAsync("MISSING-ZONE");
    }
    catch (ZoneNotFoundException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
