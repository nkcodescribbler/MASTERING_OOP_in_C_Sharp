// Chapter 11 — Records & Value Objects
// Run with: dotnet run --project Chapter11_RecordsValueObjects

using OOPBook.Chapter11_RecordsValueObjects;

Section1B_ReferenceVsValueSemantics();
Section3_CommonMistakes();
Section5_2_MutableRecordStruct();
Section5_3_ReadonlyRecordStruct();
Section5_4_WithExpression();
Section5_5_PositionalRecordsAndDeconstruction();
Section5_7_RecordInheritance();
Section6_CaseStudy();

static void Section1B_ReferenceVsValueSemantics()
{
    Header("Section 1B — Value Semantics vs Reference Semantics");

    var zone1 = new GridZone("North-7");
    var zone2 = new GridZone("North-7"); // same data, different object
    Console.WriteLine(zone1.Equals(zone2)); // false — different objects in memory

    var reading1 = new SensorReading("North-7", 42.7, new DateTime(2024, 1, 1, 10, 0, 0));
    var reading2 = new SensorReading("North-7", 42.7, new DateTime(2024, 1, 1, 10, 0, 0));
    Console.WriteLine(reading1.Equals(reading2)); // true — same content
    Console.WriteLine(reading1 == reading2);      // true — compiler-generated ==
    Console.WriteLine(reading1);
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake: an entity (has identity) declared as a record — value equality hides duplicates.
    var zoneA = new GridZoneAsRecord("North-7", true, 3);
    var zoneB = new GridZoneAsRecord("North-7", true, 3);
    var zoneSet = new HashSet<GridZoneAsRecord> { zoneA, zoneB };
    Console.WriteLine(zoneSet.Count); // 1 — zoneB treated as a duplicate, even though these are two distinct real zones

    // Mistake: record struct copy trap.
    var calibration = new GridSensorCalibration("SEN-N7-001", 1.02);
    AdjustCalibrationLoses(calibration);
    Console.WriteLine(calibration.OffsetFactor); // still 1.02 — the change inside the method was lost

    calibration = AdjustCalibrationReturns(calibration);
    Console.WriteLine(calibration.OffsetFactor); // 1.05 — correctly updated by returning the new value

    // Mistake: heap-allocated record class for a tiny high-frequency value.
    var coordOnHeap = new GeoCoordinateClass(52.37, 4.90); // every one of these is a heap allocation
    Console.WriteLine($"Heap-allocated: {coordOnHeap}");

    // Mistake: mutable record struct used as a dictionary key.
    var cache = new Dictionary<GeoCoordinateMutableStruct, string>();
    var coord = new GeoCoordinateMutableStruct(52.37, 4.90);
    cache[coord] = "Amsterdam Grid Zone";
    coord = coord with { Lat = 51.50 }; // rebinds to a new struct value
    Console.WriteLine(cache.ContainsKey(coord)); // false — original key is now unfindable
}

static void AdjustCalibrationLoses(GridSensorCalibration cal)
{
    cal = cal with { OffsetFactor = 1.05 }; // rebinds the LOCAL cal — not the original
}

static GridSensorCalibration AdjustCalibrationReturns(GridSensorCalibration cal) =>
    cal with { OffsetFactor = 1.05 };

static void Section5_2_MutableRecordStruct()
{
    Header("Section 5.2 — record struct (mutable, nominal form)");

    var delta1 = new ZoneDelta { LatDelta = 1.5, LonDelta = 0.3 };
    var delta2 = delta1; // full copy — independent value
    delta2.LatDelta = 2.0; // mutation allowed — set accessor was declared

    Console.WriteLine(delta1.LatDelta);  // 1.5 — unchanged, delta2 is a separate copy
    Console.WriteLine(delta1 == delta2); // false — different LatDelta
}

static void Section5_3_ReadonlyRecordStruct()
{
    Header("Section 5.3 — readonly record struct");

    var location1 = new GeoCoordinate(52.37, 4.90);
    var location2 = new GeoCoordinate(52.37, 4.90);
    var location3 = new GeoCoordinate(51.50, 0.12);

    Console.WriteLine(location1 == location2); // true — same coordinates
    Console.WriteLine(location1 == location3); // false — different coordinates
    // location1.Lat = 99.0; // compile error — readonly record struct prevents mutation

    var shifted = location1 with { Lat = 51.50 }; // with expression still works — produces a new instance
    Console.WriteLine(shifted);
    Console.WriteLine(location1.Lat); // 52.37 — unchanged
}

static void Section5_4_WithExpression()
{
    Header("Section 5.4 — The with Expression");

    var originalReading = new SensorReading("North-7", 42.7, new DateTime(2024, 6, 1, 10, 0, 0));
    var correctedReading = originalReading with { ValueMw = 44.1 };

    Console.WriteLine(originalReading.ValueMw);  // 42.7 — unchanged
    Console.WriteLine(correctedReading.ValueMw); // 44.1 — new record
    Console.WriteLine(correctedReading.ZoneCode); // North-7 — copied from original

    var relocatedReading = originalReading with { ZoneCode = "East-4", RecordedAt = DateTime.UtcNow };
    Console.WriteLine(ReferenceEquals(originalReading, relocatedReading)); // false — different objects
}

static void Section5_5_PositionalRecordsAndDeconstruction()
{
    Header("Section 5.5 — Positional Records: Construction and Deconstruction");

    var reading = new SensorReading("North-7", 42.7, new DateTime(2024, 6, 1, 10, 0, 0));

    var (zone, value, timestamp) = reading; // deconstruction — unpack all properties
    Console.WriteLine(zone);

    if (reading is SensorReading { ZoneCode: "North-7", ValueMw: > 40.0 }) // property pattern (positional-record deconstruction pattern needs a positional record; this record is nominal, so property patterns are used instead)
        Console.WriteLine("High-load reading in North-7 zone detected.");
}

static void Section5_7_RecordInheritance()
{
    Header("Section 5.7 — Record Inheritance");

    var alert = new CriticalAlertSnapshot("North-7", DateTime.UtcNow, "Overload detected", 1);
    Console.WriteLine(alert);

    GridAlertSnapshot baseAlert = new GridAlertSnapshot("North-7", alert.OccurredAt, "Overload detected");
    CriticalAlertSnapshot critAlert = alert;

    Console.WriteLine(baseAlert == critAlert); // false — different runtime types (EqualityContract differs)
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Hardening the Telemetry Pipeline");

    // Before — mutable class, no equality contract.
    var fixedTime = new DateTime(2024, 6, 1, 10, 0, 0);
    var before = new TelemetryNormaliserBefore();
    var mutableReading = new SensorReadingMutable { ZoneCode = "North-7", ValueMw = 4270, RecordedAt = fixedTime };
    before.Normalise(mutableReading); // mutates the caller's object in place
    Console.WriteLine($"Before (mutated in place): {mutableReading.ValueMw}");

    var badReadings = new HashSet<SensorReadingMutable>
    {
        new SensorReadingMutable { ZoneCode = "North-7", ValueMw = 42.7, RecordedAt = fixedTime },
        new SensorReadingMutable { ZoneCode = "North-7", ValueMw = 42.7, RecordedAt = fixedTime },
    };
    Console.WriteLine($"Before — HashSet count (reference equality breaks dedup): {badReadings.Count}"); // 2

    // After — immutable record, value equality, validation.
    var after = new TelemetryNormaliser();
    var reading = new SensorReading("North-7", 4270, fixedTime);
    var normalised = after.Normalise(reading); // returns a new record — original untouched
    Console.WriteLine($"After (new record returned): {normalised.ValueMw}, original still: {reading.ValueMw}");

    var uniqueReadings = new HashSet<SensorReading>
    {
        new SensorReading("North-7", 42.7, fixedTime),
        new SensorReading("North-7", 42.7, fixedTime), // duplicate — removed automatically
        new SensorReading("East-4", 88.3, fixedTime),
    };
    Console.WriteLine($"After — HashSet count (value equality dedups correctly): {uniqueReadings.Count}"); // 2

    try
    {
        _ = new SensorReading("North-7", -5, fixedTime);
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
