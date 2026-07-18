// Chapter 7 — Encapsulation
// Run with: dotnet run --project Chapter07_Encapsulation

using OOPBook.Chapter07_Encapsulation;

Section2_AnemicVsEncapsulated();
Section3_CommonMistakes();
Section5_1_GuardClauses();
Section5_2_TellDontAsk();
Section5_3_LawOfDemeter();
Section5_4_ImmutabilityLevels();
Section6_CaseStudy();

static void Section2_AnemicVsEncapsulated()
{
    Header("Section 2 — The Anemic Domain Model");

    var alerts = new ConsoleAlertService();
    var anemicZone = new GridZoneAnemic { ZoneId = "NW-01", CapacityMW = 100 };
    var service = new ZoneManagementService();
    service.UpdateLoad(anemicZone, 412.0, alerts); // rule lives in a service, not the object
    Console.WriteLine($"Anemic zone status after overload: {anemicZone.Status}");
}

static void Section3_CommonMistakes()
{
    Header("Section 3 — Common Mistakes");

    var board = new ZoneAlertBoard();
    board.AddAlert(new GridAlert { Message = "Load spike" });
    Console.WriteLine($"Alerts (read-only view): {board.Alerts.Count}");
    // board.Alerts.Add(...) — would not compile; IReadOnlyList has no Add
}

static void Section5_1_GuardClauses()
{
    Header("Section 5.1 — Guard Clauses in Mutating Methods");

    var alerts = new ConsoleAlertService();
    var zone = new GridZone("NW-01", capacityMW: 100, region: "North-West");
    zone.UpdateLoad(85.0, alerts);
    Console.WriteLine($"{zone.ZoneId}: {zone.Status}, load={zone.CurrentLoadMW}");

    try
    {
        zone.UpdateLoad(150.0, alerts); // exceeds emergency capacity (120% of 100)
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }

    zone.UpdateLoad(110.0, alerts); // over capacity but within emergency threshold -> Overloaded + alert
    Console.WriteLine($"{zone.ZoneId}: {zone.Status}");
}

static void Section5_2_TellDontAsk()
{
    Header("Section 5.2 — Tell Don't Ask");

    var alerts = new ConsoleAlertService();

    var anemicZone = new GridZoneAnemic { ZoneId = "SE-02", CapacityMW = 50, CurrentLoadMW = 60 };
    new GridMonitorAsking().Check(anemicZone, alerts); // caller extracts state, decides, writes back

    var zone = new GridZone("SE-02", capacityMW: 50, region: "South-East");
    zone.UpdateLoad(60.0, alerts);
    new GridMonitorTelling().Check(zone, alerts); // one line — no domain logic in the caller
}

static void Section5_3_LawOfDemeter()
{
    Header("Section 5.3 — Law of Demeter");

    var operatorContact = new ContactInfo { Email = "shift-lead@urbangrid.io" };
    var gridOperator = new GridOperator("OP-1", "Jamie Rivera", OperatorClearance.PermitApprover) { ContactInfo = operatorContact };
    var zone = new GridZone("NW-01", capacityMW: 100, region: "North-West");
    zone.AssignOperator(gridOperator);

    var emailService = new ConsoleEmailService();

    var anemicPermit = new ZonePermitAnemic { PermitNumber = "PRM-BAD-1", Zone = zone };
    new PermitApprovalServiceBad(emailService).NotifyApproval(anemicPermit); // three-dot navigation — LoD violation

    var permit = ZonePermit.Issue("PRM-GOOD-1", zone.ZoneId, gridOperator.OperatorId, DateTime.UtcNow.AddDays(30));
    permit.AttachZone(zone);
    new PermitApprovalServiceGood(emailService).NotifyApproval(permit); // one dot — direct collaborator
}

static void Section5_4_ImmutabilityLevels()
{
    Header("Section 5.4 — Immutability as the Strongest Form of Encapsulation");

    // Level 4 — record type: structural immutability, value equality, non-destructive mutation
    var id1 = new PermitId("PRM", 42, 2024);
    var id2 = id1 with { Sequence = 43 }; // non-destructive mutation — id1 is untouched
    Console.WriteLine($"{id1} -> {id2} (id1 unchanged: {id1.Sequence})");
}

static void Section6_CaseStudy()
{
    Header("Section 6 — Case Study: UrbanGrid Zone Permit System");

    var alerts = new ConsoleAlertService();
    var zone = new GridZone("ZONE-NW-01", capacityMW: 100, region: "North-West");
    var gridOperator = new GridOperator("OP-9", "Morgan Lee", OperatorClearance.PermitApprover)
    {
        ContactInfo = new ContactInfo { Email = "morgan.lee@urbangrid.io" }
    };
    zone.AssignOperator(gridOperator);

    var permit = ZonePermit.Issue("PRM-2024-001", zone.ZoneId, gridOperator.OperatorId, DateTime.UtcNow.AddDays(90));
    permit.AttachZone(zone);

    permit.Approve(gridOperator);
    permit.Suspend("Grid overload", gridOperator.OperatorId);
    // permit.Extend(...) would fail here — must resume/approve first in a full workflow;
    // shown separately below on a second, still-Approved permit instead.

    var region = permit.GetIssuingZoneRegion(); // LoD — no chain navigation
    Console.WriteLine($"Permit {permit.PermitNumber} issued for region: {region}");

    Console.WriteLine("Audit trail:");
    foreach (var entry in permit.AuditLog)
        Console.WriteLine($"  [{entry.Timestamp:O}] {entry.Action} by {entry.ActorId}");

    // Invalid states are structurally impossible:
    // permit.Status = PermitStatus.Approved; // compile error — no public setter
    try
    {
        permit.Cancel("reason", gridOperator.OperatorId); // OK — Suspended permits can be cancelled directly
        Console.WriteLine($"Final status: {permit.Status}");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }

    var secondPermit = ZonePermit.Issue("PRM-2024-002", zone.ZoneId, gridOperator.OperatorId, DateTime.UtcNow.AddDays(10));
    secondPermit.AttachZone(zone);
    secondPermit.Approve(gridOperator);
    secondPermit.Extend(TimeSpan.FromDays(365), gridOperator.OperatorId);
    Console.WriteLine($"{secondPermit.PermitNumber} extended, now expires {secondPermit.ExpiresAt:d}");
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}
