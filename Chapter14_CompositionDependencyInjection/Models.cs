// Chapter 14 — Composition & Dependency Injection
// The book's DI container examples use ASP.NET Core's WebApplication.CreateBuilder()
// and EF Core (AddDbContext/UseSqlServer). Full web hosting adds nothing to the
// DI lesson itself, so this project uses the same underlying container
// (Microsoft.Extensions.DependencyInjection's ServiceCollection) directly,
// console-style, via `new ServiceCollection()...BuildServiceProvider()`.
// Twilio/AWS SDK calls are replaced with console-based stand-ins so the demo
// needs no external services or network access to run.

using Microsoft.Extensions.DependencyInjection;

namespace OOPBook.Chapter14_CompositionDependencyInjection;

public class GridPermit
{
    public string PermitId { get; }
    public string ZoneCode { get; }
    public GridPermit(string permitId, string zoneCode) { PermitId = permitId; ZoneCode = zoneCode; }
}

// ----- Section 1.3 — the abstraction, and two interchangeable implementations
public interface IGridAlertNotifier { void Send(string recipientPhone, string message); }

public class SmsAlertNotifier : IGridAlertNotifier
{
    public void Send(string recipientPhone, string message) => Console.WriteLine($"[SMS -> {recipientPhone}] {message}");
}

public class PushAlertNotifier : IGridAlertNotifier
{
    public void Send(string recipientPhone, string message) => Console.WriteLine($"[PUSH -> {recipientPhone}] {message}");
}

// ----- Section 2.2 / Step 5 — test doubles ------------------------------------
public class FakeAlertNotifier : IGridAlertNotifier
{
    public List<(string Phone, string Message)> Sent { get; } = new();
    public void Send(string recipientPhone, string message) => Sent.Add((recipientPhone, message));
}

// ----- Section 3 — Common Mistakes --------------------------------------------

// Mistake 1 — tightly coupled to a concrete notifier.
public class GridZoneMonitorTightlyCoupled
{
    private readonly SmsAlertNotifier _notifier = new SmsAlertNotifier();

    public void CheckVoltage(double voltage)
    {
        if (voltage > 415.0)
            _notifier.Send("+1555OPS0001", $"Overvoltage: {voltage:F1}V");
    }
}

// Fix — dependency injected, interface-typed (also Section 1.1's original shape).
public class GridZoneMonitorSingleDependency
{
    private readonly IGridAlertNotifier _notifier;

    public GridZoneMonitorSingleDependency(IGridAlertNotifier notifier) =>
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));

    public void CheckVoltage(double voltage)
    {
        if (voltage > 415.0)
            _notifier.Send("+1555OPS0001", $"Overvoltage: {voltage:F1}V");
    }
}

// Mistake 4 — optional setter allows an incomplete object.
public class GridZoneMonitorOptionalSetter
{
    public IGridAlertNotifier? Notifier { get; set; }

    public void CheckVoltage(double voltage)
    {
        if (voltage > 415.0)
            Notifier?.Send("+1555OPS0001", $"Overvoltage: {voltage:F1}V"); // silently does nothing if null
    }
}

// Mistake 3 — a Singleton must never capture a Scoped service directly; if it
// needs one, it creates and disposes its own short-lived scope per operation.
public class GridPermitReportCache
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GridPermitReportCache(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public void RefreshZone(string zoneId)
    {
        using var scope = _scopeFactory.CreateScope();
        // Resolving inside a method is normally the Service Locator anti-pattern.
        // Acceptable ONLY here: the scope is created for this one short-lived
        // operation and disposed immediately after — not a standing, general-purpose registry.
        var repo = scope.ServiceProvider.GetRequiredService<IGridPermitRepository>();
        repo.Save(new GridPermit($"REPORT-{zoneId}", zoneId));
    }
}

// ===========================================================================
// Section 5.1 — the canonical, two-dependency GridZoneMonitor
// ===========================================================================
public interface IGridSensorReader { double ReadVoltage(string zoneId); }

public class SimulatedSensorReader : IGridSensorReader
{
    private readonly double _fixedVoltage;
    public SimulatedSensorReader(double fixedVoltage = 421.0) => _fixedVoltage = fixedVoltage;
    public double ReadVoltage(string zoneId) => _fixedVoltage; // stands in for a real Modbus/field-device reader
}

public class GridZoneMonitor
{
    private readonly IGridAlertNotifier _notifier;
    private readonly IGridSensorReader _sensorReader;

    // All mandatory dependencies declared here — nothing hidden.
    public GridZoneMonitor(IGridAlertNotifier notifier, IGridSensorReader sensorReader)
    {
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _sensorReader = sensorReader ?? throw new ArgumentNullException(nameof(sensorReader));
    }

    public void MonitorZone(string zoneId)
    {
        double voltage = _sensorReader.ReadVoltage(zoneId);
        if (voltage > 415.0)
            _notifier.Send("+1555OPS0001", $"Zone {zoneId}: overvoltage at {voltage:F1}V");
    }
}

// 5.2 — composite notifier: fans out to every registered channel.
public class CompositeAlertNotifier : IGridAlertNotifier
{
    private readonly IEnumerable<IGridAlertNotifier> _notifiers;
    public CompositeAlertNotifier(IEnumerable<IGridAlertNotifier> notifiers) =>
        _notifiers = notifiers ?? throw new ArgumentNullException(nameof(notifiers));

    public void Send(string recipientPhone, string message)
    {
        foreach (var notifier in _notifiers)
            notifier.Send(recipientPhone, message);
    }
}

// ----- Section 5.5 — Service Locator and Bastard Injection anti-patterns ----
public interface IGridPermitRepository { void Save(GridPermit permit); }
public interface IGridPermitAuditService { void Record(GridPermit permit, string action); }

public class InMemoryPermitRepository : IGridPermitRepository
{
    public List<GridPermit> Saved { get; } = new();
    public void Save(GridPermit permit) => Saved.Add(permit);
}

public class ConsolePermitAuditService : IGridPermitAuditService
{
    public void Record(GridPermit permit, string action) => Console.WriteLine($"[AUDIT] {permit.PermitId}: {action}");
}

// BAD — Service Locator via IServiceProvider: dependencies hidden inside the method body.
public class GridPermitServiceLocator
{
    private readonly IServiceProvider _services; // the locator
    public GridPermitServiceLocator(IServiceProvider services) => _services = services;

    public void ApprovePermit(GridPermit permit)
    {
        var notifier = (IGridAlertNotifier)_services.GetService(typeof(IGridAlertNotifier))!;
        var repo = (IGridPermitRepository)_services.GetService(typeof(IGridPermitRepository))!;
        var auditor = (IGridPermitAuditService)_services.GetService(typeof(IGridPermitAuditService))!;

        repo.Save(permit);
        notifier.Send("+1555OPS0001", $"Permit {permit.PermitId} approved");
        auditor.Record(permit, "Approved");
    }
}

// GOOD — explicit dependencies, constructor-injected.
public class GridPermitService
{
    private readonly IGridAlertNotifier _notifier;
    private readonly IGridPermitRepository _repo;
    private readonly IGridPermitAuditService _auditor;

    public GridPermitService(IGridAlertNotifier notifier, IGridPermitRepository repo, IGridPermitAuditService auditor)
    {
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _auditor = auditor ?? throw new ArgumentNullException(nameof(auditor));
    }

    public void ApprovePermit(GridPermit permit)
    {
        _repo.Save(permit);
        _notifier.Send("+1555OPS0001", $"Permit {permit.PermitId} approved");
        _auditor.Record(permit, "Approved");
    }
}

// BAD — Bastard Injection: a default constructor hardcodes its own dependencies.
public class GridZoneMonitorBastardInjection
{
    private readonly IGridAlertNotifier _notifier;
    private readonly IGridSensorReader _sensorReader;

    public GridZoneMonitorBastardInjection() : this(new SmsAlertNotifier(), new SimulatedSensorReader()) { }

    public GridZoneMonitorBastardInjection(IGridAlertNotifier notifier, IGridSensorReader sensorReader)
    {
        _notifier = notifier;
        _sensorReader = sensorReader;
    }
}

// ===========================================================================
// Section 6 — Case Study: SMS Provider Swap & Test Isolation
// ===========================================================================

// "Before" — hardcoded SMS dependency, untestable.
public class TwilioSmsGatewayStub // stands in for the real Twilio SDK client
{
    private readonly string _accountSid;
    private readonly string _authToken;
    public TwilioSmsGatewayStub(string accountSid, string authToken) { _accountSid = accountSid; _authToken = authToken; }
    public void SendSms(string from, string to, string body) => Console.WriteLine($"[Twilio stub, sid={_accountSid[..Math.Min(4, _accountSid.Length)]}...] {from} -> {to}: {body}");
}

public static class GridOperatorRegistryStatic
{
    public static string GetOperatorPhone(string zoneId) => "+1555999001"; // static call — hidden dependency
}

public class GridAlertServiceBefore
{
    private readonly TwilioSmsGatewayStub _gateway = new TwilioSmsGatewayStub(accountSid: "ACxxxxxxx", authToken: "xxxxxxx"); // tightly coupled, un-swappable

    public void SendZoneAlert(string zoneId, string message)
    {
        string to = GridOperatorRegistryStatic.GetOperatorPhone(zoneId); // static call — hidden dependency
        _gateway.SendSms("+1555000000", to, $"[UrbanGrid] Zone {zoneId}: {message}");
    }
}

// Step 1 — the abstraction (domain layer has no knowledge of Twilio, SNS, or any other provider).
public interface IGridOperatorRegistry { string GetOperatorPhone(string zoneId); }

// Step 2 — implement the abstraction in the infrastructure layer.
public class TwilioSettings
{
    public string AccountSid { get; init; } = "ACxxxxxxx";
    public string AuthToken { get; init; } = "xxxxxxx";
    public string FromNumber { get; init; } = "+1555000000";
}

public class TwilioAlertNotifier : IGridAlertNotifier
{
    private readonly TwilioSmsGatewayStub _gateway;
    private readonly string _fromNumber;

    public TwilioAlertNotifier(TwilioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _gateway = new TwilioSmsGatewayStub(settings.AccountSid, settings.AuthToken);
        _fromNumber = settings.FromNumber;
    }

    public void Send(string recipientPhone, string message) => _gateway.SendSms(_fromNumber, recipientPhone, message); // same shape as the "Before" state
}

// AwsSnsAlertNotifier — the backup provider, same interface. Reference-only:
// the real implementation depends on the AWS SDK (IAmazonSimpleNotificationService),
// which this project deliberately does not reference. Swapping providers is a
// one-line change in the composition root either way:
//   services.AddTransient<IGridAlertNotifier, AwsSnsAlertNotifier>();

// Step 3 — refactor the service to use composition.
public class GridAlertService
{
    private readonly IGridAlertNotifier _notifier;
    private readonly IGridOperatorRegistry _operatorRegistry;

    public GridAlertService(IGridAlertNotifier notifier, IGridOperatorRegistry operatorRegistry)
    {
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _operatorRegistry = operatorRegistry ?? throw new ArgumentNullException(nameof(operatorRegistry));
    }

    public void SendZoneAlert(string zoneId, string message)
    {
        string phone = _operatorRegistry.GetOperatorPhone(zoneId);
        _notifier.Send(phone, $"[UrbanGrid] Zone {zoneId}: {message}");
    }
}

public class InMemoryOperatorRegistry : IGridOperatorRegistry // stands in for a database-backed registry
{
    private readonly Dictionary<string, string> _phones = new() { ["ZONE-7"] = "+1555999001" };
    public string GetOperatorPhone(string zoneId) => _phones.TryGetValue(zoneId, out var p) ? p : "+1555000000";
}

// Step 5 — test doubles for unit testing without infrastructure.
public class FakeOperatorRegistry : IGridOperatorRegistry
{
    public string GetOperatorPhone(string zoneId) => "+1555999001";
}
