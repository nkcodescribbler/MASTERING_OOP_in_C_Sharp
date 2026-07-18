// Chapter 3 — Access Modifiers & Assembly Boundaries
// Domain model ("UrbanGrid") consolidated from the book's incremental
// snippets. Where the book explicitly marks a "canonical" definition
// (GridAsset in 1B, GridPermit in the Section 6 case study), that version
// is used here as the single source of truth instead of re-declaring every
// intermediate variant.
//
// NOTE: everything below lives in ONE assembly (this project). The book's
// "different assembly" examples (❌ CS0122) can't literally fail to compile
// inside a single project, so those are kept as comments — exactly how the
// book itself presents the inaccessible lines.

namespace OOPBook.Chapter03_AccessModifiersAssemblyBoundaries;

public enum GridAlertLevel { None, Warning, Critical, Offline }
public enum AssetStatus { Active, Offline, UnderMaintenance }
public enum PermitStatus { Pending, Approved, Revoked, Expired, Draft }

/// <summary>
/// Section 1A / 5.1 / 5.6 — canonical GridZone: private fields behind
/// read-only properties, a validated private setter (5.1), and a private
/// nested type whose visibility is scoped entirely to this class (5.6).
/// </summary>
public class GridZone
{
    private readonly string _zoneCode;
    private int _alertCount;
    private bool _isActive = true;
    private double _loadThresholdMw = 100.0;

    // Nested type is invisible outside GridZone — see AlertThresholdRule below (5.6)
    private readonly AlertThresholdRule _rule = AlertThresholdRule.Create(threshold: 5);

    public string ZoneCode => _zoneCode;
    public int AlertCount => _alertCount;
    public bool IsActive => _isActive;

    public double LoadThresholdMw
    {
        get => _loadThresholdMw;
        private set // private set — only this class can call it
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Load threshold must be positive.");
            _loadThresholdMw = value;
        }
    }

    public GridZone(string zoneCode)
    {
        _zoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
    }

    public void RecordAlert()
    {
        _alertCount++;
        if (_rule.IsBreach(_alertCount)) _isActive = false; // business rule enforced here, via the nested rule
    }

    public void Deactivate() => _isActive = false;

    public void UpdateThreshold(double newThreshold)
    {
        LoadThresholdMw = newThreshold; // goes through the private setter — validation runs
    }

    // Section 5.6 — private sealed nested class; invisible and uninstantiable
    // from outside GridZone. The constructor is private too, so even code
    // inside GridZone must go through the internal Create() factory.
    private sealed class AlertThresholdRule
    {
        private readonly int _threshold;
        private AlertThresholdRule(int threshold) => _threshold = threshold;

        // internal (not private): signals this is a domain-internal contract
        // rather than a pure implementation detail, even though C# actually
        // lets the outer class reach private members of its own nested types.
        internal bool IsBreach(int count) => count >= _threshold;

        internal static AlertThresholdRule Create(int threshold) => new AlertThresholdRule(threshold);
    }
}

// External code cannot reach the nested rule type at all:
//   new GridZone.AlertThresholdRule(5); // inaccessible — type is private

/// <summary>
/// Section 1B / 5.2 — canonical GridAsset: the reference base type used
/// throughout the chapter. protected members here are effectively part of
/// this type's public API, for any subclass.
/// </summary>
public abstract class GridAsset
{
    public string AssetId { get; }
    public string ZoneCode { get; }
    public AssetStatus Status { get; protected set; }
    public DateTime InstalledAt { get; }

    protected GridAsset(string assetId, string zoneCode)
    {
        AssetId = assetId ?? throw new ArgumentNullException(nameof(assetId));
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
        Status = AssetStatus.Active;
        InstalledAt = DateTime.UtcNow;
    }

    // Only GridAsset and its derived types call this
    protected void LogMaintenanceEvent(string description)
    {
        Console.WriteLine($"[{AssetId}] Maintenance: {description}");
    }

    public virtual string GetStatus() => $"{AssetId}: {Status}";
}

public class PowerSubstation : GridAsset
{
    public PowerSubstation(string assetId, string zoneCode) : base(assetId, zoneCode) { }

    public void PerformInspection() => LogMaintenanceEvent("Annual inspection complete");

    public void TakeOffline() => Status = AssetStatus.Offline; // protected set — only this hierarchy

    // Section 5.4 — sealed override: the JIT can devirtualise this call,
    // and no further subclass of PowerSubstation can override it again.
    public sealed override string GetStatus() => "Substation Active";
}

// Attempting `sub.Status = AssetStatus.Offline;` from outside the GridAsset
// hierarchy is a compile error — protected set.

/// <summary>Section 1A / 1C — internal implementation detail of the domain assembly.</summary>
internal sealed class ZoneStatusCalculator
{
    internal GridAlertLevel CalculateLevel(double loadMw) =>
        loadMw switch
        {
            > 150.0 => GridAlertLevel.Critical,
            > 120.0 => GridAlertLevel.Warning,
            _ => GridAlertLevel.None
        };
}

/// <summary>Section 3 — another implementation detail that should stay internal, not public.</summary>
internal sealed class GridZoneDiagnostics
{
    internal string Summarise(GridZone zone) => $"{zone.ZoneCode}: alerts={zone.AlertCount}, active={zone.IsActive}";
}

/// <summary>
/// Section 3 — "Common Mistakes" reference types. Intentionally flawed, so
/// the pitfalls the book describes can be shown side-by-side with the fix
/// without silently corrupting the canonical types above.
/// </summary>
public static class CommonMistakes
{
    // Mistake: no protection — any caller writes any value.
    public class GridPermitUnprotected
    {
        public string? PermitId;
        public int ZoneCount;
    }

    // Fix: private fields, constructor validation, read-only properties.
    public class GridPermitValidated
    {
        private readonly string _permitId;
        private readonly int _zoneCount;

        public string PermitId => _permitId;
        public int ZoneCount => _zoneCount;

        public GridPermitValidated(string permitId, int zoneCount)
        {
            if (string.IsNullOrWhiteSpace(permitId))
                throw new ArgumentException("Permit ID cannot be empty.", nameof(permitId));
            if (zoneCount < 1)
                throw new ArgumentOutOfRangeException(nameof(zoneCount));
            _permitId = permitId;
            _zoneCount = zoneCount;
        }
    }

    // Mistake: protected internal — any external derived class (in any
    // assembly) can reach this, which is probably unintended.
    public abstract class GridAssetLoose
    {
        protected internal string InternalDiagnosticCode { get; set; } = string.Empty;
    }

    // Fix: private protected — only derived types within THIS assembly.
    public abstract class GridAssetSecure
    {
        private protected string InternalDiagnosticCode { get; set; } = string.Empty;
    }

    // Mistake: protected mutable field lets a derived class bypass business rules.
    public class GridZoneMutableBase
    {
        protected int AlertCount;
        public void RecordAlert() => AlertCount++;
    }

    public class MonitoredGridZoneBad : GridZoneMutableBase
    {
        public void ForceReset() => AlertCount = 0; // bypasses any invariant RecordAlert() enforces
    }

    // The fix for this exact problem is the canonical GridZone above:
    // private field + protected read-only gateway + validated setter.
}

// ===========================================================================
// Section 6 — Case Study: UrbanGrid Multi-Layer Visibility Design
// Public surface:   GridPermit, IPermitRepository, PermitStatus
// Internal surface: PermitValidator, PermitApprovalService
// ===========================================================================

public interface IPermitRepository
{
    GridPermit? FindById(string permitId);
    void Save(GridPermit permit);
}

public sealed class GridPermit
{
    public string PermitId { get; }
    public string ZoneCode { get; }
    public PermitStatus Status { get; private set; } // domain controls status
    public DateTime IssuedAt { get; }
    public int ZoneCount { get; private set; }

    internal GridPermit(string permitId, string zoneCode, int zoneCount) // internal ctor — only domain code creates permits
    {
        PermitId = permitId ?? throw new ArgumentNullException(nameof(permitId));
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
        ZoneCount = zoneCount > 0 ? zoneCount : throw new ArgumentOutOfRangeException(nameof(zoneCount));
        Status = PermitStatus.Pending;
        IssuedAt = DateTime.UtcNow;
    }

    internal void Approve() // internal — only PermitApprovalService calls this
    {
        if (Status != PermitStatus.Pending)
            throw new InvalidOperationException($"Cannot approve permit {PermitId} — current status: {Status}.");
        Status = PermitStatus.Approved;
    }

    public void Revoke() // public — controllers/callers may trigger a revoke
    {
        if (Status == PermitStatus.Revoked) return;
        Status = PermitStatus.Revoked;
    }
}

// public class ExtendedPermit : GridPermit { }
// CS0509: cannot derive from sealed type 'GridPermit'

internal sealed class PermitValidator
{
    internal bool IsValid(string permitId, string zoneCode, int zoneCount) =>
        !string.IsNullOrWhiteSpace(permitId) && !string.IsNullOrWhiteSpace(zoneCode) && zoneCount >= 1;
}

internal sealed class PermitApprovalService
{
    private readonly IPermitRepository _repository;
    private readonly PermitValidator _validator;

    internal PermitApprovalService(IPermitRepository repository, PermitValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

    internal GridPermit CreateAndApprove(string permitId, string zoneCode, int zoneCount)
    {
        if (!_validator.IsValid(permitId, zoneCode, zoneCount))
            throw new ArgumentException("Invalid permit parameters.");
        var permit = new GridPermit(permitId, zoneCode, zoneCount); // internal ctor
        permit.Approve();                                           // internal method
        _repository.Save(permit);
        return permit;
    }
}

/// <summary>
/// UrbanGrid.Infrastructure equivalent — references only the public surface
/// (GridPermit, IPermitRepository, PermitStatus). Renamed from the book's
/// "SqlPermitRepository" to make clear this in-memory version needs no
/// external database package to run.
/// </summary>
public class InMemoryPermitRepository : IPermitRepository
{
    private readonly Dictionary<string, GridPermit> _store = new();

    public GridPermit? FindById(string permitId) => _store.TryGetValue(permitId, out var p) ? p : null;

    public void Save(GridPermit permit) => _store[permit.PermitId] = permit;
}

/// <summary>
/// UrbanGrid.Api equivalent. The book shows this as an ASP.NET Core
/// [ApiController] — reproduced here as a plain class so the project has no
/// dependency on the ASP.NET Core SDK. The visibility rules it demonstrates
/// (public properties/methods only; internal members are inaccessible) are
/// identical either way.
/// </summary>
public class PermitApiLayer
{
    private readonly IPermitRepository _permits;

    public PermitApiLayer(IPermitRepository permits) => _permits = permits;

    public string Get(string id)
    {
        var permit = _permits.FindById(id);
        return permit is null
            ? "404 Not Found"
            : $"{permit.PermitId} / {permit.ZoneCode} / {permit.Status}";
    }

    public string Revoke(string id)
    {
        var permit = _permits.FindById(id);
        if (permit is null) return "404 Not Found";
        permit.Revoke();     // public method — allowed
        _permits.Save(permit);
        return "200 OK";
        // BLOCKED at compile time from this layer:
        //   permit.Approve()                — internal
        //   new GridPermit(...)             — internal constructor
        //   new PermitApprovalService(...)  — internal class
    }
}
