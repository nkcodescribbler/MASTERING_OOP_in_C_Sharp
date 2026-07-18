// Chapter 16 — SOLID Principles
// Run with: dotnet run --project Chapter16_SOLIDPrinciples

using OOPBook.Chapter16_SOLIDPrinciples;

Section1_SingleResponsibility();
Section2_OpenClosed();
Section3_LiskovSubstitution();
Section4_InterfaceSegregation();
Section5_DependencyInversion();
Section6_TemplateMethodIoC();
Section7_CaseStudy();

static PermitApplication SampleApplication(string type = "Residential", decimal capacity = 30m) => new PermitApplication
{
    Type = type,
    ApplicantName = "Alex Chen",
    ApplicantEmail = "alex.chen@example.com",
    ZoneId = "ZONE-12",
    RequestedCapacityKw = capacity
};

static void Section1_SingleResponsibility()
{
    Header("Section 1 — Single Responsibility Principle");

    var before = new PermitProcessorBefore(); // BEFORE — four responsibilities in one class
    var app = SampleApplication();
    if (before.Validate(app))
    {
        var status = before.Approve(app);
        before.Save(app, status);
        before.Notify(app, status);
    }

    // AFTER — each responsibility isolated, coordinator only orchestrates.
    var processor = new PermitProcessor(
        new PermitValidator(),
        new PermitApprovalService(),
        new SqlPermitRepository("Server=urbangrid-db;..."),
        new SmtpPermitNotifier());
    processor.Process(app);

    try
    {
        processor.Process(app with { ApplicantName = "" });
    }
    catch (InvalidPermitApplicationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }
}

static void Section2_OpenClosed()
{
    Header("Section 2 — Open/Closed Principle");

    var before = new PermitFeeCalculatorBefore(); // BEFORE — editing this method for every new type
    Console.WriteLine(before.Calculate(SampleApplication("Commercial", 100)));

    // AFTER — new types are new files; PermitFeeEngine never changes.
    var engine = new PermitFeeEngine(new IPermitFeeCalculator[]
    {
        new ResidentialFeeCalculator(), new CommercialFeeCalculator(), new IndustrialFeeCalculator(), new SolarFeeCalculator()
    });
    Console.WriteLine(engine.Calculate(SampleApplication("Solar", 40)));
}

static void Section3_LiskovSubstitution()
{
    Header("Section 3 — Liskov Substitution Principle");

    PowerZone zone = new PeakLoadZoneViolating();
    zone.SetCapacity(500.0);
    Console.WriteLine(zone.CapacityKw); // Expected: 500. Actual: 200 — silent corruption.

    IPowerZone standard = new StandardPowerZone("ZONE-1", 500.0);
    standard.Allocate(300.0);
    Console.WriteLine($"Standard zone allocated: {standard.CapacityKw}");

    IPowerZone peak = new PeakLoadZone("ZONE-2", 150.0); // constraint is honest — enforced at construction
    Console.WriteLine($"Peak zone capacity (never silently clamped): {peak.CapacityKw}");

    try
    {
        _ = new PeakLoadZone("ZONE-3", 500.0); // exceeds MaxCapacityKw — fails loudly, at construction
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }
}

static void Section4_InterfaceSegregation()
{
    Header("Section 4 — Interface Segregation Principle");

    IPermitService fat = new ReadOnlyPermitReportingServiceBad(); // BEFORE — forced to implement everything
    Console.WriteLine(fat.GenerateMonthlyReport(2024, 6));
    try { fat.Submit(SampleApplication()); }
    catch (NotSupportedException) { Console.WriteLine("Caught NotSupportedException — Submit() was never honestly implementable here."); }

    IPermitReportingService honest = new ReadOnlyPermitReportingService(); // AFTER — only implements what it can do
    Console.WriteLine(honest.GenerateMonthlyReport(2024, 6));

    var full = new PermitService();
    var app = SampleApplication();
    full.Submit(app);
    full.Approve(app.Id, "Supervisor");
    var controller = new PermitLifecycleController(full); // only sees lifecycle methods
    Console.WriteLine(controller.Approve(app.Id, "Supervisor"));
}

static void Section5_DependencyInversion()
{
    Header("Section 5 — Dependency Inversion Principle");

    var before = new PermitApprovalWorkflowBefore(); // BEFORE — creates its own concrete dependencies
    before.RunApproval(SampleApplication(), "Supervisor");

    // AFTER — depends only on abstractions.
    var repo = new InMemoryPermitRepository();
    var app = SampleApplication();
    repo.Save(app, "SUBMITTED", 0m);
    var workflow = new PermitApprovalWorkflowTwoDeps(repo, new FakePermitNotifier());
    workflow.RunApproval(app.Id, "Supervisor");
    Console.WriteLine($"After DIP fix: {repo.GetById(app.Id).Status}");
}

static void Section6_TemplateMethodIoC()
{
    Header("Section 6 — All Five Together: IoC as the Unifying Thread");

    PermitApprovalTemplate residential = new ResidentialPermitApproval();
    residential.Run(SampleApplication("Residential", 40)); // fixed skeleton, variable Decide() step

    PermitApprovalTemplate industrial = new IndustrialPermitApproval();
    industrial.Run(SampleApplication("Industrial", 600));
}

static void Section7_CaseStudy()
{
    Header("Section 7 — Case Study: UrbanGrid Permit Approval System");

    // The legacy LSP violation is still reachable, exactly as the book shows it.
    PowerZone legacyZone = new PeakLoadZoneViolating();
    LegacyZoneCapacitySetter.SetZoneCapacity(legacyZone, 500.0);
    Console.WriteLine($"Legacy SetZoneCapacity still silently clamps: {legacyZone.CapacityKw}");

    // The SOLID-compliant architecture.
    var repository = new InMemoryPermitRepository();
    var notifier = new FakePermitNotifier();
    var feeEngine = new PermitFeeEngine(new IPermitFeeCalculator[] { new ResidentialFeeCalculator() });
    var approval = new PermitApprovalService();
    var workflow = new PermitApprovalWorkflow(repository, feeEngine, approval, notifier);

    var application = SampleApplication("Residential", 30.0m);
    var result = workflow.Process(application);

    Assert("status is APPROVED", result.Status == "APPROVED");
    Assert("fee matches ResidentialFeeCalculator rate", result.Fee == 30.0m * 1.50m);
    Assert("exactly one notification sent", notifier.Sent.Count == 1);
    Assert("notification references the right application", notifier.Sent.Count > 0 && notifier.Sent[0].Application.Id == application.Id);

    // The Three SOLID Additions — each is a new file; PermitApprovalWorkflow itself never changes.
    var solarWorkflow = new PermitApprovalWorkflow(
        repository,
        new PermitFeeEngine(new IPermitFeeCalculator[] { new SolarFeeCalculator() }), // OCP — new calculator, no switch edit
        approval,
        new AuditTrailPermitNotifier(new FakePermitNotifier(), new ConsoleAuditRepository())); // decorator adds audit, notifier untouched
    var solarResult = solarWorkflow.Process(SampleApplication("Solar", 20m));
    Console.WriteLine($"Solar permit fee: {solarResult.Fee}");
}

static void Assert(string description, bool condition) =>
    Console.WriteLine(condition ? $"  PASS — {description}" : $"  FAIL — {description}");

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
