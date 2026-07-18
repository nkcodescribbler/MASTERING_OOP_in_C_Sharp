namespace OOPBook.Chapter17_DomainModellingWithOOP;

// ============================================================================
// Section 3 — Common Mistakes
// ============================================================================

// Mistake 1: value object given an identity and made mutable.
public class GeoCoordinateMutable
{
    public int Id { get; set; }            // value objects have no ID
    public double Latitude { get; set; }   // mutable — values should not change
    public double Longitude { get; set; }
}

// Fix: value object as an immutable readonly record struct with structural equality.
public readonly record struct GeoCoordinate(double Latitude, double Longitude)
{
    public static GeoCoordinate Of(double lat, double lon)
    {
        if (lat < -90 || lat > 90)
            throw new ArgumentOutOfRangeException(nameof(lat), "Latitude must be between -90 and 90.");
        if (lon < -180 || lon > 180)
            throw new ArgumentOutOfRangeException(nameof(lon), "Longitude must be between -180 and 180.");
        return new GeoCoordinate(lat, lon);
    }

    public override string ToString() => $"({Latitude:F4}°, {Longitude:F4}°)";
}

// Fix: strongly-typed value objects instead of bare strings — wrong argument order becomes a compile error.
public readonly record struct PermitId(string Value)
{
    public static PermitId Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Permit ID cannot be empty.", nameof(value));
        return new PermitId(value.Trim().ToUpperInvariant());
    }

    public override string ToString() => Value;
}

public readonly record struct ZoneCode(string Value)
{
    public static ZoneCode Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Zone code cannot be empty.", nameof(value));
        return new ZoneCode(value.Trim().ToUpperInvariant());
    }

    public override string ToString() => Value;
}

// Mistake: anemic domain model — the "entity" is just a data bag...
public class GridPermitAnemic
{
    public string PermitId { get; set; } = "";
    public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"
    public DateTime ExpiryDate { get; set; }
}

// ...and all business logic — and the invariant it should protect — lives in a service instead.
public class PermitServiceAnemic
{
    public void Approve(GridPermitAnemic permit, string operatorId)
    {
        if (permit.Status != "Pending") return; // rule scattered in the service
        permit.Status = "Approved";              // invariant bypassed from outside; nothing stops
                                                   // any other code from writing permit.Status directly
    }
}

// Fix, first pass: a rich domain model — behaviour lives with the data. This is the version shown
// early in Section 3, before Section 5.2 evolves the audit log into a proper GridPermitAuditEntry list.
// Renamed GridPermitRichV1 here only so the canonical GridPermit (below) has no name collision.
public class GridPermitRichV1
{
    private PermitStatus _status;
    private readonly List<string> _auditLog = new();

    public PermitId Id { get; private init; }
    public ZoneCode ZoneCode { get; private init; }
    public DateTime ExpiryDate { get; private init; }
    public PermitStatus Status => _status;
    public IReadOnlyList<string> AuditLog => _auditLog;

    private GridPermitRichV1() { }

    public static GridPermitRichV1 Issue(PermitId id, ZoneCode zoneCode, DateTime expiryDate)
    {
        if (expiryDate <= DateTime.UtcNow)
            throw new InvalidOperationException("Expiry date must be in the future.");
        var permit = new GridPermitRichV1 { Id = id, ZoneCode = zoneCode, ExpiryDate = expiryDate };
        permit._status = PermitStatus.Pending;
        permit._auditLog.Add("Issued");
        return permit;
    }

    public void Approve(string operatorId)
    {
        if (_status != PermitStatus.Pending)
            throw new InvalidOperationException($"Cannot approve with status '{_status}'.");
        if (ExpiryDate <= DateTime.UtcNow) // guard before any state mutation
            throw new InvalidOperationException("Cannot approve an expired permit.");
        _status = PermitStatus.Approved;
        _auditLog.Add($"Approved by {operatorId}");
    }

    public void Reject(string operatorId, string reason)
    {
        if (_status != PermitStatus.Pending)
            throw new InvalidOperationException($"Cannot reject with status '{_status}'.");
        _status = PermitStatus.Rejected;
        _auditLog.Add($"Rejected by {operatorId}: {reason}");
    }
}

// Mistake: one mega-aggregate — a zone that owns everything, so loading it means joining many tables.
// GridSensorStub/GridOperatorStub are minimal placeholders that exist only to make this anti-pattern compile.
public class GridSensorStub
{
    public string Id { get; init; } = "";
}

public class GridOperatorStub
{
    public string Id { get; init; } = "";
}

public class GridZoneMegaAggregate
{
    public List<GridPermitRichV1> Permits { get; } = new();
    public List<GridSensorStub> Sensors { get; } = new();
    public List<GridOperatorStub> Operators { get; } = new();
}

// ============================================================================
// Section 5.1 / 5.2 / 5.3 — Entities, Value Objects, Aggregates & Domain Events
// (Section 6's case study is the fullest version of GridPermit/GridZone, so it
// is used here as the single canonical definition of each.)
// ============================================================================

public enum PermitStatus { Pending, Approved, Rejected, Expired }

// A record class is used here (not a struct) because SensorReading carries a string reference field
// and is compared/passed around more like a lightweight entity snapshot than a tiny value.
public record class SensorReading(string SensorId, double ValueMw, DateTime RecordedAt)
{
    public bool IsAboveThreshold(double thresholdMw) => ValueMw > thresholdMw;
}

// ─── Domain Events ──────────────────────────────────────────────────────────
public record class PermitApproved(PermitId PermitId, ZoneCode ZoneCode, string ApprovedByOperatorId, DateTime ApprovedAt);
public record class PermitRejected(PermitId PermitId, ZoneCode ZoneCode, string Reason, DateTime RejectedAt);
public record class ZoneActivated(ZoneCode ZoneCode, PermitId ActivatingPermitId, DateTime ActivatedAt);
public record class ZoneDeactivated(ZoneCode ZoneCode, DateTime DeactivatedAt);
public record class GridZoneAlert(ZoneCode ZoneCode, string Reason, DateTime OccurredAt);

// ─── Aggregate: GridPermit ───────────────────────────────────────────────────
public class GridPermitAuditEntry
{
    public Guid EntryId { get; }
    public PermitId PermitId { get; }
    public string Action { get; }
    public string? OperatorId { get; }
    public DateTime OccurredAt { get; }

    public GridPermitAuditEntry(PermitId permitId, string action, string? operatorId, DateTime occurredAt)
    {
        EntryId = Guid.NewGuid();
        PermitId = permitId;
        Action = action;
        OperatorId = operatorId;
        OccurredAt = occurredAt;
    }
}

// Aggregate root. Invariant: every status change must produce an audit entry, and domain events
// are collected internally so the application layer can dispatch them only after a successful save.
public class GridPermit
{
    private PermitStatus _status;
    private readonly List<GridPermitAuditEntry> _auditLog = new();
    private readonly List<object> _domainEvents = new();

    public PermitId Id { get; private init; }
    public ZoneCode ZoneCode { get; private init; }
    public DateTime ExpiryDate { get; private init; }
    public PermitStatus Status => _status;
    public IReadOnlyList<GridPermitAuditEntry> AuditLog => _auditLog;
    public IReadOnlyList<object> DomainEvents => _domainEvents;

    private GridPermit() { }

    // Rule 1: mandatory fields, expiry must be in the future.
    public static GridPermit Issue(PermitId id, ZoneCode zoneCode, DateTime expiryDate)
    {
        if (expiryDate <= DateTime.UtcNow)
            throw new InvalidOperationException("Cannot issue a permit with a past expiry date.");
        var permit = new GridPermit { Id = id, ZoneCode = zoneCode, ExpiryDate = expiryDate };
        permit._status = PermitStatus.Pending;
        permit._auditLog.Add(new GridPermitAuditEntry(id, "Issued", null, DateTime.UtcNow));
        return permit;
    }

    // Rules 2, 4, 5: only Pending can be approved; expiry is re-checked before mutation.
    public void Approve(string operatorId)
    {
        if (_status != PermitStatus.Pending)
            throw new InvalidOperationException($"Permit '{Id}' cannot be approved. Status: '{_status}'.");
        if (ExpiryDate <= DateTime.UtcNow)
            throw new InvalidOperationException($"Permit '{Id}' has expired.");
        _status = PermitStatus.Approved;
        _auditLog.Add(new GridPermitAuditEntry(Id, "Approved", operatorId, DateTime.UtcNow));
        _domainEvents.Add(new PermitApproved(Id, ZoneCode, operatorId, DateTime.UtcNow));
    }

    public void Reject(string operatorId, string reason)
    {
        if (_status != PermitStatus.Pending)
            throw new InvalidOperationException($"Permit '{Id}' cannot be rejected. Status: '{_status}'.");
        _status = PermitStatus.Rejected;
        _auditLog.Add(new GridPermitAuditEntry(Id, $"Rejected: {reason}", operatorId, DateTime.UtcNow));
        _domainEvents.Add(new PermitRejected(Id, ZoneCode, reason, DateTime.UtcNow));
    }

    // The domain owns the concept of "active"; a repository query would be the wrong layer for this.
    public bool IsValidForZoneActivation() => _status == PermitStatus.Approved && ExpiryDate > DateTime.UtcNow;

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ─── Entity: GridZone (a separate aggregate) ─────────────────────────────────
public class GridZone
{
    private bool _isActive;
    private int _alertCount;
    private readonly List<object> _domainEvents = new();

    public ZoneCode ZoneCode { get; private init; }
    public bool IsActive => _isActive;
    public int AlertCount => _alertCount;
    public IReadOnlyList<object> DomainEvents => _domainEvents;

    private GridZone() { }

    public static GridZone Create(ZoneCode zoneCode)
    {
        var zone = new GridZone { ZoneCode = zoneCode };
        zone._isActive = false; // Rule 6: a zone starts inactive until a valid permit activates it
        zone._alertCount = 0;
        return zone;
    }

    public void RecordAlert(string reason)
    {
        if (!_isActive)
            throw new InvalidOperationException($"Cannot record alert on inactive zone '{ZoneCode}'.");
        _alertCount++;
        _domainEvents.Add(new GridZoneAlert(ZoneCode, reason, DateTime.UtcNow));
    }

    // Rule 6: a zone activates only via an approved, non-expired permit issued for that same zone.
    public void Activate(GridPermit permit)
    {
        if (permit.ZoneCode != ZoneCode)
            throw new InvalidOperationException($"Permit '{permit.Id}' is for zone '{permit.ZoneCode}'.");
        if (!permit.IsValidForZoneActivation())
            throw new InvalidOperationException(
                $"Zone '{ZoneCode}' cannot be activated: permit '{permit.Id}' not approved or expired.");
        _isActive = true;
        _domainEvents.Add(new ZoneActivated(ZoneCode, permit.Id, DateTime.UtcNow));
    }

    public void Deactivate()
    {
        _isActive = false;
        _alertCount = 0;
        _domainEvents.Add(new ZoneDeactivated(ZoneCode, DateTime.UtcNow));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ─── Repositories (domain-layer interfaces) ─────────────────────────────────
public interface IGridPermitRepository
{
    Task<GridPermit?> GetByIdAsync(PermitId permitId);
    Task<IReadOnlyList<GridPermit>> GetByZoneAsync(ZoneCode zoneCode);
    Task SaveAsync(GridPermit permit);
}

public interface IGridZoneRepository
{
    Task<GridZone?> GetByCodeAsync(ZoneCode zoneCode);
    Task SaveAsync(GridZone zone);
}

// The book's infrastructure-layer example implements these with EF Core (EfGridPermitRepository,
// backed by GridDbContext). That's a heavy external dependency for a standalone console sample, so
// this project substitutes lightweight in-memory implementations behind the same interfaces — the
// point being made (repository = contract that hands back a fully valid aggregate) still holds.
public class InMemoryGridPermitRepository : IGridPermitRepository
{
    private readonly Dictionary<string, GridPermit> _store = new();

    public Task<GridPermit?> GetByIdAsync(PermitId permitId) =>
        Task.FromResult(_store.TryGetValue(permitId.Value, out var permit) ? permit : null);

    public Task<IReadOnlyList<GridPermit>> GetByZoneAsync(ZoneCode zoneCode) =>
        Task.FromResult((IReadOnlyList<GridPermit>)_store.Values.Where(p => p.ZoneCode == zoneCode).ToList());

    public Task SaveAsync(GridPermit permit)
    {
        _store[permit.Id.Value] = permit;
        return Task.CompletedTask;
    }
}

public class InMemoryGridZoneRepository : IGridZoneRepository
{
    private readonly Dictionary<string, GridZone> _store = new();

    public Task<GridZone?> GetByCodeAsync(ZoneCode zoneCode) =>
        Task.FromResult(_store.TryGetValue(zoneCode.Value, out var zone) ? zone : null);

    public Task SaveAsync(GridZone zone)
    {
        _store[zone.ZoneCode.Value] = zone;
        return Task.CompletedTask;
    }
}

// ─── Domain Events: dispatcher ───────────────────────────────────────────────
public interface IDomainEventDispatcher
{
    Task DispatchAsync(object domainEvent);
}

public class ConsoleDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(object domainEvent)
    {
        Console.WriteLine($"  [EVENT] {domainEvent.GetType().Name}: {domainEvent}");
        return Task.CompletedTask;
    }
}

// ─── Domain Service ──────────────────────────────────────────────────────────
public class ApprovalResult
{
    public bool Succeeded { get; private init; }
    public string? Error { get; private init; }

    public static ApprovalResult Ok() => new() { Succeeded = true };
    public static ApprovalResult Fail(string error) => new() { Succeeded = false, Error = error };
}

// Stateless; coordinates across the GridPermit and GridZone aggregates without either one knowing
// about the other. This is where cross-aggregate rules belong — not inside either aggregate itself.
public class GridPermitApprovalService
{
    private readonly IGridPermitRepository _permits;
    private readonly IGridZoneRepository _zones;
    private readonly IDomainEventDispatcher _dispatcher;

    public GridPermitApprovalService(IGridPermitRepository permits, IGridZoneRepository zones, IDomainEventDispatcher dispatcher)
    {
        _permits = permits;
        _zones = zones;
        _dispatcher = dispatcher;
    }

    public async Task<ApprovalResult> ApproveAsync(PermitId permitId, string operatorId)
    {
        var permit = await _permits.GetByIdAsync(permitId);
        if (permit is null) return ApprovalResult.Fail($"Permit '{permitId}' not found.");

        var zone = await _zones.GetByCodeAsync(permit.ZoneCode);
        if (zone is null) return ApprovalResult.Fail($"Zone '{permit.ZoneCode}' not found.");

        // Rule 3: cannot approve a permit for a zone with active alerts — a cross-aggregate rule,
        // which is exactly why it lives here in the domain service rather than in either aggregate.
        if (zone.AlertCount > 0)
            return ApprovalResult.Fail($"Zone '{permit.ZoneCode}' has {zone.AlertCount} active alerts.");

        permit.Approve(operatorId); // delegate the actual state change to the aggregate
        await _permits.SaveAsync(permit);
        foreach (var evt in permit.DomainEvents)
            await _dispatcher.DispatchAsync(evt); // dispatch only AFTER a successful save
        permit.ClearDomainEvents();
        return ApprovalResult.Ok();
    }
}
