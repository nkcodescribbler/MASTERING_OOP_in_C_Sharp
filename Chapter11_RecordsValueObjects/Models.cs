// Chapter 11 — Records & Value Objects
// SensorReading appears first as a simple positional record (1A), then a
// validated nominal record (Section 3's fix, reused by the Section 6 case
// study "After"). The validated nominal form is used everywhere as the
// canonical SensorReading — it is a strict superset of the positional form's
// behaviour (same constructor signature), so nothing is lost by not also
// declaring the bare positional version separately.

namespace OOPBook.Chapter11_RecordsValueObjects;

// ----- Section 1B — reference semantics vs value semantics ------------------
public class GridZone
{
    public string ZoneCode { get; }
    public GridZone(string zoneCode) => ZoneCode = zoneCode;
}

/// <summary>
/// Sections 1A/1B/3/5.1/5.4/5.5/5.6/6 — the canonical, validated SensorReading.
/// </summary>
public record SensorReading
{
    public string ZoneCode { get; init; }
    public double ValueMw { get; init; }
    public DateTime RecordedAt { get; init; }

    public SensorReading(string zoneCode, double valueMw, DateTime recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneCode, nameof(zoneCode));
        if (valueMw < 0)
            throw new ArgumentOutOfRangeException(nameof(valueMw), "Reading cannot be negative.");
        ZoneCode = zoneCode;
        ValueMw = valueMw;
        RecordedAt = recordedAt;
    }

    // Nominal records don't get a generated Deconstruct — declare it explicitly.
    public void Deconstruct(out string zoneCode, out double valueMw, out DateTime recordedAt)
    {
        zoneCode = ZoneCode;
        valueMw = ValueMw;
        recordedAt = RecordedAt;
    }
}

// ----- Section 3 — Common Mistakes ------------------------------------------

// Mistake: GridZone has identity (a real, evolving thing), not just value —
// a record's silent value-equality is the wrong tool here.
public record GridZoneAsRecord(string ZoneCode, bool IsActive, int AlertCount);

// Mistake: record struct copy trap — `with` inside a method produces a new
// LOCAL copy; the caller's original is untouched unless the method returns it.
public record struct GridSensorCalibration(string SensorId, double OffsetFactor);

// Mistake: allocating a record CLASS for a tiny, high-frequency value type —
// every instance is a heap allocation.
public record GeoCoordinateClass(double Lat, double Lon);

// Mistake: a MUTABLE record struct used as a dictionary key — `with` rebinds
// to a new struct, silently orphaning the original dictionary entry.
public record struct GeoCoordinateMutableStruct(double Lat, double Lon);

// Mistake: behaviour (a virtual method) added to what should be a pure data type.
public abstract record GridEvent(DateTime OccurredAt)
{
    public virtual string GetSeverityLabel() => "Unknown"; // behaviour in a data type — a smell
}

// ===========================================================================
// Section 5 — Method-Level Detail
// ===========================================================================

// 5.2 — nominal record struct with explicitly mutable properties.
// (A positional record struct would generate init-only properties instead.)
public record struct ZoneDelta
{
    public double LatDelta { get; set; }
    public double LonDelta { get; set; }
}

// 5.3 — the final, fully immutable, highest-performance value record.
public readonly record struct GeoCoordinate(double Lat, double Lon);

// 5.7 — shallow record inheritance for data extension only.
public record GridAlertSnapshot(string ZoneCode, DateTime OccurredAt, string Description);

public record CriticalAlertSnapshot(string ZoneCode, DateTime OccurredAt, string Description, int PriorityLevel)
    : GridAlertSnapshot(ZoneCode, OccurredAt, Description);

// ===========================================================================
// Section 6 — Case Study: Before (mutable class) vs After (immutable record)
// ===========================================================================
public class SensorReadingMutable
{
    public string ZoneCode { get; set; } = string.Empty; // mutable
    public double ValueMw { get; set; }                  // mutable — no Equals/GetHashCode/ToString override
    public DateTime RecordedAt { get; set; }              // mutable
}

// Mutates the caller's object — downstream stages see corrupted data.
public class TelemetryNormaliserBefore
{
    public void Normalise(SensorReadingMutable reading)
    {
        if (reading.ValueMw > 1000) reading.ValueMw = reading.ValueMw / 1000;
    }
}

// Normaliser returns a new record — the original is protected by init-only properties.
public class TelemetryNormaliser
{
    public SensorReading Normalise(SensorReading reading) =>
        reading.ValueMw > 1000 ? reading with { ValueMw = reading.ValueMw / 1000.0 } : reading;
}
