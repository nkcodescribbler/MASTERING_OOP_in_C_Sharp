// Chapter 15 — Events, Delegates & the Observer Pattern
// GridSensor/GridAlertLogger/GridDashboard evolve through three eras in the
// book: (A) plain multicast delegates, (B) the `event` keyword with
// EventHandler<TEventArgs>, and (C) a publish/subscribe event aggregator —
// the chapter's final, production-shape design, reused by the Section 6 case
// study. Each era keeps its own suffixed type names so all three remain
// runnable side by side, exactly as the book presents their evolution.

using System.Collections.Concurrent;

namespace OOPBook.Chapter15_EventsDelegatesObserverPattern;

public interface IGridAlertNotifier { void Send(string recipientPhone, string message); }

public class FakeAlertNotifier : IGridAlertNotifier
{
    public List<(string Phone, string Message)> Sent { get; } = new();
    public void Send(string recipientPhone, string message) => Sent.Add((recipientPhone, message));
}

// ===========================================================================
// Era A — Sections 1.1/1.2/5.1/5.2: plain multicast delegates
// ===========================================================================
public delegate void ZoneAlertHandler(string zoneId, double voltage);

public class GridAlertLoggerA
{
    public void OnAlertRaised(string zoneId, double voltage) => Console.WriteLine($"[LOG] Zone {zoneId}: overvoltage at {voltage:F1}V");
}

public class GridDashboardA
{
    public void OnAlertRaised(string zoneId, double voltage) => Console.WriteLine($"[DASH] Zone {zoneId}: {voltage:F1}V");
}

public class SmsAlertServiceA
{
    public void OnAlertRaised(string zoneId, double voltage) => Console.WriteLine($"[SMS] Operator notified: {zoneId}");
}

public static class ResilientInvocation
{
    // Resilient invocation — all handlers called even if one throws.
    public static void RaiseResilient(ZoneAlertHandler? handler, string zoneId, double voltage)
    {
        if (handler == null) return;

        foreach (Delegate subscriber in handler.GetInvocationList())
        {
            try
            {
                ((ZoneAlertHandler)subscriber)(zoneId, voltage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Handler {subscriber.Method.Name} threw: {ex.Message}");
            }
        }
    }
}

// ===========================================================================
// Era B — Sections 1.3/1.4/2/3/5.3/5.4/5.5: the `event` keyword
// ===========================================================================
public class GridZoneAlertEventArgs : EventArgs
{
    public string ZoneId { get; }
    public double Voltage { get; }
    public DateTime Timestamp { get; }

    public GridZoneAlertEventArgs(string zoneId, double voltage)
    {
        ZoneId = zoneId;
        Voltage = voltage;
        Timestamp = DateTime.UtcNow;
    }
}

// Section 1.3 / Section 3 mistake — dangerous public delegate field, no access control.
public class GridSensorFieldBased
{
    public ZoneAlertHandler? AlertRaised; // anyone can assign or invoke this
    public void Poll()
    {
        double v = ReadVoltage();
        if (v > 415.0) AlertRaised?.Invoke("ZONE-7", v);
    }
    private double ReadVoltage() => 421.5;
}

// Fix — the `event` keyword restricts external code to subscribe/unsubscribe only.
public class GridSensorEventBased
{
    // Standard .NET event pattern
    public event EventHandler<GridZoneAlertEventArgs>? AlertRaised;

    // Protected virtual method — the standard "On" pattern.
    protected virtual void OnAlertRaised(GridZoneAlertEventArgs e) => AlertRaised?.Invoke(this, e);

    public void Poll()
    {
        double voltage = ReadVoltage();
        if (voltage > 415.0)
            OnAlertRaised(new GridZoneAlertEventArgs("ZONE-7", voltage));
    }

    private double ReadVoltage() => 421.5;
}
// sensor.AlertRaised("ZONE-7", 999); // compile error — cannot raise from outside
// sensor.AlertRaised = null;         // compile error — cannot assign from outside

public class GridDashboardEventArgsBased : IDisposable
{
    private readonly GridSensorEventBased _sensor;

    public GridDashboardEventArgsBased(GridSensorEventBased sensor)
    {
        _sensor = sensor;
        _sensor.AlertRaised += OnAlertRaised; // subscribe
    }

    private void OnAlertRaised(object? sender, GridZoneAlertEventArgs e) => Console.WriteLine($"[DASH] Zone {e.ZoneId}: {e.Voltage:F1}V");

    public void Dispose() => _sensor.AlertRaised -= OnAlertRaised; // unsubscribe — severs the root
}

// Section 3 — memory leak mistake: dashboard is never unsubscribed, so it is
// rooted to the sensor forever, even after GridControlRoomLeaky itself is done with it.
public class GridDashboardLeakTarget
{
    public void OnAlertRaised(object? sender, GridZoneAlertEventArgs e) => Console.WriteLine($"[DASH] Zone {e.ZoneId}: {e.Voltage:F1}V");
}

public class GridControlRoomLeaky
{
    private readonly GridSensorEventBased _sensor;

    public GridControlRoomLeaky(GridSensorEventBased sensor)
    {
        _sensor = sensor;
        var dashboard = new GridDashboardLeakTarget();
        _sensor.AlertRaised += dashboard.OnAlertRaised; // dashboard is now rooted to sensor, forever
    }
}

public class GridControlRoomFixed : IDisposable
{
    private readonly GridSensorEventBased _sensor;
    private readonly GridDashboardLeakTarget _dashboard;

    public GridControlRoomFixed(GridSensorEventBased sensor)
    {
        _sensor = sensor;
        _dashboard = new GridDashboardLeakTarget();
        _sensor.AlertRaised += _dashboard.OnAlertRaised;
    }

    public void Dispose() => _sensor.AlertRaised -= _dashboard.OnAlertRaised; // remove the reference
}

// Section 3 — event raised before any subscriber can register.
public class GridSensorPrematureRaise
{
    public event EventHandler<GridZoneAlertEventArgs>? AlertRaised;

    public GridSensorPrematureRaise()
    {
        AlertRaised?.Invoke(this, new GridZoneAlertEventArgs("ZONE-INIT", 0.0)); // no subscriber can exist yet — this is always a no-op
    }
}

// Section 3 — mutable event args can cause inconsistent state across handlers.
public class GridZoneAlertEventArgsMutable : EventArgs
{
    public string ZoneId { get; set; } = string.Empty; // settable — a handler can change the data
    public double Voltage { get; set; }
}

// Section 5.5 — thread-safe invocation.
public static class ThreadSafeInvocationDemo
{
    // UNSAFE shape (commented — the race requires two real threads to observe):
    //   if (AlertRaised != null) { AlertRaised(this, args); } // AlertRaised could become null between the check and the call

    // SAFE — copy to a local before the null check; the snapshot cannot change underneath you.
    public static void PollSafeCopy(EventHandler<GridZoneAlertEventArgs>? alertRaised, object sender, GridZoneAlertEventArgs args)
    {
        var handler = alertRaised;
        if (handler != null) handler(sender, args);
    }

    // IDIOMATIC — ?.Invoke is the thread-safe, concise form.
    public static void PollIdiomatic(EventHandler<GridZoneAlertEventArgs>? alertRaised, object sender, GridZoneAlertEventArgs args) =>
        alertRaised?.Invoke(sender, args);
}

// ===========================================================================
// Era C — Section 5.6 / Section 6 case study: the Event Aggregator pattern
// ===========================================================================
public interface IGridEvent { }

public record GridZoneAlertEvent(string ZoneId, double Voltage, DateTime Timestamp, string SensorId) : IGridEvent;

public interface IGridEventAggregator
{
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGridEvent;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGridEvent;
    void Publish<TEvent>(TEvent gridEvent) where TEvent : IGridEvent;
}

public sealed class GridEventAggregator : IGridEventAggregator
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGridEvent
    {
        var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Delegate>());
        lock (list) { list.Add(handler); }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGridEvent
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var list))
        {
            lock (list) { list.Remove(handler); }
        }
    }

    public void Publish<TEvent>(TEvent gridEvent) where TEvent : IGridEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;

        List<Delegate> snapshot;
        lock (list) { snapshot = new List<Delegate>(list); } // snapshot to avoid holding the lock during handler execution

        foreach (var handler in snapshot)
        {
            try { ((Action<TEvent>)handler)(gridEvent); }
            catch (Exception ex) { Console.WriteLine($"[WARN] Handler {handler.Method.Name} threw: {ex.Message}"); }
        }
    }
}

// Publisher knows only about the aggregator — this is the chapter's final GridSensor shape.
public class GridSensor
{
    private readonly IGridEventAggregator _aggregator;
    private readonly string _sensorId;

    public GridSensor(IGridEventAggregator aggregator, string sensorId)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _sensorId = sensorId ?? throw new ArgumentNullException(nameof(sensorId));
    }

    public void Poll(string zoneId)
    {
        double voltage = ReadVoltage(zoneId);
        if (voltage > 415.0)
            _aggregator.Publish(new GridZoneAlertEvent(zoneId, voltage, DateTime.UtcNow, _sensorId)); // no knowledge of who handles this
    }

    private double ReadVoltage(string zoneId) => 421.5; // simulated hardware read
}

public class GridAlertLogger : IDisposable
{
    private readonly IGridEventAggregator _aggregator;

    public GridAlertLogger(IGridEventAggregator aggregator)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _aggregator.Subscribe<GridZoneAlertEvent>(OnAlertRaised);
    }

    private void OnAlertRaised(GridZoneAlertEvent e) => Console.WriteLine($"[LOG] {e.Timestamp:HH:mm:ss} Sensor {e.SensorId} Zone {e.ZoneId}: {e.Voltage:F1}V");
    public void Dispose() => _aggregator.Unsubscribe<GridZoneAlertEvent>(OnAlertRaised);
}

public class GridDashboard : IDisposable
{
    private readonly IGridEventAggregator _aggregator;

    public GridDashboard(IGridEventAggregator aggregator)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _aggregator.Subscribe<GridZoneAlertEvent>(OnAlertRaised);
    }

    private void OnAlertRaised(GridZoneAlertEvent e) => Console.WriteLine($"[DASH] Zone {e.ZoneId} alert panel updated");
    public void Dispose() => _aggregator.Unsubscribe<GridZoneAlertEvent>(OnAlertRaised);
}

// ===========================================================================
// Section 6 — Case Study: UrbanGrid Overvoltage Detection Pipeline
// ===========================================================================

// "Before" — sensor knows all four consumers, tightly coupled.
public class GridAuditLoggerDirect
{
    public void Record(string zoneId, double voltage) => Console.WriteLine($"[AUDIT] Zone {zoneId}: {voltage:F1}V");
}

public class GridDashboardDirect
{
    public void Update(string zoneId, double voltage) => Console.WriteLine($"[DASH] Zone {zoneId}: {voltage:F1}V");
}

public class ScadaIntegrationServiceDirect
{
    public void RecordIncident(string zoneId, double voltage, DateTime at) => Console.WriteLine($"[SCADA] Incident: Zone {zoneId} at {at:HH:mm:ss}");
}

public class GridSensorBefore
{
    private readonly GridAuditLoggerDirect _auditLogger;
    private readonly GridDashboardDirect _dashboard;
    private readonly IGridAlertNotifier _smsNotifier;
    private readonly ScadaIntegrationServiceDirect _scada;

    public GridSensorBefore(GridAuditLoggerDirect auditLogger, GridDashboardDirect dashboard, IGridAlertNotifier smsNotifier, ScadaIntegrationServiceDirect scada)
    {
        _auditLogger = auditLogger;
        _dashboard = dashboard;
        _smsNotifier = smsNotifier;
        _scada = scada;
    }

    public void Poll(string zoneId)
    {
        double voltage = ReadVoltage(zoneId);
        if (voltage > 415.0)
        {
            _auditLogger.Record(zoneId, voltage);
            _dashboard.Update(zoneId, voltage);
            _smsNotifier.Send("+1555OPS0001", $"Zone {zoneId}: {voltage:F1}V overvoltage");
            _scada.RecordIncident(zoneId, voltage, DateTime.UtcNow);
        }
    }

    private double ReadVoltage(string zoneId) => 421.5;
}

// "After" — each consumer subscribes independently through the aggregator.
public class GridAuditLogger : IDisposable
{
    private readonly IGridEventAggregator _aggregator;

    public GridAuditLogger(IGridEventAggregator aggregator)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _aggregator.Subscribe<GridZoneAlertEvent>(Handle);
    }

    private void Handle(GridZoneAlertEvent e) => Console.WriteLine($"[AUDIT] {e.Timestamp:O} | Sensor {e.SensorId} | Zone {e.ZoneId} | {e.Voltage:F1}V");
    public void Dispose() => _aggregator.Unsubscribe<GridZoneAlertEvent>(Handle);
}

public class SmsAlertService : IDisposable
{
    private readonly IGridEventAggregator _aggregator;
    private readonly IGridAlertNotifier _notifier;

    public SmsAlertService(IGridEventAggregator aggregator, IGridAlertNotifier notifier)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _aggregator.Subscribe<GridZoneAlertEvent>(Handle);
    }

    private void Handle(GridZoneAlertEvent e) => _notifier.Send("+1555OPS0001", $"[UrbanGrid] Zone {e.ZoneId}: {e.Voltage:F1}V overvoltage");
    public void Dispose() => _aggregator.Unsubscribe<GridZoneAlertEvent>(Handle);
}

public class ScadaIntegrationService : IDisposable
{
    private readonly IGridEventAggregator _aggregator;

    public ScadaIntegrationService(IGridEventAggregator aggregator)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _aggregator.Subscribe<GridZoneAlertEvent>(Handle);
    }

    private void Handle(GridZoneAlertEvent e) => Console.WriteLine($"[SCADA] Incident recorded: Zone {e.ZoneId} at {e.Timestamp:HH:mm:ss}");
    public void Dispose() => _aggregator.Unsubscribe<GridZoneAlertEvent>(Handle);
}
