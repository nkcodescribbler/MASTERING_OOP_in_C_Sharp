// Chapter 9 — Inheritance
// GridAsset and its four sealed leaf types (PowerTransformer, CircuitBreaker,
// SolarPanel, BatteryBank) reach their final, complete shape in the Section 6
// case study — that shape is used as the canonical GridAsset hierarchy
// throughout. Earlier sections' isolated mechanics demos (new-hiding, the
// fragile base class problem, sealed methods, virtual calls in constructors)
// use small, distinctly-named standalone types so each lesson stays isolated.

namespace OOPBook.Chapter09_Inheritance;

public enum AssetHealth { Healthy, Warning, Critical }

// ===========================================================================
// Section 6 — canonical GridAsset hierarchy (also used for 5.3/5.6 demos)
// ===========================================================================
public abstract class GridAsset
{
    public string AssetId { get; }
    public string Name { get; }
    public string ZoneId { get; }
    public DateTime RegisteredAt { get; }

    protected GridAsset(string assetId, string name, string zoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId, nameof(assetId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId, nameof(zoneId));
        AssetId = assetId;
        Name = name;
        ZoneId = zoneId;
        RegisteredAt = DateTime.UtcNow;
    }

    // virtual: base provides a default; derived classes may override
    public virtual string GetStatusSummary() => $"[{AssetId}] {Name} in {ZoneId}";

    // abstract: no base implementation; derived classes must override
    public abstract AssetHealth GetHealth();
    public abstract GridAsset Clone();

    // sealed override: no derived class can override this further
    public sealed override string ToString() => $"{GetType().Name}:{AssetId}";

    // [neither virtual nor abstract]: fixed — cannot be overridden
    public bool IsInZone(string z) => string.Equals(ZoneId, z, StringComparison.OrdinalIgnoreCase);
}

public sealed class PowerTransformer : GridAsset
{
    public double CapacityMVA { get; }
    public double PrimaryKV { get; }
    public double SecondaryKV { get; }
    private double _currentLoadMVA;

    public PowerTransformer(string assetId, string name, string zoneId, double capacityMVA, double primaryKV, double secondaryKV)
        : base(assetId, name, zoneId)
    {
        if (capacityMVA <= 0) throw new ArgumentOutOfRangeException(nameof(capacityMVA));
        if (primaryKV <= 0) throw new ArgumentOutOfRangeException(nameof(primaryKV));
        if (secondaryKV <= 0) throw new ArgumentOutOfRangeException(nameof(secondaryKV));
        CapacityMVA = capacityMVA;
        PrimaryKV = primaryKV;
        SecondaryKV = secondaryKV;
    }

    public void RecordLoad(double loadMVA)
    {
        if (loadMVA < 0 || loadMVA > CapacityMVA * 1.2) throw new ArgumentOutOfRangeException(nameof(loadMVA));
        _currentLoadMVA = loadMVA;
    }

    // Full replacement override — does not call base.GetStatusSummary()
    public override string GetStatusSummary() =>
        $"[{AssetId}] {Name} | {PrimaryKV}/{SecondaryKV} kV | Load: {_currentLoadMVA:F1}/{CapacityMVA:F1} MVA ({_currentLoadMVA / CapacityMVA * 100.0:F0}%)";

    public override AssetHealth GetHealth()
    {
        var r = CapacityMVA > 0 ? _currentLoadMVA / CapacityMVA : 0;
        return r switch { > 0.95 => AssetHealth.Critical, > 0.80 => AssetHealth.Warning, _ => AssetHealth.Healthy };
    }

    // Covariant return (C# 9+) — no cast needed by callers that know the concrete type
    public override PowerTransformer Clone() => new PowerTransformer(AssetId, Name + "-CLONE", ZoneId, CapacityMVA, PrimaryKV, SecondaryKV);
}

public sealed class CircuitBreaker : GridAsset
{
    public double TripCurrentA { get; }
    public bool IsTripped { get; private set; }

    public CircuitBreaker(string assetId, string name, string zoneId, double tripCurrentA) : base(assetId, name, zoneId)
    {
        if (tripCurrentA <= 0) throw new ArgumentOutOfRangeException(nameof(tripCurrentA));
        TripCurrentA = tripCurrentA;
    }

    public void Trip() => IsTripped = true;
    public void Reset() => IsTripped = false;

    // Extension override — builds on the base implementation via base.GetStatusSummary()
    public override string GetStatusSummary()
    {
        var b = base.GetStatusSummary();
        return $"{b} | Tripped: {IsTripped} | Trip: {TripCurrentA:F0} A";
    }

    public override AssetHealth GetHealth() => IsTripped ? AssetHealth.Critical : AssetHealth.Healthy;
    public override CircuitBreaker Clone() => new CircuitBreaker(AssetId, Name + "-CLONE", ZoneId, TripCurrentA);
}

public sealed class SolarPanel : GridAsset
{
    public double PeakOutputKW { get; }
    public int PanelCount { get; }
    private double _currentOutputKW;

    public SolarPanel(string assetId, string name, string zoneId, double peakOutputKW, int panelCount) : base(assetId, name, zoneId)
    {
        if (peakOutputKW <= 0) throw new ArgumentOutOfRangeException(nameof(peakOutputKW));
        if (panelCount <= 0) throw new ArgumentOutOfRangeException(nameof(panelCount));
        PeakOutputKW = peakOutputKW;
        PanelCount = panelCount;
    }

    public void RecordOutput(double kw)
    {
        if (kw < 0 || kw > PeakOutputKW) throw new ArgumentOutOfRangeException(nameof(kw));
        _currentOutputKW = kw;
    }

    public override string GetStatusSummary() =>
        $"[{AssetId}] {Name} | Output: {_currentOutputKW:F1}/{PeakOutputKW:F1} kW | Panels: {PanelCount}";

    public override AssetHealth GetHealth() => _currentOutputKW < PeakOutputKW * 0.05 ? AssetHealth.Warning : AssetHealth.Healthy;
    public override SolarPanel Clone() => new SolarPanel(AssetId, Name + "-CLONE", ZoneId, PeakOutputKW, PanelCount);
}

public sealed class BatteryBank : GridAsset
{
    public double CapacityKWh { get; }
    public double MaxChargeKW { get; }
    private double _socKWh;

    public BatteryBank(string assetId, string name, string zoneId, double capacityKWh, double maxChargeKW) : base(assetId, name, zoneId)
    {
        if (capacityKWh <= 0) throw new ArgumentOutOfRangeException(nameof(capacityKWh));
        if (maxChargeKW <= 0) throw new ArgumentOutOfRangeException(nameof(maxChargeKW));
        CapacityKWh = capacityKWh;
        MaxChargeKW = maxChargeKW;
    }

    public void SetStateOfCharge(double kWh)
    {
        if (kWh < 0 || kWh > CapacityKWh) throw new ArgumentOutOfRangeException(nameof(kWh));
        _socKWh = kWh;
    }

    public override string GetStatusSummary()
    {
        var pct = CapacityKWh > 0 ? _socKWh / CapacityKWh * 100.0 : 0;
        return $"[{AssetId}] {Name} | SoC: {_socKWh:F1}/{CapacityKWh:F1} kWh ({pct:F0}%)";
    }

    public override AssetHealth GetHealth()
    {
        var soc = CapacityKWh > 0 ? _socKWh / CapacityKWh : 0;
        return soc switch { < 0.10 => AssetHealth.Critical, < 0.20 => AssetHealth.Warning, _ => AssetHealth.Healthy };
    }

    public override BatteryBank Clone() => new BatteryBank(AssetId, Name + "-CLONE", ZoneId, CapacityKWh, MaxChargeKW);
}

// public class SmartBatteryBank : BatteryBank { } // CS0509 — BatteryBank is sealed

// ----- Section 3 — Common Mistakes ------------------------------------------

// Mistake: inheriting for reuse rather than identity ("is-a" test fails).
public class ReportGenerator : GridAsset
{
    public ReportGenerator(string id) : base(id, "Report", "N/A") { }

    // Smell: the compiler forces an implementation that makes no sense for a report.
    public override AssetHealth GetHealth() => throw new NotSupportedException("ReportGenerator is not an asset.");
    public override GridAsset Clone() => throw new NotSupportedException("ReportGenerator is not an asset.");
}

// The fix: composition — ZoneAssetReportGenerator has-a list of assets, rather than is-a one.
public sealed class ZoneAssetReportGenerator
{
    private readonly IReadOnlyList<GridAsset> _assets;
    public ZoneAssetReportGenerator(IReadOnlyList<GridAsset> assets) => _assets = assets;
    public IEnumerable<string> Summaries => _assets.Select(a => a.GetStatusSummary());
}

// Mistake: an override that skips the base implementation vs. one that extends it.
public class GridZoneStub
{
    public double RemainingCapacityMVA { get; set; } = 100;
    public void NotifyNewHighVoltageAsset(object asset) => Console.WriteLine($"  Zone notified of new HV asset: {asset}");
}

public abstract class RegisterableAssetBase
{
    public virtual void RegisterWithZone(GridZoneStub zone)
    {
        if (zone.RemainingCapacityMVA <= 0)
            throw new InvalidOperationException("Zone has no remaining capacity.");
        Console.WriteLine("  Base validation passed — capacity check OK.");
    }
}

public class HighVoltageAssetBad : RegisterableAssetBase
{
    public GridZoneStub? RegisteredZone { get; private set; }
    public override void RegisterWithZone(GridZoneStub zone) => RegisteredZone = zone; // skips base — forgets the capacity check
}

public class HighVoltageAssetGood : RegisterableAssetBase
{
    public GridZoneStub? RegisteredZone { get; private set; }

    public override void RegisterWithZone(GridZoneStub zone)
    {
        base.RegisterWithZone(zone); // base runs first
        RegisteredZone = zone;
        zone.NotifyNewHighVoltageAsset(this); // derived adds on top
    }
}

// Mistake: base constructor calling a virtual method before derived fields are set.
public abstract class GridAssetDangerousCtor
{
    public string AssetId { get; }
    public AssetHealth InitialHealthSnapshot { get; }

    protected GridAssetDangerousCtor(string assetId)
    {
        AssetId = assetId;
        InitialHealthSnapshot = GetHealth(); // DANGEROUS: derived override runs here, but derived fields are still default
    }

    public abstract AssetHealth GetHealth();
}

public sealed class BatteryBankDangerousCtor : GridAssetDangerousCtor
{
    private readonly double _capacityKWh; // NOT set when the base constructor runs

    public BatteryBankDangerousCtor(string assetId, double capacityKWh) : base(assetId)
    {
        _capacityKWh = capacityKWh; // set AFTER base ctor — too late for InitialHealthSnapshot
    }

    public override AssetHealth GetHealth() => _capacityKWh > 0 ? AssetHealth.Healthy : AssetHealth.Critical; // may be wrong at construction time!
}

// Mistake: `new` hides rather than overrides — dispatch depends on declared type, not actual type.
public class GridAssetHidingDemo
{
    public string GetStatusSummary() => "Base summary"; // not virtual
}

public class CircuitBreakerHidingDemo : GridAssetHidingDemo
{
    public new string GetStatusSummary() => "Breaker summary"; // hides, does not override
}

// GridSensor is standalone — deliberately NOT part of the GridAsset hierarchy.
public class GridSensor
{
    public string AssetId { get; }
    public GridSensor(string assetId) => AssetId = assetId;
    public string GetStatusSummary() => $"[{AssetId}] Sensor"; // NOT virtual
}

public class TemperatureSensor : GridSensor
{
    public double TemperatureC { get; private set; }
    public TemperatureSensor(string assetId) : base(assetId) { }
    public void RecordReading(double temp) => TemperatureC = temp;
    public new string GetStatusSummary() => $"[{AssetId}] Temp: {TemperatureC:F1}°C"; // hides — surprising when accessed via GridSensor
}

// ----- Section 5.4 — the fragile base class problem -------------------------
// V1: IsHealthy() and GetHealth() are independent — MonitoredTransformerFragile
// assumes IsHealthy() never calls GetHealth().
public class GridAssetFragileV1
{
    protected int CheckCount = 0;
    public virtual bool IsHealthy() { CheckCount++; return true; }
    public virtual AssetHealth GetHealth() { CheckCount++; return AssetHealth.Healthy; }
}

public class MonitoredTransformerFragile : GridAssetFragileV1
{
    public override AssetHealth GetHealth() { CheckCount++; return base.GetHealth(); } // two increments per call
    public int ObservedCheckCount => CheckCount;
}
// An "innocent" refactor of the base class — redefining IsHealthy() to call
// GetHealth() internally — would silently change ObservedCheckCount from 1 to 3
// for MonitoredTransformerFragile, because IsHealthy() now also triggers the
// (already-overridden) GetHealth() chain. That refactor is not reproduced here
// (it would require redeclaring the same class), but is exactly the risk the
// stable version below is designed to eliminate.

// The fix — a non-virtual public API in front of a protected virtual core:
public abstract class GridAssetStable
{
    protected int CheckCount = 0;

    public bool IsHealthy() // non-virtual — stable public API, safe to refactor internally
    {
        CheckCount++;
        return GetHealthCore() == AssetHealth.Healthy;
    }

    protected virtual AssetHealth GetHealthCore() => AssetHealth.Healthy;
}

public class MonitoredTransformerStable : GridAssetStable
{
    protected override AssetHealth GetHealthCore() { CheckCount++; return base.GetHealthCore(); } // one increment — predictable
    public int ObservedCheckCount => CheckCount;
}

// ----- Section 5.5 — sealed methods in an abstract intermediate class -------
public interface ISimpleLogger<T>
{
    void LogDebug(string message);
}

public class ConsoleLogger<T> : ISimpleLogger<T>
{
    public void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
}

public abstract class MonitoredGridAssetSealed : GridAsset
{
    protected readonly ISimpleLogger<MonitoredGridAssetSealed> Log;

    protected MonitoredGridAssetSealed(string assetId, string name, string zoneId, ISimpleLogger<MonitoredGridAssetSealed> log)
        : base(assetId, name, zoneId)
    {
        ArgumentNullException.ThrowIfNull(log);
        Log = log;
    }

    // sealed: no further derived class can override GetStatusSummary again
    public sealed override string GetStatusSummary()
    {
        var summary = base.GetStatusSummary();
        Log.LogDebug($"Status requested for {AssetId}");
        return summary;
    }

    // still unimplemented — remains abstract for further derived classes
    public abstract override AssetHealth GetHealth();
}

public sealed class MonitoredPowerTransformer : MonitoredGridAssetSealed
{
    public MonitoredPowerTransformer(string assetId, string name, string zoneId, ISimpleLogger<MonitoredGridAssetSealed> log)
        : base(assetId, name, zoneId, log) { }

    public override AssetHealth GetHealth() => AssetHealth.Healthy; // placeholder — real version checks transformer load
    public override GridAsset Clone() => new MonitoredPowerTransformer(AssetId, Name + "-CLONE", ZoneId, Log);
    // public override string GetStatusSummary() // CS0239 — cannot override sealed member
}

// ===========================================================================
// Section 6 — The Inspection Service
// ===========================================================================
public record AssetInspectionResult(string AssetId, string AssetType, string Summary, AssetHealth Health);

public record ZoneInspectionReport(string ZoneId, IReadOnlyList<AssetInspectionResult> Results, DateTime InspectedAt)
{
    public int TotalAssets => Results.Count;
    public int CriticalCount => Results.Count(r => r.Health == AssetHealth.Critical);
    public int WarningCount => Results.Count(r => r.Health == AssetHealth.Warning);
    public int HealthyCount => Results.Count(r => r.Health == AssetHealth.Healthy);
}

public sealed class ZoneInspectionService
{
    private readonly IReadOnlyList<GridAsset> _assets;
    private readonly ISimpleLogger<ZoneInspectionService> _log;

    public ZoneInspectionService(IReadOnlyList<GridAsset> assets, ISimpleLogger<ZoneInspectionService> log)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(log);
        _assets = assets;
        _log = log;
    }

    public ZoneInspectionReport InspectZone(string zoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId, nameof(zoneId));
        var results = _assets
            .Where(a => a.IsInZone(zoneId))
            .Select(asset => new AssetInspectionResult(
                AssetId: asset.AssetId,
                AssetType: asset.GetType().Name,
                Summary: asset.GetStatusSummary(), // virtual -> correct type dispatched
                Health: asset.GetHealth()           // abstract -> correct type dispatched
            )).ToList();

        var critical = results.Where(r => r.Health == AssetHealth.Critical).ToList();
        if (critical.Count > 0)
            _log.LogDebug($"{critical.Count} critical in {zoneId}: {string.Join(", ", critical.Select(r => r.AssetId))}");

        return new ZoneInspectionReport(zoneId, results, DateTime.UtcNow);
    }

    public IReadOnlyList<GridAsset> CloneForSimulation(string zoneId) =>
        _assets.Where(a => a.IsInZone(zoneId)).Select(a => a.Clone()).ToList().AsReadOnly();
}
