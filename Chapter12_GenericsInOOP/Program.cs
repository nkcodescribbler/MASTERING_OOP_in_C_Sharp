// Chapter 12 — Generics in OOP
// Run with: dotnet run --project Chapter12_GenericsInOOP

using OOPBook.Chapter12_GenericsInOOP;

Section1A_WhyGenericsExist();
Section1B_TypeInference();
Section1C_ConstraintSystem();
Section3_CommonMistakes();
Section5_1_GenericClassesAndMethods();
Section5_2_Constraints();
Section5_3_CovarianceAndContravariance();
Section5_5_JitSpecialisation();
Section5_6_LambdasAndClosures();
Section6_CaseStudy();

static void Section1A_WhyGenericsExist()
{
    Header("Section 1A — What Is a Generic Type");

    var cache = new ObjectCache();
    cache.Add("Z001", new GridZone("Z001", "North-7"));
    try
    {
        var permit = (GridPermit)cache.Get("Z001"); // compiles, throws InvalidCastException at runtime
    }
    catch (InvalidCastException)
    {
        Console.WriteLine("Caught InvalidCastException — object-based cache lost type safety.");
    }

    var zoneCache = new GridCache<GridZone>();
    zoneCache.Add("Z001", new GridZone("Z001", "North-7"));
    var zone = zoneCache.Get("Z001"); // returns GridZone — no cast needed
    Console.WriteLine($"Generic cache returned: {zone.Id}");
    // zoneCache.Add("P001", new GridPermit(...)); // compile error — caught before runtime
}

static void Section1B_TypeInference()
{
    Header("Section 1B — Open Types, Closed Types, and Type Inference");

    var activeZone = new GridZone("Z001", "North-7");
    activeZone.Activate();
    var zones = new List<GridZone> { activeZone };
    var permits = new List<GridPermit> { new GridPermit("P001", "North-7") };

    var zone = FindFirst(zones, z => z.IsActive);              // T inferred as GridZone
    var permit = FindFirst(permits, p => p.ZoneCode == "North-7"); // T inferred as GridPermit
    Console.WriteLine($"{zone?.Id}, {permit?.Id}");
}

static T? FindFirst<T>(IEnumerable<T> source, Func<T, bool> predicate) => source.FirstOrDefault(predicate);

static void Section1C_ConstraintSystem()
{
    Header("Section 1C — Generic Interfaces and the Constraint System");

    var processor = new Processor<GridZone>();
    processor.Process(new GridZone("Z002", "East-4")); // T is guaranteed to have IGridEntity members
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var constrained = new ConstrainedProcessor<GridZone>();
    Console.WriteLine(constrained.GetId(new GridZone("Z003", "South-1")));

    var dispatcher = new Dispatcher(); // "generic" but really a type switch
    dispatcher.Process(new GridZone("Z004", "West-1"));

    var generic = new GenericDispatcher(); // fixed via constraint — shared operation
    generic.Process(new GridZone("Z005", "North-7"));

    var typed = new TypedDispatcher(); // fixed via separate overloads
    typed.Process(new GridPermit("P002", "North-7"));

    var flex = new FlexReadingProcessor(); // accepts array, Queue<T>, and lazy LINQ alike
    var arrayReadings = new[] { new SensorReading("North-7", 10, DateTime.UtcNow) };
    var queue = new Queue<SensorReading>();
    flex.ProcessReadings(arrayReadings);
    flex.ProcessReadings(queue);
    flex.ProcessReadings(arrayReadings.Where(r => r.ValueMw > 0));
    Console.WriteLine("FlexReadingProcessor accepted array, Queue<T>, and a lazy LINQ query with no conversion.");

    // for-loop closure trap:
    var zoneCodes = new[] { "North-7", "East-4", "South-2" };
    var handlers = new List<Action>();
    for (int i = 0; i < zoneCodes.Length; i++)
    {
        int captured = i; // fix — each lambda gets its own independent variable slot
        handlers.Add(() => Console.WriteLine(zoneCodes[captured]));
    }
    handlers[0](); // "North-7"
    handlers[1](); // "East-4"
}

static void Section5_1_GenericClassesAndMethods()
{
    Header("Section 5.1 — Generic Classes and Generic Methods");

    var zoneRepo = new Repository<GridZone>();
    var permitRepo = new Repository<GridPermit>(); // two distinct closed types from one generic class
    zoneRepo.Add(new GridZone("Z010", "North-7"));
    permitRepo.Add(new GridPermit("P010", "North-7"));
    Console.WriteLine($"zoneRepo has {zoneRepo.GetAll().Count}, permitRepo has {permitRepo.GetAll().Count}");

    var auditable = new AuditableRepository<GridZone>();
    auditable.Add(new GridZone("Z011", "East-4")); // logs, then delegates to base.Add

    var zoneOnly = new ZoneOnlyRepository();
    zoneOnly.Add(new GridZone("Z012", "North-7"));
    Console.WriteLine($"ZoneOnlyRepository domain query: {zoneOnly.GetByZoneCode("North-7").Count} zone(s)");

    var service = new GridZoneService(new[] { new GridZone("Z013", "North-7") });
    Console.WriteLine($"GridZoneService.GetActiveZone: {service.GetActiveZone("North-7")?.Id ?? "none active"}");
}

static void Section5_2_Constraints()
{
    Header("Section 5.2 — Constraints");

    var filter = new RangeFilter<double>();
    var inRange = filter.Between(new[] { 42.7, 88.3, 102.1, 55.0 }, 50.0, 90.0);
    Console.WriteLine($"In range: {string.Join(", ", inRange)}"); // 88.3, 55.0

    var entityProcessor = new EntityProcessor<GridZone>();
    var created = entityProcessor.CreateDefault(); // requires the `new()` constraint
    entityProcessor.Process(new GridZone("Z020", "North-7"));
    Console.WriteLine($"CreateDefault() produced an empty GridZone: Id='{created.Id}'");

    var converter = new OOPBook.Chapter12_GenericsInOOP.Converter<GridZone, GridPermit>();
    Console.WriteLine($"Converter produced: {converter.Convert(new GridZone("Z021", "North-7")).Id ?? "(empty)"}");
}

static void Section5_3_CovarianceAndContravariance()
{
    Header("Section 5.3 — Covariance and Contravariance");

    var inMemory = new InMemoryRepository<GridZone>();
    inMemory.Add(new GridZone("Z030", "North-7"));

    IReadRepository<GridZone> zoneReader = inMemory;
    IReadRepository<IGridEntity> entityReader = zoneReader; // covariant — safe assignment
    foreach (var e in entityReader.GetAll())
        Console.WriteLine($"{e.Id} — zone: {e.ZoneCode}");

    IEntityEventHandler<IGridEntity> generalHandler = new GeneralAuditHandler();
    IEntityEventHandler<GridZone> zoneHandler = generalHandler; // contravariant
    zoneHandler.OnAdded(new GridZone("Z031", "West-1"));
}

static void Section5_5_JitSpecialisation()
{
    Header("Section 5.5 — JIT Specialisation for Value Types");

    var legacyReadings = new System.Collections.ArrayList(); // pre-generics collection
    legacyReadings.Add(42.7); // boxed — allocates a heap object
    var v = (double)legacyReadings[0]!; // unboxed — heap object becomes garbage

    var modernReadings = new List<double>(); // JIT-specialised, 8-byte element slots
    modernReadings.Add(42.7); // stored directly in the backing double[] — no heap object
    var v2 = modernReadings[0];

    Console.WriteLine($"Legacy (boxed): {v}, Modern (specialised): {v2}");
}

static void Section5_6_LambdasAndClosures()
{
    Header("Section 5.6 — Func<T>, Action<T>, and Closure Capture");

    var zones = new List<GridZone> { new GridZone("Z040", "North-7") };
    zones[0].Activate();
    var allReadings = new List<SensorReading> { new SensorReading("North-7", 88.0, DateTime.UtcNow) };

    Func<GridZone, bool> isActive = zone => zone.IsActive;
    Func<GridZone, string> getZoneCode = zone => zone.ZoneCode;
    IEnumerable<string> activeZoneCodes = zones.Where(isActive).Select(getZoneCode);
    Console.WriteLine($"Active zone codes: {string.Join(", ", activeZoneCodes)}");

    Action<SensorReading> logReading = r => Console.WriteLine($"[{r.RecordedAt:HH:mm:ss}] Zone {r.ZoneCode}: {r.ValueMw} MW");
    allReadings.Where(r => r.ValueMw > 80.0).ToList().ForEach(logReading);

    double threshold = 80.0; // closure captures the VARIABLE, not the value at definition time
    Func<SensorReading, bool> isHighLoad = r => r.ValueMw > threshold;
    Console.WriteLine($"High-load readings: {allReadings.Count(isHighLoad)}");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Eliminating the Duplicate Repository Problem");

    var before = new ZoneRepositoryDuplicated(); // "before" — one of four near-identical hand-written classes
    before.Add(new GridZone("Z100", "North-7"));
    Console.WriteLine($"Before: {before.GetAll().Count} zone(s), duplicated Add/GetById/GetAll/Remove per entity type.");

    var zoneRepo = new InMemoryRepository<GridZone>(); // "after" — one generic implementation for every entity type
    var permitRepo = new InMemoryRepository<GridPermit>();
    zoneRepo.Add(new GridZone("Z200", "North-7"));
    permitRepo.Add(new GridPermit("P200", "North-7"));

    var logger = new BulkAuditLogger();
    logger.LogAll(zoneRepo);   // covariant assignment — IReadRepository<GridZone> -> IReadRepository<IGridEntity>
    logger.LogAll(permitRepo); // same method, different closed type

    try
    {
        zoneRepo.Add(new GridZone("Z200", "East-4")); // duplicate Id — the shared implementation enforces this once, for every entity type
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

