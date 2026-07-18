// Chapter 5 — Properties & Controlled State
// PowerZone and GridPermit are built up incrementally through the book and
// converge on one final shape in the Section 6 case study. Rather than
// redeclaring three near-identical versions of each, this file defines the
// final (case-study) shape once and Program.cs points out, section by
// section, which property technique each part of it demonstrates.

namespace OOPBook.Chapter05_PropertiesControlledState;

public enum ZoneStatus { Pending, Active, Inactive, Faulted, Decommissioned }

// ----- Section 1 — "what is a property" before/after ------------------------
public class PowerZoneField
{
    public string? Name; // zone.Name = ""; zone.Name = null; — nothing stops this
}

public class PowerZoneWithProperty
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Zone name cannot be empty.", nameof(value));
            _name = value;
        }
    }
}

// ----- Section 3 — common mistakes: read-only view + methods over cost ------
public class GridAlert
{
    public string Message { get; init; } = string.Empty;
    public DateTime RaisedAt { get; init; } = DateTime.UtcNow;
}

public class AlertBoard
{
    // Expose a read-only view; internal mutations go through a controlled method.
    private readonly List<GridAlert> _alerts = new();
    public IReadOnlyList<GridAlert> Alerts => _alerts.AsReadOnly();

    public void AddAlert(GridAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _alerts.Add(alert);
    }
}

// ----- Section 5.1 — auto-properties and backing fields ---------------------
public class GridSensor
{
    // No initializer — a string property with no default is null until set.
    // (Teaching example only; earlier chapters define the fuller GridSensor.)
    public string SensorId { get; set; } = string.Empty;
}

public class PowerZoneDefaults
{
    public string Name { get; set; } = string.Empty;       // default empty string
    public bool IsActive { get; set; } = true;              // default active
    public List<string> Tags { get; set; } = new();         // default empty list
    public ZoneStatus Status { get; set; } = ZoneStatus.Pending;
}

// ----- Section 5.7 — property vs method: cost should be visible -------------
public class PermitLookupService
{
    private readonly Dictionary<string, bool> _knownPermits = new()
    {
        ["P-2024-0042"] = true
    };

    // A method, not a property — signals that this may do real work (here: a
    // dictionary lookup standing in for a database call or audit log write).
    public bool Validate(string permitId, bool isExpired) =>
        _knownPermits.ContainsKey(permitId) && !isExpired;
}

// ===========================================================================
// Section 6 — Case Study: UrbanGrid Zone Status & Permit Validity
// Also used for Sections 5.1 (validated Capacity setter), 5.2 (computed
// properties), 5.5 (asymmetric access), 5.6 (serialisation) and 5.8 (indexer)
// — the book builds this exact class across those sections.
// ===========================================================================

public class PowerZone
{
    // Identity — set once in the constructor
    public string ZoneCode { get; }
    public string DisplayName { get; }

    // Operational state
    public ZoneStatus Status { get; private set; } = ZoneStatus.Pending;

    // Load tracking
    private double _currentLoad;
    private double _capacity;
    private string _lastFaultReason = string.Empty;

    public double Capacity
    {
        get => _capacity;
        private set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), $"Capacity must be positive. Got: {value}");
            _capacity = value;
        }
    }

    public double CurrentLoad
    {
        get => _currentLoad;
        internal set // only the telemetry service (same assembly) updates this
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), $"Load cannot be negative. Got: {value}");
            _currentLoad = value;
        }
    }

    // Computed diagnostics — no backing field, calculated fresh on each read
    public double LoadPercentage => _capacity > 0 ? _currentLoad / _capacity * 100.0 : 0.0;
    public bool IsOverloaded => LoadPercentage > 100.0;
    public bool IsNearCapacity => LoadPercentage is > 80.0 and <= 100.0;
    public bool IsOperational => Status == ZoneStatus.Active;
    public string ZoneLabel => $"[{ZoneCode}] {DisplayName}";

    public string StatusSummary =>
        Status == ZoneStatus.Active && IsOverloaded ? $"[{ZoneCode}] OVERLOADED — {LoadPercentage:F1}%" :
        Status == ZoneStatus.Active && IsNearCapacity ? $"[{ZoneCode}] Near Capacity — {LoadPercentage:F1}%" :
        Status == ZoneStatus.Active ? $"[{ZoneCode}] OK — {LoadPercentage:F1}%" :
        $"[{ZoneCode}] {Status}";

    public PowerZone(string zoneCode, string displayName, double capacity)
    {
        if (string.IsNullOrWhiteSpace(zoneCode))
            throw new ArgumentException("ZoneCode cannot be blank.", nameof(zoneCode));
        ZoneCode = zoneCode.ToUpperInvariant();
        DisplayName = displayName;
        Capacity = capacity; // uses the validating setter
    }

    public void Activate()
    {
        if (Status == ZoneStatus.Decommissioned)
            throw new InvalidOperationException($"Zone {ZoneCode} is decommissioned.");
        Status = ZoneStatus.Active;
    }

    public void Deactivate() => Status = ZoneStatus.Inactive;

    public void SetFaulted(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Fault reason must be provided.", nameof(reason));
        Status = ZoneStatus.Faulted;
        _lastFaultReason = reason;
    }

    public string LastFaultReason => _lastFaultReason;
}

/// <summary>
/// Section 5.3/5.4/6 — GridPermit built with required init-only properties
/// (compiler-enforced completeness) plus computed, always-consistent state.
/// </summary>
public class GridPermit
{
    // Required — compiler enforces that all four are set at construction
    public required string PermitId { get; init; }
    public required string ZoneCode { get; init; }
    public required DateTime IssuedAt { get; init; }
    public required DateTime ExpiryDate { get; init; }

    // Optional
    public string? ApprovedBy { get; init; }
    public string Notes { get; init; } = string.Empty;

    // Computed — always consistent, can never go stale
    public bool IsExpired => DateTime.UtcNow > ExpiryDate;
    public bool HasValidDateRange => ExpiryDate > IssuedAt; // expiry must follow issue
    public bool IsValid => !IsExpired && HasValidDateRange && !string.IsNullOrWhiteSpace(PermitId);
    public int DaysRemaining => IsExpired ? 0 : (int)(ExpiryDate - DateTime.UtcNow).TotalDays;

    public string Summary =>
        $"Permit {PermitId} for {ZoneCode} — " + (IsExpired ? "EXPIRED" : $"Valid, {DaysRemaining}d remaining");
}

/// <summary>Section 5.8 / Wiring It Together — indexer plus LINQ-backed views.</summary>
public class ZoneRegistry
{
    private readonly Dictionary<string, PowerZone> _zones = new();

    // Indexer — get and set a zone by code
    public PowerZone? this[string zoneCode]
    {
        get => _zones.TryGetValue(zoneCode.ToUpperInvariant(), out var zone) ? zone : null;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.IsNullOrWhiteSpace(zoneCode))
                throw new ArgumentException("Zone code cannot be blank.", nameof(zoneCode));
            _zones[zoneCode.ToUpperInvariant()] = value;
        }
    }

    public int Count => _zones.Count;
    public IReadOnlyCollection<PowerZone> AllZones => _zones.Values.ToList().AsReadOnly();
    public IEnumerable<PowerZone> ActiveZones => _zones.Values.Where(z => z.IsOperational);

    public void Register(PowerZone zone) => this[zone.ZoneCode] = zone;
}
