// Chapter 1 — Classes, Objects & Object Lifecycle
// Domain model ("UrbanGrid") used throughout this chapter's examples.
// Consolidated from the book's incremental snippets (Sections 1, 5 and 6) into
// their final, complete form.

namespace OOPBook.Chapter01_ClassesObjectsLifecycle;

/// <summary>
/// Section 1A / 5.4 — a class is the blueprint, objects are the real things
/// built from it. Includes the nullable-reference-type refinements from 5.4.
/// </summary>
public class GridZone
{
    // Data — each object built from this class gets its own copy of these
    private readonly string _zoneCode;
    private bool _isActive;
    private int _alertCount;

    // Non-nullable — ZoneCode must always have a value
    public string ZoneCode => _zoneCode;

    // Nullable — a zone may not yet have been assigned to a sector
    public string? SectorName { get; private set; }

    // Nullable — an alert message is optional
    public string? LastAlertMessage { get; private set; }

    // Setup — runs once when a new zone object is created (Chapter 4 covers this fully)
    public GridZone(string zoneCode)
    {
        _zoneCode = zoneCode;
        _isActive = true;
        _alertCount = 0;
    }

    // Behaviour — things this zone can do or tell you
    public string GetZoneCode() => _zoneCode;
    public bool IsActive() => _isActive;

    // switches the zone offline
    public void Deactivate() => _isActive = false;

    public void AssignSector(string sectorName)
    {
        SectorName = sectorName;
    }

    public string GetDisplayName()
    {
        // SectorName has been checked for null and is safe to use.
        // If SectorName is null, we fall back to ZoneCode alone.
        return SectorName is not null
            ? $"{ZoneCode} ({SectorName})"
            : ZoneCode;
    }
}

/// <summary>
/// Section 5.5 — IDisposable pattern for a class holding a managed resource.
/// A real implementation would hold a network/handle-based connection; this
/// is simplified to illustrate the disposal pattern itself.
/// </summary>
public class GridSensor : IDisposable
{
    private readonly string _sensorId;
    private bool _disposed;

    public GridSensor(string sensorId)
    {
        _sensorId = sensorId ?? throw new ArgumentNullException(nameof(sensorId));
        // Initialise connection here in a real implementation
    }

    public string SensorId => _sensorId;

    public void Dispose()
    {
        if (_disposed) return;   // Guard against double-dispose
        // Release managed resources here (e.g., close connection)
        _disposed = true;
    }
}

/// <summary>
/// Section 5.3 — object initialiser syntax. GridAsset is a lightweight
/// descriptor, not a domain entity with identity rules, so public
/// get/set properties with defaults are appropriate here.
/// </summary>
public class GridAsset
{
    public string AssetId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string ZoneCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Sections 5.1 / 5.2 — field vs property distinction, and methods as
/// domain operations rather than field setters.
/// </summary>
public class PowerSubstation : IDisposable
{
    // Private fields — state storage owned by the class
    private readonly string _substationId;
    private readonly string _zoneCode;
    private bool _isOnline;
    private double _loadMw;
    private readonly GridSensor _sensor;
    private bool _disposed;

    // Object initialisation — fields are assigned their starting values here.
    // Constructor design, validation patterns, and chaining are covered in Chapter 4.
    public PowerSubstation(string substationId, string zoneCode, GridSensor sensor)
    {
        _substationId = substationId;
        _zoneCode = zoneCode;
        _sensor = sensor;
        _isOnline = true;
        _loadMw = 0.0;
    }

    // Read-only properties — expose state, prevent external mutation
    public string SubstationId => _substationId;
    public string ZoneCode => _zoneCode;
    public bool IsOnline => _isOnline;
    public double LoadMw => _loadMw;

    // Domain operation — validates and records together
    public void RecordLoad(double loadMw)
    {
        if (loadMw < 0)
            throw new ArgumentOutOfRangeException(nameof(loadMw),
                "Load cannot be negative.");

        _loadMw = loadMw;

        // If load exceeds a threshold, zone-level alerting belongs in a
        // higher-level component — this method only manages its own state.
    }

    // Domain operation — signals a business event, not a flag flip
    public void GoOffline()
    {
        _isOnline = false;
        _loadMw = 0.0;  // An offline substation carries no load
    }

    public string GetStatus()
    {
        return _isOnline
            ? $"Substation {_substationId} online — {_loadMw:F1} MW"
            : $"Substation {_substationId} offline";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sensor?.Dispose();  // ?. = null-conditional: only calls Dispose() if _sensor is not null
        _disposed = true;
        // GC.SuppressFinalize not needed here — PowerSubstation has no finaliser.
    }
}

/// <summary>
/// Section 1 (UML) / Section 6A — tracks registered substations and answers
/// zone-level status queries. Simplified for the lifecycle illustration.
/// </summary>
public class GridControlCentre
{
    private readonly List<PowerSubstation> _substations = new();

    public void Register(PowerSubstation substation) => _substations.Add(substation);

    public string GetZoneStatus(string zoneCode)
    {
        var match = _substations.FirstOrDefault(s => s.ZoneCode == zoneCode);
        return match?.GetStatus() ?? $"No substation registered for zone {zoneCode}";
    }
}

/// <summary>
/// Section 3 — "Common Mistakes" reference types. These are intentionally
/// flawed so the pitfalls described in the book can be demonstrated safely
/// (via try/catch in Program.cs) instead of crashing the whole demo.
/// </summary>
public static class CommonMistakes
{
    // Mistake: public field — broken class invariant waiting to happen.
    public class PowerSubstationWithPublicFields
    {
        public bool IsOnline = true;
        public double LoadMw = 0.0;
    }

    // Mistake: three mandatory fields, all nullable by default without enforcement.
    public class GridPermit
    {
        public string? PermitId { get; set; }
        public string? ZoneCode { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    // Mistake: a class doing too many jobs (violates single responsibility —
    // see Chapter 16, SOLID Principles). Shown here as commentary only,
    // since compiling a second GridZone with the same name isn't possible:
    //
    //   public class GridZone
    //   {
    //       public string ZoneCode { get; }
    //       public bool IsActive() => true;
    //       public void CalibrateSensor(string sensorId, double offset) { /* ... */ } // sensor concern — doesn't belong here
    //       public bool IsPermitValid(string permitId) { /* ... */ }                   // permit concern — doesn't belong here
    //       public string GenerateStatusReport() { /* ... */ }                          // reporting concern — doesn't belong here
    //   }
}
