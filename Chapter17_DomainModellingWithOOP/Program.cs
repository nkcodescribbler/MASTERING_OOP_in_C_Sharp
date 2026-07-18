// Chapter 17 — Domain Modelling with OOP
// Run with: dotnet run --project Chapter17_DomainModellingWithOOP

using OOPBook.Chapter17_DomainModellingWithOOP;

Section3_CommonMistakes();
Section5_1_EntitiesVsValueObjects();
Section5_2_AggregatesAndAggregateRoots();
await Section5_3_DomainEvents();
await Section5_4_Repositories();
await Section5_5_DomainServices();
await Section6_CaseStudy();

static void ApprovePermitStringly(string permitId, string zoneCode, string operatorId) =>
    Console.WriteLine($"[stringly-typed] approving {permitId} in {zoneCode} by {operatorId} — which argument is which? the compiler cannot tell you.");

static void ApprovePermitTyped(PermitId permitId, ZoneCode zoneCode, string operatorId) =>
    Console.WriteLine($"[strongly-typed] approving {permitId} in {zoneCode} by {operatorId} — swapping the first two arguments would now be a compile error.");

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var mutableCoordinate = new GeoCoordinateMutable { Id = 1, Latitude = 40.7128, Longitude = -74.0060 };
    Console.WriteLine($"Mistake — GeoCoordinateMutable has an Id ({mutableCoordinate.Id}) and can be reassigned after construction.");

    var coordinate = GeoCoordinate.Of(40.7128, -74.0060);
    Console.WriteLine($"Fix — GeoCoordinate is an immutable value object: {coordinate}");
    try { GeoCoordinate.Of(200, 0); }
    catch (ArgumentOutOfRangeException ex) { Console.WriteLine($"Caught: {ex.Message}"); }

    ApprovePermitStringly("P-100", "north-7", "op-1");
    ApprovePermitTyped(PermitId.Of("P-100"), ZoneCode.Of("north-7"), "op-1");

    var anemicPermit = new GridPermitAnemic { PermitId = "P-200", Status = "Pending", ExpiryDate = DateTime.UtcNow.AddDays(30) };
    new PermitServiceAnemic().Approve(anemicPermit, "op-2");
    Console.WriteLine($"Mistake — anemic model: status changed to '{anemicPermit.Status}' from outside, with no invariant enforced by the object itself.");

    var richPermit = GridPermitRichV1.Issue(PermitId.Of("P-201"), ZoneCode.Of("north-7"), DateTime.UtcNow.AddDays(30));
    richPermit.Approve("op-2");
    Console.WriteLine($"Fix — rich model: {richPermit.Id} status is now '{richPermit.Status}', audit trail: [{string.Join(", ", richPermit.AuditLog)}]");
    try { richPermit.Approve("op-3"); }
    catch (InvalidOperationException ex) { Console.WriteLine($"Caught: {ex.Message} — the aggregate itself refuses the invalid transition."); }

    var mega = new GridZoneMegaAggregate();
    mega.Sensors.Add(new GridSensorStub { Id = "SENSOR-1" });
    mega.Operators.Add(new GridOperatorStub { Id = "OP-1" });
    Console.WriteLine($"Mistake — GridZoneMegaAggregate loaded {mega.Permits.Count} permits, {mega.Sensors.Count} sensors, {mega.Operators.Count} operators as one object graph.");

    Console.WriteLine("Mistake (repository) — a query like 'GetActivePermitForZone' embedding Status == \"Approved\" && ExpiryDate > UtcNow");
    Console.WriteLine("puts a domain rule inside data-access code. Fix: GridPermit.IsValidForZoneActivation() below keeps that rule in the domain.");
}

static void Section5_1_EntitiesVsValueObjects()
{
    Header("Section 5.1 — Entities vs Value Objects: The Identity Test");

    var zoneCode = ZoneCode.Of("north-7");
    var zone = GridZone.Create(zoneCode); // entity — identity is ZoneCode, tracked across its lifetime
    Console.WriteLine($"Entity: GridZone '{zone.ZoneCode}' created, IsActive={zone.IsActive}");

    var permit = GridPermit.Issue(PermitId.Of("P-300"), zoneCode, DateTime.UtcNow.AddDays(30));
    permit.Approve("op-1");
    zone.Activate(permit); // activating requires a valid, matching permit — see Section 6 Rule 6
    zone.RecordAlert("Voltage spike");
    Console.WriteLine($"Entity behaviour: GridZone '{zone.ZoneCode}' IsActive={zone.IsActive}, AlertCount={zone.AlertCount}");

    var coordinateA = GeoCoordinate.Of(40.7128, -74.0060);
    var coordinateB = GeoCoordinate.Of(40.7128, -74.0060);
    Console.WriteLine($"Value object: two GeoCoordinates with the same lat/lon are equal by value: {coordinateA == coordinateB}");

    var reading = new SensorReading("SENSOR-1", 4.8, DateTime.UtcNow);
    Console.WriteLine($"SensorReading (record class — has a string reference field): {reading}, above 4.0MW threshold: {reading.IsAboveThreshold(4.0)}");
}

static void Section5_2_AggregatesAndAggregateRoots()
{
    Header("Section 5.2 — Aggregates & Aggregate Roots");

    var permit = GridPermit.Issue(PermitId.Of("P-301"), ZoneCode.Of("north-7"), DateTime.UtcNow.AddDays(30));
    permit.Approve("op-1");
    Console.WriteLine($"Aggregate root GridPermit '{permit.Id}': status={permit.Status}, audit entries={permit.AuditLog.Count}, pending domain events={permit.DomainEvents.Count}");
    foreach (var entry in permit.AuditLog)
        Console.WriteLine($"  [AUDIT] {entry.Action} by {entry.OperatorId ?? "(system)"} at {entry.OccurredAt:HH:mm:ss}");

    try
    {
        var expired = GridPermit.Issue(PermitId.Of("P-302"), ZoneCode.Of("north-7"), DateTime.UtcNow.AddSeconds(1));
        Thread.Sleep(1100);
        expired.Approve("op-1"); // invariant enforced inside the aggregate — no external code can approve an expired permit
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message} — the aggregate itself protects this invariant, everywhere it is used.");
    }
}

static async Task Section5_3_DomainEvents()
{
    Header("Section 5.3 — Domain Events");

    IDomainEventDispatcher dispatcher = new ConsoleDomainEventDispatcher();
    IGridPermitRepository permits = new InMemoryGridPermitRepository();

    var permit = GridPermit.Issue(PermitId.Of("P-303"), ZoneCode.Of("north-7"), DateTime.UtcNow.AddDays(30));
    await permits.SaveAsync(permit);

    await ApproveAsync(PermitId.Of("P-303"));

    async Task ApproveAsync(PermitId permitId)
    {
        var p = await permits.GetByIdAsync(permitId) ?? throw new InvalidOperationException($"Permit '{permitId}' not found.");
        p.Approve("op-1");             // 1. domain operation — event collected internally
        await permits.SaveAsync(p);    // 2. persist first
        foreach (var evt in p.DomainEvents)
            await dispatcher.DispatchAsync(evt); // 3. dispatch AFTER save
        p.ClearDomainEvents();
    }
}

static async Task Section5_4_Repositories()
{
    Header("Section 5.4 — Repositories: Bridge to Persistent Storage");

    // The book's infrastructure example is EfGridPermitRepository (EF Core + GridDbContext). This
    // project uses InMemoryGridPermitRepository behind the same IGridPermitRepository interface —
    // the domain layer only ever depends on the interface, never on how it is implemented.
    IGridPermitRepository repository = new InMemoryGridPermitRepository();

    var permit = GridPermit.Issue(PermitId.Of("P-304"), ZoneCode.Of("south-2"), DateTime.UtcNow.AddDays(30));
    await repository.SaveAsync(permit);

    var reloaded = await repository.GetByIdAsync(PermitId.Of("P-304"));
    Console.WriteLine($"Repository round-trip: reloaded permit '{reloaded?.Id}' with status '{reloaded?.Status}'.");

    var southPermits = await repository.GetByZoneAsync(ZoneCode.Of("south-2"));
    Console.WriteLine($"GetByZoneAsync('south-2') returned {southPermits.Count} permit(s) — the domain retrieves a fully valid aggregate, nothing more.");
}

static async Task Section5_5_DomainServices()
{
    Header("Section 5.5 — Domain Services");

    IGridPermitRepository permits = new InMemoryGridPermitRepository();
    IGridZoneRepository zones = new InMemoryGridZoneRepository();
    IDomainEventDispatcher dispatcher = new ConsoleDomainEventDispatcher();
    var approvalService = new GridPermitApprovalService(permits, zones, dispatcher);

    var zoneCode = ZoneCode.Of("east-3");
    var zone = GridZone.Create(zoneCode);
    await zones.SaveAsync(zone);

    var permit = GridPermit.Issue(PermitId.Of("P-305"), zoneCode, DateTime.UtcNow.AddDays(30));
    await permits.SaveAsync(permit);

    var result = await approvalService.ApproveAsync(PermitId.Of("P-305"), "op-1");
    Console.WriteLine($"GridPermitApprovalService.ApproveAsync: Succeeded={result.Succeeded}");

    // Rule 3 in action — a zone with an active alert blocks approval, even though the permit itself is valid.
    var alertedZoneCode = ZoneCode.Of("west-9");
    var alertedZone = GridZone.Create(alertedZoneCode);
    var seedPermit = GridPermit.Issue(PermitId.Of("P-SEED"), alertedZoneCode, DateTime.UtcNow.AddDays(30));
    seedPermit.Approve("op-0");
    alertedZone.Activate(seedPermit);
    alertedZone.RecordAlert("Overload detected");
    await zones.SaveAsync(alertedZone);

    var blockedPermit = GridPermit.Issue(PermitId.Of("P-306"), alertedZoneCode, DateTime.UtcNow.AddDays(30));
    await permits.SaveAsync(blockedPermit);

    var blockedResult = await approvalService.ApproveAsync(PermitId.Of("P-306"), "op-1");
    Console.WriteLine($"Approval against an alerted zone: Succeeded={blockedResult.Succeeded}, Error='{blockedResult.Error}'");
}

static async Task Section6_CaseStudy()
{
    Header("Section 6 — Case Study: UrbanGrid Zone Permit Domain Model");

    IGridPermitRepository permits = new InMemoryGridPermitRepository();
    IGridZoneRepository zones = new InMemoryGridZoneRepository();
    IDomainEventDispatcher dispatcher = new ConsoleDomainEventDispatcher();
    var approvalService = new GridPermitApprovalService(permits, zones, dispatcher);

    var zoneCode = ZoneCode.Of("north-7");
    var zone = GridZone.Create(zoneCode);
    await zones.SaveAsync(zone);

    var permit = GridPermit.Issue(PermitId.Of("P-400"), zoneCode, DateTime.UtcNow.AddDays(30));
    await permits.SaveAsync(permit);

    var result = await approvalService.ApproveAsync(PermitId.Of("P-400"), "supervisor-1");
    Assert("approval succeeded", result.Succeeded);

    var approvedPermit = await permits.GetByIdAsync(PermitId.Of("P-400"));
    Assert("permit status is Approved", approvedPermit?.Status == PermitStatus.Approved);
    Assert("permit has an audit entry for the approval", approvedPermit?.AuditLog.Any(a => a.Action == "Approved") == true);

    zone = await zones.GetByCodeAsync(zoneCode) ?? throw new InvalidOperationException("Zone not found.");
    zone.Activate(approvedPermit!);
    await zones.SaveAsync(zone);
    Assert("zone is active after Activate(permit)", zone.IsActive);

    zone.RecordAlert("Transformer temperature high");
    Assert("alert count increments", zone.AlertCount == 1);

    var rejectedPermit = GridPermit.Issue(PermitId.Of("P-401"), zoneCode, DateTime.UtcNow.AddDays(30));
    rejectedPermit.Reject("supervisor-1", "Capacity study incomplete");
    Assert("rejected permit status is Rejected", rejectedPermit.Status == PermitStatus.Rejected);
    Assert("rejecting raises a PermitRejected domain event", rejectedPermit.DomainEvents.OfType<PermitRejected>().Any());

    try
    {
        var mismatchedPermit = GridPermit.Issue(PermitId.Of("P-402"), ZoneCode.Of("south-2"), DateTime.UtcNow.AddDays(30));
        mismatchedPermit.Approve("supervisor-1");
        zone.Activate(mismatchedPermit); // wrong zone — the aggregate rejects this even though the permit itself is valid
        Assert("activating with a mismatched zone throws", false);
    }
    catch (InvalidOperationException)
    {
        Assert("activating with a mismatched zone throws", true);
    }
}

static void Assert(string description, bool condition) =>
    Console.WriteLine(condition ? $"  PASS — {description}" : $"  FAIL — {description}");

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
