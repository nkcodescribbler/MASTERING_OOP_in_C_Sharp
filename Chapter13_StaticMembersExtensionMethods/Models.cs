// Chapter 13 — Static Members & Extension Methods
// The case study's SmtpAlertNotifier is included as reference code (it
// compiles) but is never invoked from Program.cs — this project has no test
// SMTP server, so the runnable demo uses a ConsoleAlertNotifier instead.
// GridHelpers.SendAlert is similarly present for reference but not called.

namespace OOPBook.Chapter13_StaticMembersExtensionMethods;

public enum PermitType { Installation, Maintenance, Emergency }
public enum AlertLevel { Invalid, Normal, Warning, Critical }

public interface IGridEntity { string Id { get; } string ZoneCode { get; } }

// ----- Section 1A — static members alongside instance members ---------------
public class GridZone : IGridEntity
{
    private static int _zoneCount = 0; // ONE slot shared by ALL GridZone objects
    private readonly string _zoneCode;
    private bool _isActive;

    public GridZone(string id, string zoneCode)
    {
        Id = id;
        _zoneCode = zoneCode;
        _zoneCount++; // increments the shared counter — not thread-safe, see Section 5.4
    }

    public string Id { get; }
    public string ZoneCode => _zoneCode; // exposes IGridEntity.ZoneCode
    public void Activate() => _isActive = true;
    public void Deactivate() => _isActive = false;
    public bool IsActive => _isActive;

    public static int ZoneCount => _zoneCount;
    public static bool IsValidZoneCode(string code) => !string.IsNullOrWhiteSpace(code) && code.Length <= 10;

    // Section 3 — instance state needs an instance method; static methods take explicit input instead.
    // public static string GetDisplayCode() => _zoneCode.ToUpperInvariant(); // CS0120 — cannot access instance field from static method
    public string GetDisplayCode() => _zoneCode.ToUpperInvariant();
    public static string FormatCode(string? raw) => raw?.Trim().ToUpperInvariant() ?? string.Empty;
}

// ----- Section 1B — static utility class -------------------------------------
public static class GridMath
{
    public const double KilowattsPerMegawatt = 1000.0;
    public static double MegawattsToKilowatts(double megawatts) => megawatts * KilowattsPerMegawatt;
    public static double KilowattsToMegawatts(double kilowatts) => kilowatts / KilowattsPerMegawatt;
    public static bool IsWithinCapacity(double currentMw, double maxMw) => currentMw >= 0 && currentMw <= maxMw;
}
// new GridMath(); // compile error — static class cannot be instantiated

// ----- Section 1C / 3 — extension methods (final, null-safe version) --------
public record SensorReading(string ZoneCode, double ValueMw, DateTime Timestamp);

public static class SensorReadingExtensions
{
    // Replaces the Section 1C version — adds an explicit null guard (Section 3 fix).
    public static bool IsAlertLevel(this SensorReading? reading, double thresholdMw)
    {
        if (reading is null) return false;
        return reading.ValueMw > thresholdMw;
    }

    public static string ToDisplayString(this SensorReading reading) =>
        $"[{reading.ZoneCode}] {reading.ValueMw:F1} MW at {reading.Timestamp:HH:mm:ss}";
}

// ----- Section 2A — static factory methods on the type itself ---------------
public class GridPermit : IGridEntity
{
    public string Id { get; }
    public string ZoneCode { get; }
    public PermitType Type { get; }
    public DateTime ExpiryDate { get; }

    private GridPermit(string id, string zoneCode, PermitType type, TimeSpan validity)
    {
        Id = id;
        ZoneCode = zoneCode;
        Type = type;
        ExpiryDate = DateTime.UtcNow.Add(validity);
    }

    public static GridPermit ForInstallation(string id, string zoneCode) => new GridPermit(id, zoneCode, PermitType.Installation, TimeSpan.FromDays(365));
    public static GridPermit ForMaintenance(string id, string zoneCode) => new GridPermit(id, zoneCode, PermitType.Maintenance, TimeSpan.FromDays(180));
    public static GridPermit ForEmergency(string id, string zoneCode) => new GridPermit(id, zoneCode, PermitType.Emergency, TimeSpan.FromDays(7));
}

// ----- Section 2B — a good, focused static utility ---------------------------
public static class ZoneCodeParser
{
    public static bool TryParse(string code, out string region, out int number)
    {
        region = string.Empty;
        number = 0;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var parts = code.Split('-');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[1], out number)) return false;

        region = parts[0];
        return true;
    }

    public static bool IsValidFormat(string code) => TryParse(code, out _, out _);
}

// ===========================================================================
// Section 5 — Method-Level Detail
// ===========================================================================

// 5.1 — pure-function static utility class.
public static class SensorAnalysis
{
    public const double CriticalThresholdMw = 150.0;
    public const double WarningThresholdMw = 100.0;
    public const double MinimumReadingMw = 0.0;

    public static AlertLevel ClassifyReading(double valueMw)
    {
        if (valueMw < MinimumReadingMw) return AlertLevel.Invalid;
        if (valueMw >= CriticalThresholdMw) return AlertLevel.Critical;
        if (valueMw >= WarningThresholdMw) return AlertLevel.Warning;
        return AlertLevel.Normal;
    }

    public static double NormaliseToPercentage(double valueMw, double capacityMw)
    {
        if (capacityMw <= 0) throw new ArgumentOutOfRangeException(nameof(capacityMw));
        return Math.Clamp(valueMw / capacityMw * 100.0, 0.0, 100.0);
    }

    // Nullable Max() avoids double-enumeration of IEnumerable; returns 0.0 on empty sequence.
    public static double PeakReading(IEnumerable<SensorReading> readings) => readings.Max(r => (double?)r.ValueMw) ?? 0.0;
}

// 5.2 — static constructor and initialisation order.
public static class ZoneRegistry
{
    private static readonly Dictionary<string, string> _zoneRegions;
    private static readonly HashSet<string> _criticalZones;

    static ZoneRegistry()
    {
        _zoneRegions = new Dictionary<string, string>
        {
            ["North-7"] = "Northern Grid",
            ["East-4"] = "Eastern Grid",
        };
        _criticalZones = new HashSet<string> { "North-7", "East-4" };
    }

    public static bool IsCritical(string zoneCode) => _criticalZones.Contains(zoneCode);
    public static string? GetRegion(string zoneCode) => _zoneRegions.TryGetValue(zoneCode, out var r) ? r : null;
}

public static class BadOrder
{
    public static readonly string Full = $"{Base}/Platform"; // Base is null here at this point in initialisation order -> "/Platform"
    public static readonly string Base = "UrbanGrid";        // initialised AFTER Full
}

public static class GoodOrder
{
    public static readonly string Base = "UrbanGrid";  // declared first — initialised first
    public static readonly string Full = $"{Base}/Platform";
}

// 5.3 — extension methods: declaration, discovery, naming conventions.
public static class GridPermitExtensions
{
    public static bool IsExpired(this GridPermit permit) => permit.ExpiryDate < DateTime.UtcNow;
    public static bool IsExpiredWithin(this GridPermit permit, TimeSpan window) => permit.ExpiryDate < DateTime.UtcNow.Add(window);

    public static string ToStatusSummary(this GridPermit permit) =>
        $"[{permit.Id}] {permit.ZoneCode} — {(permit.IsExpired() ? "EXPIRED" : "Active")} (expires {permit.ExpiryDate:yyyy-MM-dd})";
}

public static class GridEntityExtensions
{
    public static bool IsInZone(this IGridEntity entity, string zoneCode) =>
        string.Equals(entity.ZoneCode, zoneCode, StringComparison.OrdinalIgnoreCase);

    public static string ToAuditKey(this IGridEntity entity) => $"{entity.GetType().Name}:{entity.Id}";
}

// 5.3b — static abstract interface members (C# 11 / .NET 7+).
public interface IGridMeasure<T> where T : IGridMeasure<T>
{
    static abstract T operator +(T left, T right);
    static abstract T Zero { get; }
}

public readonly struct GridPower : IGridMeasure<GridPower>
{
    public decimal Megawatts { get; }
    public GridPower(decimal megawatts) => Megawatts = megawatts;

    public static GridPower operator +(GridPower a, GridPower b) => new(a.Megawatts + b.Megawatts);
    public static GridPower Zero => new(0m);
}

public static class GridMeasureHelper
{
    // Generic algorithm — works for any IGridMeasure<T>; T.Zero and operator+ resolved at compile time.
    public static T SumReadings<T>(IEnumerable<T> values) where T : IGridMeasure<T>
    {
        var total = T.Zero;
        foreach (var v in values) total = total + v;
        return total;
    }
}

// 5.4 — static field thread safety. Kept as small, distinct types (the book
// notes GridPermit's production form, from Section 2A, uses a private
// constructor + factory methods; this is a deliberately simplified variant).
public class GridPermitCounterUnsafe
{
    private static int _totalIssued = 0;
    public static int TotalIssued => _totalIssued;

    public GridPermitCounterUnsafe(string id, string zoneCode)
    {
        _totalIssued++; // RACE CONDITION — not atomic
    }
}

public class GridPermitCounterSafe
{
    private static int _totalIssued = 0;
    public static int TotalIssued => System.Threading.Volatile.Read(ref _totalIssued); // prevents stale CPU-cached reads

    public GridPermitCounterSafe(string id, string zoneCode)
    {
        System.Threading.Interlocked.Increment(ref _totalIssued); // atomic — no race condition possible
    }

    public static void Reset() => System.Threading.Interlocked.Exchange(ref _totalIssued, 0);
}

// 5.5 — decision guide: static vs instance vs extension, illustrated via determinism.
public static class GridAlertClassifierBefore
{
    public static bool IsPeakLoadHour(int hour) => hour >= 8 && hour <= 20; // already pure — takes int, no clock dependency

    public static AlertLevel ClassifyByTime(double mw)
    {
        var hour = DateTime.UtcNow.Hour; // always "now" — non-deterministic, hard to test
        if (hour >= 22 || hour < 6) return AlertLevel.Critical;
        return SensorAnalysis.ClassifyReading(mw);
    }
}

public static class GridAlertClassifierAfter
{
    public static bool IsPeakLoadHour(int hour) => hour >= 8 && hour <= 20; // already pure — no change needed

    public static AlertLevel ClassifyByTime(double mw, DateTime asOf)
    {
        var hour = asOf.Hour; // caller supplies the timestamp — pure function, fully testable
        if (hour >= 22 || hour < 6) return AlertLevel.Critical;
        return SensorAnalysis.ClassifyReading(mw);
    }
}

// ===========================================================================
// Section 6 — Case Study: From Static Sprawl to Clean Design
// ===========================================================================

// "Before" — the GridHelpers anti-pattern: shared mutable state, hardwired I/O,
// and unrelated responsibilities crammed into one static class.
public static class GridHelpers
{
    private static int _permitsProcessed = 0; // shared mutable state — bleeds across tests
    private static int _alertsSent = 0;

    public static AlertLevel ClassifyReading(double mw)
    {
        if (mw >= 150.0) return AlertLevel.Critical;
        if (mw >= 100.0) return AlertLevel.Warning;
        return AlertLevel.Normal;
    }

    public static void ProcessPermit(GridPermit permit)
    {
        _permitsProcessed++; // not thread-safe
        Console.WriteLine($"Processed: {permit.Id}");
    }

    public static int PermitsProcessed => _permitsProcessed;

    // Present for reference — not invoked from Program.cs (no test SMTP server available).
    public static void SendAlert(string message)
    {
        _alertsSent++;
        // using var smtp = new System.Net.Mail.SmtpClient("smtp.urbangrid.io");
        // smtp.Send(new System.Net.Mail.MailMessage("noreply@urbangrid.io", "alerts@urbangrid.io", "Alert", message));
        // SmtpClient hardwired — no injection point, cannot mock.
    }
}

// "After" — decomposed into focused, correctly-typed members.
public interface IAlertNotifier { Task NotifyAsync(string zone, string message); }

public class SmtpConfiguration
{
    public string SmtpHost { get; init; } = "smtp.urbangrid.io";
    public int SmtpPort { get; init; } = 587;
    public string AlertsAddress { get; init; } = "alerts@urbangrid.io";
}

// Present for reference — matches the book's production shape. Not invoked
// from Program.cs; ConsoleAlertNotifier is used instead for the runnable demo.
public class SmtpAlertNotifier : IAlertNotifier
{
    private readonly SmtpConfiguration _config;
    public SmtpAlertNotifier(SmtpConfiguration config) => _config = config;

    public async Task NotifyAsync(string zone, string message)
    {
        using var smtp = new System.Net.Mail.SmtpClient(_config.SmtpHost, _config.SmtpPort);
        using var mail = new System.Net.Mail.MailMessage("noreply@urbangrid.io", _config.AlertsAddress)
        {
            Subject = $"[{zone}] Grid Alert",
            Body = message
        };
        await smtp.SendMailAsync(mail);
    }
}

public class ConsoleAlertNotifier : IAlertNotifier
{
    public Task NotifyAsync(string zone, string message)
    {
        Console.WriteLine($"[ALERT -> {zone}] {message}");
        return Task.CompletedTask;
    }
}

public class PermitProcessor
{
    private readonly IAlertNotifier _notifier;
    private int _processedThisRequest = 0;

    public PermitProcessor(IAlertNotifier notifier) => _notifier = notifier;

    public async Task ProcessAsync(GridPermit permit)
    {
        _processedThisRequest++;
        if (permit.IsExpired())
            await _notifier.NotifyAsync(permit.ZoneCode, $"Permit {permit.Id} is expired");
    }

    public int ProcessedCount => _processedThisRequest;
}
