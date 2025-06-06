using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace EfCoreOwnedEntityTests
{
    // Domain Models
    public class Foo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Bar> Bars { get; set; } = new List<Bar>();
    }

    public class Bar
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int FooId { get; set; }
        public Foo Foo { get; set; }
        public List<Baz> Bazs { get; set; } = new List<Baz>();
    }

    public class Baz
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int BarId { get; set; }
        public Bar Bar { get; set; }
        public Zir Zir { get; set; }
    }

    // Owned Type
    public class Zir
    {
        public string Property1 { get; set; }
        public string Property2 { get; set; }
        public int Value { get; set; }
    }

    // DbContext
    public class TestDbContext : DbContext
    {
        public DbSet<Foo> Foos { get; set; }
        public DbSet<Bar> Bars { get; set; }
        public DbSet<Baz> Bazs { get; set; }

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure relationships
            modelBuilder.Entity<Foo>()
                .HasMany(f => f.Bars)
                .WithOne(b => b.Foo)
                .HasForeignKey(b => b.FooId);

            modelBuilder.Entity<Bar>()
                .HasMany(b => b.Bazs)
                .WithOne(bz => bz.Bar)
                .HasForeignKey(bz => bz.BarId);

            // Configure Zir as owned type
            modelBuilder.Entity<Baz>()
                .OwnsOne(b => b.Zir, zir =>
                {
                    zir.Property(z => z.Property1).HasMaxLength(100);
                    zir.Property(z => z.Property2).HasMaxLength(100);
                });
        }
    }

    public class OwnedEntityMovementTests : IDisposable
    {
        private readonly TestDbContext _context;
        private readonly ITestOutputHelper _output;

        public OwnedEntityMovementTests(ITestOutputHelper output)
        {
            _output = output;
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging() // To see more detailed errors
                .Options;

            _context = new TestDbContext(options);
            _context.Database.EnsureCreated();
        }

        [Fact]
        public void Moving_Entity_With_OwnedType_Between_Collections_Should_Preserve_OwnedType()
        {
            // Arrange - Create initial data
            var foo = new Foo { Name = "Test Foo" };
            var sourceBar = new Bar { Name = "Source Bar", Foo = foo };
            var targetBar = new Bar { Name = "Target Bar", Foo = foo };
            
            var baz = new Baz 
            { 
                Name = "Test Baz",
                Bar = sourceBar,
                Zir = new Zir 
                { 
                    Property1 = "Important Value 1",
                    Property2 = "Important Value 2",
                    Value = 42
                }
            };
            
            sourceBar.Bazs.Add(baz);
            foo.Bars.Add(sourceBar);
            foo.Bars.Add(targetBar);

            _context.Foos.Add(foo);
            _context.SaveChanges();

            // Clear the context to ensure we're working with fresh entities
            _context.ChangeTracker.Clear();

            // Act - Retrieve and move the entity
            var retrievedFoo = _context.Foos
                .Include(f => f.Bars)
                    .ThenInclude(b => b.Bazs)
                        .ThenInclude(bz => bz.Zir) // This is not necessary as owned types are auto-included
                .Single(f => f.Id == foo.Id);

            var retrievedSourceBar = retrievedFoo.Bars.Single(b => b.Name == "Source Bar");
            var retrievedTargetBar = retrievedFoo.Bars.Single(b => b.Name == "Target Bar");
            var bazToMove = retrievedSourceBar.Bazs.Single();

            _output.WriteLine($"Before move - Baz.Zir is null: {bazToMove.Zir == null}");
            _output.WriteLine($"Before move - Zir.Property1: {bazToMove.Zir?.Property1}");
            _output.WriteLine($"Before move - Zir.Value: {bazToMove.Zir?.Value}");

            // Move the Baz from source to target using Clear() and AddRange()
            var bazList = retrievedSourceBar.Bazs.ToList();
            retrievedSourceBar.Bazs.Clear();
            retrievedTargetBar.Bazs.AddRange(bazList);

            _output.WriteLine($"\nAfter move, before SaveChanges - Baz.Zir is null: {bazToMove.Zir == null}");
            _output.WriteLine($"After move, before SaveChanges - Zir.Property1: {bazToMove.Zir?.Property1}");

            // Check entity states before saving
            var bazEntry = _context.Entry(bazToMove);
            _output.WriteLine($"\nBaz state before SaveChanges: {bazEntry.State}");
            if (bazToMove.Zir != null)
            {
                var zirEntry = _context.Entry(bazToMove.Zir);
                _output.WriteLine($"Zir state before SaveChanges: {zirEntry.State}");
            }

            _context.SaveChanges();

            // Assert - Check if Zir is preserved after SaveChanges
            _output.WriteLine($"\nAfter SaveChanges - Baz.Zir is null: {bazToMove.Zir == null}");
            _output.WriteLine($"After SaveChanges - Zir.Property1: {bazToMove.Zir?.Property1}");
            
            // This assertion will likely FAIL, demonstrating the issue
            Assert.NotNull(bazToMove.Zir);
            Assert.Equal("Important Value 1", bazToMove.Zir.Property1);
            Assert.Equal("Important Value 2", bazToMove.Zir.Property2);
            Assert.Equal(42, bazToMove.Zir.Value);

            // Verify the move actually happened
            Assert.Empty(retrievedSourceBar.Bazs);
            Assert.Single(retrievedTargetBar.Bazs);
            Assert.Equal(bazToMove.Id, retrievedTargetBar.Bazs.Single().Id);
        }

        [Fact]
        public void Moving_Entity_With_OwnedType_Using_Individual_Operations_Shows_Same_Issue()
        {
            // Arrange
            var foo = new Foo { Name = "Test Foo" };
            var sourceBar = new Bar { Name = "Source Bar", Foo = foo };
            var targetBar = new Bar { Name = "Target Bar", Foo = foo };
            
            var baz = new Baz 
            { 
                Name = "Test Baz",
                Bar = sourceBar,
                Zir = new Zir 
                { 
                    Property1 = "Value 1",
                    Property2 = "Value 2",
                    Value = 100
                }
            };
            
            sourceBar.Bazs.Add(baz);
            foo.Bars.Add(sourceBar);
            foo.Bars.Add(targetBar);

            _context.Foos.Add(foo);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            // Act
            var retrievedFoo = _context.Foos
                .Include(f => f.Bars)
                    .ThenInclude(b => b.Bazs)
                .Single(f => f.Id == foo.Id);

            var retrievedSourceBar = retrievedFoo.Bars.Single(b => b.Name == "Source Bar");
            var retrievedTargetBar = retrievedFoo.Bars.Single(b => b.Name == "Target Bar");
            var bazToMove = retrievedSourceBar.Bazs.Single();

            // Try different approach - Remove then Add
            retrievedSourceBar.Bazs.Remove(bazToMove);
            retrievedTargetBar.Bazs.Add(bazToMove);

            _context.SaveChanges();

            // Assert - This will also likely fail
            Assert.NotNull(bazToMove.Zir);
            Assert.Equal("Value 1", bazToMove.Zir.Property1);
        }

        [Fact]
        public void Workaround_Creating_New_Zir_Instance_Works()
        {
            // Arrange
            var foo = new Foo { Name = "Test Foo" };
            var sourceBar = new Bar { Name = "Source Bar", Foo = foo };
            var targetBar = new Bar { Name = "Target Bar", Foo = foo };
            
            var baz = new Baz 
            { 
                Name = "Test Baz",
                Bar = sourceBar,
                Zir = new Zir 
                { 
                    Property1 = "Original Value 1",
                    Property2 = "Original Value 2",
                    Value = 999
                }
            };
            
            sourceBar.Bazs.Add(baz);
            foo.Bars.Add(sourceBar);
            foo.Bars.Add(targetBar);

            _context.Foos.Add(foo);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            // Act
            var retrievedFoo = _context.Foos
                .Include(f => f.Bars)
                    .ThenInclude(b => b.Bazs)
                .Single(f => f.Id == foo.Id);

            var retrievedSourceBar = retrievedFoo.Bars.Single(b => b.Name == "Source Bar");
            var retrievedTargetBar = retrievedFoo.Bars.Single(b => b.Name == "Target Bar");
            var bazToMove = retrievedSourceBar.Bazs.Single();

            // Workaround: Store the Zir data and recreate it
            var originalZir = bazToMove.Zir;
            var zirData = new { originalZir.Property1, originalZir.Property2, originalZir.Value };

            retrievedSourceBar.Bazs.Clear();
            retrievedTargetBar.Bazs.Add(bazToMove);
            
            // Recreate the Zir
            bazToMove.Zir = new Zir
            {
                Property1 = zirData.Property1,
                Property2 = zirData.Property2,
                Value = zirData.Value
            };

            _context.SaveChanges();

            // Assert - This should pass with the workaround
            Assert.NotNull(bazToMove.Zir);
            Assert.Equal("Original Value 1", bazToMove.Zir.Property1);
            Assert.Equal("Original Value 2", bazToMove.Zir.Property2);
            Assert.Equal(999, bazToMove.Zir.Value);
        }

        [Fact]
        public void Diagnostic_Test_Shows_Zir_State_Changes()
        {
            // This test helps diagnose what's happening to the owned entity
            var foo = new Foo { Name = "Test Foo" };
            var sourceBar = new Bar { Name = "Source Bar", Foo = foo };
            var targetBar = new Bar { Name = "Target Bar", Foo = foo };
            
            var baz = new Baz 
            { 
                Name = "Test Baz",
                Bar = sourceBar,
                Zir = new Zir { Property1 = "Test", Property2 = "Data", Value = 1 }
            };
            
            sourceBar.Bazs.Add(baz);
            foo.Bars.Add(sourceBar);
            foo.Bars.Add(targetBar);

            _context.Foos.Add(foo);
            _context.SaveChanges();
            _context.ChangeTracker.Clear();

            // Retrieve and examine
            var retrievedFoo = _context.Foos
                .Include(f => f.Bars)
                    .ThenInclude(b => b.Bazs)
                .Single(f => f.Id == foo.Id);

            var retrievedSourceBar = retrievedFoo.Bars.Single(b => b.Name == "Source Bar");
            var retrievedTargetBar = retrievedFoo.Bars.Single(b => b.Name == "Target Bar");
            var bazToMove = retrievedSourceBar.Bazs.Single();

            // Log initial state
            _output.WriteLine("=== Initial State ===");
            LogEntityStates();

            // Move
            retrievedSourceBar.Bazs.Clear();
            retrievedTargetBar.Bazs.Add(bazToMove);

            _output.WriteLine("\n=== After Move ===");
            LogEntityStates();

            // Detect changes manually
            _context.ChangeTracker.DetectChanges();
            
            _output.WriteLine("\n=== After DetectChanges ===");
            LogEntityStates();

            _context.SaveChanges();

            _output.WriteLine("\n=== After SaveChanges ===");
            _output.WriteLine($"Baz.Zir is null: {bazToMove.Zir == null}");
            if (bazToMove.Zir != null)
            {
                _output.WriteLine($"Zir values: {bazToMove.Zir.Property1}, {bazToMove.Zir.Property2}, {bazToMove.Zir.Value}");
            }

            void LogEntityStates()
            {
                var bazEntry = _context.Entry(bazToMove);
                _output.WriteLine($"Baz: State={bazEntry.State}, BarId={bazEntry.CurrentValues["BarId"]}");
                
                if (bazToMove.Zir != null)
                {
                    var zirEntry = _context.Entry(bazToMove.Zir);
                    _output.WriteLine($"Zir: State={zirEntry.State}");
                    foreach (var prop in zirEntry.Properties)
                    {
                        _output.WriteLine($"  {prop.Metadata.Name}: Current={prop.CurrentValue}, Original={prop.OriginalValue}, IsModified={prop.IsModified}");
                    }
                }
                else
                {
                    _output.WriteLine("Zir: null");
                }
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}