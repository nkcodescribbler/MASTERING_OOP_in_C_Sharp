// Chapter 2 — Structs, Value Types & Reference Types
// Run with: dotnet run --project Chapter02_StructsValueReferenceTypes

using System.Collections;
using OOPBook.Chapter02_StructsValueReferenceTypes;

Section1B_ValueVsReferenceCopy();
Section1D_Immutability();
Section3_CommonMistakes();
Section5_2_BoxingAndUnboxing();
Section5_4_RefAndInParameters();
Section6_CaseStudy();

// ---------------------------------------------------------------------
// Section 1B — value type copy vs reference type copy
// ---------------------------------------------------------------------
static void Section1B_ValueVsReferenceCopy()
{
    Header("Section 1B — Value Types vs Reference Types");

    // VALUE TYPE — SensorReading is a struct
    var reading1 = new SensorReading(112.3, DateTime.UtcNow, "N7");
    var reading2 = reading1; // Full copy — reading2 gets its own independent data

    // REFERENCE TYPE — GridZone is a class
    var zone1 = new GridZone("North-7");
    var zone2 = zone1; // Reference copy — both variables point to the same object
    zone2.Deactivate();

    Console.WriteLine($"reading1 == reading2 values: {reading1.LoadMw} / {reading2.LoadMw} (independent copies)");
    Console.WriteLine($"zone1.IsActive(): {zone1.IsActive()} (aliased — both see the same change)");
}

// ---------------------------------------------------------------------
// Section 1D — immutable struct, copying semantics, pass-by-value
// ---------------------------------------------------------------------
static void Section1D_Immutability()
{
    Header("Section 1D — Copying Semantics: Independence by Design");

    var reading = new SensorReading(112.3, DateTime.UtcNow, "N7");

    // You cannot do: reading.LoadMw = 0.0; — compiler error, property is read-only
    var corrected = reading.WithLoad(0.0); // new instance — original is untouched
    Console.WriteLine(reading.LoadMw);     // still 112.3 — original unchanged
    Console.WriteLine(corrected.LoadMw);   // 0.0 — new independent value

    var original = reading;
    var copy = original; // full copy — independent data
    Console.WriteLine(original.ToString());
    Console.WriteLine(copy.ToString());

    var live = new SensorReading(98.7, DateTime.UtcNow, "N7");
    LogReading(live); // 'live' is copied into the 'reading' parameter — live is unchanged
}

static void LogReading(SensorReading reading)
{
    // 'reading' is a copy — any change here does not affect the caller
    Console.WriteLine(reading.ToString());
}

// ---------------------------------------------------------------------
// Section 3 — Common Mistakes
// ---------------------------------------------------------------------
static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake 1 — mutable struct: mutation silently affects only the copy
    var mutable = new MutableSensorReading { LoadMw = 112.3, TakenAt = DateTime.UtcNow };
    ResetLoad(mutable);
    Console.WriteLine($"After ResetLoad: {mutable.LoadMw}"); // still 112.3 — the reset never happened

    // Mistake 2 — property returns a copy; mutation is silently lost.
    // The book's example uses a MUTABLE GeoCoordinate with a Shift(...) method and does
    // NOT compile (CS1612: "Cannot modify the return value of ... because it is not a
    // variable"). That version is intentionally omitted from this runnable file — the
    // corrected, immutable GeoCoordinate below is the one actually used in this project.
    var sensor = new GridSensor { Location = new GeoCoordinate(52.3, 4.9) };
    sensor.Location = sensor.Location.WithLatitudeShift(0.5); // correct usage — replace the value entirely
    Console.WriteLine(sensor.Location.Latitude); // 52.8 — update applied correctly

    // Mistake 3 — oversized struct: every assignment copies ~50 bytes
    var snapshots = GetAllSnapshots();
    foreach (var snapshot in snapshots)
        Process(snapshot); // ~50-byte copy on every iteration

    // Mistake 4 — default ValueType.Equals may box fields on comparison
    var set = new HashSet<SensorReading>();
    set.Add(new SensorReading(112.3, DateTime.UtcNow, "N7"));
    Console.WriteLine($"HashSet count: {set.Count} (equality/GetHashCode for structs — see Chapter 11)");
}

static void ResetLoad(MutableSensorReading r)
{
    r.LoadMw = 0.0; // mutates the copy — 'reading' in the caller is unchanged
}

static IEnumerable<GridZoneSnapshot> GetAllSnapshots()
{
    yield return new GridZoneSnapshot { ZoneCode = "North-7", LoadMw = 112.3, IsActive = true, RecordedAt = DateTime.UtcNow };
    yield return new GridZoneSnapshot { ZoneCode = "East-4", LoadMw = 87.1, IsActive = true, RecordedAt = DateTime.UtcNow };
}

static void Process(GridZoneSnapshot snapshot)
{
    Console.WriteLine($"Processing {snapshot.ZoneCode}: {snapshot.LoadMw:F1} MW");
}

// ---------------------------------------------------------------------
// Section 5.2 — Boxing and Unboxing
// ---------------------------------------------------------------------
static void Section5_2_BoxingAndUnboxing()
{
    Header("Section 5.2 — Boxing and Unboxing");

    int loadPercent = 87; // value type — lives on the stack, 4 bytes, no heap involved

    // BOXING — loadPercent is wrapped in a new heap object
    object boxed = loadPercent;

    // UNBOXING — extract the value back out of the heap object
    int unboxed = (int)boxed;
    Console.WriteLine(unboxed); // 87

    // Trap 1 — storing structs in a non-generic collection boxes them
    var readings = new ArrayList();
    readings.Add(new SensorReading(112.3, DateTime.UtcNow, "N7")); // boxed — one heap allocation

    // Trap 2 — assigning a struct to an object variable also boxes it
    object boxed2 = new SensorReading(112.3, DateTime.UtcNow, "N7");

    // The fix — use a generic typed collection
    var typedReadings = new List<SensorReading>();
    typedReadings.Add(new SensorReading(112.3, DateTime.UtcNow, "N7")); // no boxing
    Console.WriteLine($"typedReadings.Count = {typedReadings.Count} (no boxing)");
}

// ---------------------------------------------------------------------
// Section 5.4 — ref and in parameters
// ---------------------------------------------------------------------
static void Section5_4_RefAndInParameters()
{
    Header("Section 5.4 — ref and in Parameters");

    var r = new SensorReading(112.3, DateTime.UtcNow, "N7");
    Console.WriteLine(Describe(in r));  // no copy — r's location is passed directly
    Console.WriteLine(Describe(r));     // also valid — compiler applies 'in' automatically

    var mutableReading = new MutableReading { LoadMw = 55.0, ZoneCode = "N7" };
    LogMutable(in mutableReading); // compiler may generate a defensive copy — not readonly
    Log(in r);                     // readonly struct — no defensive copy, full optimisation

    var current = new SensorReading(112.3, DateTime.UtcNow, "N7");
    Recalibrate(ref current, 0.98); // 'ref' required at call site — makes mutation visible
    Console.WriteLine(current.LoadMw); // 110.054 — caller's variable was updated
}

static string Describe(in SensorReading reading) => reading.ToString();

static void LogMutable(in MutableReading reading) => Console.WriteLine(reading.Summarise());

static void Log(in SensorReading reading) => Console.WriteLine(reading.ToString());

static void Recalibrate(ref SensorReading reading, double correctionFactor)
{
    // Creates a new reading with adjusted load — replaces the caller's variable
    reading = reading.WithLoad(reading.LoadMw * correctionFactor);
}

// ---------------------------------------------------------------------
// Section 6 — Case Study: the UrbanGrid sensor pipeline
// ---------------------------------------------------------------------
static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Value Types in the Sensor Pipeline");

    var snapshot = new ZoneLoadSnapshot("North-7", current: 0, peak: 0, GridAlertLevel.None, DateTime.UtcNow);
    Console.WriteLine($"Initial: {snapshot.ZoneCode} — {snapshot.CurrentLoadMw:F1} MW, level={snapshot.AlertLevel}");

    var updated = snapshot.WithReading(112.3, GridAlertLevel.Warning, DateTime.UtcNow);
    Console.WriteLine($"Updated: {updated.ZoneCode} — {updated.CurrentLoadMw:F1} MW (peak {updated.PeakLoadMw:F1}), level={updated.AlertLevel}");

    // Original snapshot is untouched — a new value was returned
    Console.WriteLine($"Original still: {snapshot.CurrentLoadMw:F1} MW, level={snapshot.AlertLevel}");
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
