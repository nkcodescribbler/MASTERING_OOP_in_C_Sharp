// Chapter 12 — Generics in OOP
// IGridEntity, GridZone and GridPermit are shared by nearly every section.
// The IRepository<T>/IReadRepository<out T>/InMemoryRepository<T> trio from
// the Section 6 case study is the chapter's culmination and is used as the
// canonical repository implementation; the Section 5.1 Repository<T> and its
// two extension patterns are kept separate (distinct names) since they
// illustrate mechanics the case study's version doesn't repeat.

namespace OOPBook.Chapter12_GenericsInOOP;

public interface IGridEntity { string Id { get; } string ZoneCode { get; } }
public interface IActivatable { void Activate(); }

public class GridZone : IGridEntity, IActivatable
{
    public string Id { get; }
    public string ZoneCode { get; }
    public bool IsActive { get; private set; }

    public GridZone(string id, string zoneCode)
    {
        Id = id;
        ZoneCode = zoneCode;
    }

    // Parameterless constructor — satisfies `where T : new()` in the 5.2 constraint demo.
    public GridZone() : this(string.Empty, string.Empty) { }

    public void Activate() => IsActive = true;
}

public class GridPermit : IGridEntity
{
    public string Id { get; }
    public string ZoneCode { get; }
    public bool IsApproved { get; private set; }

    public GridPermit(string id, string zoneCode)
    {
        Id = id;
        ZoneCode = zoneCode;
    }

    // Parameterless constructor — satisfies `where TOut : new()` in the Converter<,> demo.
    public GridPermit() : this(string.Empty, string.Empty) { }

    public void Approve() => IsApproved = true;
}

public record SensorReading(string ZoneCode, double ValueMw, DateTime RecordedAt);

// ----- Section 1A — why generics exist ---------------------------------------
public class ObjectCache
{
    private readonly Dictionary<string, object> _store = new();
    public void Add(string key, object value) => _store[key] = value;
    public object Get(string key) => _store[key];
}

public class GridCache<T>
{
    private readonly Dictionary<string, T> _store = new();
    public void Add(string key, T value) => _store[key] = value;
    public T Get(string key) => _store[key];
}

// ----- Section 1C — constraint system ----------------------------------------
public class Processor<T> where T : IGridEntity
{
    public void Process(T item) => Console.WriteLine($"Entity {item.Id} in zone {item.ZoneCode}");
}

// ----- Section 3 — Common Mistakes --------------------------------------------
// Mistake: T is unconstrained — entity.Id would not compile.
//   public class UnconstrainedProcessor<T>
//   {
//       public string GetId(T entity) => entity.Id; // compile error — T has no Id property
//   }
// The fix — add the interface constraint:
public class ConstrainedProcessor<T> where T : IGridEntity
{
    public string GetId(T entity) => entity.Id;
}

// Mistake: a type switch inside a "generic" method — not actually generic.
public class Dispatcher
{
    public void Process<T>(T entity)
    {
        if (entity is GridZone zone) zone.Activate();
        else if (entity is GridPermit p) p.Approve();
    }
}

// Fix, option A — shared operation via an interface constraint.
public class GenericDispatcher
{
    public void Process<T>(T entity) where T : IActivatable => entity.Activate();
}

// Fix, option B — separate overloads when the operations are genuinely different.
public class TypedDispatcher
{
    public void Process(GridZone zone) => zone.Activate();
    public void Process(GridPermit p) => p.Approve();
}

// Mistake: over-constrained parameter type forces unnecessary allocation.
public class ReadingProcessor
{
    public void ProcessReadings(List<SensorReading> readings) { /* ... */ }
}

// Fix — accept the most general sequence type the method actually needs.
public class FlexReadingProcessor
{
    public void ProcessReadings(IEnumerable<SensorReading> readings) { /* ... */ }
}

// Mistake: variance is not valid on class type parameters.
//   public class Repository<out T> { ... } // compile error

// ===========================================================================
// Section 5 — Method-Level Detail
// ===========================================================================

// 5.1 — generic interface with a default member, and a generic class implementing it.
public interface IRepository<T> where T : class, IGridEntity
{
    void Add(T entity);
    T? GetById(string id);
    IReadOnlyList<T> GetAll();
    bool Remove(string id);
    bool Exists(string id) => GetById(id) is not null; // default interface member
}

public class Repository<T> : IRepository<T> where T : class, IGridEntity
{
    private readonly List<T> _items = new();
    public void Add(T entity) { ArgumentNullException.ThrowIfNull(entity); _items.Add(entity); }
    public T? GetById(string id) => _items.FirstOrDefault(e => e.Id == id);
    public IReadOnlyList<T> GetAll() => _items.AsReadOnly();
    public bool Remove(string id) { var e = GetById(id); return e is not null && _items.Remove(e); }
    public bool Exists(string id) => _items.Any(e => e.Id == id);
}

// Pattern A — keep T open, add shared behaviour. `new` hides the base method —
// the audit only fires when called through AuditableRepository<T> directly.
public class AuditableRepository<T> : Repository<T> where T : class, IGridEntity
{
    public new void Add(T entity)
    {
        Console.WriteLine($"[Audit] Adding {typeof(T).Name} {entity.Id}");
        base.Add(entity);
    }
}

// Pattern B — close T to GridZone, add a domain-specific query.
public class ZoneOnlyRepository : Repository<GridZone>
{
    public IReadOnlyList<GridZone> GetByZoneCode(string zoneCode) =>
        GetAll().Where(z => z.ZoneCode == zoneCode).ToList().AsReadOnly();
}

// The class itself is concrete — only the method that needs type flexibility is generic.
public class GridZoneService
{
    private readonly List<GridZone> _zones = new();
    public GridZoneService(IEnumerable<GridZone> zones) => _zones = zones.ToList();

    public T? FindFirst<T>(IEnumerable<T> source, Func<T, bool> predicate) => source.FirstOrDefault(predicate);

    public GridZone? GetActiveZone(string zoneCode) => FindFirst(_zones, z => z.ZoneCode == zoneCode && z.IsActive);
}

// 5.2 — constraints.
public class RangeFilter<T> where T : IComparable<T>
{
    public IEnumerable<T> Between(IEnumerable<T> source, T min, T max) =>
        source.Where(item => item.CompareTo(min) >= 0 && item.CompareTo(max) <= 0);
}

public class EntityProcessor<T> where T : class, IGridEntity, new()
{
    public T CreateDefault() => new T();
    public void Process(T e) => Console.WriteLine($"{e.Id} in zone {e.ZoneCode}");
}

public class Converter<TIn, TOut>
    where TIn : class, IGridEntity
    where TOut : class, new()
{
    public TOut Convert(TIn input) => new TOut();
}

// 5.3 — covariance and contravariance.
public interface IReadRepository<out T> where T : IGridEntity
{
    T? GetById(string id);
    IEnumerable<T> GetAll();
    bool Exists(string id);
}

public interface IEntityEventHandler<in TEntity> where TEntity : IGridEntity
{
    void OnAdded(TEntity entity);
}

public class GeneralAuditHandler : IEntityEventHandler<IGridEntity>
{
    public void OnAdded(IGridEntity entity) => Console.WriteLine($"[Audit] {entity.Id} added");
}

// ===========================================================================
// Section 6 — Case Study: Eliminating the Duplicate Repository Problem
// ===========================================================================

// "Before" — one of four near-identical, hand-written repository classes.
public class ZoneRepositoryDuplicated
{
    private readonly List<GridZone> _zones = new();

    public void Add(GridZone zone)
    {
        if (_zones.Any(z => z.Id == zone.Id))
            throw new InvalidOperationException($"Zone {zone.Id} already exists.");
        _zones.Add(zone);
        Console.WriteLine($"[Audit] Added GridZone {zone.Id}"); // duplicated in every repository
    }

    public GridZone? GetById(string id) => _zones.FirstOrDefault(z => z.Id == id);
    public IReadOnlyList<GridZone> GetAll() => _zones.AsReadOnly();

    public bool Remove(string id)
    {
        var found = _zones.FirstOrDefault(z => z.Id == id);
        return found is not null && _zones.Remove(found);
    }
}
// PermitRepository, SensorRepository, MaintenanceRecordRepository would be
// identical in structure — only the type name changes each time.

// "After" — one generic implementation shared by every entity type.
public class InMemoryRepository<T> : IRepository<T>, IReadRepository<T> where T : class, IGridEntity
{
    private readonly List<T> _items = new();

    public void Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (_items.Any(e => e.Id == entity.Id))
            throw new InvalidOperationException($"{typeof(T).Name} '{entity.Id}' already exists.");
        _items.Add(entity);
        Console.WriteLine($"[Audit] Added {typeof(T).Name} {entity.Id} (zone: {entity.ZoneCode})");
    }

    public T? GetById(string id) => _items.FirstOrDefault(e => e.Id == id);
    public IReadOnlyList<T> GetAll() => _items.AsReadOnly();
    IEnumerable<T> IReadRepository<T>.GetAll() => _items.AsReadOnly(); // explicit impl — disambiguates from IRepository<T>.GetAll()

    public bool Remove(string id)
    {
        var e = GetById(id);
        if (e is null) return false;
        _items.Remove(e);
        return true;
    }

    public bool Exists(string id) => _items.Any(e => e.Id == id);
}

// Covariant assignment — IReadRepository<out T> enables one method for all entity types.
public class BulkAuditLogger
{
    public void LogAll(IReadRepository<IGridEntity> source)
    {
        foreach (var e in source.GetAll())
            Console.WriteLine($"{e.GetType().Name} | {e.Id} | zone: {e.ZoneCode}");
    }
}
