// Chapter 19 — Exception Handling in OOP
// Run with: dotnet run --project Chapter19_ExceptionHandlingInOOP

using OOPBook.Chapter19_ExceptionHandlingInOOP;

Section1_1_ThrowToCatch();
Section2_1_WhyExceptionsExist();
Section3_CommonMistakes();
Section5_1_TryCatchFinallyWhen();
Section5_2_CustomExceptionHierarchy();
Section5_3_GuardClauses();
Section5_4_ResultPattern();
Section5_5_GlobalExceptionHandling();
Section6_CaseStudy();

static PermitApplication SampleApplication(decimal capacity = 30m, string name = "Alex Chen") => new PermitApplication
{
    Id = Guid.NewGuid(),
    Type = "Residential",
    ApplicantName = name,
    ApplicantEmail = "alex.chen@example.com",
    ZoneId = "ZONE-12",
    RequestedCapacityKw = capacity
};

static void Section1_1_ThrowToCatch()
{
    Header("Section 1.1 — What Is an Exception? (throw site to catch site)");

    var validator = new PermitValidator();
    var controller = new DemoBoundary(validator);

    Console.WriteLine(controller.SubmitPermit(SampleApplication()));                  // 200 OK
    Console.WriteLine(controller.SubmitPermit(SampleApplication(capacity: 0)));       // 400 BadRequest
}

static void Section2_1_WhyExceptionsExist()
{
    Header("Section 2.1 — Why Exceptions Exist");

    var validator = new PermitValidator();
    var repository = new InMemoryPermitRepository();
    var notifier = new FakePermitNotifier();

    // With exceptions — the happy path is a straight line.
    var application = SampleApplication();
    validator.Validate(application);           // throws on failure
    repository.Save(application, "APPROVED", 45.0m); // throws on failure
    notifier.Notify(application, "APPROVED");  // throws on failure
    Console.WriteLine("Happy path completed with no inline error checks at any call site.");
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    Console.WriteLine("-- Mistake 1: Swallowing Exceptions Silently --");
    var logger = new ConsoleLogger<object>();
    var failingRepo = new SqlPermitRepository("Server=urbangrid-db;...", SqlFailureMode.Generic);
    var app1 = SampleApplication();

    try { failingRepo.Save(app1, "APPROVED", 0m); }
    catch { /* BAD — nothing here. The failure vanishes; nobody finds out the permit was not saved. */ }
    Console.WriteLine("BAD: exception swallowed — caller has no idea the save failed.");

    try
    {
        failingRepo.Save(app1, "APPROVED", 0m);
    }
    catch (PermitPersistenceException ex)
    {
        logger.LogError(ex, $"Failed to save permit {app1.Id}");
        Console.WriteLine("GOOD: the failure was logged and remains visible to the caller (rethrown in a real app).");
    }

    Console.WriteLine("-- Mistake 2: Losing the Stack Trace with 'throw ex;' --");
    try { ThrowExResetsStackTrace(); }
    catch (Exception ex) { Console.WriteLine($"BAD  — stack trace now starts at ThrowExResetsStackTrace: {FirstStackLine(ex)}"); }

    try { ThrowPreservesStackTrace(); }
    catch (Exception ex) { Console.WriteLine($"GOOD — stack trace still starts at the original throw site: {FirstStackLine(ex)}"); }

    Console.WriteLine("-- Mistake 3: Using Exceptions for Ordinary Control Flow --");
    var lookup = new PermitLookup(new List<PermitApplication> { app1 });
    try { lookup.GetPermitOrThrow(Guid.NewGuid()); }
    catch (PermitNotFoundException ex) { Console.WriteLine($"BAD  — routine 'not found' used exception machinery: {ex.Message}"); }

    var found = lookup.TryGetPermit(Guid.NewGuid(), out var missing);
    Console.WriteLine($"GOOD — TryGetPermit returned {found} with no exception thrown at all.");

    Console.WriteLine("-- Mistake 4: Catching Exception Too Broadly, Too Early --");
    var validator = new PermitValidator();
    var repository = new InMemoryPermitRepository();
    var notifier = new FakePermitNotifier();
    try
    {
        validator.Validate(SampleApplication(capacity: 0)); // this is the one that will actually fail
        repository.Save(SampleApplication(), "APPROVED", 0m);
        notifier.Notify(SampleApplication(), "APPROVED");
    }
    catch (Exception) { Console.WriteLine("BAD  — \"Something went wrong.\" (which call failed? no idea)"); }

    Console.WriteLine("-- Mistake 5: Leaking Resources on the Exception Path --");
    Console.WriteLine("BAD — if Execute() throws, Dispose() is never reached:");
    try
    {
        var badConn = new SqlConnectionStub("Server=urbangrid-db;...", SqlFailureMode.Generic);
        badConn.Open();
        badConn.Execute("INSERT INTO Permits ...", new { });
        badConn.Dispose(); // skipped entirely — Execute() above throws first
    }
    catch (SimulatedSqlException ex) { Console.WriteLine($"  Caught: {ex.Message} (connection was never disposed)"); }

    Console.WriteLine("GOOD — 'using' guarantees Dispose(), exception or not:");
    try
    {
        using var goodConn = new SqlConnectionStub("Server=urbangrid-db;...", SqlFailureMode.Generic);
        goodConn.Open();
        goodConn.Execute("INSERT INTO Permits ...", new { });
    }
    catch (SimulatedSqlException ex) { Console.WriteLine($"  Caught: {ex.Message} (connection was disposed automatically)"); }
}

static void ThrowExResetsStackTrace()
{
    try { throw new InvalidOperationException("original failure"); }
    catch (Exception ex) { throw ex; } // BAD — resets the stack trace to this line
}

static void ThrowPreservesStackTrace()
{
    try { throw new InvalidOperationException("original failure"); }
    catch (Exception) { throw; } // GOOD — preserves the full original stack trace
}

static string FirstStackLine(Exception ex) =>
    (ex.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim() ?? "(no stack trace captured)";

static void Section5_1_TryCatchFinallyWhen()
{
    Header("Section 5.1 — try / catch / finally / when");

    Console.WriteLine("-- LoadPermit: single-catch-family with a 'when' filter --");
    Console.WriteLine(LoadPermitDemo(SqlFailureMode.None));
    Console.WriteLine(LoadPermitDemo(SqlFailureMode.Timeout));
    Console.WriteLine(LoadPermitDemo(SqlFailureMode.Generic));

    Console.WriteLine("-- Multiple catches, most specific type first --");
    Console.WriteLine(MultiCatchDemo(SqlFailureMode.DuplicateKey));
    Console.WriteLine(MultiCatchDemo(SqlFailureMode.Generic));
}

static string LoadPermitDemo(SqlFailureMode mode)
{
    SqlConnectionStub? conn = null;
    try
    {
        conn = new SqlConnectionStub("Server=urbangrid-db;...", mode);
        conn.Open();
        var permit = conn.QuerySingle(Guid.NewGuid());
        return $"Loaded permit {permit.Id}.";
    }
    catch (SimulatedSqlException ex) when (ex.Number == -2) // SQL error -2: command timeout
    {
        return $"PermitPersistenceException: Timed out loading permit. ({ex.Message})";
    }
    catch (SimulatedSqlException ex)
    {
        return $"PermitPersistenceException: Failed to load permit. ({ex.Message})";
    }
    finally
    {
        conn?.Dispose(); // always runs — exception thrown, caught, or neither
    }
}

static string MultiCatchDemo(SqlFailureMode mode)
{
    var application = SampleApplication();
    try
    {
        using var conn = new SqlConnectionStub("Server=urbangrid-db;...", mode);
        conn.Open();
        conn.Execute("INSERT INTO Permits ...", new { });
        return "Insert succeeded.";
    }
    catch (SimulatedSqlException ex) when (ex.Number == 2601) // duplicate key
    {
        return $"PermitPersistenceException: Permit {application.Id} already exists. ({ex.Message})";
    }
    catch (SimulatedSqlException ex)
    {
        return $"PermitPersistenceException: Database error. ({ex.Message})";
    }
    catch (Exception ex)
    {
        return $"PermitPersistenceException: Unexpected failure. ({ex.Message})";
    }
}

static void Section5_2_CustomExceptionHierarchy()
{
    Header("Section 5.2 — Custom Exception Hierarchies");

    PermitException[] examples =
    {
        new InvalidPermitApplicationException("Requested capacity must be greater than zero."),
        new PermitCapacityExceededException(750, 500),
        new PermitPersistenceException("Failed to save permit.", new SimulatedSqlException("connection reset", 53))
    };

    foreach (var ex in examples)
        Console.WriteLine($"{ex.GetType().Name}: [{ex.ErrorCode}] {ex.Message}");
}

static void Section5_3_GuardClauses()
{
    Header("Section 5.3 — Guard Clauses and the ArgumentException Family");

    try { new PermitApplicationBuilder().WithApplicantName(""); }
    catch (ArgumentException ex) { Console.WriteLine($"Caught: {ex.Message}"); }

    try { new PermitApplicationBuilder().WithApplicantName("Alex Chen").WithZone(null!); }
    catch (ArgumentNullException ex) { Console.WriteLine($"Caught: {ex.ParamName} must not be null."); }

    var zone = new PeakLoadZone("ZONE-9", 150.0);
    Console.WriteLine($"Built PeakLoadZone '{zone.ZoneId}' with capacity {zone.CapacityKw}kW.");

    try { _ = new PeakLoadZone("ZONE-9", -10); }
    catch (ArgumentOutOfRangeException ex) { Console.WriteLine($"Caught: {ex.Message}"); }

    var built = new PermitApplicationBuilder()
        .WithApplicantName("Jamie Lee")
        .WithZone(zone)
        .WithCapacity(120)
        .Build();
    Console.WriteLine($"Built application for {built.ApplicantName} in zone {built.ZoneId}.");
}

static void Section5_4_ResultPattern()
{
    Header("Section 5.4 — The Result Pattern");

    IPermitApprovalService approvalService = new PermitApprovalService();

    var normal = approvalService.Approve(SampleApplication(capacity: 30m));
    Console.WriteLine($"Approve(30kW): IsSuccess={normal.IsSuccess}, Value={normal.Value}");

    var oversized = approvalService.Approve(SampleApplication(capacity: 600m));
    Console.WriteLine($"Approve(600kW): IsSuccess={oversized.IsSuccess}, Error='{oversized.Error}', ErrorCode={oversized.ErrorCode}");
    Console.WriteLine("Note: this is an expected business branch, routed with Result<T> — no exception involved.");
}

static void Section5_5_GlobalExceptionHandling()
{
    Header("Section 5.5 — Global Exception Handling in .NET 8");

    var logger = new ConsoleLogger<GlobalPermitExceptionHandler>();
    var handler = new GlobalPermitExceptionHandler(logger);

    var known = handler.Handle(new InvalidPermitApplicationException("Requested capacity must be greater than zero."), "/permits");
    Console.WriteLine($"Response: {known}");

    var unknown = handler.Handle(new InvalidOperationException("Unhandled bug"), "/permits");
    Console.WriteLine($"Response: {unknown}");

    Console.WriteLine("Reference only (not compiled) — real ASP.NET Core wiring:");
    Console.WriteLine("  builder.Services.AddExceptionHandler<GlobalPermitExceptionHandler>();");
    Console.WriteLine("  builder.Services.AddProblemDetails();");
    Console.WriteLine("  app.UseExceptionHandler();");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: UrbanGrid Permit Processing Failure Pipeline");

    var logger = new ConsoleLogger<GlobalPermitExceptionHandler>();
    var boundaryHandler = new GlobalPermitExceptionHandler(logger);

    // Happy path.
    var workingWorkflow = new PermitApprovalWorkflow(
        new InMemoryPermitRepository(),
        new PermitFeeEngine(new[] { new ResidentialFeeCalculator() }),
        new PermitApprovalService(),
        new FakePermitNotifier(),
        new PermitValidator());
    var controller = new PermitController(workingWorkflow);
    Console.WriteLine($"Submit(valid application): {controller.Submit(SampleApplication())}");

    // Invalid application — the workflow throws, and the boundary handler catches it exactly
    // the way it would if PermitController.Submit had not caught it itself.
    try
    {
        controller.Submit(SampleApplication(capacity: 0));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Submit(invalid application): {boundaryHandler.Handle(ex, "/permits")}");
    }

    // Persistence failure — infra layer wraps a technical failure as a PermitException.
    var failingWorkflow = new PermitApprovalWorkflow(
        new ThrowingPermitRepository(),
        new PermitFeeEngine(new[] { new ResidentialFeeCalculator() }),
        new PermitApprovalService(),
        new FakePermitNotifier(),
        new PermitValidator());
    var failingController = new PermitController(failingWorkflow);
    try
    {
        failingController.Submit(SampleApplication());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Submit(persistence failure): {boundaryHandler.Handle(ex, "/permits")}");
    }

    Console.WriteLine();
    Console.WriteLine("-- Step 6 — Unit Tests (hand-rolled assertions; no xUnit dependency) --");

    Process_InvalidApplication_ThrowsInvalidPermitApplicationException();
    Process_PersistenceFailure_WrapsAsPermitPersistenceException();
}

static void Process_InvalidApplication_ThrowsInvalidPermitApplicationException()
{
    var workflow = new PermitApprovalWorkflow(new InMemoryPermitRepository(),
        new PermitFeeEngine(new[] { new ResidentialFeeCalculator() }),
        new PermitApprovalService(), new FakePermitNotifier(), new PermitValidator());

    var invalidApplication = new PermitApplication
    {
        Id = Guid.NewGuid(),
        Type = "Residential",
        ApplicantName = "",
        ApplicantEmail = "a@b.com",
        ZoneId = "ZONE-12",
        RequestedCapacityKw = 30.0m
    };

    try
    {
        workflow.Process(invalidApplication);
        Assert("throws InvalidPermitApplicationException", false);
    }
    catch (InvalidPermitApplicationException ex)
    {
        Assert("throws InvalidPermitApplicationException", true);
        Assert("ErrorCode is PERMIT_INVALID", ex.ErrorCode == "PERMIT_INVALID");
    }
}

static void Process_PersistenceFailure_WrapsAsPermitPersistenceException()
{
    var workflow = new PermitApprovalWorkflow(new ThrowingPermitRepository(),
        new PermitFeeEngine(new[] { new ResidentialFeeCalculator() }),
        new PermitApprovalService(), new FakePermitNotifier(), new PermitValidator());

    var validApplication = SampleApplication();

    try
    {
        workflow.Process(validApplication);
        Assert("wraps failure as PermitPersistenceException", false);
    }
    catch (PermitPersistenceException ex)
    {
        Assert("wraps failure as PermitPersistenceException", true);
        Assert("InnerException is not null", ex.InnerException is not null);
        Assert("InnerException is InvalidOperationException", ex.InnerException is InvalidOperationException);
    }
}

static void Assert(string description, bool condition) =>
    Console.WriteLine(condition ? $"  PASS — {description}" : $"  FAIL — {description}");

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
