// Chapter 18 — Core Design Patterns
// Run with: dotnet run --project Chapter18_CoreDesignPatterns

using OOPBook.Chapter18_CoreDesignPatterns;

Section5_1_Creational();
Section5_2_Structural();
Section5_3_Behavioural();
Section6_CaseStudy();

static void Section5_1_Creational()
{
    Header("Section 5.1 — Creational Patterns");

    Console.WriteLine("-- Factory Method --");
    GridAlertService svc = new SmsAlertService();
    svc.RaiseAlert("North-7", "Threshold exceeded"); // [SMS] North-7: Threshold exceeded
    GridAlertService emailSvc = new EmailAlertService();
    emailSvc.RaiseAlert("North-7", "Threshold exceeded");

    Console.WriteLine("-- Abstract Factory --");
    new GridZoneMonitor(new ProductionMonitoringFactory(), "SEN-N7-001").CheckZone("North-7");
    new GridZoneMonitor(new TestMonitoringFactory(), "SEN-N7-002").CheckZone("North-7"); // simulated reading is 0.0 — never alerts

    Console.WriteLine("-- Builder --");
    var permit = new GridPermit.Builder("PRM-001", "North-7", DateTime.UtcNow.AddYears(1))
        .WithAuditLog()
        .AsRenewal()
        .Build();
    Console.WriteLine($"Built {permit.PermitId} for {permit.ZoneCode}: HasAuditLog={permit.HasAuditLog}, IsRenewal={permit.IsRenewal}");

    Console.WriteLine("-- Singleton --");
    Console.WriteLine(ReferenceEquals(GridControlCentre.Instance, GridControlCentre.Instance)); // True — always the same instance
    GridControlCentre.Instance.Register(new PowerSubstation("SUB-North-7", "North-7", new GridSensorStub("SEN-N7-001")));
    Console.WriteLine($"SubstationCount: {GridControlCentre.Instance.SubstationCount}");

    Console.WriteLine("-- Prototype --");
    var template = new GridPermitTemplate("PRM-TEMPLATE", "North-7", DateTime.UtcNow.AddYears(1), hasAuditLog: true);
    var eastPermit = template.CloneFor("East-4", "PRM-002");
    var southPermit = template.CloneFor("South-1", "PRM-003");
    Console.WriteLine($"eastPermit {eastPermit.PermitId}/{eastPermit.ZoneCode}: HasAuditLog={eastPermit.HasAuditLog}"); // True — inherited from template
    Console.WriteLine($"southPermit {southPermit.PermitId}/{southPermit.ZoneCode}: HasAuditLog={southPermit.HasAuditLog}");
}

static void Section5_2_Structural()
{
    Header("Section 5.2 — Structural Patterns");

    Console.WriteLine("-- Adapter --");
    IGridSensor sensor = new LegacySensorAdapter(new LegacySensorReader("LEGACY-SEN-001"), "SEN-N7-001");
    var r = sensor.ReadValue();
    Console.WriteLine($"Sensor {r.SensorId}: {r.Value} MW at {r.Timestamp}");

    Console.WriteLine("-- Decorator --");
    IAlertSender sender = new LoggingAlertDecorator(new RetryAlertDecorator(new SmsAlertSender(), max: 3));
    sender.Send("North-7", "Threshold exceeded");
    // [LOG→] North-7
    // [SMS] North-7: Threshold exceeded
    // [LOG←] North-7: delivered.

    Console.WriteLine("-- Facade --");
    var facade = new ZoneCommissioningFacade(GridControlCentre.Instance, new GridPermitAuditService());
    facade.CommissionZone("East-4", "SEN-E4-001", "PRM-002");

    Console.WriteLine("-- Proxy --");
    ISubstationInfo s = new SubstationProxy("North-7", true);
    Console.WriteLine(s.ZoneCode);         // lightweight — no DB call
    var h = s.GetHistoricalReadings();     // DB load triggered here, only now
    Console.WriteLine($"Historical readings loaded: {h.Count}");
}

static void Section5_3_Behavioural()
{
    Header("Section 5.3 — Behavioural Patterns");

    Console.WriteLine("-- Strategy --");
    var eval = new GridSensorEvaluator(new ThresholdEvaluationStrategy(100.0));
    eval.Evaluate(new SensorReading("SEN-N7-001", 112.3, DateTime.UtcNow), "North-7");
    // ALERT — Zone North-7: 112.3 MW

    eval.SetStrategy(new RateOfChangeStrategy(20.0));
    // _last starts at 0.0 on a new strategy instance.
    eval.Evaluate(new SensorReading("SEN-N7-001", 96.5, DateTime.UtcNow), "North-7");  // |96.5-0| = 96.5 > 20 → ALERT
    eval.Evaluate(new SensorReading("SEN-N7-001", 98.0, DateTime.UtcNow), "North-7");  // |98.0-96.5| = 1.5 <= 20 → OK

    Console.WriteLine("-- Observer --");
    var zone = new GridZoneSubject("North-7");
    zone.Subscribe(new AlertNotifierObserver());
    zone.Subscribe(new AuditLogObserver());
    zone.GoOffline();
    // [ALERT] Zone North-7 offline.
    // [AUDIT] Zone North-7: offline

    Console.WriteLine("-- Template Method --");
    GridZoneReportGenerator gen = new PlainTextReportGenerator();
    Console.WriteLine(gen.Generate("North-7"));
    GridZoneReportGenerator jsonGen = new JsonReportGenerator();
    Console.WriteLine(jsonGen.Generate("North-7"));

    Console.WriteLine("-- Command --");
    var commandZone = new GridZone("North-7");
    var inv = new GridCommandInvoker();
    inv.Enqueue(new DeactivateZoneCommand(commandZone));
    inv.ExecuteNext();
    // [COMMAND] Zone North-7 deactivated.
    // [AUDIT] Logged: Deactivate zone North-7
    inv.UndoLast();
    // [UNDO] Zone North-7 reactivated.
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: UrbanGrid Zone Alert Pipeline (Strategy + Decorator + Command)");

    // Composition root — wire everything here, once.
    IAlertSender sender = new LoggingAlertDecorator(new SmsAlertSender());
    var pipeline = new ZoneAlertPipeline(new ThresholdEvaluationStrategy(100.0), sender);
    pipeline.Process(new SensorReading("SEN-N7-001", 87.4, DateTime.UtcNow), "North-7");  // below threshold — no alert
    pipeline.Process(new SensorReading("SEN-N7-001", 112.3, DateTime.UtcNow), "North-7"); // above threshold — alert fires
    pipeline.PrintAuditLog();
    // [LOG→] North-7: Sensor SEN-N7-001 reading 112.3 MW exceeded threshold.
    // [SMS] North-7: Sensor SEN-N7-001 reading 112.3 MW exceeded threshold.
    // [LOG←] North-7: delivered.
    // ─── Audit Log ───
    //   ✓ Alert zone North-7: Sensor SEN-N7-001 reading 112.3 MW exceeded threshold.
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
