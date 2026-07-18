// Chapter 16 — SOLID Principles
// PermitApprovalWorkflow appears three times as the chapter progresses: a
// two-dependency DIP violation (5.2), its two-dependency fix (5.3), and the
// four-dependency, fully SOLID-compliant version from the Section 7 case
// study. The case study version is used as the canonical PermitApprovalWorkflow;
// the two earlier stages are kept under distinct names so the DIP lesson
// itself stays visible. SqlConnection/SmtpClient/SendGrid are replaced with
// small in-process stand-ins so nothing here needs a database, SMTP server,
// or external SDK to run.

namespace OOPBook.Chapter16_SOLIDPrinciples;

public record PermitApplication
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = string.Empty;
    public string ApplicantName { get; init; } = string.Empty;
    public string ApplicantEmail { get; init; } = string.Empty;
    public string ZoneId { get; init; } = string.Empty;
    public decimal RequestedCapacityKw { get; init; }
    public string Status { get; init; } = "SUBMITTED";
}

public class InvalidPermitApplicationException : Exception
{
    public InvalidPermitApplicationException(string message) : base(message) { }
}

// Minimal stand-ins for infrastructure the book's snippets reference
// (SqlConnection/Dapper's conn.Execute, SmtpClient) — no external package needed.
public class SqlConnectionStub : IDisposable
{
    public void Open() => Console.WriteLine("  (sql) connection opened");
    public void Execute(string sql, object parameters) => Console.WriteLine($"  (sql) {sql}");
    public void Dispose() { }
}

public class SmtpClientStub
{
    public SmtpClientStub(string host) { }
    public void Send(string from, string to, string subject, string body) => Console.WriteLine($"  (smtp) {from} -> {to}: {subject}");
}

// ===========================================================================
// Section 1 — Single Responsibility Principle
// ===========================================================================

// BEFORE — one class, four responsibilities.
public class PermitProcessorBefore
{
    public bool Validate(PermitApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.ApplicantName)) return false;
        if (application.RequestedCapacityKw <= 0) return false;
        if (application.ZoneId is null) return false;
        return true;
    }

    public string Approve(PermitApplication application) => application.RequestedCapacityKw > 500 ? "PENDING_SUPERVISOR" : "APPROVED";

    public void Save(PermitApplication application, string status)
    {
        using var conn = new SqlConnectionStub();
        conn.Open();
        conn.Execute("INSERT INTO Permits (ApplicantName, ZoneId, Status) VALUES (@Name, @Zone, @Status)",
            new { Name = application.ApplicantName, Zone = application.ZoneId, Status = status });
    }

    public void Notify(PermitApplication application, string status)
    {
        var smtp = new SmtpClientStub("smtp.urbangrid.local");
        smtp.Send("permits@urbangrid.local", application.ApplicantEmail, "Your permit status", $"Your permit has been {status}.");
    }
}

// FIX — one class, one reason to change, each below.
public class PermitValidator
{
    public bool Validate(PermitApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.ApplicantName)) return false;
        if (application.RequestedCapacityKw <= 0) return false;
        if (application.ZoneId is null) return false;
        return true;
    }
}

// Consolidated across the chapter (Section 1 needed only Save; 5.3 added
// GetById/UpdateStatus; Section 7 is the final, consolidated shape below).
public interface IPermitRepository
{
    PermitApplication GetById(Guid id);
    void Save(PermitApplication application, string status, decimal fee);
    void UpdateStatus(Guid id, string status);
}

public class SqlPermitRepository : IPermitRepository
{
    private readonly string _connectionString;
    private readonly Dictionary<Guid, PermitApplication> _simulatedTable = new(); // stands in for the real table

    public SqlPermitRepository(string connectionString) => _connectionString = connectionString;

    public PermitApplication GetById(Guid id) => _simulatedTable.TryGetValue(id, out var p) ? p : throw new KeyNotFoundException(id.ToString());

    public void Save(PermitApplication application, string status, decimal fee)
    {
        using var conn = new SqlConnectionStub();
        conn.Open();
        conn.Execute("INSERT INTO Permits (ApplicantName, ZoneId, Status, FeeAmount) VALUES (@Name, @Zone, @Status, @Fee)",
            new { Name = application.ApplicantName, Zone = application.ZoneId, Status = status, Fee = fee });
        _simulatedTable[application.Id] = application with { Status = status };
    }

    public void UpdateStatus(Guid id, string status)
    {
        if (_simulatedTable.TryGetValue(id, out var existing))
            _simulatedTable[id] = existing with { Status = status };
    }
}

public interface IPermitNotifier { void Notify(PermitApplication application, string status); }

public class SmtpPermitNotifier : IPermitNotifier
{
    public void Notify(PermitApplication application, string status)
    {
        var smtp = new SmtpClientStub("smtp.urbangrid.local");
        smtp.Send("permits@urbangrid.local", application.ApplicantEmail, "Your permit status", $"Your permit has been {status}.");
    }
}

// Concrete decision implementation — pure business logic, no infrastructure.
// (Declared here; implements IPermitApprovalService, defined below in Section 7's block —
// C# does not require interfaces to be declared before their implementers in the same assembly.)
public class PermitApprovalService : IPermitApprovalService
{
    public string Approve(PermitApplication application) => application.RequestedCapacityKw > 500 ? "PENDING_SUPERVISOR" : "APPROVED";
}

// Coordinator — orchestrates, owns no business logic.
public class PermitProcessor
{
    private readonly PermitValidator _validator;
    private readonly PermitApprovalService _approval;
    private readonly IPermitRepository _repository;
    private readonly IPermitNotifier _notifier;

    public PermitProcessor(PermitValidator validator, PermitApprovalService approval, IPermitRepository repository, IPermitNotifier notifier)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _approval = approval ?? throw new ArgumentNullException(nameof(approval));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    public void Process(PermitApplication application)
    {
        if (!_validator.Validate(application))
            throw new InvalidPermitApplicationException("Application failed validation.");

        string status = _approval.Approve(application);
        _repository.Save(application, status, fee: 0m); // fee calculation added in the Section 7 case study
        _notifier.Notify(application, status);
    }
}

// ===========================================================================
// Section 2 — Open/Closed Principle
// ===========================================================================

// BEFORE — every new permit type requires editing this method.
public class PermitFeeCalculatorBefore
{
    public decimal Calculate(PermitApplication application) => application.Type switch
    {
        "Residential" => application.RequestedCapacityKw * 1.50m,
        "Commercial" => application.RequestedCapacityKw * 2.75m,
        "Industrial" => application.RequestedCapacityKw * 4.00m,
        _ => throw new NotSupportedException($"Unknown type: {application.Type}")
    };
}

public interface IPermitFeeCalculator
{
    bool Applies(PermitApplication application);
    decimal Calculate(PermitApplication application);
}

public class ResidentialFeeCalculator : IPermitFeeCalculator
{
    public bool Applies(PermitApplication a) => a.Type == "Residential";
    public decimal Calculate(PermitApplication a) => a.RequestedCapacityKw * 1.50m;
}

public class CommercialFeeCalculator : IPermitFeeCalculator
{
    public bool Applies(PermitApplication a) => a.Type == "Commercial";
    public decimal Calculate(PermitApplication a) => a.RequestedCapacityKw * 2.75m;
}

public class IndustrialFeeCalculator : IPermitFeeCalculator
{
    public bool Applies(PermitApplication a) => a.Type == "Industrial";
    public decimal Calculate(PermitApplication a) => a.RequestedCapacityKw * 4.00m;
}

// Added later with zero changes to any existing file (Section 7 "SOLID Additions").
public class SolarFeeCalculator : IPermitFeeCalculator
{
    public bool Applies(PermitApplication a) => a.Type == "Solar";
    public decimal Calculate(PermitApplication a) => a.RequestedCapacityKw * 3.20m;
}

public interface IPermitFeeEngine { decimal Calculate(PermitApplication application); }

public class PermitFeeEngine : IPermitFeeEngine
{
    private readonly List<IPermitFeeCalculator> _calculators;

    public PermitFeeEngine(IEnumerable<IPermitFeeCalculator> calculators) =>
        _calculators = calculators?.ToList() ?? throw new ArgumentNullException(nameof(calculators));

    public decimal Calculate(PermitApplication application)
    {
        var calc = _calculators.FirstOrDefault(c => c.Applies(application))
            ?? throw new NotSupportedException($"No calculator registered for type: {application.Type}");
        return calc.Calculate(application);
    }
}

// ===========================================================================
// Section 3 — Liskov Substitution Principle
// ===========================================================================

// The violating hierarchy — kept exactly as the book presents it, since the
// Section 7 case study's SetZoneCapacity deliberately still uses it.
public class PowerZone
{
    public virtual double CapacityKw { get; protected set; }
    public virtual double AllocationKw { get; protected set; }

    public virtual void SetCapacity(double capacityKw)
    {
        if (capacityKw <= 0) throw new ArgumentOutOfRangeException(nameof(capacityKw));
        CapacityKw = capacityKw;
    }

    public virtual void Allocate(double kw)
    {
        if (kw > CapacityKw - AllocationKw) throw new InvalidOperationException("Insufficient capacity.");
        AllocationKw += kw;
    }
}

// VIOLATION — subtype silently changes the contract of SetCapacity.
public class PeakLoadZoneViolating : PowerZone
{
    private const double MaxPeakCapacityKw = 200.0;

    public override void SetCapacity(double capacityKw) => CapacityKw = Math.Min(capacityKw, MaxPeakCapacityKw); // caller has no way of knowing this was silently clamped
}

// FIX — different contracts become different abstractions instead of a broken override.
public interface IPowerZone
{
    string ZoneId { get; }
    double CapacityKw { get; }
    void Allocate(double kw);
}

public class StandardPowerZone : IPowerZone
{
    public string ZoneId { get; }
    public double CapacityKw { get; private set; }
    public double Allocated { get; private set; }

    public StandardPowerZone(string zoneId, double capacityKw)
    {
        ZoneId = zoneId;
        CapacityKw = capacityKw > 0 ? capacityKw : throw new ArgumentOutOfRangeException(nameof(capacityKw));
    }

    public void Allocate(double kw)
    {
        if (kw > CapacityKw - Allocated) throw new InvalidOperationException("Insufficient capacity.");
        Allocated += kw;
    }
}

public class PeakLoadZone : IPowerZone
{
    public const double MaxCapacityKw = 200.0;

    public string ZoneId { get; }
    public double CapacityKw { get; } // set once — immutable by design
    public double Allocated { get; private set; }

    public PeakLoadZone(string zoneId, double capacityKw)
    {
        if (capacityKw <= 0 || capacityKw > MaxCapacityKw)
            throw new ArgumentOutOfRangeException(nameof(capacityKw), $"Peak zone capacity must be between 0 and {MaxCapacityKw} kW.");
        ZoneId = zoneId;
        CapacityKw = capacityKw;
    }

    public void Allocate(double kw)
    {
        if (kw > CapacityKw - Allocated) throw new InvalidOperationException("Insufficient peak capacity.");
        Allocated += kw;
    }
}

// ===========================================================================
// Section 4 — Interface Segregation Principle
// ===========================================================================
public record PermitStatisticsReport(int Year, int Month, int Total, int Approved, int Pending);

// BEFORE — a fat interface that no single implementation can honestly fulfil.
public interface IPermitService
{
    PermitApplication GetById(Guid id);
    IEnumerable<PermitApplication> GetByZone(string zoneId);
    void Submit(PermitApplication application);
    void Approve(Guid permitId, string approverName);
    void Reject(Guid permitId, string reason);
    void RequestRevision(Guid permitId, string comments);
    IEnumerable<PermitApplication> GetAllPending();
    PermitStatisticsReport GenerateMonthlyReport(int year, int month);
    void Archive(Guid permitId);
    void PurgeExpired(DateTime olderThan);
}

public class ReadOnlyPermitReportingServiceBad : IPermitService
{
    public IEnumerable<PermitApplication> GetAllPending() => Enumerable.Empty<PermitApplication>();
    public PermitStatisticsReport GenerateMonthlyReport(int y, int m) => new(y, m, 0, 0, 0);

    // Cannot honestly implement these — but forced to by the fat interface.
    public PermitApplication GetById(Guid id) => throw new NotSupportedException();
    public IEnumerable<PermitApplication> GetByZone(string zoneId) => throw new NotSupportedException();
    public void Submit(PermitApplication a) => throw new NotSupportedException();
    public void Approve(Guid id, string approver) => throw new NotSupportedException();
    public void Reject(Guid id, string reason) => throw new NotSupportedException();
    public void RequestRevision(Guid id, string comments) => throw new NotSupportedException();
    public void Archive(Guid id) => throw new NotSupportedException();
    public void PurgeExpired(DateTime olderThan) => throw new NotSupportedException();
}

// FIX — segregated by client.
public interface IPermitQueryService
{
    PermitApplication GetById(Guid id);
    IEnumerable<PermitApplication> GetByZone(string zoneId);
}

public interface IPermitSubmissionService { void Submit(PermitApplication application); }

public interface IPermitLifecycleService
{
    void Approve(Guid permitId, string approverName);
    void Reject(Guid permitId, string reason);
    void RequestRevision(Guid permitId, string comments);
}

public interface IPermitReportingService
{
    IEnumerable<PermitApplication> GetAllPending();
    PermitStatisticsReport GenerateMonthlyReport(int year, int month);
}

public interface IPermitArchiveService
{
    void Archive(Guid permitId);
    void PurgeExpired(DateTime olderThan);
}

public class PermitService : IPermitQueryService, IPermitSubmissionService, IPermitLifecycleService, IPermitReportingService, IPermitArchiveService
{
    private readonly Dictionary<Guid, PermitApplication> _store = new();

    public PermitApplication GetById(Guid id) => _store[id];
    public IEnumerable<PermitApplication> GetByZone(string zoneId) => _store.Values.Where(p => p.ZoneId == zoneId);
    public void Submit(PermitApplication application) => _store[application.Id] = application;
    public void Approve(Guid permitId, string approverName) => _store[permitId] = _store[permitId] with { Status = "APPROVED" };
    public void Reject(Guid permitId, string reason) => _store[permitId] = _store[permitId] with { Status = "REJECTED" };
    public void RequestRevision(Guid permitId, string comments) => _store[permitId] = _store[permitId] with { Status = "REVISION_REQUESTED" };
    public IEnumerable<PermitApplication> GetAllPending() => _store.Values.Where(p => p.Status == "PENDING_SUPERVISOR");
    public PermitStatisticsReport GenerateMonthlyReport(int year, int month) =>
        new(year, month, _store.Count, _store.Values.Count(p => p.Status == "APPROVED"), _store.Values.Count(p => p.Status.StartsWith("PENDING")));
    public void Archive(Guid permitId) => _store.Remove(permitId);
    public void PurgeExpired(DateTime olderThan) { /* no-op for this in-memory demo */ }
}

public class ReadOnlyPermitReportingService : IPermitReportingService // only implements what it can honestly do
{
    public IEnumerable<PermitApplication> GetAllPending() => Enumerable.Empty<PermitApplication>();
    public PermitStatisticsReport GenerateMonthlyReport(int year, int month) => new(year, month, 0, 0, 0);
}

// Lifecycle controller — only sees lifecycle transition methods. The book's
// version inherits ASP.NET Core's ControllerBase; reproduced here as a plain
// class (returning a string instead of IActionResult) so this project has no
// ASP.NET Core dependency — the ISP lesson is unaffected either way.
public class PermitLifecycleController
{
    private readonly IPermitLifecycleService _lifecycleService;
    public PermitLifecycleController(IPermitLifecycleService lifecycleService) => _lifecycleService = lifecycleService;

    public string Approve(Guid permitId, string approver)
    {
        _lifecycleService.Approve(permitId, approver);
        return "200 OK";
    }
}

// ===========================================================================
// Section 5 — Dependency Inversion Principle
// ===========================================================================

// BEFORE — the high-level workflow directly creates its dependencies.
public class PermitApprovalWorkflowBefore
{
    private readonly SqlPermitRepository _repository = new SqlPermitRepository("Server=urbangrid-db;Database=Permits;...");
    private readonly SmtpPermitNotifier _notifier = new SmtpPermitNotifier();

    public void RunApproval(PermitApplication permit, string approver)
    {
        if (permit.RequestedCapacityKw <= 500)
        {
            _repository.UpdateStatus(permit.Id, "APPROVED");
            _notifier.Notify(permit, "APPROVED");
        }
        else
        {
            _repository.UpdateStatus(permit.Id, "PENDING_SUPERVISOR");
            _notifier.Notify(permit, "PENDING_SUPERVISOR");
        }
    }
}

// FIX — depends only on abstractions (two-dependency stage, superseded by the
// four-dependency PermitApprovalWorkflow in the Section 7 case study, below).
public class PermitApprovalWorkflowTwoDeps
{
    private readonly IPermitRepository _repository;
    private readonly IPermitNotifier _notifier;

    public PermitApprovalWorkflowTwoDeps(IPermitRepository repository, IPermitNotifier notifier)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    public void RunApproval(Guid permitId, string approver)
    {
        var permit = _repository.GetById(permitId);
        string status = permit.RequestedCapacityKw <= 500 ? "APPROVED" : "PENDING_SUPERVISOR";
        _repository.UpdateStatus(permitId, status);
        _notifier.Notify(permit, status);
    }
}

public class FakePermitNotifier : IPermitNotifier
{
    public List<(PermitApplication Application, string Status)> Sent { get; } = new();
    public void Notify(PermitApplication application, string status) => Sent.Add((application, status));
}

// ===========================================================================
// Section 6 — All Five Together: IoC as the Unifying Thread (Template Method)
// ===========================================================================
public abstract class PermitApprovalTemplate
{
    // Template method — the fixed algorithm skeleton. Not virtual, so subclasses cannot override it.
    public void Run(PermitApplication application)
    {
        Validate(application);
        string status = Decide(application); // variable step — subclass decides
        Save(application, status);
        Notify(application, status);
    }

    protected abstract string Decide(PermitApplication application);
    private void Validate(PermitApplication a) { /* common validation */ }
    private void Save(PermitApplication a, string status) => Console.WriteLine($"  (save) {a.Id} -> {status}");
    private void Notify(PermitApplication a, string status) => Console.WriteLine($"  (notify) {a.ApplicantEmail} -> {status}");
}

public class ResidentialPermitApproval : PermitApprovalTemplate
{
    protected override string Decide(PermitApplication application) => application.RequestedCapacityKw <= 50 ? "APPROVED" : "PENDING_REVIEW";
}

public class IndustrialPermitApproval : PermitApprovalTemplate
{
    protected override string Decide(PermitApplication application) => application.RequestedCapacityKw <= 500 ? "APPROVED" : "PENDING_SUPERVISOR";
}

// ===========================================================================
// Section 7 — Case Study: UrbanGrid Permit Approval System
// ===========================================================================

// "The Original" — SetZoneCapacity reveals the LSP violation from Section 3.2
// by having to special-case the subtype (still using the violating hierarchy).
public static class LegacyZoneCapacitySetter
{
    public static void SetZoneCapacity(PowerZone zone, double capacityKw)
    {
        if (zone is PeakLoadZoneViolating)
            zone.SetCapacity(Math.Min(capacityKw, 200.0)); // silently applies different rules depending on subtype
        else
            zone.SetCapacity(capacityKw);
    }
}

public record PermitResult(Guid Id, string Status, decimal Fee);

public interface IPermitApprovalService
{
    string Approve(PermitApplication application); // returns "APPROVED" or "PENDING_SUPERVISOR"
}

// The complete, SOLID-compliant orchestrator — supersedes both
// PermitApprovalWorkflowBefore and PermitApprovalWorkflowTwoDeps above.
public class PermitApprovalWorkflow
{
    private readonly IPermitRepository _repository;
    private readonly IPermitFeeEngine _feeEngine;
    private readonly IPermitApprovalService _approvalService;
    private readonly IPermitNotifier _notifier;

    public PermitApprovalWorkflow(IPermitRepository repository, IPermitFeeEngine feeEngine, IPermitApprovalService approvalService, IPermitNotifier notifier)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _feeEngine = feeEngine ?? throw new ArgumentNullException(nameof(feeEngine));
        _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    public PermitResult Process(PermitApplication application)
    {
        decimal fee = _feeEngine.Calculate(application);       // OCP — new types add a calculator, not a switch case
        string status = _approvalService.Approve(application); // SRP — the workflow orchestrates; it does not decide
        _repository.Save(application, status, fee);            // DIP — depends on IPermitRepository
        _notifier.Notify(application, status);                 // DIP — depends on IPermitNotifier
        return new PermitResult(application.Id, status, fee);
    }
}

public class InMemoryPermitRepository : IPermitRepository
{
    private readonly Dictionary<Guid, PermitApplication> _store = new();
    public PermitApplication GetById(Guid id) => _store[id];
    public void Save(PermitApplication application, string status, decimal fee) => _store[application.Id] = application with { Status = status };
    public void UpdateStatus(Guid id, string status) => _store[id] = _store[id] with { Status = status };
}

// The Three SOLID Additions — each is a NEW file; nothing existing changes.
public interface IAuditRepository { void Record(Guid permitId, string status, DateTime at); }

public class ConsoleAuditRepository : IAuditRepository
{
    public void Record(Guid permitId, string status, DateTime at) => Console.WriteLine($"  (audit) {permitId} -> {status} @ {at:O}");
}

// Decorator pattern (covered fully in Chapter 18): wraps any IPermitNotifier
// and adds audit behaviour without changing the inner notifier or the workflow.
public class AuditTrailPermitNotifier : IPermitNotifier
{
    private readonly IPermitNotifier _inner;
    private readonly IAuditRepository _audit;

    public AuditTrailPermitNotifier(IPermitNotifier inner, IAuditRepository audit)
    {
        _inner = inner;
        _audit = audit;
    }

    public void Notify(PermitApplication application, string status)
    {
        _inner.Notify(application, status);                          // existing behaviour
        _audit.Record(application.Id, status, DateTime.UtcNow);       // new behaviour added
    }
}

// SendGridPermitNotifier — a further provider swap (new file, one registration
// line). Reference-only: the real implementation needs the SendGrid SDK, which
// this project deliberately does not reference.
//   public class SendGridPermitNotifier : IPermitNotifier { ... }
