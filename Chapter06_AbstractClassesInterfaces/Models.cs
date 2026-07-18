// Chapter 6 — Abstract Classes & Interfaces
// GridAssetBase, its three subtypes (PowerSubstation/SolarFarm/WindTurbine)
// and IAlertNotifier's implementors are shared across Sections 1, 5 and the
// Section 6 case study — the book reuses these exact types throughout.

namespace OOPBook.Chapter06_AbstractClassesInterfaces;

public enum AssetStatus { Offline, Online, Maintenance, Faulted }
public enum AssetHealthStatus { Healthy, Degraded, Critical }
public enum AlertLevel { Info, Warning, Critical }

// Supporting value types — full record coverage in Chapter 11
public record AlertMessage(string ZoneCode, AlertLevel Level, string Body);
public record ZoneHealthReport(string ZoneCode, bool IsHealthy, string Summary);

/// <summary>Section 1 — the abstract base every UrbanGrid asset derives from.</summary>
public abstract class GridAssetBase
{
    public string AssetId { get; }
    public string ZoneCode { get; }
    public AssetStatus Status { get; protected set; } = AssetStatus.Offline;
    public string AssetLabel => $"[{AssetId}] {ZoneCode}";

    private readonly List<string> _eventLog = new();
    public IReadOnlyList<string> EventLog => _eventLog.AsReadOnly();

    protected GridAssetBase(string assetId, string zoneCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId, nameof(assetId));
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneCode, nameof(zoneCode));
        AssetId = assetId.ToUpperInvariant();
        ZoneCode = zoneCode.ToUpperInvariant();
    }

    // Abstract — each asset type provides its own implementation
    public abstract double ReadCurrentOutput();
    public abstract AssetHealthStatus GetHealthStatus();

    // Virtual — default shutdown; subtypes may override
    public virtual void Shutdown()
    {
        Status = AssetStatus.Offline;
        LogEvent("Shutdown initiated");
    }

    protected void LogEvent(string message) =>
        _eventLog.Add($"[{DateTime.UtcNow:O}] {AssetId}: {message}");
}

// GridAssetBase asset = new GridAssetBase(...); // CS0144 — cannot create instance of abstract class
// public class BrokenSubstation : GridAssetBase {} // CS0534 — missing required overrides

/// <summary>Section 5.1 — three concrete asset types, each with different output behaviour.</summary>
public class PowerSubstation : GridAssetBase
{
    private double _outputMW;

    public PowerSubstation(string id, string zone, double outputMW) : base(id, zone)
    {
        _outputMW = outputMW;
        Status = AssetStatus.Online;
    }

    public override double ReadCurrentOutput() => Status == AssetStatus.Online ? _outputMW : 0;

    public override AssetHealthStatus GetHealthStatus() =>
        Status == AssetStatus.Online && _outputMW > 0 ? AssetHealthStatus.Healthy :
        Status == AssetStatus.Faulted ? AssetHealthStatus.Critical :
        AssetHealthStatus.Degraded;

    // Override: substation must clear load before going offline
    public override void Shutdown()
    {
        _outputMW = 0;
        LogEvent("Load cleared to zero"); // inherited protected method
        base.Shutdown();                  // delegate to base implementation
    }
}

public class SolarFarm : GridAssetBase
{
    private readonly double _peakMW;

    public SolarFarm(string id, string zone, double peakMW) : base(id, zone) =>
        (_peakMW, Status) = (peakMW, AssetStatus.Online);

    public override double ReadCurrentOutput()
    {
        if (Status != AssetStatus.Online) return 0;
        int hour = DateTime.UtcNow.Hour;
        return hour is >= 6 and <= 18 ? _peakMW * 0.75 : 0.0;
    }

    public override AssetHealthStatus GetHealthStatus() =>
        Status == AssetStatus.Online ? AssetHealthStatus.Healthy :
        Status == AssetStatus.Faulted ? AssetHealthStatus.Critical :
        AssetHealthStatus.Degraded;
    // No Shutdown() override — inherits the base implementation as-is
}

public class WindTurbine : GridAssetBase
{
    private readonly double _ratedMW;
    private double _windFactor = 0.6;

    public WindTurbine(string id, string zone, double ratedMW) : base(id, zone) =>
        (_ratedMW, Status) = (ratedMW, AssetStatus.Online);

    public void UpdateWindConditions(double factor)
    {
        _windFactor = Math.Clamp(factor, 0.0, 1.0);
        LogEvent($"Wind factor updated: {_windFactor:P0}");
    }

    public override double ReadCurrentOutput() => Status == AssetStatus.Online ? _ratedMW * _windFactor : 0;

    public override AssetHealthStatus GetHealthStatus() =>
        Status == AssetStatus.Online ? AssetHealthStatus.Healthy : AssetHealthStatus.Degraded;
}

/// <summary>Section 1 / 5.3 — interface with a default member (C# 8+).</summary>
public interface IAlertNotifier
{
    Task NotifyAsync(AlertMessage message, CancellationToken ct = default);
    bool CanReach(string recipient);

    // Default — all existing implementors receive this without any code change,
    // unless they choose to override it with something more efficient (see PushNotifier).
    async Task NotifyBatchAsync(IEnumerable<AlertMessage> messages, CancellationToken ct = default)
    {
        foreach (var msg in messages)
            await NotifyAsync(msg, ct); // calls each type's own NotifyAsync
    }
}

public interface IZoneHealthChecker
{
    ZoneHealthReport CheckHealth(string zoneCode);
}

public interface IGridSensor
{
    double ReadCurrentLoad();
    bool IsResponding { get; }
}

public class SmsNotifier : IAlertNotifier
{
    private string _apiKey;
    public SmsNotifier(string apiKey) => _apiKey = apiKey;
    public void UpdateApiKey(string newKey) => _apiKey = newKey; // concrete-type-only member

    public async Task NotifyAsync(AlertMessage message, CancellationToken ct = default)
    {
        await Task.Delay(20, ct);
        Console.WriteLine($"[SMS]   {message.ZoneCode} | {message.Level} | {message.Body}");
    }

    public bool CanReach(string recipient) =>
        !string.IsNullOrWhiteSpace(recipient) && recipient.StartsWith("+");
}

public class EmailNotifier : IAlertNotifier
{
    public async Task NotifyAsync(AlertMessage message, CancellationToken ct = default)
    {
        await Task.Delay(10, ct);
        Console.WriteLine($"[Email] {message.ZoneCode} | {message.Level} | {message.Body}");
    }

    public bool CanReach(string recipient) => recipient.Contains('@');
}

public class PushNotifier : IAlertNotifier
{
    private List<string> _deviceTokens = new();
    public void SetDeviceTokens(IEnumerable<string> tokens) => _deviceTokens = tokens.ToList();
    public bool CanReach(string recipient) => recipient.StartsWith("device:");

    public async Task NotifyAsync(AlertMessage message, CancellationToken ct = default) =>
        await SendSingleAsync(message, ct);

    // Overrides the interface's default — uses the push platform's batch endpoint
    public async Task NotifyBatchAsync(IEnumerable<AlertMessage> messages, CancellationToken ct = default) =>
        await SendBulkAsync(messages, ct); // single round-trip

    private Task SendSingleAsync(AlertMessage m, CancellationToken ct) => Task.CompletedTask;
    private Task SendBulkAsync(IEnumerable<AlertMessage> msgs, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Section 1 — implicit (CheckHealth) vs explicit (IAlertNotifier members) implementation.</summary>
public class GridMonitor : IAlertNotifier, IZoneHealthChecker
{
    // Explicit — only reachable as ((IAlertNotifier)monitor).NotifyAsync(...)
    Task IAlertNotifier.NotifyAsync(AlertMessage message, CancellationToken ct) => Task.CompletedTask;

    bool IAlertNotifier.CanReach(string recipient) => recipient.Contains('@');

    // Implicit — reachable as monitor.CheckHealth(...) directly
    public ZoneHealthReport CheckHealth(string zoneCode) =>
        new ZoneHealthReport(zoneCode, IsHealthy: true, Summary: "All sensors nominal");
}

/// <summary>Section 2 — loosely coupled service, depends on the interface, not a concrete notifier.</summary>
public class ZoneAlertService
{
    private readonly IAlertNotifier _notifier;
    private const string OnCallOperator = "ops@urbangrid.io";

    public ZoneAlertService(IAlertNotifier notifier) => _notifier = notifier;

    public async Task RaiseAlertAsync(string zoneCode, AlertLevel level, string body)
    {
        if (!_notifier.CanReach(OnCallOperator)) return;
        await _notifier.NotifyAsync(new AlertMessage(zoneCode, level, body));
    }
}

// ----- Section 3 — Common Mistakes: the "fat interface" anti-pattern -------
public interface IGridComponent
{
    double ReadCurrentOutput();
    AssetHealthStatus GetHealthStatus();
    void Shutdown();
    Task NotifyAsync(AlertMessage message, CancellationToken ct = default);
    bool CanReach(string recipient);
    ZoneHealthReport CheckHealth(string zoneCode);
}

// A SensorDriver implementing IGridComponent is forced to write stubs for
// capabilities it doesn't conceptually own (alerting, health-check reporting).
public class SensorDriver : IGridComponent
{
    public double ReadCurrentOutput() => 0.0;
    public AssetHealthStatus GetHealthStatus() => AssetHealthStatus.Healthy;
    public void Shutdown() { }
    public ZoneHealthReport CheckHealth(string z) => new(z, true, "OK");
    public Task NotifyAsync(AlertMessage m, CancellationToken ct = default) =>
        throw new NotImplementedException(); // no alert capability
    public bool CanReach(string recipient) =>
        throw new NotImplementedException(); // no reach-check capability
}

// The fix — segregated, focused contracts (Section 5.4):
public interface IReadableAsset { double ReadCurrentOutput(); AssetHealthStatus GetHealthStatus(); }
public interface IShutdownable { void Shutdown(); }

// ----- Section 5.5 — covariance & contravariance ----------------------------
public interface IAssetReader<out TAsset> where TAsset : GridAssetBase
{
    TAsset Read(string assetId);
    IEnumerable<TAsset> ReadAll();
}

public class SolarFarmReader : IAssetReader<SolarFarm>
{
    private readonly List<SolarFarm> _farms = new() { new SolarFarm("SOL-01", "ALPHA-7", 40.0) };
    public SolarFarm Read(string assetId) => _farms.First(f => f.AssetId == assetId);
    public IEnumerable<SolarFarm> ReadAll() => _farms;
}

public interface IAssetProcessor<in TAsset> where TAsset : GridAssetBase
{
    void Process(TAsset asset);
}

public class GridAssetProcessor : IAssetProcessor<GridAssetBase>
{
    public void Process(GridAssetBase asset) => Console.WriteLine(asset.AssetLabel);
}

// ===========================================================================
// Section 6 — Case Study: UrbanGrid Asset Monitoring & Alert Pipeline
// ===========================================================================
public class ZoneOutputMonitor
{
    private readonly List<GridAssetBase> _assets = new();
    private readonly List<IAlertNotifier> _notifiers = new();
    private readonly double _thresholdMW;
    private const string OnCallOperator = "ops@urbangrid.io";

    public ZoneOutputMonitor(double thresholdMW) => _thresholdMW = thresholdMW;

    public void RegisterAsset(GridAssetBase asset) => _assets.Add(asset);
    public void RegisterNotifier(IAlertNotifier n) => _notifiers.Add(n);

    public async Task EvaluateZoneAsync(string zoneCode, CancellationToken ct = default)
    {
        var zone = zoneCode.ToUpperInvariant(); // normalise once — used everywhere below
        var zoneAssets = _assets.Where(a => a.ZoneCode == zone).ToList();
        double total = zoneAssets.Sum(a => a.ReadCurrentOutput());

        var health = zoneAssets.ToDictionary(a => a, a => a.GetHealthStatus());
        bool hasCritical = health.Values.Any(h => h == AssetHealthStatus.Critical);

        if (total < _thresholdMW || hasCritical)
        {
            var level = hasCritical ? AlertLevel.Critical : AlertLevel.Warning;
            var body = hasCritical
                ? $"Critical asset in {zone}. Output: {total:F1} MW"
                : $"Output below threshold in {zone}: {total:F1} MW (min {_thresholdMW} MW)";

            foreach (var notifier in _notifiers)
            {
                if (notifier.CanReach(OnCallOperator))
                {
                    await notifier.NotifyAsync(new AlertMessage(zone, level, body), ct);
                    break;
                }
            }

            foreach (var asset in zoneAssets.Where(a => health[a] == AssetHealthStatus.Critical))
                asset.Shutdown();
        }
        else
        {
            Console.WriteLine($"[{zoneCode}] OK — {total:F1} MW across {zoneAssets.Count} asset(s)");
        }
    }
}
