// Chapter 20 — OOP Performance
// Run with: dotnet run --project Chapter20_OOPPerformance

using OOPBook.Chapter20_OOPPerformance;

Section1_1_AllocationCost();
Section3_CommonMistakes();
Section5_1_ValueTypesForHighFrequencyData();
Section5_2_SpanAndMemory();
await Section5_3_ObjectPooling();
Section5_4_SealedAndDevirtualisation();
Section5_5_ServerGcVsWorkstationGc();
await Section5_6_AsyncStateMachines();
Section5_7_MeasurementTools();
await Section6_CaseStudy();

static void Section1_1_AllocationCost()
{
    Header("Section 1.1 — Allocation Cost: \"Just Create an Object\" Is Never Free");

    const int readingCount = 1000; // scaled down from the book's 50,000/sec for a quick demo
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < readingCount; i++)
    {
        var reading = new SensorReadingRecord
        {
            LoadMw = PerformanceMistakeDemos.ReadNextLoad(),
            TakenAt = DateTime.UtcNow,
            ZoneCode = "N7"
        };
        PerformanceMistakeDemos.Process(reading);
    }
    long after = GC.GetAllocatedBytesForCurrentThread();
    Console.WriteLine($"{readingCount} SensorReadingRecord (class) allocations used ~{after - before:N0} bytes — each later requiring the GC to reclaim it.");
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    Console.WriteLine("-- Mistake 1: rewriting a record class as a mutable class with public fields --");
    var mutable = new PermitApplicationMutableFields { Id = Guid.NewGuid(), ApplicantName = "Alex Chen", RequestedCapacityKw = 30m };
    Console.WriteLine($"PermitApplicationMutableFields compiles and runs fine — the point is it was changed with zero supporting evidence: {mutable.ApplicantName}, {mutable.RequestedCapacityKw}kW");

    Console.WriteLine("-- Mistake 2: assuming LINQ is slow, rewriting as a for-loop with no profiler evidence --");
    var readings = Enumerable.Range(0, 500)
        .Select(i => new SensorReading(80.0 + i % 60, DateTime.UtcNow, "N7"))
        .ToList();
    var viaLinq = PerformanceMistakeDemos.GetHighLoadReadingsLinq(readings).Count();
    var viaLoop = PerformanceMistakeDemos.GetHighLoadReadingsForLoop(readings).Count;
    Console.WriteLine($"LINQ found {viaLinq} high-load readings; for-loop found {viaLoop} — same result. Measure before rewriting either.");

    Console.WriteLine("-- Mistake 3: converting to a struct with no consideration of its size --");
    var oversized = new PermitApplicationOversizedStruct
    {
        Id = Guid.NewGuid(),
        ApplicantName = "Alex Chen",
        ApplicantEmail = "alex.chen@example.com",
        ZoneId = "ZONE-12",
        RequestedCapacityKw = 30m,
        SubmittedAt = DateTime.UtcNow
    };
    Console.WriteLine($"PermitApplicationOversizedStruct — 6 fields, ~60+ bytes copied on every call by value: {oversized.ApplicantName}");

    Console.WriteLine("-- Mistake 4: repeatedly allocating a large array that lands on the LOH --");
    using (var source = new MemoryStream(new byte[100_000]))
    {
        var buffer = PerformanceMistakeDemos.ReadTelemetryBatchAllocating(source);
        Console.WriteLine($"Allocated a fresh {buffer.Length:N0}-byte buffer on the LOH for this single call. See Section 5.3 for the pooled fix.");
    }

    Console.WriteLine("-- Mistake 5: a meaningless single-run Stopwatch measurement --");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    _ = TelemetryParser.ParseLine("112.3,2026-07-03T14:22:01,N7");
    sw.Stop();
    Console.WriteLine($"Took {sw.ElapsedMilliseconds}ms — meaningless in isolation (Debug build, no warm-up, single run). See Section 5.7/6 for a disciplined comparison.");
}

static void Section5_1_ValueTypesForHighFrequencyData()
{
    Header("Section 5.1 — Value Types for High-Frequency Data");

    const int readingCount = 1000;
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < readingCount; i++)
    {
        var reading = new SensorReading(PerformanceMistakeDemos.ReadNextLoad(), DateTime.UtcNow, "N7");
        ProcessReading(in reading);
    }
    long after = GC.GetAllocatedBytesForCurrentThread();
    Console.WriteLine($"{readingCount} readonly-struct SensorReading readings processed with ~{after - before:N0} bytes of managed allocation (vs. Section 1.1's class version).");
}

static void ProcessReading(in SensorReading reading)
{
    // 'in' avoids copying all of SensorReading's fields on every call.
    // 'readonly struct' lets the compiler skip defensive copies entirely.
    if (reading.LoadMw > 100.0)
        RaiseHighLoadAlert(reading);
}

static void RaiseHighLoadAlert(SensorReading reading) =>
    Console.WriteLine($"  [ALERT] Zone {reading.ZoneCode}: {reading.LoadMw:F1} MW at {reading.TakenAt:HH:mm:ss}");

static void Section5_2_SpanAndMemory()
{
    Header("Section 5.2 — Span<T> and Memory<T>: Processing Without Allocation");

    const string line = "112.3,2026-07-03T14:22:01,N7";

    var viaSplit = TelemetryParser.ParseLine(line);
    Console.WriteLine($"ParseLine (string.Split): {viaSplit.ZoneCode} @ {viaSplit.LoadMw} MW — allocates string[] + 3 strings per call.");

    var viaSpan = TelemetryParser.ParseLineFast(line.AsSpan());
    Console.WriteLine($"ParseLineFast (Span<char>): {viaSpan.ZoneCode} @ {viaSpan.LoadMw} MW — slices the same memory, only one accepted allocation.");
}

static async Task Section5_3_ObjectPooling()
{
    Header("Section 5.3 — Object Pooling: ArrayPool<T> and MemoryPool<T>");

    using (var source = new MemoryStream(new byte[100_000]))
    {
        int bytesRead = PooledTelemetryReader.ReadTelemetryBatch(source, data =>
            Console.WriteLine($"  Processed {data.Length:N0} bytes from a RENTED buffer — no fresh LOH allocation."));
        Console.WriteLine($"ReadTelemetryBatch (ArrayPool<byte>) processed {bytesRead:N0} bytes.");
    }

    using (var source = new MemoryStream(new byte[100_000]))
    {
        await PooledTelemetryReader.ReadBatchAsync(source, async data =>
        {
            await Task.Yield();
            Console.WriteLine($"  Processed {data.Length:N0} bytes from a RENTED Memory<byte> that survived the await boundary.");
        });
    }
}

static void Section5_4_SealedAndDevirtualisation()
{
    Header("Section 5.4 — sealed and Devirtualisation");

    GridAsset asset = new BatteryBank("BB-04", "North Battery Array", "North-7", 500.0, 100.0);
    var health1 = asset.GetHealth(); // declared type is the BASE class — vtable lookup required
    Console.WriteLine($"Via GridAsset (base-typed reference): health = {health1:F1}%");

    BatteryBank battery = new BatteryBank("BB-04", "North Battery Array", "North-7", 500.0, 100.0);
    var health2 = battery.GetHealth(); // declared type is the SEALED class — devirtualisable
    Console.WriteLine($"Via BatteryBank (sealed, concretely-typed reference): health = {health2:F1}% — the JIT can devirtualise this call.");
}

static void Section5_5_ServerGcVsWorkstationGc()
{
    Header("Section 5.5 — Server GC vs Workstation GC");

    Console.WriteLine($"GCSettings.IsServerGC in this process: {System.Runtime.GCSettings.IsServerGC}");
    Console.WriteLine("Reference only (not compiled) — .csproj setting used to opt in for real, throughput-oriented workloads:");
    Console.WriteLine("""
      <PropertyGroup>
        <ServerGarbageCollection>true</ServerGarbageCollection>
        <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
      </PropertyGroup>
    """);
}

static async Task Section5_6_AsyncStateMachines()
{
    Header("Section 5.6 — Async State Machines and Value-Type Local Variable Promotion");

    var cache = new TelemetryCache();
    cache.Seed("N7", new SensorReading(112.3, DateTime.UtcNow, "N7"));

    var cached = await cache.GetCachedOrFetchAsync("N7"); // synchronous path — no Task allocated
    Console.WriteLine($"GetCachedOrFetchAsync('N7') [cache hit]:  {cached.ZoneCode} @ {cached.LoadMw} MW — completed synchronously.");

    var fetched = await cache.GetCachedOrFetchAsync("N8"); // genuinely async — boxes once, on suspension
    Console.WriteLine($"GetCachedOrFetchAsync('N8') [cache miss]: {fetched.ZoneCode} @ {fetched.LoadMw} MW — awaited a real fetch.");
}

static void Section5_7_MeasurementTools()
{
    Header("Section 5.7 — BenchmarkDotNet and dotMemory: Measurement Tools");

    Console.WriteLine("Reference only (not compiled) — the book measures with BenchmarkDotNet:");
    Console.WriteLine("""
      [MemoryDiagnoser]
      [SimpleJob(RuntimeMoniker.Net80)]
      public class TelemetryParsingBenchmarks
      {
          [Benchmark(Baseline = true)]
          public SensorReading ParseWithSplit() => TelemetryParser.ParseLine(Line);

          [Benchmark]
          public SensorReading ParseWithSpan() => TelemetryParser.ParseLineFast(Line.AsSpan());
      }
    """);
    Console.WriteLine("BenchmarkDotNet is a real, worthwhile package, but a heavy dependency for a single console sample.");
    Console.WriteLine("SimpleBenchmark below follows the same warm-up + many-iterations discipline without the extra package — used next, in the Section 6 case study.");
}

static async Task Section6_CaseStudy()
{
    Header("Section 6 — Case Study: UrbanGrid Telemetry Pipeline — Allocation Reduction");

    const string line = "112.3,2026-07-03T14:22:01,N7";

    Console.WriteLine("Step 1-2 — Baseline benchmark and profiling target: TelemetryParser.ParseLine vs ParseLineFast.");
    SimpleBenchmark.Compare(
        "Step 1-2 baseline",
        baseline: () => TelemetryParser.ParseLine(line),
        candidate: () => TelemetryParser.ParseLineFast(line.AsSpan()));

    Console.WriteLine();
    Console.WriteLine("Step 3 — SensorReading is already a readonly struct (Section 5.1) — no boxing on the hot path.");

    Console.WriteLine();
    Console.WriteLine("Step 4 — Replace string.Split with Span<char>, and cache the ZoneCode (removes ParseLineFast's one remaining allocation).");
    SimpleBenchmark.Compare(
        "Step 4 — Span + ZoneCodeCache vs. original string.Split",
        baseline: () => TelemetryParser.ParseLine(line),
        candidate: () => TelemetryParserCached.ParseLine(line.AsSpan()));

    Console.WriteLine();
    Console.WriteLine("Step 5 — Pool the network buffer (Section 5.3's ArrayPool<byte>) and enable Server GC (Section 5.5) for the ingest pipeline.");
    using (var source = new MemoryStream(new byte[100_000]))
    {
        await PooledTelemetryReader.ReadBatchAsync(source, async data =>
        {
            await Task.CompletedTask;
            Console.WriteLine($"  Ingest pipeline processed {data.Length:N0} bytes with a pooled, awaited buffer.");
        });
    }

    Console.WriteLine();
    Console.WriteLine("Step 6 — Re-benchmark and confirm: the Span+cache version above already shows the allocation drop directly.");

    Console.WriteLine();
    Console.WriteLine("Outcome: the book reports a 99% allocation reduction across all six steps combined —");
    Console.WriteLine("readonly struct + Span<char> parsing + ZoneCode caching + pooled buffers + Server GC, each removing one more source of per-reading allocation.");
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
