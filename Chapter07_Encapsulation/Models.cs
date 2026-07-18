// Chapter 7 — Encapsulation
// GridZone and ZonePermit both start as anemic data bags (Sections 2/3) and
// are rebuilt as properly encapsulated domain objects (Sections 5 and 6).
// Both shapes are kept — under distinct names — so the "before" and "after"
// can run side by side, the way the book presents them.

namespace OOPBook.Chapter07_Encapsulation;

public enum ZoneStatus { Offline, Active, Inactive, Overloaded, Faulted, Decommissioned }
public enum AlertSeverity { Info, Warning, Critical }
public enum PermitStatus { Pending, Approved, Suspended, Cancelled }
public enum OperatorClearance { PermitApprover }

// Section 5.4 — Level 4 immutability: a record gives structural immutability, value
// equality, and non-destructive mutation via 'with' for free.
public record PermitId(string Prefix, int Sequence, int Year);

public interface IAlertService { void Raise(AlertSeverity severity, string message); }

public class ConsoleAlertService : IAlertService
{
    public void Raise(AlertSeverity severity, string message) => Console.WriteLine($"[ALERT:{severity}] {message}");
}

public interface IEmailService { void Send(string to, string body); }

public class ConsoleEmailService : IEmailService
{
    public void Send(string to, string body) => Console.WriteLine($"[EMAIL to {to}] {body}");
}

public class ContactInfo
{
    public string? Email { get; init; }
}

public class GridOperator
{
    public string OperatorId { get; }
    public string Name { get; }
    public ContactInfo? ContactInfo { get; init; }
    private readonly HashSet<OperatorClearance> _clearances;

    public GridOperator(string operatorId, string name, params OperatorClearance[] clearances)
    {
        OperatorId = operatorId;
        Name = name;
        _clearances = clearances.ToHashSet();
    }

    public bool HasClearance(OperatorClearance clearance) => _clearances.Contains(clearance);
}

// ----- Section 2/3 — the anemic model (rules scattered, everything settable)
public class GridZoneAnemic
{
    public string ZoneId { get; set; } = string.Empty;
    public double CurrentLoadMW { get; set; }   // anyone sets any value
    public double CapacityMW { get; set; }      // no validation
    public ZoneStatus Status { get; set; }      // any caller changes status
    public DateTime LastUpdated { get; set; }
}

public class ZoneManagementService
{
    public void UpdateLoad(GridZoneAnemic zone, double newLoad, IAlertService alerts)
    {
        if (newLoad < 0)
            throw new ArgumentException("Load cannot be negative.");

        zone.CurrentLoadMW = newLoad;
        zone.LastUpdated = DateTime.UtcNow;

        if (zone.CurrentLoadMW > zone.CapacityMW)
        {
            zone.Status = ZoneStatus.Overloaded;
            alerts.Raise(AlertSeverity.Critical, $"Zone {zone.ZoneId} overloaded: {zone.CurrentLoadMW:F1}/{zone.CapacityMW:F1} MW");
        }
        else
        {
            zone.Status = zone.CurrentLoadMW > 0 ? ZoneStatus.Active : ZoneStatus.Offline;
        }
    }
}

// Section 5.2 "Ask" anti-pattern operating on the anemic zone directly.
public class GridMonitorAsking
{
    public void Check(GridZoneAnemic zone, IAlertService alerts)
    {
        if (zone.CurrentLoadMW > zone.CapacityMW)
        {
            zone.Status = ZoneStatus.Overloaded; // caller decides what "overloaded" means
            alerts.Raise(AlertSeverity.Critical, $"Zone {zone.ZoneId} overloaded: {zone.CurrentLoadMW:F1}/{zone.CapacityMW:F1} MW");
        }
    }
}

// ----- Mistake 3 — returning a mutable internal collection ------------------
public class GridAlert
{
    public string Message { get; init; } = string.Empty;
}

public class ZoneAlertBoard
{
    private const int MaxAlerts = 100;
    private readonly List<GridAlert> _alerts = new();
    public IReadOnlyList<GridAlert> Alerts => _alerts.AsReadOnly(); // read-only view

    public void AddAlert(GridAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        if (_alerts.Count >= MaxAlerts)
            throw new InvalidOperationException($"Reached the maximum of {MaxAlerts} active alerts.");
        _alerts.Add(alert);
    }
}

// ===========================================================================
// Section 5 — the encapsulated GridZone: guard clauses, Tell Don't Ask
// ===========================================================================
public class GridZone
{
    public string ZoneId { get; }
    public double CapacityMW { get; }
    public string Region { get; }
    public GridOperator? AssignedOperator { get; private set; }

    private double _currentLoadMW;
    private ZoneStatus _status = ZoneStatus.Offline;

    public double CurrentLoadMW => _currentLoadMW;
    public ZoneStatus Status => _status;

    public GridZone(string zoneId, double capacityMW, string region)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityMW);
        ZoneId = zoneId;
        CapacityMW = capacityMW;
        Region = region;
    }

    public void AssignOperator(GridOperator gridOperator) => AssignedOperator = gridOperator;

    // Section 5.1 — sets a new load, then evaluates status/alerts in one call.
    public void UpdateLoad(double newLoadMW, IAlertService alertService)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newLoadMW);
        ArgumentNullException.ThrowIfNull(alertService);

        if (newLoadMW > CapacityMW * 1.2)
            throw new InvalidOperationException($"Load {newLoadMW:F1} MW exceeds emergency capacity for zone {ZoneId}.");

        _currentLoadMW = newLoadMW;
        UpdateStatusAndAlert(alertService);
    }

    // Private helper — internal rule, callers have no need to see it
    private void UpdateStatusAndAlert(IAlertService alertService)
    {
        var previous = _status;
        _status = _currentLoadMW switch
        {
            0 => ZoneStatus.Offline,
            var l when l > CapacityMW => ZoneStatus.Overloaded,
            _ => ZoneStatus.Active
        };

        if (_status == ZoneStatus.Overloaded && previous != ZoneStatus.Overloaded)
            alertService.Raise(AlertSeverity.Critical, $"Zone {ZoneId} overloaded: {_currentLoadMW:F1}/{CapacityMW:F1} MW");
    }

    // Section 5.2 — Tell Don't Ask: re-evaluates status from the current load
    // without changing it (used when load arrived via another path).
    public void EvaluateLoad(IAlertService alertService)
    {
        if (_currentLoadMW <= CapacityMW)
        {
            _status = _currentLoadMW > 0 ? ZoneStatus.Active : ZoneStatus.Offline;
            return;
        }

        var wasAlreadyOverloaded = _status == ZoneStatus.Overloaded;
        _status = ZoneStatus.Overloaded;
        if (!wasAlreadyOverloaded) // raise only on transition, not on every call
            alertService.Raise(AlertSeverity.Critical, $"Zone {ZoneId} overloaded: {_currentLoadMW:F1}/{CapacityMW:F1} MW");
    }
}

// Section 5.2 — the "Tell" version: caller triggers behaviour, no domain knowledge required.
public class GridMonitorTelling
{
    public void Check(GridZone zone, IAlertService alerts) => zone.EvaluateLoad(alerts);
}

// ----- Section 5.3 — Law of Demeter: anemic (bad) vs encapsulated (good) ----
public class ZonePermitAnemic
{
    public string PermitNumber { get; init; } = string.Empty;
    public GridZone? Zone { get; init; } // exposes the whole graph — LoD violation waiting to happen
}

public class PermitApprovalServiceBad
{
    private readonly IEmailService _emailService;
    public PermitApprovalServiceBad(IEmailService emailService) => _emailService = emailService;

    public void NotifyApproval(ZonePermitAnemic permit)
    {
        // Three dots — navigating through Zone -> AssignedOperator -> ContactInfo
        string? email = permit.Zone?.AssignedOperator?.ContactInfo?.Email;
        if (email is not null)
            _emailService.Send(email, $"Permit {permit.PermitNumber} has been approved.");
    }
}

// ===========================================================================
// Section 6 — Case Study: the fully encapsulated ZonePermit
// ===========================================================================
public readonly record struct AuditEntry(string Action, string ActorId, DateTime Timestamp);

public sealed class ZonePermit
{
    // Private state — never accessible directly from outside
    private PermitStatus _status;
    private DateTime _expiresAt;
    private readonly List<AuditEntry> _auditLog = new();
    private GridZone? _zone;

    // Private constructor — callers must use the factory method
    private ZonePermit() { }

    public string PermitNumber { get; private init; } = string.Empty;
    public string ZoneId { get; private init; } = string.Empty;
    public string IssuingOperatorId { get; private init; } = string.Empty;
    public DateTime ExpiresAt => _expiresAt;
    public PermitStatus Status => _status;
    public bool IsExpired => _expiresAt <= DateTime.UtcNow;
    public bool IsActive => _status == PermitStatus.Approved && !IsExpired;
    public IReadOnlyList<AuditEntry> AuditLog => _auditLog.AsReadOnly();

    // Factory method (Chapter 4 pattern) — enforces creation invariants
    public static ZonePermit Issue(string permitNumber, string zoneId, string issuingOperatorId, DateTime expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permitNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuingOperatorId);
        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Permit must expire in the future.", nameof(expiresAt));

        var permit = new ZonePermit
        {
            PermitNumber = permitNumber,
            ZoneId = zoneId,
            IssuingOperatorId = issuingOperatorId
        };
        permit._expiresAt = expiresAt;
        permit._status = PermitStatus.Pending;
        permit.Record("Permit issued", issuingOperatorId);
        return permit;
    }

    public void Approve(GridOperator approvingOperator)
    {
        ArgumentNullException.ThrowIfNull(approvingOperator);
        if (!approvingOperator.HasClearance(OperatorClearance.PermitApprover))
            throw new UnauthorizedAccessException($"Operator {approvingOperator.Name} lacks PermitApprover clearance.");
        if (_status != PermitStatus.Pending)
            throw new InvalidOperationException($"Cannot approve permit {PermitNumber}: only Pending permits can be approved. Current: {_status}.");
        if (IsExpired)
            throw new InvalidOperationException($"Cannot approve permit {PermitNumber}: permit has expired.");

        _status = PermitStatus.Approved;
        Record("Approved", approvingOperator.OperatorId);
    }

    public void Suspend(string reason, string operatorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        if (_status != PermitStatus.Approved)
            throw new InvalidOperationException($"Cannot suspend permit {PermitNumber}: only Approved permits can be suspended.");

        _status = PermitStatus.Suspended;
        Record($"Suspended — {reason}", operatorId);
    }

    public void Cancel(string reason, string operatorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        if (_status == PermitStatus.Approved)
            throw new InvalidOperationException($"Cannot cancel an Approved permit directly. Suspend permit {PermitNumber} first.");
        if (_status == PermitStatus.Cancelled)
            throw new InvalidOperationException($"Permit {PermitNumber} is already cancelled.");

        _status = PermitStatus.Cancelled;
        Record($"Cancelled — {reason}", operatorId);
    }

    public void Extend(TimeSpan duration, string operatorId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration.Ticks, nameof(duration));
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        if (_status == PermitStatus.Cancelled)
            throw new InvalidOperationException("Cannot extend a Cancelled permit. Issue a new permit.");
        if (IsExpired)
            throw new InvalidOperationException($"Cannot extend expired permit {PermitNumber}. Issue a new permit.");

        _expiresAt = _expiresAt.Add(duration);
        Record($"Extended by {duration.TotalDays:F0} days", operatorId);
    }

    // Zone attachment — called after Issue() once the zone reference is available.
    public void AttachZone(GridZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (_zone is not null)
            throw new InvalidOperationException($"Permit {PermitNumber} already has a zone attached.");
        _zone = zone;
        Record($"Zone {zone.ZoneId} attached", IssuingOperatorId);
    }

    // LoD-respecting helpers — encapsulate navigation so callers never traverse the graph
    public string GetIssuingZoneRegion() =>
        _zone?.Region ?? throw new InvalidOperationException($"No zone attached to permit {PermitNumber}. Call AttachZone() after Issue().");

    public string GetOperatorNotificationEmail()
    {
        if (_zone?.AssignedOperator?.ContactInfo?.Email is not { Length: > 0 } email)
            throw new InvalidOperationException(
                $"No notification email available for permit {PermitNumber}. Ensure the zone has an assigned operator with a contact email.");
        return email;
    }

    private void Record(string action, string actorId) => _auditLog.Add(new AuditEntry(action, actorId, DateTime.UtcNow));
}

public class PermitApprovalServiceGood
{
    private readonly IEmailService _emailService;
    public PermitApprovalServiceGood(IEmailService emailService) => _emailService = emailService;

    public void NotifyApproval(ZonePermit permit)
    {
        string email = permit.GetOperatorNotificationEmail(); // one dot — direct collaborator
        _emailService.Send(email, $"Permit {permit.PermitNumber} has been approved.");
    }
}
