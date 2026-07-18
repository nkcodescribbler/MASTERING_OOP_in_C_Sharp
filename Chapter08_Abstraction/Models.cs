// Chapter 8 — Abstraction: Concept, Strategies & Decision Guide
// The book's Section 6 case study uses ILogger<T> (Microsoft.Extensions.Logging)
// and a DI container (services.AddSingleton<...>) to wire everything up. To
// keep this project dependency-free, ILogger<T> is replaced with a minimal
// ISimpleLogger<T> (same pattern used in Chapter 4), and the DI container
// registration is replaced with plain manual construction in Program.cs —
// the abstraction lessons themselves are unaffected either way.

namespace OOPBook.Chapter08_Abstraction;

public enum AlertSeverity { Info, Warning, Critical }
public enum ZoneStatus { Offline, Active, Overloaded, Faulted }

public record AlertMessage(
    string AlertId,
    string ZoneId,
    AlertSeverity Severity,
    string Text,
    string OperatorPhone,
    string OperatorEmail,
    string OperatorDeviceToken,
    DateTime RaisedAt);

public interface ISimpleLogger<T>
{
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(Exception ex, string message);
}

public class ConsoleLogger<T> : ISimpleLogger<T>
{
    public void LogInformation(string message) => Console.WriteLine($"[INFO] {message}");
    public void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
    public void LogError(Exception ex, string message) => Console.WriteLine($"[ERROR] {message} :: {ex.Message}");
}

public class GridOperator
{
    public string Name { get; init; } = string.Empty;
}

public class GridZone
{
    public string ZoneId { get; init; } = string.Empty;
    public ZoneStatus Status { get; init; }
    public double CurrentLoadMW { get; init; }
    public double CapacityMW { get; init; }
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    public GridOperator? AssignedOperator { get; init; }
}

public record ZoneEvent(string ZoneId, string Description, DateTime OccurredAt);

// ===========================================================================
// Section 1/6 Part 1 — the alert-channel abstraction
// ===========================================================================
public interface IAlertChannel
{
    string ChannelName { get; } // identifies the channel in logs and diagnostics
    bool CanDeliver(AlertSeverity severity);
    Task SendAsync(AlertMessage message, CancellationToken ct = default);
}

// Minimal stand-ins for the book's external gateway/client dependencies —
// no real network calls, just enough behaviour to make the channels runnable.
public class SmsGatewayClient
{
    public Task SendSmsAsync(string phone, string body, CancellationToken ct = default)
    {
        Console.WriteLine($"  (gateway) SMS -> {phone}: {body}");
        return Task.CompletedTask;
    }
}

public interface IEmailClient
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public class FakeEmailClient : IEmailClient
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Console.WriteLine($"  (gateway) Email -> {to}: {subject}");
        return Task.CompletedTask;
    }
}

public interface IPushProvider
{
    Task SendAsync(string deviceToken, string body, CancellationToken ct = default);
}

public class FakePushProvider : IPushProvider
{
    public Task SendAsync(string deviceToken, string body, CancellationToken ct = default)
    {
        Console.WriteLine($"  (gateway) Push -> {deviceToken}: {body}");
        return Task.CompletedTask;
    }
}

public sealed class SmsAlertChannel : IAlertChannel
{
    private readonly SmsGatewayClient _gateway;
    private readonly ISimpleLogger<SmsAlertChannel> _log;

    public string ChannelName => "SMS";

    public SmsAlertChannel(SmsGatewayClient gateway, ISimpleLogger<SmsAlertChannel> log)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(log);
        _gateway = gateway;
        _log = log;
    }

    public bool CanDeliver(AlertSeverity severity) => true; // catch-all: SMS delivers every severity, including Critical

    public async Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        _log.LogInformation($"Sending SMS alert {message.AlertId} for zone {message.ZoneId}");

        // SMS: keep first 152 chars + "..." = 155 chars (within 160-char limit)
        var smsBody = message.Text.Length > 155 ? message.Text[..152] + "..." : message.Text;

        await _gateway.SendSmsAsync(message.OperatorPhone, smsBody, ct);
    }
}

public sealed class EmailAlertChannel : IAlertChannel
{
    private readonly IEmailClient _email;
    private readonly ISimpleLogger<EmailAlertChannel> _log;

    public string ChannelName => "Email";

    public EmailAlertChannel(IEmailClient email, ISimpleLogger<EmailAlertChannel> log)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(log);
        _email = email;
        _log = log;
    }

    public bool CanDeliver(AlertSeverity severity) => severity is AlertSeverity.Warning or AlertSeverity.Info;

    public async Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        _log.LogInformation($"Sending email alert {message.AlertId} for zone {message.ZoneId}");
        await _email.SendAsync(
            to: message.OperatorEmail,
            subject: $"UrbanGrid Alert — Zone {message.ZoneId} — {message.Severity}",
            body: $"<h2>Zone {message.ZoneId}</h2><p>{message.Severity}: {message.Text}</p>",
            ct: ct);
    }
}

public sealed class PushNotificationChannel : IAlertChannel
{
    private readonly IPushProvider _push;
    private readonly ISimpleLogger<PushNotificationChannel> _log;

    public string ChannelName => "Push";

    public PushNotificationChannel(IPushProvider push, ISimpleLogger<PushNotificationChannel> log)
    {
        ArgumentNullException.ThrowIfNull(push);
        ArgumentNullException.ThrowIfNull(log);
        _push = push;
        _log = log;
    }

    // Critical alerts and warnings go via push; Info stays on email
    public bool CanDeliver(AlertSeverity severity) => severity is AlertSeverity.Critical or AlertSeverity.Warning;

    public async Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        _log.LogInformation($"Sending push alert {message.AlertId} for zone {message.ZoneId}");
        await _push.SendAsync(message.OperatorDeviceToken, $"[{message.Severity}] Zone {message.ZoneId}: {message.Text}", ct);
    }
}

// Test double (Section 2) — implements the contract, records calls instead of sending
public sealed class FakeAlertChannel : IAlertChannel
{
    private readonly List<AlertMessage> _sent = new();
    public string ChannelName => "Fake";
    public bool CanDeliver(AlertSeverity severity) => true;

    public Task SendAsync(AlertMessage message, CancellationToken ct = default)
    {
        _sent.Add(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AlertMessage> SentMessages => _sent.AsReadOnly();
}

// Production dispatcher (Section 6 Part 1) — supersedes the simplified S2 teaching version.
public sealed class AlertDispatcher
{
    private readonly IReadOnlyList<IAlertChannel> _channels;
    private readonly ISimpleLogger<AlertDispatcher> _log;

    public AlertDispatcher(IEnumerable<IAlertChannel> channels, ISimpleLogger<AlertDispatcher> log)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(log);
        var channelList = channels.ToList();
        if (channelList.Count == 0)
            throw new ArgumentException("At least one channel must be configured.", nameof(channels));
        _channels = channelList.AsReadOnly();
        _log = log;
    }

    public async Task DispatchAsync(AlertMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var eligibleChannels = _channels.Where(c => c.CanDeliver(message.Severity)).ToList();

        if (eligibleChannels.Count == 0)
        {
            _log.LogWarning($"No channel available for severity {message.Severity}. Alert {message.AlertId} dropped.");
            return;
        }

        _log.LogInformation(
            $"Dispatching alert {message.AlertId} via {eligibleChannels.Count} channel(s): " +
            string.Join(", ", eligibleChannels.Select(c => c.ChannelName)));

        await Task.WhenAll(eligibleChannels.Select(c => SendSafeAsync(c, message, ct)));
    }

    private async Task SendSafeAsync(IAlertChannel channel, AlertMessage message, CancellationToken ct)
    {
        try { await channel.SendAsync(message, ct); }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Channel {channel.ChannelName} failed to deliver alert {message.AlertId}");
        }
    }
}

// ----- Section 3 — Common Mistakes ------------------------------------------
// Unnecessary abstraction: one implementation, no benefit.
public interface IGridZoneService
{
    GridZone? GetById(string zoneId);
    IReadOnlyList<GridZone> GetAll();
}

public sealed class GridZoneService : IGridZoneService // only implementation — the interface adds no value here
{
    private readonly List<GridZone> _zones;
    public GridZoneService(IEnumerable<GridZone> zones) => _zones = zones.ToList();
    public GridZone? GetById(string zoneId) => _zones.FirstOrDefault(z => z.ZoneId == zoneId);
    public IReadOnlyList<GridZone> GetAll() => _zones.AsReadOnly();
}

// ===========================================================================
// Section 5.1 — Abstraction through interfaces
// ===========================================================================
public class DataSourceUnavailableException : Exception
{
    public DataSourceUnavailableException(string message) : base(message) { }
}

// Clean abstraction (Section 5.1/5.5) — designed from the caller's need, not
// from any specific data source's structure.
public interface IZoneDataSource
{
    Task<GridZone?> GetByIdAsync(string zoneId, CancellationToken ct = default);
    Task<IReadOnlyList<GridZone>> GetActiveZonesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ZoneEvent>> GetRecentEventsAsync(string zoneId, TimeSpan window, CancellationToken ct = default);
}

public sealed class InMemoryZoneDataSource : IZoneDataSource
{
    private readonly List<GridZone> _zones;
    private readonly List<ZoneEvent> _events;

    public InMemoryZoneDataSource(IEnumerable<GridZone> zones, IEnumerable<ZoneEvent>? events = null)
    {
        _zones = zones.ToList();
        _events = events?.ToList() ?? new List<ZoneEvent>();
    }

    public Task<GridZone?> GetByIdAsync(string zoneId, CancellationToken ct = default) =>
        Task.FromResult(_zones.FirstOrDefault(z => z.ZoneId == zoneId));

    public Task<IReadOnlyList<GridZone>> GetActiveZonesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GridZone>>(_zones.Where(z => z.Status == ZoneStatus.Active).ToList());

    public Task<IReadOnlyList<ZoneEvent>> GetRecentEventsAsync(string zoneId, TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        IReadOnlyList<ZoneEvent> recent = _events.Where(e => e.ZoneId == zoneId && e.OccurredAt >= cutoff).ToList();
        return Task.FromResult(recent);
    }
}

// ----- explicit interface implementation for two competing contracts -------
public interface IZoneEventSink { void Record(string eventText); } // operational stream
public interface IZoneAuditLog { void Record(string eventText); }  // compliance trail

public interface IEventStream { void Append(string text); }
public interface IAuditStore { void WriteImmutable(string text); }

public class ConsoleEventStream : IEventStream
{
    public void Append(string text) => Console.WriteLine(text);
}

public class ConsoleAuditStore : IAuditStore
{
    public void WriteImmutable(string text) => Console.WriteLine(text);
}

public sealed class ZoneActivityRecorder : IZoneEventSink, IZoneAuditLog
{
    private readonly IEventStream _eventStream;
    private readonly IAuditStore _auditStore;

    public ZoneActivityRecorder(IEventStream stream, IAuditStore store)
    {
        _eventStream = stream;
        _auditStore = store;
    }

    void IZoneEventSink.Record(string eventText) => _eventStream.Append($"[EVENT] {DateTime.UtcNow:o} {eventText}");
    void IZoneAuditLog.Record(string eventText) => _auditStore.WriteImmutable($"[AUDIT] {DateTime.UtcNow:o} {eventText}");
}

// ----- Section 5.3 — premature abstraction, before and after ---------------
public interface IZoneLoadCalculator { double Calculate(GridZone zone); }

public sealed class ZoneLoadCalculator : IZoneLoadCalculator // before: one implementation, no benefit
{
    public double Calculate(GridZone zone) => LoadPercentage(zone);
    public static double LoadPercentage(GridZone zone) => zone.CapacityMW <= 0 ? 0 : zone.CurrentLoadMW / zone.CapacityMW * 100.0;
}

public static class ZoneLoadMetrics // after: direct implementation, zero abstraction overhead
{
    public static double LoadPercentage(GridZone zone) => zone.CapacityMW <= 0 ? 0 : zone.CurrentLoadMW / zone.CapacityMW * 100.0;
}

// ===========================================================================
// Section 6 Part 2 — Zone Reporting: abstract class + Template Method
// ===========================================================================
public record ReportSection(string Name, string Content);
public record ZoneHealthReport(string ZoneId, IReadOnlyList<ReportSection> Sections, DateTime GeneratedAt);

public class ZoneNotFoundException : Exception
{
    public ZoneNotFoundException(string zoneId) : base($"Zone {zoneId} not found.") { }
}

public abstract class ZoneHealthReporter
{
    private readonly IZoneDataSource _dataSource;
    private readonly ISimpleLogger<ZoneHealthReporter> _log;

    protected ZoneHealthReporter(IZoneDataSource dataSource, ISimpleLogger<ZoneHealthReporter> log)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(log);
        _dataSource = dataSource;
        _log = log;
    }

    // Template method — not virtual, so derived classes cannot change the sequence
    public async Task<ZoneHealthReport> GenerateAsync(string zoneId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        _log.LogInformation($"{GetType().Name} generating report for zone {zoneId}");
        var zone = await _dataSource.GetByIdAsync(zoneId, ct) ?? throw new ZoneNotFoundException(zoneId);

        await ValidateForFormatAsync(zone, ct);
        var sections = await BuildSectionsAsync(zone, ct);
        var report = new ZoneHealthReport(zone.ZoneId, sections, DateTime.UtcNow);
        _log.LogInformation($"{GetType().Name} completed: {sections.Count} sections");
        return report;
    }

    // Abstract steps — derived classes must implement these
    protected abstract Task ValidateForFormatAsync(GridZone zone, CancellationToken ct);
    protected abstract Task<IReadOnlyList<ReportSection>> BuildSectionsAsync(GridZone zone, CancellationToken ct);
}

public sealed class CsvZoneHealthReporter : ZoneHealthReporter
{
    public CsvZoneHealthReporter(IZoneDataSource dataSource, ISimpleLogger<ZoneHealthReporter> log) : base(dataSource, log) { }

    protected override Task ValidateForFormatAsync(GridZone zone, CancellationToken ct) => Task.CompletedTask;

    protected override Task<IReadOnlyList<ReportSection>> BuildSectionsAsync(GridZone zone, CancellationToken ct)
    {
        var sections = new List<ReportSection>
        {
            new("Header", "ZoneId,Status,LoadMW,CapacityMW,LoadPct,LastUpdated"),
            new("DataRow", string.Join(',', zone.ZoneId, zone.Status,
                $"{zone.CurrentLoadMW:F1}", $"{zone.CapacityMW:F1}",
                $"{ZoneLoadMetrics.LoadPercentage(zone):F1}", zone.LastUpdated.ToString("o")))
        };
        return Task.FromResult<IReadOnlyList<ReportSection>>(sections);
    }
}

public sealed class HtmlZoneHealthReporter : ZoneHealthReporter
{
    public HtmlZoneHealthReporter(IZoneDataSource dataSource, ISimpleLogger<ZoneHealthReporter> log) : base(dataSource, log) { }

    protected override Task ValidateForFormatAsync(GridZone zone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(zone.AssignedOperator?.Name))
            throw new InvalidOperationException($"Zone {zone.ZoneId} has no assigned operator — required for HTML header.");
        return Task.CompletedTask;
    }

    protected override Task<IReadOnlyList<ReportSection>> BuildSectionsAsync(GridZone zone, CancellationToken ct)
    {
        IReadOnlyList<ReportSection> sections = new List<ReportSection>
        {
            new("Header", $"<h1>Zone {zone.ZoneId}</h1><p>Operator: {zone.AssignedOperator!.Name}</p>"),
            new("Table", $"<table><tr><td>Load</td><td>{zone.CurrentLoadMW:F1} MW</td></tr></table>")
        };
        return Task.FromResult(sections);
    }
}
