// Chapter 1 — Classes, Objects & Object Lifecycle
// Runnable companion to the book chapter. Run each section's demo with:
//   dotnet run --project Chapter01_ClassesObjectsLifecycle
//
// This file walks through the chapter in the same order as the text:
// Section 1 (definition), Section 3 (common mistakes), Section 5
// (method-level detail) and Section 6 (object lifecycle).

using OOPBook.Chapter01_ClassesObjectsLifecycle;

Section1D_ReferenceSemantics();
Section3_CommonMistakes();
Section5_1And5_2_FieldsPropertiesMethods();
Section5_3_ObjectInitialiser();
Section5_4_NullableReferenceTypes();
Section5_5_IDisposablePattern();
Section6_ObjectLifecycle();

// ---------------------------------------------------------------------
// Section 1C / 1D — class definition vs object instance, reference semantics
// ---------------------------------------------------------------------
static void Section1D_ReferenceSemantics()
{
    Header("Section 1D — Reference Semantics: Two Variables, One Object");

    // 1C — one class, three independent zone objects
    var northZone = new GridZone("North-7");   // Zone 1 — its own data
    var eastZone = new GridZone("East-4");     // Zone 2 — its own data
    var southZone = new GridZone("South-1");   // Zone 3 — its own data

    Console.WriteLine($"{northZone.GetZoneCode()}, {eastZone.GetZoneCode()}, {southZone.GetZoneCode()}");

    // 1D — two variables, one object
    var zone1 = new GridZone("North-7");

    // zone2 gets a copy of the same reference — no new zone created
    var zone2 = zone1;
    // We use zone2's reference to deactivate the zone
    zone2.Deactivate();

    Console.WriteLine(zone1.IsActive());                     // false — zone1's reference leads to the same object
    Console.WriteLine(zone2.IsActive());                     // false — same zone, same result
    Console.WriteLine(ReferenceEquals(zone1, zone2));         // true  — confirmed: both point to the same object
}

// ---------------------------------------------------------------------
// Section 3 — Common Mistakes (demonstrated safely via try/catch)
// ---------------------------------------------------------------------
static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake 1: aliasing surprise (same lesson as 1D, restated in the book)
    var zone1 = new GridZone("North-7");
    var zone2 = zone1; // Not a copy — both variables point to the same object
    zone2.Deactivate();
    Console.WriteLine($"Aliasing surprise — zone1.IsActive(): {zone1.IsActive()}"); // false — zone1 was also affected

    // Mistake 2: public fields — broken class invariant
    var substation = new CommonMistakes.PowerSubstationWithPublicFields();
    substation.IsOnline = false;
    substation.LoadMw = 112.3; // Offline substation now shows 112.3 MW load — invariant broken, nobody knows
    Console.WriteLine($"Broken invariant — IsOnline={substation.IsOnline}, LoadMw={substation.LoadMw}");

    // Mistake 3: nullable fields with no enforcement — object initialiser
    // omits a required field silently, causing a NullReferenceException later.
    var permit = new CommonMistakes.GridPermit
    {
        ZoneCode = "North-7"
        // PermitId missing — no error here
        // ExpiryDate missing — no error here
    };

    try
    {
        Console.WriteLine(permit.PermitId!.Length); // NullReferenceException — PermitId is null
    }
    catch (NullReferenceException)
    {
        Console.WriteLine("Caught NullReferenceException — PermitId was never set (this is the pitfall the book warns about).");
    }
}

// ---------------------------------------------------------------------
// Section 5.1 / 5.2 — fields vs properties, methods as domain operations
// ---------------------------------------------------------------------
static void Section5_1And5_2_FieldsPropertiesMethods()
{
    Header("Section 5.1/5.2 — Fields, Properties & Methods");

    var sensor = new GridSensor("SEN-N7-001");
    var substation = new PowerSubstation("SUB-N7-01", "North-7", sensor);

    Console.WriteLine(substation.GetStatus()); // Substation SUB-N7-01 online — 0.0 MW

    substation.RecordLoad(112.3);
    Console.WriteLine(substation.GetStatus());

    substation.GoOffline();
    Console.WriteLine(substation.GetStatus());

    try
    {
        substation.RecordLoad(-5);
    }
    catch (ArgumentOutOfRangeException ex)
    {
        Console.WriteLine($"Caught expected validation error: {ex.Message}");
    }

    substation.Dispose();
}

// ---------------------------------------------------------------------
// Section 5.3 — object initialiser syntax
// ---------------------------------------------------------------------
static void Section5_3_ObjectInitialiser()
{
    Header("Section 5.3 — Object Initialiser Syntax");

    var sensor = new GridAsset
    {
        AssetId = "SEN-N7-001",
        AssetType = "PowerSensor",
        ZoneCode = "North-7"
        // IsActive defaults to true — no need to set it
    };

    Console.WriteLine($"{sensor.AssetId} / {sensor.AssetType} / {sensor.ZoneCode} / active={sensor.IsActive}");
}

// ---------------------------------------------------------------------
// Section 5.4 — nullable reference types
// ---------------------------------------------------------------------
static void Section5_4_NullableReferenceTypes()
{
    Header("Section 5.4 — Nullable Reference Types");

    var zone = new GridZone("North-7");
    Console.WriteLine(zone.GetDisplayName()); // "North-7" — no sector assigned yet

    zone.AssignSector("Industrial");
    Console.WriteLine(zone.GetDisplayName()); // "North-7 (Industrial)"
}

// ---------------------------------------------------------------------
// Section 5.5 — IDisposable pattern, using statement / using declaration
// ---------------------------------------------------------------------
static void Section5_5_IDisposablePattern()
{
    Header("Section 5.5 — IDisposable Pattern");

    // using statement — Dispose() is called when the block exits
    using (var sensorA = new GridSensor("SEN-N7-001"))
    {
        Console.WriteLine($"Using sensor {sensorA.SensorId} inside a using block.");
    }

    // using declaration (C# 8+) — Dispose() is called when the variable goes out of scope
    using var sensorB = new GridSensor("SEN-N7-002");
    Console.WriteLine($"Using sensor {sensorB.SensorId} via a using declaration.");
}

// ---------------------------------------------------------------------
// Section 6 — Object Lifecycle & Memory Management
// ---------------------------------------------------------------------
static void Section6_ObjectLifecycle()
{
    Header("Section 6 — Object Lifecycle");

    // Stage 1: Creation (CLR allocation)
    var sensor = new GridSensor("SEN-N7-001");
    var substation = new PowerSubstation("SUB-N7-01", "North-7", sensor);
    var controlCentre = new GridControlCentre();

    // substation is now reachable through two references
    controlCentre.Register(substation);

    Console.WriteLine(substation.GetStatus()); // Substation SUB-N7-01 online — 0.0 MW
    Console.WriteLine(controlCentre.GetZoneStatus("North-7"));

    // Stage 2: Operation (reachability)
    substation.RecordLoad(112.3);
    Console.WriteLine(substation.GetStatus());

    substation.GoOffline();
    Console.WriteLine(substation.GetStatus());

    // Stage 3: Disposal (resource lifetime) — deterministic via using
    var lifecycleSensor = new GridSensor("SEN-N7-002");
    using (var decommissioned = new PowerSubstation("SUB-N7-02", "North-7", lifecycleSensor))
    {
        decommissioned.RecordLoad(45.2);
        decommissioned.GoOffline();
    }
    // Dispose() called automatically here — sensor connection closed
    Console.WriteLine("Substation decommissioned — Dispose() ran automatically at end of using block.");
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
