namespace OOPBook.Chapter19_ExceptionHandlingInOOP;

public class PermitApplication
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string ApplicantName { get; init; }
    public required string ApplicantEmail { get; init; }
    public string? ZoneId { get; init; }
    public required decimal RequestedCapacityKw { get; init; }
}

// ============================================================================
// Section 5.2 — Custom Exception Hierarchy (introduced early so every later
// section, including the Section 3 mistakes, can reference it)
// ============================================================================

public abstract class PermitException : Exception
{
    public string ErrorCode { get; }

    protected PermitException(string errorCode, string message) : base(message) => ErrorCode = errorCode;

    protected PermitException(string errorCode, string message, Exception inner) : base(message, inner) =>
        ErrorCode = errorCode;
}

public sealed class InvalidPermitApplicationException : PermitException
{
    public InvalidPermitApplicationException(string message) : base("PERMIT_INVALID", message) { }
}

// Thrown only for a hard structural ceiling (e.g. a PeakLoadZone's kW cap) — not the
// same as a routing threshold, which is a business decision handled with Result<T>, not
// an exception (see Section 5.4).
public sealed class PermitCapacityExceededException : PermitException
{
    public double RequestedKw { get; }
    public double MaxAllowedKw { get; }

    public PermitCapacityExceededException(double requestedKw, double maxAllowedKw)
        : base("PERMIT_CAPACITY_EXCEEDED", $"Requested {requestedKw}kW exceeds the hard limit of {maxAllowedKw}kW.")
    {
        RequestedKw = requestedKw;
        MaxAllowedKw = maxAllowedKw;
    }
}

public sealed class PermitPersistenceException : PermitException
{
    public PermitPersistenceException(string message, Exception inner) : base("PERMIT_PERSISTENCE_FAILURE", message, inner) { }
}

// ============================================================================
// Infrastructure stand-ins
// System.Data.SqlClient.SqlException has no public constructor, so it cannot be built
// directly in a demo or a unit test (the book makes this same point in Section 6, Step 6).
// SimulatedSqlException/SqlConnectionStub stand in for it so the try/catch/when patterns
// throughout this chapter still compile and run without a real database dependency.
// ============================================================================

public class SimulatedSqlException : Exception
{
    public int Number { get; }
    public SimulatedSqlException(string message, int number) : base(message) => Number = number;
}

public enum SqlFailureMode { None, Timeout, DuplicateKey, Generic }

public class SqlConnectionStub : IDisposable
{
    private readonly string _connectionString;
    private readonly SqlFailureMode _failureMode;

    public SqlConnectionStub(string connectionString, SqlFailureMode failureMode = SqlFailureMode.None)
    {
        _connectionString = connectionString;
        _failureMode = failureMode;
    }

    public void Open() => Console.WriteLine($"  [DB] Connection opened ({_connectionString}).");

    // Stands in for Dapper's QuerySingle<T> extension method.
    public PermitApplication QuerySingle(Guid permitId)
    {
        ThrowIfConfiguredToFail();
        return new PermitApplication
        {
            Id = permitId,
            Type = "Residential",
            ApplicantName = "Alex Chen",
            ApplicantEmail = "alex.chen@example.com",
            ZoneId = "ZONE-12",
            RequestedCapacityKw = 30.0m
        };
    }

    public void Execute(string sql, object parameters)
    {
        ThrowIfConfiguredToFail();
        Console.WriteLine($"  [DB] Executed: {sql}");
    }

    private void ThrowIfConfiguredToFail()
    {
        switch (_failureMode)
        {
            case SqlFailureMode.Timeout: throw new SimulatedSqlException("Command timeout.", number: -2);
            case SqlFailureMode.DuplicateKey: throw new SimulatedSqlException("Violation of unique constraint.", number: 2601);
            case SqlFailureMode.Generic: throw new SimulatedSqlException("A connection-level error occurred.", number: 53);
        }
    }

    public void Dispose() => Console.WriteLine("  [DB] Connection disposed.");
}

public interface ISimpleLogger<T>
{
    void LogError(Exception ex, string message);
    void LogInformation(string message);
}

public class ConsoleLogger<T> : ISimpleLogger<T>
{
    public void LogError(Exception ex, string message) =>
        Console.WriteLine($"  [ERROR] {typeof(T).Name}: {message} — {ex.GetType().Name}: {ex.Message}");

    public void LogInformation(string message) => Console.WriteLine($"  [INFO] {typeof(T).Name}: {message}");
}

// ============================================================================
// Section 1.1 — ASP.NET Core stand-in
// The book shows IActionResult/Ok()/BadRequest() from a real ASP.NET Core controller.
// ActionResult here is a minimal, dependency-free substitute with the same shape, used
// purely to make the throw-to-catch mechanic (and Section 5.5/6's boundary handling)
// visible in a console project.
// ============================================================================

public class ActionResult
{
    public int StatusCode { get; }
    public object? Body { get; }

    private ActionResult(int statusCode, object? body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public static ActionResult Ok(object? body = null) => new(200, body);
    public static ActionResult BadRequest(object? body = null) => new(400, body);
    public static ActionResult InternalServerError(object? body = null) => new(500, body);

    public override string ToString() =>
        Body is null ? $"{StatusCode}" : $"{StatusCode} {System.Text.Json.JsonSerializer.Serialize(Body)}";
}

public class PermitValidator
{
    public void Validate(PermitApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.ApplicantName))
            throw new InvalidPermitApplicationException("Applicant name is required.");
        if (application.RequestedCapacityKw <= 0)
            throw new InvalidPermitApplicationException("Requested capacity must be positive.");
        if (application.ZoneId is null)
            throw new InvalidPermitApplicationException("A zone must be specified.");
    }
}

// Local stand-in matching the book's SubmitPermit (Section 1.1) — deliberately a manual
// try/catch, exactly as the book shows before Section 5.5 introduces centralised handling.
public class DemoBoundary
{
    private readonly PermitValidator _validator;
    public DemoBoundary(PermitValidator validator) => _validator = validator;

    public ActionResult SubmitPermit(PermitApplication application)
    {
        try
        {
            _validator.Validate(application);
            return ActionResult.Ok();
        }
        catch (InvalidPermitApplicationException ex)
        {
            return ActionResult.BadRequest(new { error = ex.Message });
        }
    }
}

// ============================================================================
// Section 3, Mistake 3 — Using Exceptions for Ordinary Control Flow
// ============================================================================

// Throwaway type for this illustration only — not part of the PermitException hierarchy
// above; this mistake is precisely about a case where an exception type should not exist
// for this purpose at all.
public sealed class PermitNotFoundException : Exception
{
    public PermitNotFoundException(Guid id) : base($"Permit {id} was not found.") { }
}

public class PermitLookup
{
    private readonly List<PermitApplication> _allPermits;
    public PermitLookup(List<PermitApplication> allPermits) => _allPermits = allPermits;

    // BAD — a routine lookup implemented as an exception throw.
    public PermitApplication GetPermitOrThrow(Guid id)
    {
        var permit = _allPermits.FirstOrDefault(p => p.Id == id);
        if (permit is null) throw new PermitNotFoundException(id); // routine, not exceptional
        return permit;
    }

    // GOOD — the expected "not found" case never touches exception machinery.
    public bool TryGetPermit(Guid id, out PermitApplication? permit)
    {
        permit = _allPermits.FirstOrDefault(p => p.Id == id);
        return permit is not null;
    }
}

// ============================================================================
// Section 5.3 — Guard Clauses and the ArgumentException Family
// ============================================================================

public interface IPowerZone
{
    string ZoneId { get; }
    double CapacityKw { get; }
}

public class PeakLoadZone : IPowerZone
{
    public string ZoneId { get; }
    public double CapacityKw { get; }

    public PeakLoadZone(string zoneId, double capacityKw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityKw);
        ZoneId = zoneId;
        CapacityKw = capacityKw;
    }
}

public class PermitApplicationBuilder
{
    private string _name = string.Empty;
    private decimal _capacityKw;
    private IPowerZone _zone = null!;

    public PermitApplicationBuilder WithApplicantName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Applicant name cannot be blank.", nameof(name));
        _name = name;
        return this;
    }

    public PermitApplicationBuilder WithZone(IPowerZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone); // IPowerZone, not a concrete zone type
        _zone = zone;
        return this;
    }

    // WithCapacity()/Build() extend the book's two-method excerpt just enough to make the
    // builder produce a real PermitApplication end-to-end.
    public PermitApplicationBuilder WithCapacity(decimal capacityKw)
    {
        _capacityKw = capacityKw;
        return this;
    }

    public PermitApplication Build() => new PermitApplication
    {
        Id = Guid.NewGuid(),
        Type = "Residential",
        ApplicantName = _name,
        ApplicantEmail = $"{_name.Replace(" ", ".").ToLowerInvariant()}@example.com",
        ZoneId = _zone?.ZoneId,
        RequestedCapacityKw = _capacityKw
    };
}

// ============================================================================
// Section 5.4 — The Result Pattern
// ============================================================================

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool ok, T? v, string? e, string? c)
    {
        IsSuccess = ok;
        Value = v;
        Error = e;
        ErrorCode = c;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string error, string code) => new(false, default, error, code);
}

public interface IPermitApprovalService
{
    Result<string> Approve(PermitApplication application);
}

public class PermitApprovalService : IPermitApprovalService
{
    private const double MaxResidentialCapacityKw = 500.0;

    public Result<string> Approve(PermitApplication application)
    {
        if (application.RequestedCapacityKw > (decimal)MaxResidentialCapacityKw)
            return Result<string>.Failure(
                $"Requested {application.RequestedCapacityKw}kW requires supervisor approval.",
                "PERMIT_CAPACITY_EXCEEDED");

        return Result<string>.Success("APPROVED");
    }
}

// ============================================================================
// Section 5.5 — Global Exception Handling
// The book's version implements ASP.NET Core's IExceptionHandler against a real
// HttpContext/ProblemDetails pipeline. GlobalPermitExceptionHandler here keeps the same
// decision logic (PermitException → 400 with error code; anything else → 500, generic
// message only) but operates on the console-friendly ActionResult defined above instead.
// ============================================================================

public class GlobalPermitExceptionHandler
{
    private readonly ISimpleLogger<GlobalPermitExceptionHandler> _logger;
    public GlobalPermitExceptionHandler(ISimpleLogger<GlobalPermitExceptionHandler> logger) => _logger = logger;

    public ActionResult Handle(Exception exception, string path)
    {
        _logger.LogError(exception, $"Unhandled exception on {path}");

        if (exception is PermitException permitEx)
        {
            return ActionResult.BadRequest(new
            {
                title = "Permit request could not be processed.",
                detail = permitEx.Message, // safe: domain messages contain no secrets
                errorCode = permitEx.ErrorCode
            });
        }

        // Unrecognised: a bug or infrastructure failure — generic message only,
        // detail lives in the log, never in exception.Message sent to the client.
        return ActionResult.InternalServerError(new { title = "An unexpected error occurred." });
    }
}

// ============================================================================
// Section 6 — Case Study: UrbanGrid Permit Processing Failure Pipeline
// ============================================================================

public interface IPermitFeeCalculator
{
    bool CanHandle(string permitType);
    decimal Calculate(PermitApplication application);
}

public class ResidentialFeeCalculator : IPermitFeeCalculator
{
    public bool CanHandle(string permitType) => permitType == "Residential";
    public decimal Calculate(PermitApplication application) => application.RequestedCapacityKw * 1.50m;
}

public interface IPermitFeeEngine
{
    decimal Calculate(PermitApplication application);
}

public class PermitFeeEngine : IPermitFeeEngine
{
    private readonly IReadOnlyList<IPermitFeeCalculator> _calculators;
    public PermitFeeEngine(IEnumerable<IPermitFeeCalculator> calculators) => _calculators = calculators.ToList();

    public decimal Calculate(PermitApplication application)
    {
        var calculator = _calculators.FirstOrDefault(c => c.CanHandle(application.Type))
            ?? throw new InvalidOperationException($"No fee calculator registered for permit type '{application.Type}'.");
        return calculator.Calculate(application);
    }
}

public interface IPermitNotifier
{
    void Notify(PermitApplication application, string status);
}

public class FakePermitNotifier : IPermitNotifier
{
    public List<(PermitApplication Application, string Status)> Sent { get; } = new();
    public void Notify(PermitApplication application, string status) => Sent.Add((application, status));
}

public interface IPermitRepository
{
    void Save(PermitApplication application, string status, decimal fee);
}

public class InMemoryPermitRepository : IPermitRepository
{
    private readonly Dictionary<Guid, (string Status, decimal Fee)> _store = new();
    public void Save(PermitApplication application, string status, decimal fee) => _store[application.Id] = (status, fee);
}

// Step 3 — Infrastructure Layer: the only place that knows about SQL at all. It catches
// exactly the technical exception it can translate and wraps it, preserving InnerException.
public class SqlPermitRepository : IPermitRepository
{
    private readonly string _connectionString;
    private readonly SqlFailureMode _failureMode;

    public SqlPermitRepository(string connectionString, SqlFailureMode failureMode = SqlFailureMode.None)
    {
        _connectionString = connectionString;
        _failureMode = failureMode;
    }

    public void Save(PermitApplication application, string status, decimal fee)
    {
        try
        {
            using var conn = new SqlConnectionStub(_connectionString, _failureMode);
            conn.Open();
            conn.Execute("INSERT INTO Permits (...) VALUES (...)", new { application.ApplicantName, application.ZoneId, status, fee });
        }
        catch (SimulatedSqlException ex)
        {
            // Server name / connection string stay in InnerException — logged, not returned.
            throw new PermitPersistenceException($"Failed to save permit {application.Id}.", ex);
        }
    }
}

// Step 6 test double — deliberately throws on every call so the workflow's
// persistence-failure path can be exercised without any real infrastructure.
public sealed class ThrowingPermitRepository : IPermitRepository
{
    public void Save(PermitApplication application, string status, decimal fee) =>
        throw new InvalidOperationException("Simulated database failure.");
}

public record class PermitResult(Guid Id, string Status, decimal Fee);

// Step 4 — Application Layer. Five dependencies: this chapter adds PermitValidator to the
// four-dependency version from Chapter 16 §7 (repository, feeEngine, approvalService, notifier).
public class PermitApprovalWorkflow
{
    private readonly IPermitRepository _repository;
    private readonly IPermitFeeEngine _feeEngine;
    private readonly IPermitApprovalService _approvalService;
    private readonly IPermitNotifier _notifier;
    private readonly PermitValidator _validator;

    public PermitApprovalWorkflow(IPermitRepository repository, IPermitFeeEngine feeEngine,
        IPermitApprovalService approvalService, IPermitNotifier notifier, PermitValidator validator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _feeEngine = feeEngine ?? throw new ArgumentNullException(nameof(feeEngine));
        _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public PermitResult Process(PermitApplication application)
    {
        _validator.Validate(application); // genuinely exceptional if it fails here

        decimal fee = _feeEngine.Calculate(application);

        var decision = _approvalService.Approve(application); // expected business branch
        string status = decision.IsSuccess ? decision.Value! : "PENDING_SUPERVISOR";

        try
        {
            _repository.Save(application, status, fee); // infra failure propagates, genuinely exceptional
        }
        catch (PermitException)
        {
            throw; // already a domain exception (e.g. SqlPermitRepository already wrapped it) — propagate as-is
        }
        catch (Exception ex)
        {
            // Guarantees a failure reaching the application boundary is always a PermitException,
            // even if a repository implementation throws a raw technical exception directly instead
            // of wrapping it itself (see Step 6's ThrowingPermitRepository test double).
            throw new PermitPersistenceException($"Failed to save permit {application.Id}.", ex);
        }

        _notifier.Notify(application, status);
        return new PermitResult(application.Id, status, fee);
    }
}

// Step 5 — Application Boundary: a controller-style entry point using the ActionResult
// stand-in. In the real app, GlobalPermitExceptionHandler intercepts anything this method
// doesn't catch itself — here that's demonstrated explicitly in Program.cs.
public class PermitController
{
    private readonly PermitApprovalWorkflow _workflow;
    public PermitController(PermitApprovalWorkflow workflow) => _workflow = workflow;

    public ActionResult Submit(PermitApplication application)
    {
        // If this throws, the caller's GlobalPermitExceptionHandler catches it — a straight happy path.
        var result = _workflow.Process(application);
        return ActionResult.Ok(result);
    }
}
