// Chapter 2 — Structs, Value Types & Reference Types
// Domain model ("UrbanGrid") consolidated from the book's incremental
// snippets into their final, complete form (Sections 1, 5 and 6).

namespace OOPBook.Chapter02_StructsValueReferenceTypes;

/// <summary>
/// Section 5.1 / 6 — final version of SensorReading: an immutable value
/// type with no identity. Grows from a 2-field struct in Section 1 to this
/// 3-field readonly struct once ZoneCode is introduced in Section 5.1.
/// </summary>
public readonly struct SensorReading
{
    public double LoadMw { get; }
    public DateTime TakenAt { get; }
    public string ZoneCode { get; }

    public SensorReading(double loadMw, DateTime takenAt, string zoneCode)
    {
        LoadMw = loadMw;
        TakenAt = takenAt;
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
    }

    // To "change" a value — produce a new instance, leave the original untouched
    public SensorReading WithLoad(double newLoad) =>
        new SensorReading(newLoad, TakenAt, ZoneCode);

    public override string ToString() =>
        $"[{ZoneCode}] {LoadMw:F1} MW at {TakenAt:HH:mm:ss}";
}

/// <summary>
/// Section 3 — the corrected, immutable version of GeoCoordinate.
/// </summary>
public readonly struct GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoCoordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    // Returns a new coordinate — does not mutate the original
    public GeoCoordinate WithLatitudeShift(double delta) =>
        new GeoCoordinate(Latitude + delta, Longitude);
}

/// <summary>
/// Section 1B / 3 — a minimal reference-type stand-in used purely to
/// contrast reference-copy semantics against SensorReading's value-copy
/// semantics. The full GridZone (identity, lifecycle) is Chapter 1's class.
/// </summary>
public class GridZone
{
    private bool _isActive = true;
    public string ZoneCode { get; }
    public GridZone(string zoneCode) => ZoneCode = zoneCode;
    public bool IsActive() => _isActive;
    public void Deactivate() => _isActive = false;
}

/// <summary>
/// Section 3 — teaching version of GridSensor, simplified to hold a
/// mutable-struct property purely to illustrate the "property returns a
/// copy" pitfall. The production GridSensor (IDisposable) is Chapter 1's.
/// </summary>
public class GridSensor
{
    public GeoCoordinate Location { get; set; }
}

/// <summary>
/// Section 3 — an oversized struct (~50 bytes). Public fields are used
/// here deliberately to keep the per-field byte-size annotations from the
/// book readable; a production struct would use properties (see 5.1).
/// </summary>
public struct GridZoneSnapshot
{
    public string ZoneCode;        // string ref — 8B
    public double LoadMw;          // 8B
    public double PeakLoadMw;      // 8B
    public double AverageLoadMw;   // 8B
    public int AlertCount;         // 4B
    public int SensorCount;        // 4B
    public bool IsActive;          // 1B
    public bool IsOverloaded;      // 1B
    public DateTime RecordedAt;    // 8B
    // Total: ~50+ bytes
}

/// <summary>
/// Section 3 — mutable struct. Mutating a copy (e.g. inside a method that
/// takes it by value) silently does nothing to the caller's original.
/// </summary>
public struct MutableSensorReading
{
    public double LoadMw { get; set; }
    public DateTime TakenAt { get; set; }
}

/// <summary>
/// Section 5.4 — a non-readonly struct, used to show that the compiler
/// generates defensive copies for 'in' parameters when it cannot prove the
/// struct is immutable.
/// </summary>
public struct MutableReading
{
    public double LoadMw { get; set; }
    public string ZoneCode { get; set; }
    public string Summarise() => $"{ZoneCode}: {LoadMw:F1} MW";
}

/// <summary>Section 6 — case study enum for alert severity.</summary>
public enum GridAlertLevel { None, Warning, Critical, Offline }

/// <summary>
/// Section 6 — immutable zone summary; replaced, not mutated, each time a
/// new reading arrives.
/// </summary>
public readonly struct ZoneLoadSnapshot
{
    public string ZoneCode { get; }
    public double CurrentLoadMw { get; }
    public double PeakLoadMw { get; }
    public GridAlertLevel AlertLevel { get; }
    public DateTime SnapshotAt { get; }

    public ZoneLoadSnapshot(string zoneCode, double current, double peak,
                             GridAlertLevel level, DateTime at)
    {
        ZoneCode = zoneCode;
        CurrentLoadMw = current;
        PeakLoadMw = peak;
        AlertLevel = level;
        SnapshotAt = at;
    }

    // Returns a new snapshot with updated fields — getter-only properties
    // don't support 'with' on a struct; use the constructor instead.
    public ZoneLoadSnapshot WithReading(double loadMw, GridAlertLevel level, DateTime at) =>
        new ZoneLoadSnapshot(ZoneCode, loadMw, Math.Max(PeakLoadMw, loadMw), level, at);
}
