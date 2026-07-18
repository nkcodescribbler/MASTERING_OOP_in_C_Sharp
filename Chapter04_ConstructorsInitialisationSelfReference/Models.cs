// Chapter 4 — Constructors, Object Initialisation & Self-Reference
// Each subsection of the book introduces its own small type so the specific
// constructor technique being taught stays isolated and unambiguous. The
// Section 5 case study (near the bottom) is the chapter's culminating,
// fully worked example and is used as-is, without renaming.

namespace OOPBook.Chapter04_ConstructorsInitialisationSelfReference;

// ----- 1A: parameterised constructor guarantees validity -------------------
public class GridZone
{
    public string ZoneCode { get; }
    public bool IsActive { get; private set; }
    public int AlertCount { get; private set; }

    public GridZone(string zoneCode)
    {
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("Zone code cannot be empty.", nameof(zoneCode));

        ZoneCode = zoneCode;
        IsActive = true;
        AlertCount = 0;
    }
}

// ----- 1B: the four constructor types ---------------------------------------
public class GridDiagnostics
{
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    // No constructor declared -> compiler generates: public GridDiagnostics() { }
}

public class GridPermit1B
{
    public string PermitId { get; }
    public string ZoneCode { get; }
    public int MaxLoad { get; }

    public GridPermit1B(string permitId, string zoneCode, int maxLoad)
    {
        if (string.IsNullOrWhiteSpace(permitId))
            throw new ArgumentException("Permit ID required.", nameof(permitId));
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("Zone code required.", nameof(zoneCode));
        if (maxLoad <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLoad), "Max load must be positive.");

        PermitId = permitId;
        ZoneCode = zoneCode;
        MaxLoad = maxLoad;
    }

    // Copy constructor — creates an independent clone
    public GridPermit1B(GridPermit1B source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        PermitId = source.PermitId;
        ZoneCode = source.ZoneCode;
        MaxLoad = source.MaxLoad;
    }
}

public class ZoneRegistry
{
    // Static constructor — runs once, before any instance is created
    public static readonly Dictionary<string, GridZone> KnownZones;

    static ZoneRegistry()
    {
        KnownZones = new Dictionary<string, GridZone>
        {
            ["North-7"] = new GridZone("North-7"),
            ["South-3"] = new GridZone("South-3"),
            ["Central-1"] = new GridZone("Central-1"),
        };
    }
}

// ----- Section 3 mistake 1: two-step initialisation, before and after ------
public class GridZoneTwoStep
{
    public string? ZoneCode { get; set; } // settable — caller can forget to set it
    public bool IsActive { get; set; }
}

public class GridZoneSingleStep
{
    public string ZoneCode { get; }
    public bool IsActive { get; private set; }

    public GridZoneSingleStep(string zoneCode)
    {
        ZoneCode = !string.IsNullOrWhiteSpace(zoneCode)
            ? zoneCode
            : throw new ArgumentException("Zone code required.", nameof(zoneCode));
        IsActive = true;
    }
}

// ----- Section 3 mistake 2: virtual method call in a constructor -----------
public class GridAssetValidating
{
    public GridAssetValidating(string assetId)
    {
        Validate(); // if a subclass overrides Validate(), its fields are not set yet
    }

    protected virtual void Validate() { }
}

public class PowerSubstationValidating : GridAssetValidating
{
    private readonly string? _regionCode;

    public PowerSubstationValidating(string assetId, string regionCode) : base(assetId)
    {
        _regionCode = regionCode;
    }

    protected override void Validate()
    {
        // _regionCode is null here — the base constructor runs before this
        // derived field is assigned. InvalidOperationException is correct:
        // this is a STATE error (object is partially constructed), not an
        // argument error.
        if (string.IsNullOrWhiteSpace(_regionCode))
            throw new InvalidOperationException("Region code is not yet initialised.");
    }
}

// ----- Section 3 mistake 3: heavy logic in the constructor body ------------
public interface ISimplePermitRepository
{
    IReadOnlyList<GridPermit1B> GetByZone(string zoneCode);
}

public class InMemorySimplePermitRepository : ISimplePermitRepository
{
    public IReadOnlyList<GridPermit1B> GetByZone(string zoneCode) =>
        new List<GridPermit1B> { new GridPermit1B("P-900", zoneCode, 200) };
}

public class ZoneReport
{
    public IReadOnlyList<GridPermit1B> Permits { get; }

    // Constructor receives already-loaded data; a factory method owns the loading.
    public ZoneReport(IReadOnlyList<GridPermit1B> permits)
    {
        Permits = permits ?? throw new ArgumentNullException(nameof(permits));
    }

    public static ZoneReport LoadFor(string zoneCode, ISimplePermitRepository repo) =>
        new ZoneReport(repo.GetByZone(zoneCode));
}

// ----- 4.1: constructor chaining (this(...) and base(...)) -----------------
public class GridPermitChained
{
    public string PermitId { get; }
    public string ZoneCode { get; }
    public int MaxLoad { get; }
    public string RequestedBy { get; }

    // Master constructor — all validation lives here
    public GridPermitChained(string permitId, string zoneCode, int maxLoad, string requestedBy)
    {
        if (string.IsNullOrWhiteSpace(permitId))
            throw new ArgumentException("PermitId required.", nameof(permitId));
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("ZoneCode required.", nameof(zoneCode));
        if (maxLoad <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLoad));
        if (string.IsNullOrWhiteSpace(requestedBy))
            throw new ArgumentException("RequestedBy required.", nameof(requestedBy));

        PermitId = permitId;
        ZoneCode = zoneCode;
        MaxLoad = maxLoad;
        RequestedBy = requestedBy;
    }

    // Convenience overload — chains to the master constructor
    public GridPermitChained(string permitId, string zoneCode, int maxLoad)
        : this(permitId, zoneCode, maxLoad, "system") // master runs first
    {
        // body runs AFTER the master constructor completes — often empty
    }
}

public abstract class GridAssetChained
{
    public string AssetId { get; }
    public string ZoneCode { get; }

    protected GridAssetChained(string assetId, string zoneCode)
    {
        AssetId = assetId ?? throw new ArgumentNullException(nameof(assetId));
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
    }
}

public class PowerSubstationChained : GridAssetChained
{
    public double MaxCapacityMW { get; }

    public PowerSubstationChained(string assetId, string zoneCode, double maxCapacityMW)
        : base(assetId, zoneCode) // GridAsset constructor runs first
    {
        MaxCapacityMW = maxCapacityMW > 0
            ? maxCapacityMW
            : throw new ArgumentOutOfRangeException(nameof(maxCapacityMW));
    }
}

// ----- 4.2: constructor accessibility ---------------------------------------
public sealed class GridPermitFactoryMade
{
    public enum Status { Pending, Draft }

    public string PermitId { get; }
    public string ZoneCode { get; }
    public int MaxLoad { get; }
    public Status CurrentStatus { get; private set; }

    // Private — only factory methods inside this class can produce instances
    private GridPermitFactoryMade(string permitId, string zoneCode, int maxLoad, Status status)
    {
        if (string.IsNullOrWhiteSpace(permitId))
            throw new ArgumentException("PermitId cannot be empty.", nameof(permitId));
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("ZoneCode cannot be empty.", nameof(zoneCode));
        if (maxLoad < 0) // 0 is allowed: CreateDraft uses maxLoad = 0
            throw new ArgumentOutOfRangeException(nameof(maxLoad), "MaxLoad cannot be negative.");

        PermitId = permitId;
        ZoneCode = zoneCode;
        MaxLoad = maxLoad;
        CurrentStatus = status;
    }

    public static GridPermitFactoryMade CreateNew(string permitId, string zoneCode, int maxLoad) =>
        new GridPermitFactoryMade(permitId, zoneCode, maxLoad, Status.Pending);

    public static GridPermitFactoryMade CreateDraft(string zoneCode) =>
        new GridPermitFactoryMade(Guid.NewGuid().ToString(), zoneCode, 0, Status.Draft);
    // var bad = new GridPermitFactoryMade(...);  // CS0122 — constructor is private
}

// internal constructor — assembly-controlled creation. (Same assembly here,
// so PermitFactoryInternal CAN call it; a different assembly could not.)
public class GridPermitInternalCtor
{
    public string PermitId { get; }
    public string ZoneCode { get; }

    internal GridPermitInternalCtor(string permitId, string zoneCode)
    {
        PermitId = permitId ?? throw new ArgumentNullException(nameof(permitId));
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
    }
}

public class PermitFactoryInternal
{
    public GridPermitInternalCtor Create(string permitId, string zoneCode) =>
        new GridPermitInternalCtor(permitId, zoneCode); // same assembly — allowed
}
// In a different assembly:
//   var permit = new GridPermitInternalCtor("P-001", "North-7"); // CS0122
//   var permit = factory.Create("P-001", "North-7");             // OK, through the public factory

// protected constructor — base class design; cannot be instantiated directly
public abstract class GridAssetProtectedCtor
{
    public string AssetId { get; }
    public string ZoneCode { get; }

    protected GridAssetProtectedCtor(string assetId, string zoneCode) // only derived classes can call this
    {
        AssetId = assetId ?? throw new ArgumentNullException(nameof(assetId));
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
    }
}
// new GridAssetProtectedCtor("A-01", "North-7"); // cannot instantiate an abstract class

// ----- 4.3: self-reference (this) -------------------------------------------
public class GridZoneThis
{
    private string _zoneCode;
    private bool _isActive;

    public GridZoneThis(string zoneCode, bool isActive)
    {
        this._zoneCode = zoneCode; // this. is optional here — names already differ by underscore
        this._isActive = isActive;
    }

    public string ZoneCode => _zoneCode;
    public bool IsActive => _isActive;
}

// A single ZoneMonitor is reused by the "this" and "this-escape" demos below;
// Register accepts object so it can track whichever zone type is registering.
public class ZoneMonitor
{
    private readonly List<object> _zones = new();
    public void Register(object zone) => _zones.Add(zone);
    public int RegisteredCount => _zones.Count;
}

public class GridZoneSubscribing
{
    public string ZoneCode { get; }
    public GridZoneSubscribing(string zoneCode) => ZoneCode = zoneCode;

    // Safe — object is fully constructed before 'this' is passed
    public void Subscribe(ZoneMonitor monitor) => monitor.Register(this);
}

// this-escape — a critical warning: 'this' passed before construction finishes
public class GridZoneUnsafe
{
    public string ZoneCode { get; private set; } = string.Empty;
    public int AlertCount { get; private set; }

    public GridZoneUnsafe(string zoneCode, ZoneMonitor monitor)
    {
        monitor.Register(this); // this escapes here
        ZoneCode = zoneCode;    // ZoneCode is not yet assigned when Register runs
        AlertCount = 0;
    }
}

public class GridZoneSafe
{
    public string ZoneCode { get; }
    public int AlertCount { get; private set; }

    private GridZoneSafe(string zoneCode)
    {
        ZoneCode = zoneCode;
        AlertCount = 0;
    }

    // Factory method — construction completes before the reference is shared
    public static GridZoneSafe CreateAndRegister(string zoneCode, ZoneMonitor monitor)
    {
        var zone = new GridZoneSafe(zoneCode); // fully constructed
        monitor.Register(zone);                // passed to collaborator after construction
        return zone;
    }
}

// this in fluent method chains
public class ZoneConfigBuilder
{
    private string _zoneCode = string.Empty;
    private int _maxAlerts = 5;
    private bool _monitored = true;

    public ZoneConfigBuilder WithZoneCode(string code) { _zoneCode = code; return this; }
    public ZoneConfigBuilder WithMaxAlerts(int count) { _maxAlerts = count; return this; }
    public ZoneConfigBuilder NotMonitored() { _monitored = false; return this; }

    public ZoneConfiguration Build()
    {
        if (string.IsNullOrWhiteSpace(_zoneCode))
            throw new InvalidOperationException("ZoneCode is required. Call WithZoneCode() before Build().");

        return new ZoneConfiguration
        {
            ZoneCode = _zoneCode,
            MaxAlertCount = _maxAlerts,
            IsMonitored = _monitored
        };
    }
}

// ----- 4.4/5-Step1: required members; also used by ZoneConfigBuilder above -
public class ZoneConfiguration
{
    public required string ZoneCode { get; init; }
    public required int MaxAlertCount { get; init; }
    public required bool IsMonitored { get; init; }
    public string? Description { get; init; } // optional — no required
}
// var bad = new ZoneConfiguration { MaxAlertCount = 5, IsMonitored = true }; // CS9035 — ZoneCode missing

// ----- 4.5: init-only setters ------------------------------------------------
public class GridAlertEvent
{
    public string AlertId { get; init; } = string.Empty;
    public string ZoneCode { get; init; } = string.Empty;
    public DateTime RaisedAt { get; init; } = DateTime.UtcNow;
    public int Severity { get; init; }
}
// alert.Severity = 5; // CS8852 — cannot set init property after object creation

public class CriticalAlertEvent : GridAlertEvent
{
    public string EscalationPath { get; init; } = string.Empty;

    public CriticalAlertEvent(string alertId, string zoneCode)
    {
        AlertId = alertId;  // init setter accessible from a constructor
        ZoneCode = zoneCode;
    }
}

// ----- 4.6: out parameters ----------------------------------------------------
public static class ZoneParser
{
    public static bool TryParseZoneCode(string input, out string zoneCode)
    {
        if (!string.IsNullOrWhiteSpace(input) && input.Contains('-'))
        {
            zoneCode = input.Trim().ToUpperInvariant(); // success path
            return true;
        }
        zoneCode = string.Empty; // failure path — compiler requires BOTH paths to assign zoneCode
        return false;
    }
}

// ----- 4.7: primary constructors (C# 12) --------------------------------------
public interface IZoneValidator
{
    void Validate(string zoneCode);
}

public class PassthroughZoneValidator : IZoneValidator
{
    public void Validate(string zoneCode)
    {
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("Zone code required.", nameof(zoneCode));
    }
}

// Traditional constructor — explicit fields + constructor body
public class PermitServiceTraditional
{
    private readonly ISimplePermitRepository _repo;
    private readonly IZoneValidator _validator;

    public PermitServiceTraditional(ISimplePermitRepository repo, IZoneValidator validator)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public GridPermit1B CreatePermit(string permitId, string zoneCode, int maxLoad)
    {
        _validator.Validate(zoneCode);
        var permit = new GridPermit1B(permitId, zoneCode, maxLoad);
        return permit;
    }
}

// C# 12 primary constructor — parameters are in scope throughout the class
public class PermitServicePrimaryCtor(ISimplePermitRepository repo, IZoneValidator validator)
{
    public GridPermit1B CreatePermit(string permitId, string zoneCode, int maxLoad)
    {
        validator.Validate(zoneCode); // use captured parameter directly
        var permit = new GridPermit1B(permitId, zoneCode, maxLoad);
        return permit;
    }
}

// ----- 4.8: object initialisers vs constructors -------------------------------
public class GridPermitDto
{
    public string PermitId { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;
    public int MaxLoad { get; set; }
    public string RequestedBy { get; set; } = "system";
}
// Combining both approaches (required ctor fields + optional init properties)
// is demonstrated by the Section 5 case study's GridPermit, below — no need
// to redeclare a near-identical type here.

// ===========================================================================
// Section 5 — Case Study: UrbanGrid Permit and Zone Configuration Construction
// ===========================================================================

public enum PermitStatus { Pending, Approved, Revoked, Expired, Draft }

public sealed class GridPermit
{
    // Required properties — enforced by the private constructor
    public string PermitId { get; }
    public string ZoneCode { get; }
    public int MaxLoad { get; }
    public PermitStatus Status { get; internal set; }
    public DateTime IssuedAt { get; }

    // Optional properties — settable via object initialiser
    public string RequestedBy { get; init; } = "system";
    public string? Notes { get; init; }

    // Private constructor — all validation rules enforced here
    private GridPermit(string permitId, string zoneCode, int maxLoad, PermitStatus status)
    {
        if (string.IsNullOrWhiteSpace(permitId))
            throw new ArgumentException("PermitId cannot be empty.", nameof(permitId));
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("ZoneCode cannot be empty.", nameof(zoneCode));
        if (maxLoad < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLoad), $"MaxLoad cannot be negative. Got: {maxLoad}");

        PermitId = permitId;
        ZoneCode = zoneCode;
        MaxLoad = maxLoad;
        Status = status;
        IssuedAt = DateTime.UtcNow;
    }

    // Factory methods — named creation paths. Object initialiser is valid
    // here because factory methods live inside this same class.
    public static GridPermit CreateNew(string permitId, string zoneCode, int maxLoad, string requestedBy = "system") =>
        new GridPermit(permitId, zoneCode, maxLoad, PermitStatus.Pending) { RequestedBy = requestedBy };

    public static GridPermit CreateDraft(string zoneCode) =>
        new GridPermit(Guid.NewGuid().ToString(), zoneCode, 0, PermitStatus.Draft);
}

public sealed class PermitRegistry
{
    private readonly Dictionary<string, GridPermit> _permits = new();
    private readonly Dictionary<string, ZoneConfiguration> _zones = new();

    public GridPermit IssuePermit(string permitId, string zoneCode, int requestedLoad)
    {
        if (!TryGetZone(zoneCode, out var config))
            throw new InvalidOperationException($"Zone '{zoneCode}' is not registered in UrbanGrid.");

        // config! — non-null when TryGetZone returns true; the compiler
        // cannot infer this from TryGetValue's contract.
        if (requestedLoad > config!.MaxAlertCount * 100)
            throw new InvalidOperationException($"Requested load {requestedLoad} exceeds zone capacity for '{zoneCode}'.");

        // permit is fully constructed before being stored — 'this' never escapes early
        var permit = GridPermit.CreateNew(permitId, zoneCode, requestedLoad, "PermitRegistry");
        _permits[permitId] = permit; // stored only after full construction
        return permit;
    }

    public bool TryGetZone(string zoneCode, out ZoneConfiguration? config) =>
        _zones.TryGetValue(zoneCode, out config);

    public bool TryGetPermit(string permitId, out GridPermit? permit) =>
        _permits.TryGetValue(permitId, out permit);

    public void RegisterZone(ZoneConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config); // null guard before any dereference
        _zones[config.ZoneCode] = config;
    }
}

// Minimal stand-in for Microsoft.Extensions.Logging.ILogger<T>, so this
// project has no dependency on an external logging package.
public interface ISimpleLogger<T>
{
    void LogInformation(string message, params object?[] args);
}

public class ConsoleLogger<T> : ISimpleLogger<T>
{
    // Replaces named "{Placeholder}" tokens with args, in the order both appear —
    // a minimal stand-in for structured logging's message-template semantics.
    public void LogInformation(string message, params object?[] args)
    {
        var result = System.Text.RegularExpressions.Regex.Replace(message, "\\{[A-Za-z]+\\}", _ =>
        {
            var index = _placeholderIndex++;
            return index < args.Length ? args[index]?.ToString() ?? "null" : string.Empty;
        });
        Console.WriteLine("[INFO] " + result);
        _placeholderIndex = 0;
    }

    private int _placeholderIndex;
}

// The book places this in a separate UrbanGrid.Application namespace; kept
// in the same namespace here (a single file-scoped namespace can't be mixed
// with an additional namespace block in one file) — the primary-constructor
// DI pattern being demonstrated is unaffected either way.
// Primary constructor — clean DI wiring; no domain-level rules of its own
public class PermitService(PermitRegistry registry, ISimpleLogger<PermitService> logger)
{
    public GridPermit IssuePermit(string permitId, string zoneCode, int maxLoad)
    {
        logger.LogInformation("Issuing permit {PermitId} for zone {ZoneCode}", permitId, zoneCode);
        var permit = registry.IssuePermit(permitId, zoneCode, maxLoad);
        logger.LogInformation("Permit {PermitId} issued. Status: {Status}", permit.PermitId, permit.Status);
        return permit;
    }
}
