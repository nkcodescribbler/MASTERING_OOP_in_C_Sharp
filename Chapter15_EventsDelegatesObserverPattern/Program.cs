// Chapter 15 — Events, Delegates & the Observer Pattern
// Run with: dotnet run --project Chapter15_EventsDelegatesObserverPattern

using OOPBook.Chapter15_EventsDelegatesObserverPattern;

Section1_1And1_2_DelegatesAndMulticast();
Section1_3_TheEventKeyword();
Section1_4_EventHandlerStandard();
Section3_CommonMistakes();
Section5_1_DelegateDeclaration();
Section5_2_MulticastInvocationList();
Section5_3_ControlledSubscription();
Section5_4_MemoryManagement();
Section5_5_ThreadSafeInvocation();
Section5_6_EventAggregator();
Section6_CaseStudy();

static void Section1_1And1_2_DelegatesAndMulticast()
{
    Header("Section 1.1/1.2 — Delegates and Multicast Delegates");

    var logger = new GridAlertLoggerA();
    ZoneAlertHandler handler = logger.OnAlertRaised; // method group syntax — no parentheses
    handler("ZONE-7", 421.5);

    var dashboard = new GridDashboardA();
    handler += dashboard.OnAlertRaised; // now two methods are registered
    handler("ZONE-7", 421.5);           // both called in sequence

    handler -= dashboard.OnAlertRaised; // removes dashboard's handler
    handler("ZONE-7", 421.5);           // only logger fires now
}

static void Section1_3_TheEventKeyword()
{
    Header("Section 1.3 — The event Keyword");

    var logger = new GridAlertLoggerA();
    var fieldSensor = new GridSensorFieldBased();
    fieldSensor.AlertRaised += logger.OnAlertRaised; // a legitimate subscriber registers first
    fieldSensor.AlertRaised!("ZONE-7", 999);          // problem: anyone can invoke it directly
    fieldSensor.AlertRaised = null;                   // problem: anyone can wipe all subscriptions

    var eventSensor = new GridSensorEventBased();
    eventSensor.AlertRaised += (sender, e) => Console.WriteLine($"[LOG] Zone {e.ZoneId}: overvoltage at {e.Voltage:F1}V");
    eventSensor.Poll();
    // eventSensor.AlertRaised("ZONE-7", 999); // compile error — cannot raise from outside
    // eventSensor.AlertRaised = null;         // compile error — cannot assign from outside
}

static void Section1_4_EventHandlerStandard()
{
    Header("Section 1.4 — EventHandler<TEventArgs>: the .NET Standard");

    var sensor = new GridSensorEventBased();
    sensor.AlertRaised += (sender, e) => Console.WriteLine($"Zone {e.ZoneId} alert: {e.Voltage:F1}V at {e.Timestamp:HH:mm:ss}");
    sensor.Poll();
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    // Mistake: memory leak — dashboard is never unsubscribed, rooted forever.
    var sensorForLeak = new GridSensorEventBased();
    var leakyRoom = new GridControlRoomLeaky(sensorForLeak);
    Console.WriteLine("GridControlRoomLeaky constructed — its dashboard can never be garbage collected while sensorForLeak is alive.");

    using (var fixedRoom = new GridControlRoomFixed(sensorForLeak))
    {
        sensorForLeak.Poll();
    }
    Console.WriteLine("GridControlRoomFixed disposed — its dashboard was unsubscribed and is now eligible for GC.");

    // Mistake: raising without a null check.
    var unguarded = new GridSensorEventBased(); // no subscribers registered
    unguarded.Poll(); // safe here — GridSensorEventBased already uses AlertRaised?.Invoke internally

    // Mistake: raising an event from inside the constructor — no subscriber can exist yet.
    var premature = new GridSensorPrematureRaise(); // the AlertRaised?.Invoke inside the ctor is always a no-op

    // Mistake: mutable event args.
    var mutableArgs = new GridZoneAlertEventArgsMutable { ZoneId = "ZONE-7", Voltage = 420 };
    Console.WriteLine("A handler could mutate mutableArgs.Voltage, and every handler after it would see the changed value.");
}

static void Section5_1_DelegateDeclaration()
{
    Header("Section 5.1 — Delegate Declaration and Invocation");

    var logger = new GridAlertLoggerA();
    ZoneAlertHandler d1 = logger.OnAlertRaised;                                    // 1. method group
    ZoneAlertHandler d3 = (zoneId, voltage) => Console.WriteLine($"[LAMBDA] Zone {zoneId}: {voltage:F1}V"); // 3. lambda

    d1("ZONE-7", 421.5);
    d3("ZONE-7", 421.5);

    Action<string, double> action = (zoneId, voltage) => Console.WriteLine($"Zone {zoneId}: {voltage:F1}V");
    Func<string, double, string> formatter = (zoneId, voltage) => $"[{zoneId}] {voltage:F1}V";
    action("ZONE-7", 421.5);
    Console.WriteLine(formatter("ZONE-7", 421.5));
}

static void Section5_2_MulticastInvocationList()
{
    Header("Section 5.2 — Multicast Invocation List");

    var logger = new GridAlertLoggerA();
    var dash = new GridDashboardA();
    var sms = new SmsAlertServiceA();

    ZoneAlertHandler alert = logger.OnAlertRaised;
    alert += dash.OnAlertRaised;
    alert += sms.OnAlertRaised;

    alert("ZONE-7", 421.5); // all three fire in order

    Delegate[] list = alert.GetInvocationList();
    Console.WriteLine($"Handlers registered: {list.Length}"); // 3

    ResilientInvocation.RaiseResilient(alert, "ZONE-7", 421.5); // all handlers called even if one throws
}

static void Section5_3_ControlledSubscription()
{
    Header("Section 5.3 — The event Keyword: Controlled Subscription");

    var sensor = new GridSensorEventBased();

    EventHandler<GridZoneAlertEventArgs> handler = (s, e) => Console.WriteLine($"Zone {e.ZoneId}: {e.Voltage:F1}V");
    sensor.AlertRaised += handler; // subscribe
    sensor.Poll();
    sensor.AlertRaised -= handler; // unsubscribe using the same reference — only possible because it was stored in a variable
    sensor.Poll(); // no output — handler was removed
}

static void Section5_4_MemoryManagement()
{
    Header("Section 5.4 — Memory Management: Unsubscription and Event Leaks");

    var sensor = new GridSensorEventBased();
    using (var dashboard = new GridDashboardEventArgsBased(sensor))
    {
        sensor.Poll(); // dashboard receives the alert
    }
    // dashboard.Dispose() called automatically — unsubscribed, eligible for GC
    sensor.Poll(); // no [DASH] output this time — dashboard already unsubscribed
}

static void Section5_5_ThreadSafeInvocation()
{
    Header("Section 5.5 — Thread-Safe Invocation with ?.Invoke");

    EventHandler<GridZoneAlertEventArgs>? handlers = null;
    handlers += (s, e) => Console.WriteLine($"Handled: {e.ZoneId}");

    var args = new GridZoneAlertEventArgs("ZONE-7", 421.5);
    ThreadSafeInvocationDemo.PollSafeCopy(handlers, "sensor", args);
    ThreadSafeInvocationDemo.PollIdiomatic(handlers, "sensor", args);
}

static void Section5_6_EventAggregator()
{
    Header("Section 5.6 — Event Aggregator Pattern");

    var aggregator = new GridEventAggregator();
    using var logger = new GridAlertLogger(aggregator);   // subscriber knows only about the aggregator
    using var dashboard = new GridDashboard(aggregator);

    var sensor = new GridSensor(aggregator, "SENSOR-MAIN-01"); // publisher knows only about the aggregator
    sensor.Poll("ZONE-7"); // delivered to every current subscriber, with no direct coupling
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: Overvoltage Detection Pipeline");

    // Before — sensor knows all four consumers directly.
    var before = new GridSensorBefore(
        new GridAuditLoggerDirect(),
        new GridDashboardDirect(),
        new FakeAlertNotifier(),
        new ScadaIntegrationServiceDirect());
    before.Poll("ZONE-7");

    // After — every consumer subscribes independently through the aggregator.
    var aggregator = new GridEventAggregator();
    var notifier = new FakeAlertNotifier();

    using var auditLogger = new GridAuditLogger(aggregator);
    using var smsService = new SmsAlertService(aggregator, notifier);
    using var scada = new ScadaIntegrationService(aggregator);

    var sensor = new GridSensor(aggregator, "SENSOR-MAIN-01");
    sensor.Poll("ZONE-7"); // audit, SMS, and SCADA all react — sensor knows about none of them

    // Step 5 — unit test in full isolation (manual assertions; no test framework required to run this file).
    GridZoneAlertEvent? receivedEvent = null;
    var testAggregator = new GridEventAggregator();
    var testSensor = new GridSensor(testAggregator, "SENSOR-001");
    testAggregator.Subscribe<GridZoneAlertEvent>(e => receivedEvent = e);
    testSensor.Poll("ZONE-7");

    Assert("event was published", receivedEvent is not null);
    Assert("zone matches", receivedEvent?.ZoneId == "ZONE-7");
    Assert("sensor id matches", receivedEvent?.SensorId == "SENSOR-001");
    Assert("voltage exceeds threshold", receivedEvent?.Voltage > 415.0);

    var fakeNotifier = new FakeAlertNotifier();
    var testAggregator2 = new GridEventAggregator();
    using var smsUnderTest = new SmsAlertService(testAggregator2, fakeNotifier);
    testAggregator2.Publish(new GridZoneAlertEvent("ZONE-7", 421.5, DateTime.UtcNow, "SENSOR-001"));
    Assert("exactly one SMS sent", fakeNotifier.Sent.Count == 1);
    Assert("SMS message mentions the zone", fakeNotifier.Sent.Count > 0 && fakeNotifier.Sent[0].Message.Contains("ZONE-7"));
}

static void Assert(string description, bool condition) =>
    Console.WriteLine(condition ? $"  PASS — {description}" : $"  FAIL — {description}");

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
