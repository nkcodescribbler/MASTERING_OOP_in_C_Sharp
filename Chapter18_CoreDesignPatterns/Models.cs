namespace OOPBook.Chapter18_CoreDesignPatterns;

public record class SensorReading(string SensorId, double Value, DateTime Timestamp);

// ============================================================================
// Section 5.1 — Creational Patterns
// ============================================================================

// ─── Factory Method ───────────────────────────────────────────────────────
// IAlertSender is the shared abstraction reused later by Decorator (Section 5.2)
// and the Section 6 case study — one canonical definition throughout the chapter.
public interface IAlertSender
{
    void Send(string zoneCode, string message);
}

public abstract class GridAlertService
{
    protected abstract IAlertSender CreateSender(); // Factory Method

    public void RaiseAlert(string zoneCode, string message) => CreateSender().Send(zoneCode, message);
}

public class SmsAlertService : GridAlertService
{
    protected override IAlertSender CreateSender() => new SmsAlertSender();
}

public class EmailAlertService : GridAlertService
{
    protected override IAlertSender CreateSender() => new EmailAlertSender();
}

public class SmsAlertSender : IAlertSender
{
    public void Send(string z, string m) => Console.WriteLine($"[SMS] {z}: {m}");
}

public class EmailAlertSender : IAlertSender
{
    public void Send(string z, string m) => Console.WriteLine($"[Email] {z}: {m}");
}

// ─── Abstract Factory ─────────────────────────────────────────────────────
public interface IGridSensor
{
    SensorReading ReadValue();
}

public interface IGridMonitoringFactory
{
    IGridSensor CreateSensor(string sensorId);
    IAlertSender CreateAlertSender();
}

// Production family
public class ProductionGridSensor : IGridSensor
{
    private readonly string _id;
    public ProductionGridSensor(string id) => _id = id;
    public SensorReading ReadValue() => new SensorReading(_id, 112.3, DateTime.UtcNow);
}

public class ProductionAlertSender : IAlertSender
{
    public void Send(string z, string m) => Console.WriteLine($"[SMS→Prod] {z}: {m}");
}

public class ProductionMonitoringFactory : IGridMonitoringFactory
{
    public IGridSensor CreateSensor(string id) => new ProductionGridSensor(id);
    public IAlertSender CreateAlertSender() => new ProductionAlertSender();
}

// Test family — identical structure, only implementations differ
public class SimulatedGridSensor : IGridSensor
{
    private readonly string _id;
    public SimulatedGridSensor(string id) => _id = id;
    public SensorReading ReadValue() => new SensorReading(_id, 0.0, DateTime.UtcNow);
}

public class LogOnlyAlertSender : IAlertSender
{
    public void Send(string z, string m) => Console.WriteLine($"[LOG] {z}: {m}");
}

public class TestMonitoringFactory : IGridMonitoringFactory
{
    public IGridSensor CreateSensor(string id) => new SimulatedGridSensor(id);
    public IAlertSender CreateAlertSender() => new LogOnlyAlertSender();
}

// Client — zero concrete types visible
public class GridZoneMonitor
{
    private readonly IGridSensor _sensor;
    private readonly IAlertSender _sender;

    public GridZoneMonitor(IGridMonitoringFactory f, string id)
    {
        _sensor = f.CreateSensor(id);
        _sender = f.CreateAlertSender();
    }

    public void CheckZone(string zone)
    {
        var r = _sensor.ReadValue();
        if (r.Value > 100.0) _sender.Send(zone, $"Sensor {r.SensorId} over threshold");
    }
}

// ─── Builder ──────────────────────────────────────────────────────────────
public class GridPermit
{
    public string PermitId { get; }
    public string ZoneCode { get; }
    public DateTime ExpiryDate { get; }
    public bool HasAuditLog { get; }
    public IReadOnlyList<SensorReading> AttachedReadings { get; }
    public bool IsRenewal { get; }

    private GridPermit(Builder b)
    {
        PermitId = b.PermitId;
        ZoneCode = b.ZoneCode;
        ExpiryDate = b.ExpiryDate;
        HasAuditLog = b.HasAuditLog;
        AttachedReadings = b.AttachedReadings.AsReadOnly();
        IsRenewal = b.IsRenewal;
    }

    public sealed class Builder
    {
        public string PermitId { get; }
        public string ZoneCode { get; }
        public DateTime ExpiryDate { get; }
        public bool HasAuditLog { get; private set; }
        public List<SensorReading> AttachedReadings { get; } = new();
        public bool IsRenewal { get; private set; }

        public Builder(string permitId, string zoneCode, DateTime expiryDate)
        {
            if (string.IsNullOrWhiteSpace(permitId))
                throw new ArgumentException("Permit ID required.", nameof(permitId));
            if (string.IsNullOrWhiteSpace(zoneCode))
                throw new ArgumentException("Zone code required.", nameof(zoneCode));
            PermitId = permitId;
            ZoneCode = zoneCode;
            ExpiryDate = expiryDate;
        }

        public Builder WithAuditLog() { HasAuditLog = true; return this; }
        public Builder AsRenewal() { IsRenewal = true; return this; }
        public GridPermit Build() => new GridPermit(this);
    }
}

// ─── Singleton ────────────────────────────────────────────────────────────
// A lightweight stand-in used only to give the Singleton and Facade a "Register" target.
public class GridSensorStub
{
    public string Id { get; }
    public GridSensorStub(string id) => Id = id;
}

public class PowerSubstation
{
    public string Id { get; }
    public string ZoneCode { get; }
    public GridSensorStub Sensor { get; }

    public PowerSubstation(string id, string zoneCode, GridSensorStub sensor)
    {
        Id = id;
        ZoneCode = zoneCode;
        Sensor = sensor;
    }
}

public sealed class GridControlCentre
{
    private static readonly Lazy<GridControlCentre> _instance = new(() => new GridControlCentre(), isThreadSafe: true);

    public static GridControlCentre Instance => _instance.Value;

    private readonly List<PowerSubstation> _substations = new();

    private GridControlCentre() { }

    public void Register(PowerSubstation s) => _substations.Add(s);
    public int SubstationCount => _substations.Count;
}

// ─── Prototype ────────────────────────────────────────────────────────────
// GridPermitTemplate: cloneable variant, mutable by design for cloning.
// Distinct from the immutable GridPermit built above via Builder.
// (The book's constructor has a copy/paste bug — `HasAuditLog = audit` referencing an
// undeclared `audit` variable instead of the `hasAuditLog` parameter. Fixed here.)
public class GridPermitTemplate : ICloneable
{
    public string PermitId { get; set; }
    public string ZoneCode { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool HasAuditLog { get; set; }

    public GridPermitTemplate(string id, string zone, DateTime exp, bool hasAuditLog)
    {
        PermitId = id;
        ZoneCode = zone;
        ExpiryDate = exp;
        HasAuditLog = hasAuditLog;
    }

    public object Clone() => MemberwiseClone();

    // Typed clone — cleaner than casting ICloneable.Clone()
    public GridPermitTemplate CloneFor(string newZone, string newId)
    {
        var c = (GridPermitTemplate)MemberwiseClone();
        c.ZoneCode = newZone;
        c.PermitId = newId;
        c.ExpiryDate = DateTime.UtcNow.AddYears(1);
        return c;
    }
}

// ============================================================================
// Section 5.2 — Structural Patterns
// ============================================================================

// ─── Adapter ──────────────────────────────────────────────────────────────
// Third-party SDK — imagined as un-modifiable, so it lives here in-project instead
// of as a real external package.
public class LegacySensorReader
{
    private readonly string _id;
    public LegacySensorReader(string id) => _id = id;
    public double ReadRaw() => 98.7;
}

public class LegacySensorAdapter : IGridSensor
{
    private readonly LegacySensorReader _legacy;
    private readonly string _sensorId;

    public LegacySensorAdapter(LegacySensorReader r, string id)
    {
        _legacy = r;
        _sensorId = id;
    }

    public SensorReading ReadValue() => new SensorReading(_sensorId, _legacy.ReadRaw(), DateTime.UtcNow);
}

// ─── Decorator ────────────────────────────────────────────────────────────
public class LoggingAlertDecorator : IAlertSender
{
    private readonly IAlertSender _inner;
    public LoggingAlertDecorator(IAlertSender i) => _inner = i;

    public void Send(string z, string m)
    {
        Console.WriteLine($"[LOG→] {z}");
        _inner.Send(z, m);
        Console.WriteLine($"[LOG←] {z}: delivered.");
    }
}

public class RetryAlertDecorator : IAlertSender
{
    private readonly IAlertSender _inner;
    private readonly int _max;

    public RetryAlertDecorator(IAlertSender i, int max = 3)
    {
        _inner = i;
        _max = max;
    }

    public void Send(string z, string m)
    {
        for (int i = 1; i <= _max; i++)
        {
            try { _inner.Send(z, m); return; }
            catch (Exception ex) when (i < _max)
            {
                Console.WriteLine($"[RETRY] Attempt {i}: {ex.Message}");
            }
        }
    }
}

// ─── Facade ───────────────────────────────────────────────────────────────
// GridZone here is the shared, canonical version also used by the Command pattern
// below — it exposes both the plain constructor the Facade needs and the
// GetZoneCode()/IsActive()/Activate()/Deactivate() members Command needs.
public class GridZone
{
    private readonly string _zoneCode;
    private bool _isActive;

    public GridZone(string zoneCode)
    {
        _zoneCode = zoneCode;
        _isActive = true;
    }

    public string GetZoneCode() => _zoneCode;
    public bool IsActive() => _isActive;
    public void Activate() => _isActive = true;
    public void Deactivate() => _isActive = false;
}

public class GridPermitAuditService
{
    public void LogCommission(string permitId, string zoneCode) =>
        Console.WriteLine($"[AUDIT] Permit {permitId} commissioned zone {zoneCode}.");
}

public class ZoneCommissioningFacade
{
    private readonly GridControlCentre _centre;
    private readonly GridPermitAuditService _audit;

    public ZoneCommissioningFacade(GridControlCentre c, GridPermitAuditService a)
    {
        _centre = c;
        _audit = a;
    }

    // Five coordination steps hidden behind one call.
    public GridZone CommissionZone(string zoneCode, string sensorId, string permitId)
    {
        var zone = new GridZone(zoneCode);
        var sensor = new GridSensorStub(sensorId);
        var substation = new PowerSubstation($"SUB-{zoneCode}", zoneCode, sensor);
        _centre.Register(substation);
        _audit.LogCommission(permitId, zoneCode);
        Console.WriteLine($"Zone {zoneCode} commissioned with sensor {sensorId}.");
        return zone;
    }
}

// ─── Proxy ────────────────────────────────────────────────────────────────
public interface ISubstationInfo
{
    string ZoneCode { get; }
    bool IsOnline { get; }
    IReadOnlyList<SensorReading> GetHistoricalReadings(); // expensive
}

public class SubstationRecord : ISubstationInfo
{
    private readonly string _z;
    private readonly bool _o;
    private List<SensorReading>? _hist;

    public SubstationRecord(string z, bool o) { _z = z; _o = o; }

    public string ZoneCode => _z;
    public bool IsOnline => _o;

    public IReadOnlyList<SensorReading> GetHistoricalReadings()
    {
        _hist ??= LoadFromDb();
        return _hist.AsReadOnly();
    }

    private List<SensorReading> LoadFromDb()
    {
        Console.WriteLine("DB load...");
        return new();
    }
}

public class SubstationProxy : ISubstationInfo
{
    private readonly string _z;
    private readonly bool _o;
    private SubstationRecord? _real;

    public SubstationProxy(string z, bool o) { _z = z; _o = o; }

    public string ZoneCode => _z;
    public bool IsOnline => _o;

    public IReadOnlyList<SensorReading> GetHistoricalReadings()
    {
        _real ??= new SubstationRecord(_z, _o);
        return _real.GetHistoricalReadings();
    }
}

// ============================================================================
// Section 5.3 — Behavioural Patterns
// ============================================================================

// ─── Strategy ─────────────────────────────────────────────────────────────
// IAlertEvaluationStrategy / ThresholdEvaluationStrategy are reused, unchanged,
// by the Section 6 case study pipeline below.
public interface IAlertEvaluationStrategy
{
    bool ShouldAlert(SensorReading r);
}

public class ThresholdEvaluationStrategy : IAlertEvaluationStrategy
{
    private readonly double _mw;
    public ThresholdEvaluationStrategy(double mw) => _mw = mw;
    public bool ShouldAlert(SensorReading r) => r.Value > _mw;
}

public class RateOfChangeStrategy : IAlertEvaluationStrategy
{
    private readonly double _max;
    private double _last;
    public RateOfChangeStrategy(double max) => _max = max;

    public bool ShouldAlert(SensorReading r)
    {
        var d = Math.Abs(r.Value - _last);
        _last = r.Value;
        return d > _max;
    }
}

public class GridSensorEvaluator
{
    private IAlertEvaluationStrategy _strategy;
    public GridSensorEvaluator(IAlertEvaluationStrategy s) => _strategy = s;
    public void SetStrategy(IAlertEvaluationStrategy s) => _strategy = s;

    public void Evaluate(SensorReading r, string zone)
    {
        if (_strategy.ShouldAlert(r))
            Console.WriteLine($"ALERT — Zone {zone}: {r.Value} MW");
        else
            Console.WriteLine($"Zone {zone}: {r.Value} MW — OK.");
    }
}

// ─── Observer (classical GoF form — see Chapter 15 for the C# event-based form) ──
public interface IZoneStatusObserver
{
    void OnStatusChanged(string zone, bool isOnline);
}

public class GridZoneSubject // GoF Subject role
{
    private readonly string _code;
    private bool _online = true;
    private readonly List<IZoneStatusObserver> _obs = new();

    public GridZoneSubject(string code) => _code = code;

    public void Subscribe(IZoneStatusObserver o) => _obs.Add(o);
    public void Unsubscribe(IZoneStatusObserver o) => _obs.Remove(o);

    public void GoOffline()
    {
        _online = false;
        foreach (var o in _obs.ToList()) o.OnStatusChanged(_code, _online);
    }
}

public class AlertNotifierObserver : IZoneStatusObserver
{
    public void OnStatusChanged(string z, bool on)
    {
        if (!on) Console.WriteLine($"[ALERT] Zone {z} offline.");
    }
}

public class AuditLogObserver : IZoneStatusObserver
{
    public void OnStatusChanged(string z, bool on) =>
        Console.WriteLine($"[AUDIT] Zone {z}: {(on ? "online" : "offline")}");
}

// ─── Template Method ──────────────────────────────────────────────────────
public abstract class GridZoneReportGenerator
{
    // Non-virtual — the skeleton cannot be overridden by subclasses.
    public string Generate(string zoneCode)
    {
        var data = CollectData(zoneCode);
        var filtered = FilterByRelevance(data);
        var body = FormatOutput(filtered); // varies by subclass
        Deliver(zoneCode, body);
        return body;
    }

    private IReadOnlyList<SensorReading> CollectData(string z)
    {
        Console.WriteLine($"Collecting data for {z}...");
        return new List<SensorReading>();
    }

    private IReadOnlyList<SensorReading> FilterByRelevance(IReadOnlyList<SensorReading> r)
    {
        Console.WriteLine("Filtering...");
        return r;
    }

    private void Deliver(string z, string b) => Console.WriteLine($"Delivering report for {z}.");

    protected abstract string FormatOutput(IReadOnlyList<SensorReading> readings);
}

public class PlainTextReportGenerator : GridZoneReportGenerator
{
    protected override string FormatOutput(IReadOnlyList<SensorReading> r) =>
        $"Zone Report — {r.Count} readings. Format: plain text.";
}

public class JsonReportGenerator : GridZoneReportGenerator
{
    protected override string FormatOutput(IReadOnlyList<SensorReading> r) =>
        $"{{\"readingCount\": {r.Count}, \"format\": \"json\"}}";
}

// ─── Command ──────────────────────────────────────────────────────────────
public interface IGridCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

public class DeactivateZoneCommand : IGridCommand
{
    private readonly GridZone _zone;
    private bool _wasActive;

    public DeactivateZoneCommand(GridZone zone) => _zone = zone;

    public string Description => $"Deactivate zone {_zone.GetZoneCode()}";

    public void Execute()
    {
        _wasActive = _zone.IsActive();
        _zone.Deactivate();
        Console.WriteLine($"[COMMAND] Zone {_zone.GetZoneCode()} deactivated.");
    }

    public void Undo()
    {
        if (_wasActive)
        {
            _zone.Activate();
            Console.WriteLine($"[UNDO] Zone {_zone.GetZoneCode()} reactivated.");
        }
    }
}

public class GridCommandInvoker
{
    private readonly Stack<IGridCommand> _history = new();
    private readonly Queue<IGridCommand> _pending = new();

    public void Enqueue(IGridCommand c) => _pending.Enqueue(c);

    public void ExecuteNext()
    {
        if (!_pending.TryDequeue(out var c)) return;
        c.Execute();
        _history.Push(c);
        Console.WriteLine($"[AUDIT] Logged: {c.Description}");
    }

    public void UndoLast()
    {
        if (_history.TryPop(out var c)) c.Undo();
    }
}

// ============================================================================
// Section 6 — Case Study: Zone Alert Pipeline (Strategy + Decorator + Command)
// ============================================================================

// A pipeline-scoped command — deliberately simpler than IGridCommand above (no Undo,
// since alert sends are not reversible), so it gets its own interface.
public interface IAlertCommand
{
    void Execute();
    string Description { get; }
}

public class SendAlertCommand : IAlertCommand
{
    private readonly IAlertSender _s;
    private readonly string _z, _m;

    public SendAlertCommand(IAlertSender s, string z, string m)
    {
        _s = s;
        _z = z;
        _m = m;
    }

    public string Description => $"Alert zone {_z}: {_m}";
    public void Execute() => _s.Send(_z, _m);
}

// Wires all three patterns together.
public class ZoneAlertPipeline
{
    private readonly IAlertEvaluationStrategy _strategy;
    private readonly IAlertSender _sender;
    private readonly List<IAlertCommand> _auditLog = new();

    public ZoneAlertPipeline(IAlertEvaluationStrategy s, IAlertSender sender)
    {
        _strategy = s;
        _sender = sender;
    }

    public void Process(SensorReading r, string zone)
    {
        if (!_strategy.ShouldAlert(r)) return;
        var msg = $"Sensor {r.SensorId} reading {r.Value} MW exceeded threshold.";
        var cmd = new SendAlertCommand(_sender, zone, msg);
        _auditLog.Add(cmd);
        cmd.Execute();
    }

    public void PrintAuditLog()
    {
        Console.WriteLine("─── Audit Log ───");
        foreach (var e in _auditLog) Console.WriteLine($"  ✓ {e.Description}");
    }
}
