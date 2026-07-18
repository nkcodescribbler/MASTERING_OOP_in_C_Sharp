using System.Buffers;

namespace OOPBook.Chapter20_OOPPerformance;

// ============================================================================
// Section 1.1 — Allocation Cost
// ============================================================================

// A rushed reimplementation of what Chapter 2 designed correctly as a readonly struct —
// a class, reintroducing exactly the per-reading heap allocation the struct was meant to avoid.
public class SensorReadingRecord
{
    public double LoadMw { get; set; }
    public DateTime TakenAt { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
}

// ============================================================================
// Section 5.1 — Value Types for High-Frequency Data (cross-reference to Chapter 2)
// The canonical SensorReading used throughout the rest of this chapter.
// ============================================================================

public readonly struct SensorReading
{
    public double LoadMw { get; }
    public DateTime TakenAt { get; }
    public string ZoneCode { get; }

    public SensorReading(double loadMw, DateTime takenAt, string zoneCode) =>
        (LoadMw, TakenAt, ZoneCode) = (loadMw, takenAt, zoneCode);
}

// ============================================================================
// Section 3 — Common Mistakes
// ============================================================================

// Mistake 1: rewriting a validated, immutable record class into a plain mutable class with
// public fields on a "feeling" that property access is slow — with zero supporting evidence.
public class PermitApplicationMutableFields // was: public record class PermitApplication (Chapter 16)
{
    public Guid Id;                    // was: an init-only positional property
    public string ApplicantName = "";  // was: a validated, immutable property
    public decimal RequestedCapacityKw;
}

// Mistake 3: converting to a struct with no consideration of size, identity, or how it is
// passed around. Six fields, 60+ bytes — this struct is copied in full on every method call.
public struct PermitApplicationOversizedStruct // was: public record class PermitApplication (Chapter 16)
{
    public Guid Id;
    public string ApplicantName;
    public string ApplicantEmail;
    public string ZoneId;
    public decimal RequestedCapacityKw;
    public DateTime SubmittedAt;
}

public static class PerformanceMistakeDemos
{
    private static readonly Random _rng = new(42);

    public static double ReadNextLoad() => 80.0 + _rng.NextDouble() * 60.0;

    public static void Process(SensorReadingRecord reading) { /* simulated processing work */ }

    // Mistake 2: assuming the LINQ query is the slow part and hand-rewriting it as a for-loop,
    // with no profiler evidence. Both versions are provided so the point can be demonstrated
    // rather than just asserted — see Program.cs for the actual timing comparison.
    public static IEnumerable<SensorReading> GetHighLoadReadingsLinq(List<SensorReading> readings) =>
        readings.Where(r => r.LoadMw > 100.0);

    public static List<SensorReading> GetHighLoadReadingsForLoop(List<SensorReading> readings)
    {
        var result = new List<SensorReading>();
        foreach (var r in readings)
            if (r.LoadMw > 100.0)
                result.Add(r);
        return result;
    }

    // Mistake 4: repeatedly allocating large arrays (>= 85,000 bytes) that land on the Large
    // Object Heap, which historically is not compacted by default.
    public static byte[] ReadTelemetryBatchAllocating(Stream source)
    {
        var buffer = new byte[100_000]; // lands on the LOH every call
        source.Read(buffer, 0, buffer.Length);
        return buffer;
    }
}

// ============================================================================
// Section 5.2 — Span<T> and Memory<T>: Processing Without Allocation
// Both methods are reused as-is by the Section 5.7 benchmark and Section 6 case study.
// ============================================================================

public static class TelemetryParser
{
    // BAD — string.Split allocates a new string[] AND a new string per token, every call.
    public static SensorReading ParseLine(string line)
    {
        var parts = line.Split(','); // allocates string[3] + 3 new strings
        return new SensorReading(double.Parse(parts[0]), DateTime.Parse(parts[1]), parts[2]);
    }

    // GOOD — Span<char> slices the SAME underlying memory, allocating nothing (aside from
    // the one accepted string allocation for ZoneCode, which Section 6 removes via caching).
    public static SensorReading ParseLineFast(ReadOnlySpan<char> line)
    {
        int firstComma = line.IndexOf(',');
        int secondComma = line.Slice(firstComma + 1).IndexOf(',') + firstComma + 1;

        return new SensorReading(
            double.Parse(line.Slice(0, firstComma)),
            DateTime.Parse(line.Slice(firstComma + 1, secondComma - firstComma - 1)),
            line.Slice(secondComma + 1).ToString()); // one accepted allocation — Section 6 removes it
    }
}

// ============================================================================
// Section 5.3 — Object Pooling: ArrayPool<T> and MemoryPool<T>
// ============================================================================

public delegate void SpanProcessor(ReadOnlySpan<byte> data);

public static class PooledTelemetryReader
{
    // GOOD — the fix for Section 3 Mistake 4's LOH-fragmenting buffer allocation.
    public static int ReadTelemetryBatch(Stream source, SpanProcessor process)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(100_000); // reused, not freshly allocated
        try
        {
            int bytesRead = source.Read(buffer, 0, buffer.Length);
            process(buffer.AsSpan(0, bytesRead));
            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer); // MUST return in a finally — Chapter 19's
        }                                            // "using guarantees disposal" discipline
    }

    // MemoryPool<T> — the equivalent pattern for Memory<T>, useful when the buffer must
    // survive an await boundary.
    public static async Task ReadBatchAsync(Stream source, Func<ReadOnlyMemory<byte>, Task> process)
    {
        using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(100_000);
        int bytesRead = await source.ReadAsync(owner.Memory);
        await process(owner.Memory.Slice(0, bytesRead));
    }
}

// ============================================================================
// Section 5.4 — sealed and Devirtualisation (resolving Chapter 9's forward reference)
// Minimal local restatement of Chapter 9's GridAsset/BatteryBank, since each chapter
// project in this solution is self-contained.
// ============================================================================

public abstract class GridAsset
{
    public string Id { get; }
    public string Name { get; }
    public string ZoneCode { get; }

    protected GridAsset(string id, string name, string zoneCode)
    {
        Id = id;
        Name = name;
        ZoneCode = zoneCode;
    }

    public abstract double GetHealth();
}

// sealed — the JIT can devirtualise GetHealth() when the declared (compile-time) type is
// BatteryBank itself, rather than the base GridAsset, because no further override is possible.
public sealed class BatteryBank : GridAsset
{
    public double CapacityKwh { get; }
    public double CurrentChargeKwh { get; }

    public BatteryBank(string id, string name, string zoneCode, double capacityKwh, double currentChargeKwh)
        : base(id, name, zoneCode)
    {
        CapacityKwh = capacityKwh;
        CurrentChargeKwh = currentChargeKwh;
    }

    public override double GetHealth() => CapacityKwh <= 0 ? 0 : CurrentChargeKwh / CapacityKwh * 100.0;
}

// ============================================================================
// Section 5.6 — Async State Machines and Value-Type Local Variable Promotion
// ============================================================================

public class TelemetryCache
{
    private readonly Dictionary<string, SensorReading> _cache = new();

    public void Seed(string zoneId, SensorReading reading) => _cache[zoneId] = reading;

    public async ValueTask<SensorReading> GetCachedOrFetchAsync(string zoneId)
    {
        if (_cache.TryGetValue(zoneId, out SensorReading cached))
            return cached; // synchronous path — no Task allocated, no state machine boxed

        return await FetchLatestReadingAsync(zoneId); // genuinely async — boxes once, on suspension
    }

    private async Task<SensorReading> FetchLatestReadingAsync(string zoneId)
    {
        await Task.Delay(5); // simulates a real network/database fetch
        var reading = new SensorReading(123.4, DateTime.UtcNow, zoneId);
        _cache[zoneId] = reading;
        return reading;
    }
}

// ============================================================================
// Section 5.7 — Measurement Tools
// The book uses BenchmarkDotNet ([MemoryDiagnoser], [Benchmark]) for statistically sound,
// Release-mode, warmed-up measurement. That's a real and worthwhile package, but a heavy,
// output-format-changing dependency for a single console sample. SimpleBenchmark below is a
// small, dependency-free stand-in that follows the same discipline the book calls out in
// Section 3's Mistake 5 (warm up first, run many iterations, avoid Debug-build noise) and is
// reused by the Section 6 case study to actually measure the allocation reduction.
// ============================================================================

public static class SimpleBenchmark
{
    public static void Compare(string label, Action baseline, Action candidate, int iterations = 100_000)
    {
        // Warm-up — pays the JIT compilation cost before any measurement starts.
        baseline();
        candidate();

        long allocBeforeBaseline = GC.GetAllocatedBytesForCurrentThread();
        var swBaseline = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) baseline();
        swBaseline.Stop();
        long allocBaseline = GC.GetAllocatedBytesForCurrentThread() - allocBeforeBaseline;

        long allocBeforeCandidate = GC.GetAllocatedBytesForCurrentThread();
        var swCandidate = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) candidate();
        swCandidate.Stop();
        long allocCandidate = GC.GetAllocatedBytesForCurrentThread() - allocBeforeCandidate;

        Console.WriteLine($"{label} ({iterations:N0} iterations):");
        Console.WriteLine($"  Baseline  (ParseWithSplit): {swBaseline.ElapsedMilliseconds}ms, ~{allocBaseline:N0} bytes allocated");
        Console.WriteLine($"  Candidate (ParseWithSpan) : {swCandidate.ElapsedMilliseconds}ms, ~{allocCandidate:N0} bytes allocated");
    }
}

// ============================================================================
// Section 6 — Case Study, Step 4: Replace string.Split with Span<char>, Cache the ZoneCode
// ============================================================================

public static class ZoneCodeCache
{
    private static readonly string[] _known = { "N1", "N2", "N3", "N4", "N5", "N6", "N7", "N8", "N9", "N10" };

    public static string Resolve(ReadOnlySpan<char> zoneSpan)
    {
        foreach (var known in _known)
            if (zoneSpan.Equals(known, StringComparison.Ordinal))
                return known; // cached reference — zero allocation

        return zoneSpan.ToString(); // unrecognised code — rare, one-time cost
    }
}

public static class TelemetryParserCached
{
    // Step 4's final form: Span-based parsing plus ZoneCodeCache — the one remaining
    // allocation from ParseLineFast is now gone for every recognised zone code.
    public static SensorReading ParseLine(ReadOnlySpan<char> line)
    {
        int firstComma = line.IndexOf(',');
        int secondComma = line.Slice(firstComma + 1).IndexOf(',') + firstComma + 1;

        return new SensorReading(
            double.Parse(line.Slice(0, firstComma)),
            DateTime.Parse(line.Slice(firstComma + 1, secondComma - firstComma - 1)),
            ZoneCodeCache.Resolve(line.Slice(secondComma + 1)));
    }
}
