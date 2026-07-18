// Chapter 10 — Polymorphism & Pattern Matching
// The GridAsset hierarchy reaches its final shape in the Section 6 "Redesign"
// (GetAlertLevel and GenerateReport both abstract, Clone covariant). That
// final shape is used as the canonical hierarchy everywhere in this file.
// Two mistakes from Section 3 specifically depend on a VIRTUAL (not abstract)
// method with a weak default — those use small, separately named types so
// the canonical hierarchy's abstract contract stays intact.

namespace OOPBook.Chapter10_PolymorphismPatternMatching;

public enum GridAlertLevel { Normal, Warning, High, Critical }

// ----- Section 1A — compile-time polymorphism (method overloading) ---------
public class GridAlert
{
    public GridAlertLevel Level { get; }
    public string Message { get; }
    public GridAlert(GridAlertLevel level, string message) { Level = level; Message = message; }
}

public class GridAlertLogger
{
    public void Log(string message) => Console.WriteLine($"[ALERT] {message}");
    public void Log(GridAlert alert) => Console.WriteLine($"[ALERT] {alert.Level}: {alert.Message}");
    public void Log(GridAlertLevel level, string message) => Console.WriteLine($"[{level}] {message}");
}

// ===========================================================================
// Canonical GridAsset hierarchy (Section 6 "Redesign" shape, used throughout)
// ===========================================================================
public abstract class GridAsset
{
    public string AssetId { get; }
    public string ZoneCode { get; }

    protected GridAsset(string assetId, string zoneCode)
    {
        AssetId = assetId ?? throw new ArgumentNullException(nameof(assetId));
        ZoneCode = zoneCode ?? throw new ArgumentNullException(nameof(zoneCode));
    }

    // abstract — no implementation; every concrete asset MUST provide one
    public abstract string GenerateReport();
    public abstract GridAlertLevel GetAlertLevel();
    public abstract GridAsset Clone();

    // Non-virtual — no override allowed; every asset uses exactly this behaviour
    public string GetIdentity() => $"{AssetId}@{ZoneCode}";
}

public class PowerSubstation : GridAsset
{
    public double LoadMw { get; }
    public bool IsOnline { get; }

    public PowerSubstation(string assetId, string zoneCode, double loadMw, bool isOnline) : base(assetId, zoneCode)
    {
        LoadMw = loadMw;
        IsOnline = isOnline;
    }

    public override string GenerateReport() =>
        IsOnline ? $"Substation {AssetId} | Zone {ZoneCode} | {LoadMw:F1} MW" : $"Substation {AssetId} | Zone {ZoneCode} | offline";

    // sealed override — allows the JIT to devirtualise the call in many cases
    public sealed override GridAlertLevel GetAlertLevel()
    {
        if (!IsOnline) return GridAlertLevel.Normal;
        if (LoadMw > 100.0) return GridAlertLevel.Critical;
        if (LoadMw > 50.0) return GridAlertLevel.High;
        return GridAlertLevel.Normal;
    }

    public override PowerSubstation Clone() => new PowerSubstation(AssetId, ZoneCode, LoadMw, IsOnline); // covariant return
}

public class GridSensor : GridAsset
{
    public bool IsCalibrated { get; }

    public GridSensor(string assetId, string zoneCode, bool isCalibrated) : base(assetId, zoneCode) => IsCalibrated = isCalibrated;

    public override string GenerateReport() => $"Sensor {AssetId} | Zone {ZoneCode} | Calibrated: {IsCalibrated}";
    public override GridAlertLevel GetAlertLevel() => IsCalibrated ? GridAlertLevel.Normal : GridAlertLevel.High;
    public override GridSensor Clone() => new GridSensor(AssetId, ZoneCode, IsCalibrated);
}

public class ZoneMonitor : GridAsset
{
    public bool IsOperational { get; }

    public ZoneMonitor(string assetId, string zoneCode, bool isOperational) : base(assetId, zoneCode) => IsOperational = isOperational;

    public override string GenerateReport() => $"Monitor {AssetId} | Zone {ZoneCode} | Operational: {IsOperational}";
    public override GridAlertLevel GetAlertLevel() => IsOperational ? GridAlertLevel.Normal : GridAlertLevel.Critical;
    public override ZoneMonitor Clone() => new ZoneMonitor(AssetId, ZoneCode, IsOperational);
}

// Section 2 — adding a new type; no existing caller needs to change.
public class SolarRelay : GridAsset
{
    public SolarRelay(string assetId, string zoneCode) : base(assetId, zoneCode) { }
    public override string GenerateReport() => $"Solar relay {AssetId} | Zone {ZoneCode}";
    public override GridAlertLevel GetAlertLevel() => GridAlertLevel.Normal;
    public override SolarRelay Clone() => new SolarRelay(AssetId, ZoneCode);
}

// ----- Section 1B — GridAssetLogger: overload resolution vs virtual dispatch
public class GridAssetLogger
{
    public void Log(GridAsset asset) => Console.WriteLine($"[ASSET] {asset.AssetId}");        // Overload A — base type
    public void Log(PowerSubstation sub) => Console.WriteLine($"[SUB]   {sub.AssetId} at {sub.LoadMw:F1} MW"); // Overload B — derived type
}

// ----- Section 3 — Common Mistakes: type-switch violates OCP ---------------
public class GridAssetSwitcher
{
    // WRONG: type switch on the hierarchy — every new asset type requires editing this method.
    public string GetAssetSummaryWrong(GridAsset asset)
    {
        if (asset is PowerSubstation sub) return $"Substation: {sub.AssetId}, load {sub.LoadMw} MW";
        if (asset is GridSensor sen) return $"Sensor: {sen.AssetId}, calibrated: {sen.IsCalibrated}";
        if (asset is ZoneMonitor mon) return $"Monitor: {mon.AssetId}, zone: {mon.ZoneCode}";
        return "Unknown asset";
        // Add SolarRelay -> must edit this method and every other method like it.
    }

    // CORRECT: polymorphism — caller is stable; new types extend without touching this.
    public string GetAssetSummaryCorrect(GridAsset asset) => asset.GenerateReport();
}

// ----- Section 3 mistakes that need a VIRTUAL (not abstract) base method ---
// Mistake: no override — the base's weak default silently ships to production.
public class GridAssetWeakDefault
{
    public string AssetId { get; }
    public GridAssetWeakDefault(string assetId) => AssetId = assetId;
    public virtual string GenerateReport() => $"Asset {AssetId} — no detail available"; // weak default
}

public class SolarRelayNoOverride : GridAssetWeakDefault // forgot to override — wrong data, no compiler error
{
    public SolarRelayNoOverride(string assetId) : base(assetId) { }
}

// Mistake: `new` hides the base method — does NOT participate in virtual dispatch.
public class GridAssetVirtualReport
{
    public string AssetId { get; }
    public GridAssetVirtualReport(string assetId) => AssetId = assetId;
    public virtual string GenerateReport() => $"Asset {AssetId} — base report";
}

public class GridSensorHidingReport : GridAssetVirtualReport
{
    public GridSensorHidingReport(string assetId) : base(assetId) { }
    public new string GenerateReport() => $"Sensor {AssetId} direct report"; // creates a SEPARATE method, not an override
}

// ----- Section 5.3/5.4/5.5 — pattern matching over the canonical hierarchy -
public class GridAlertRouter
{
    public string RouteAlert(GridAsset asset) => asset switch
    {
        PowerSubstation sub when sub.LoadMw > 100.0 && sub.IsOnline => $"CRITICAL: Substation {sub.AssetId} at {sub.LoadMw:F1} MW",
        PowerSubstation sub when !sub.IsOnline => $"INFO: Substation {sub.AssetId} offline",
        PowerSubstation sub => $"OK: Substation {sub.AssetId} at {sub.LoadMw:F1} MW",
        GridSensor sen when !sen.IsCalibrated => $"WARNING: Sensor {sen.AssetId} uncalibrated",
        GridSensor sen => $"OK: Sensor {sen.AssetId} operating normally",
        _ => $"UNCLASSIFIED: Asset {asset.AssetId} — manual review required"
    };
}

public class GridAssetClassifier
{
    public GridAlertLevel ClassifyAsset(GridAsset asset) => asset switch
    {
        PowerSubstation { IsOnline: true, LoadMw: > 100.0 } => GridAlertLevel.Critical,
        PowerSubstation { IsOnline: true, LoadMw: > 50.0 } => GridAlertLevel.High,
        PowerSubstation { IsOnline: true } => GridAlertLevel.Normal,
        PowerSubstation { IsOnline: false } => GridAlertLevel.Normal,
        GridSensor { IsCalibrated: false } => GridAlertLevel.High,
        _ => GridAlertLevel.Normal
    };
}

public class GridSensorAnalyser
{
    public string EvaluateReadings(double[] readings) => readings switch
    {
        [] => "No readings received",
        [var only] => $"Single reading: {only:F1} MW",
        [var first, var second] when second > first * 2.0 => $"Spike: {first:F1} -> {second:F1} MW",
        [var start, .., var end] when end > 100.0 => $"Sustained high load: {start:F1} -> {end:F1} MW",
        [var head, .. var rest] => $"{1 + rest.Length} readings, lead {head:F1} MW"
    };
}

// ===========================================================================
// Section 6 — Case Study: Alert Processing & Asset Classification
// ===========================================================================

// "The Problem" — a type-switch antipattern operating on the canonical hierarchy.
public class GridEventProcessor
{
    public GridAlertLevel ClassifyAlert(GridAsset asset)
    {
        if (asset is PowerSubstation sub)
        {
            if (!sub.IsOnline) return GridAlertLevel.Normal;
            if (sub.LoadMw > 100.0) return GridAlertLevel.Critical;
            if (sub.LoadMw > 50.0) return GridAlertLevel.High;
            return GridAlertLevel.Normal;
        }
        if (asset is GridSensor sen)
            return sen.IsCalibrated ? GridAlertLevel.Normal : GridAlertLevel.High;
        if (asset is ZoneMonitor mon)
            return mon.IsOperational ? GridAlertLevel.Normal : GridAlertLevel.Critical;

        return GridAlertLevel.Normal;
    }
}

// "The Redesign" — polymorphism for behaviour dispatch, pattern matching for routing.
public class GridControlCentre
{
    public void ProcessEvent(GridAsset asset)
    {
        var level = asset.GetAlertLevel();   // polymorphic — asset owns this
        var report = asset.GenerateReport(); // polymorphic — asset owns this

        string channel = (asset, level) switch
        {
            (PowerSubstation sub, GridAlertLevel.Critical) => $"EMERGENCY DISPATCH | {report}",
            (ZoneMonitor mon, GridAlertLevel.Critical) => $"ON-CALL PAGE | Zone {mon.ZoneCode} monitor down | {report}",
            (GridSensor sen, GridAlertLevel.High) => $"MAINTENANCE QUEUE | Sensor uncalibrated: {sen.AssetId}",
            (_, GridAlertLevel.High) => $"ELEVATED LOG | {report}",
            _ => $"INFO LOG | {report}"
        };

        Console.WriteLine(channel);
    }

    public void ProcessAll(IEnumerable<GridAsset> assets)
    {
        foreach (var asset in assets)
            ProcessEvent(asset);
    }
}
