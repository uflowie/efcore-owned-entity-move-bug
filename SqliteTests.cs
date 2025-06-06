using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace EfCoreOwnedEntityParentMoveTestsSimple
{
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

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityWithOwnedData>()
                .OwnsOne(e => e.Data);
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
            optionsBuilder.UseSqlite($"Data Source={Guid.NewGuid()}.db");
            optionsBuilder.EnableSensitiveDataLogging();
            var context = new TestDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public void Moving_Entity_Between_Parents_Should_Preserve_OwnedData()
        {
            using var context = CreateContext();
            _output.WriteLine("Testing with SQLite");

            // Arrange
            var parent1 = new Parent();
            var parent2 = new Parent();

            var entity = new EntityWithOwnedData
            {
                Data = new OwnedData { Value = "Test Value" }
            };

            parent1.Entities.Add(entity);

            context.Parents.Add(parent1);
            context.Parents.Add(parent2);

            context.SaveChanges();

            Assert.NotNull(entity.Data);

            parent1.Entities.Remove(entity);
            parent2.Entities.Add(entity);

            context.SaveChanges();

            Assert.Equal(1, parent2.Entities.Count);

            // fixup works fine...
            Assert.Equal(entity.Parent, parent2);

            // ... but owned data is null
            Assert.NotNull(entity.Data);
        }
    }
}