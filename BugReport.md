# Bug Report: Owned Entity Data Deleted When Moving Entity Between Parents

## Summary
When moving an entity with owned data from one parent to another in Entity Framework Core, the owned data is unexpectedly deleted during `SaveChanges()`.

## Expected Behavior
The owned data should be preserved when moving an entity between parents.

## Actual Behavior
The owned data becomes null after moving the entity to a new parent.

## Minimal Reproduction

```csharp
public class Parent
{
    public int Id { get; set; }
    public List<EntityWithOwnedData> Entities { get; set; } = new List<EntityWithOwnedData>();
}

public class EntityWithOwnedData
{
    public Parent Parent { get; set; }
    public int Id { get; set; }
    public OwnedData? Data { get; set; }
}

public class OwnedData
{
    public string Value { get; set; }
}

public class TestDbContext : DbContext
{
    public DbSet<Parent> Parents { get; set; }
    public DbSet<EntityWithOwnedData> Entities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityWithOwnedData>()
            .OwnsOne(e => e.Data);
    }
}

// Test that demonstrates the issue
var parent1 = new Parent();
var parent2 = new Parent();

var entity = new EntityWithOwnedData
{
    Data = new OwnedData { Value = "Test Value" }
};

parent1.Entities.Add(entity);
context.Parents.AddRange(parent1, parent2);
context.SaveChanges();

Assert.NotNull(entity.Data); // ✅ Passes - data exists

// Move entity to new parent
parent1.Entities.Remove(entity);
parent2.Entities.Add(entity);
context.SaveChanges();

Assert.NotNull(entity.Data); // ❌ Fails - data is null
```

## Environment
- Entity Framework Core 9.0.0
- .NET 9.0
- Reproduced on SQLite, PostgreSQL, and SQL Server

The issue occurs specifically when using owned entities in a collection navigation property and moving the containing entity between different parents.