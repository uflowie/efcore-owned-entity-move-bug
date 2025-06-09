using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace EfCoreOwnedEntityParentMoveTestsOneToOne
{
    public class Parent
    {
        public int Id { get; set; }
        public EntityWithOwnedData? Entity { get; set; } // Changed from List to single entity
    }

    public class EntityWithOwnedData
    {
        public Parent Parent { get; set; } // Kept for EF Core to manage relationship
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

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityWithOwnedData>()
                .OwnsOne(e => e.Data);

            // Optional: Configure the 1:1 relationship if needed, 
            // though EF Core might infer it correctly with the navigation properties.
            // modelBuilder.Entity<Parent>()
            //     .HasOne(p => p.Entity)
            //     .WithOne(e => e.Parent)
            //     .HasForeignKey<EntityWithOwnedData>(e => e.Id); // Or a specific FK property if ParentId is on Entity
        }
    }

    public class OwnedEntityParentMovementTests
    {
        private readonly ITestOutputHelper _output;

        public OwnedEntityParentMovementTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static TestDbContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
            optionsBuilder.UseSqlite($"Data Source={Guid.NewGuid()}_one_to_one.db"); // Changed DB name
            optionsBuilder.EnableSensitiveDataLogging();
            var context = new TestDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public void Moving_Entity_Between_Parents_Should_Preserve_OwnedData()
        {
            using var context = CreateContext();
            _output.WriteLine("Testing with SQLite (One-to-One)");

            // Arrange
            var parent1 = new Parent();
            var parent2 = new Parent();

            var entity = new EntityWithOwnedData
            {
                Data = new OwnedData { Value = "Test Value" }
            };

            parent1.Entity = entity; // Changed from Entities.Add

            context.Parents.Add(parent1);
            context.Parents.Add(parent2);

            context.SaveChanges();

            Assert.NotNull(entity.Data);
            Assert.NotNull(parent1.Entity);
            Assert.Null(parent2.Entity);

            // Act: Move entity from parent1 to parent2
            parent1.Entity = null;       // Remove from parent1
            parent2.Entity = entity;     // Add to parent2
            // EF Core should automatically update entity.Parent due to fixup

            context.SaveChanges();

            // Assert
            Assert.Null(parent1.Entity);
            Assert.NotNull(parent2.Entity);
            Assert.Same(entity, parent2.Entity); // Check if it's the same instance

            // Fixup should work if Parent navigation property is correctly managed by EF Core
            Assert.NotNull(entity.Parent); // Ensure Parent is not null
            Assert.Equal(parent2.Id, entity.Parent.Id); // Check if Parent is parent2
            Assert.Same(parent2, entity.Parent); // Check if Parent instance is parent2

            // ... owned data should be preserved
            Assert.NotNull(entity.Data);
            Assert.Equal("Test Value", entity.Data.Value);
        }
    }
}
