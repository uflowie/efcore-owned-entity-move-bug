using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace EfCoreOwnedEntityParentMoveTests
{
    // Domain Models for parent entity movement tests
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Employee> Employees { get; set; } = new List<Employee>();
    }

    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; }
        public Address Address { get; set; }
        public ContactInfo ContactInfo { get; set; }
    }

    // Owned Types
    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string ZipCode { get; set; }
    }

    public class ContactInfo
    {
        public string Email { get; set; }
        public string Phone { get; set; }
        public string EmergencyContact { get; set; }
    }

    // Alternative structure for single entity relationships
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Manager Manager { get; set; }
    }

    public class Manager
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ProjectId { get; set; }
        public Project Project { get; set; }
        public ManagerProfile Profile { get; set; }
    }

    // Owned type for Manager
    public class ManagerProfile
    {
        public string Department { get; set; }
        public int YearsOfExperience { get; set; }
        public string Certification { get; set; }
    }

    // DbContext
    public class CompanyDbContext : DbContext
    {
        public DbSet<Company> Companies { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Manager> Managers { get; set; }

        public CompanyDbContext(DbContextOptions<CompanyDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure Company-Employee relationship
            modelBuilder.Entity<Company>()
                .HasMany(c => c.Employees)
                .WithOne(e => e.Company)
                .HasForeignKey(e => e.CompanyId);

            // Configure Employee owned types
            modelBuilder.Entity<Employee>()
                .OwnsOne(e => e.Address, address =>
                {
                    address.Property(a => a.Street).HasMaxLength(200);
                    address.Property(a => a.City).HasMaxLength(100);
                    address.Property(a => a.ZipCode).HasMaxLength(20);
                });

            modelBuilder.Entity<Employee>()
                .OwnsOne(e => e.ContactInfo, contact =>
                {
                    contact.Property(c => c.Email).HasMaxLength(100);
                    contact.Property(c => c.Phone).HasMaxLength(50);
                    contact.Property(c => c.EmergencyContact).HasMaxLength(200);
                });

            // Configure Project-Manager relationship
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Manager)
                .WithOne(m => m.Project)
                .HasForeignKey<Manager>(m => m.ProjectId);

            // Configure Manager owned type
            modelBuilder.Entity<Manager>()
                .OwnsOne(m => m.Profile, profile =>
                {
                    profile.Property(p => p.Department).HasMaxLength(100);
                    profile.Property(p => p.Certification).HasMaxLength(200);
                });
        }
    }

    public class OwnedEntityParentMovementTests : IDisposable
    {
        private readonly CompanyDbContext _context;
        private readonly ITestOutputHelper _output;

        public OwnedEntityParentMovementTests(ITestOutputHelper output)
        {
            _output = output;
            var options = new DbContextOptionsBuilder<CompanyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging()
                .Options;

            _context = new CompanyDbContext(options);
            _context.Database.EnsureCreated();
        }

        [Fact]
        public void Moving_Employee_Between_Companies_Should_Preserve_OwnedTypes()
        {
            // Arrange - Create two companies and an employee with owned types
            var sourceCompany = new Company { Name = "Source Corp" };
            var targetCompany = new Company { Name = "Target Corp" };
            
            var employee = new Employee 
            { 
                Name = "John Doe",
                Company = sourceCompany,
                Address = new Address 
                { 
                    Street = "123 Main St",
                    City = "Anytown",
                    ZipCode = "12345"
                },
                ContactInfo = new ContactInfo
                {
                    Email = "john@example.com",
                    Phone = "555-1234",
                    EmergencyContact = "Jane Doe - 555-5678"
                }
            };
            
            sourceCompany.Employees.Add(employee);
            _context.Companies.Add(sourceCompany);
            _context.Companies.Add(targetCompany);
            _context.SaveChanges();

            // Clear context to simulate a fresh retrieval
            _context.ChangeTracker.Clear();

            // Act - Move employee from source to target company
            var retrievedSourceCompany = _context.Companies
                .Include(c => c.Employees)
                .Single(c => c.Id == sourceCompany.Id);
            
            var retrievedTargetCompany = _context.Companies
                .Include(c => c.Employees)
                .Single(c => c.Id == targetCompany.Id);

            var employeeToMove = retrievedSourceCompany.Employees.Single();

            _output.WriteLine($"Before move - Employee Address is null: {employeeToMove.Address == null}");
            _output.WriteLine($"Before move - Address.Street: {employeeToMove.Address?.Street}");
            _output.WriteLine($"Before move - ContactInfo.Email: {employeeToMove.ContactInfo?.Email}");

            // Move the employee
            retrievedSourceCompany.Employees.Remove(employeeToMove);
            retrievedTargetCompany.Employees.Add(employeeToMove);

            _output.WriteLine($"\nAfter move, before SaveChanges - Address is null: {employeeToMove.Address == null}");
            _output.WriteLine($"After move, before SaveChanges - ContactInfo is null: {employeeToMove.ContactInfo == null}");

            // Check states
            LogEntityState(employeeToMove);

            _context.SaveChanges();

            // Assert - Check if owned types are preserved
            _output.WriteLine($"\nAfter SaveChanges - Address is null: {employeeToMove.Address == null}");
            _output.WriteLine($"After SaveChanges - ContactInfo is null: {employeeToMove.ContactInfo == null}");
            
            // These assertions will likely FAIL
            Assert.NotNull(employeeToMove.Address);
            Assert.Equal("123 Main St", employeeToMove.Address.Street);
            Assert.Equal("Anytown", employeeToMove.Address.City);
            Assert.Equal("12345", employeeToMove.Address.ZipCode);

            Assert.NotNull(employeeToMove.ContactInfo);
            Assert.Equal("john@example.com", employeeToMove.ContactInfo.Email);
            Assert.Equal("555-1234", employeeToMove.ContactInfo.Phone);

            // Verify the move happened
            Assert.Empty(retrievedSourceCompany.Employees);
            Assert.Single(retrievedTargetCompany.Employees);
        }

        [Fact]
        public void Moving_Manager_Between_Projects_Should_Preserve_Profile()
        {
            // Test with one-to-one relationship
            var sourceProject = new Project { Name = "Project Alpha" };
            var targetProject = new Project { Name = "Project Beta" };
            
            var manager = new Manager
            {
                Name = "Alice Smith",
                Project = sourceProject,
                Profile = new ManagerProfile
                {
                    Department = "Engineering",
                    YearsOfExperience = 10,
                    Certification = "PMP"
                }
            };

            sourceProject.Manager = manager;
            _context.Projects.Add(sourceProject);
            _context.Projects.Add(targetProject);
            _context.SaveChanges();

            _context.ChangeTracker.Clear();

            // Act - Move manager to different project
            var retrievedManager = _context.Managers
                .Include(m => m.Project)
                .Single(m => m.Id == manager.Id);

            var retrievedTargetProject = _context.Projects
                .Single(p => p.Id == targetProject.Id);

            _output.WriteLine($"Before move - Manager Profile is null: {retrievedManager.Profile == null}");
            _output.WriteLine($"Before move - Profile.Department: {retrievedManager.Profile?.Department}");

            // Change the manager's project
            retrievedManager.Project = retrievedTargetProject;
            retrievedTargetProject.Manager = retrievedManager;

            _context.SaveChanges();

            // Assert
            _output.WriteLine($"\nAfter SaveChanges - Profile is null: {retrievedManager.Profile == null}");
            
            // This assertion will likely FAIL
            Assert.NotNull(retrievedManager.Profile);
            Assert.Equal("Engineering", retrievedManager.Profile.Department);
            Assert.Equal(10, retrievedManager.Profile.YearsOfExperience);
            Assert.Equal("PMP", retrievedManager.Profile.Certification);
        }

        [Fact]
        public void Direct_Parent_Assignment_Change_Shows_Issue()
        {
            // Even simpler test - just change the parent FK directly
            var company1 = new Company { Name = "Company 1" };
            var company2 = new Company { Name = "Company 2" };
            
            var employee = new Employee 
            { 
                Name = "Test Employee",
                Company = company1,
                Address = new Address 
                { 
                    Street = "Test Street",
                    City = "Test City",
                    ZipCode = "00000"
                }
            };
            
            company1.Employees.Add(employee);
            _context.Companies.Add(company1);
            _context.Companies.Add(company2);
            _context.SaveChanges();

            _context.ChangeTracker.Clear();

            // Act - Just change the CompanyId
            var retrievedEmployee = _context.Employees
                .Include(e => e.Company)
                .Single(e => e.Id == employee.Id);

            _output.WriteLine($"Before change - CompanyId: {retrievedEmployee.CompanyId}");
            _output.WriteLine($"Before change - Address is null: {retrievedEmployee.Address == null}");

            // Just change the foreign key
            retrievedEmployee.CompanyId = company2.Id;

            _context.SaveChanges();

            // Assert
            _output.WriteLine($"\nAfter SaveChanges - CompanyId: {retrievedEmployee.CompanyId}");
            _output.WriteLine($"After SaveChanges - Address is null: {retrievedEmployee.Address == null}");
            
            Assert.NotNull(retrievedEmployee.Address);
            Assert.Equal("Test Street", retrievedEmployee.Address.Street);
        }

        [Fact]
        public void Workaround_Recreating_OwnedTypes_When_Moving_Parents()
        {
            // Demonstrate workaround for moving between parents
            var sourceCompany = new Company { Name = "Source Corp" };
            var targetCompany = new Company { Name = "Target Corp" };
            
            var employee = new Employee 
            { 
                Name = "Worker",
                Company = sourceCompany,
                Address = new Address 
                { 
                    Street = "456 Oak Ave",
                    City = "Somewhere",
                    ZipCode = "67890"
                },
                ContactInfo = new ContactInfo
                {
                    Email = "worker@example.com",
                    Phone = "555-9999",
                    EmergencyContact = "Emergency Contact"
                }
            };
            
            sourceCompany.Employees.Add(employee);
            _context.Companies.Add(sourceCompany);
            _context.Companies.Add(targetCompany);
            _context.SaveChanges();

            _context.ChangeTracker.Clear();

            // Act with workaround
            var retrievedSourceCompany = _context.Companies
                .Include(c => c.Employees)
                .Single(c => c.Id == sourceCompany.Id);
            
            var retrievedTargetCompany = _context.Companies
                .Include(c => c.Employees)
                .Single(c => c.Id == targetCompany.Id);

            var employeeToMove = retrievedSourceCompany.Employees.Single();

            // Store owned type data
            var addressData = new 
            { 
                employeeToMove.Address.Street, 
                employeeToMove.Address.City, 
                employeeToMove.Address.ZipCode 
            };
            var contactData = new 
            { 
                employeeToMove.ContactInfo.Email, 
                employeeToMove.ContactInfo.Phone, 
                employeeToMove.ContactInfo.EmergencyContact 
            };

            // Move employee
            retrievedSourceCompany.Employees.Remove(employeeToMove);
            retrievedTargetCompany.Employees.Add(employeeToMove);

            // Recreate owned types
            employeeToMove.Address = new Address
            {
                Street = addressData.Street,
                City = addressData.City,
                ZipCode = addressData.ZipCode
            };
            employeeToMove.ContactInfo = new ContactInfo
            {
                Email = contactData.Email,
                Phone = contactData.Phone,
                EmergencyContact = contactData.EmergencyContact
            };

            _context.SaveChanges();

            // Assert - This should PASS with the workaround
            Assert.NotNull(employeeToMove.Address);
            Assert.Equal("456 Oak Ave", employeeToMove.Address.Street);
            Assert.NotNull(employeeToMove.ContactInfo);
            Assert.Equal("worker@example.com", employeeToMove.ContactInfo.Email);
        }

        [Fact]
        public void Diagnostic_Parent_Move_With_Detailed_Tracking()
        {
            // Detailed diagnostic test
            var company1 = new Company { Name = "Company 1" };
            var company2 = new Company { Name = "Company 2" };
            
            var employee = new Employee 
            { 
                Name = "Diagnostic Test",
                Company = company1,
                Address = new Address { Street = "A", City = "B", ZipCode = "C" }
            };
            
            company1.Employees.Add(employee);
            _context.Companies.Add(company1);
            _context.Companies.Add(company2);
            _context.SaveChanges();
            
            var employeeId = employee.Id;
            _context.ChangeTracker.Clear();

            // Retrieve and log
            var emp = _context.Employees.Find(employeeId);
            
            _output.WriteLine("=== Initial State ===");
            LogDetailedState(emp);

            // Change parent
            emp.CompanyId = company2.Id;
            
            _output.WriteLine("\n=== After Parent Change ===");
            LogDetailedState(emp);

            _context.ChangeTracker.DetectChanges();
            
            _output.WriteLine("\n=== After DetectChanges ===");
            LogDetailedState(emp);

            _context.SaveChanges();
            
            _output.WriteLine("\n=== After SaveChanges ===");
            _output.WriteLine($"Address is null: {emp.Address == null}");
            if (emp.Address != null)
            {
                _output.WriteLine($"Address values: {emp.Address.Street}, {emp.Address.City}, {emp.Address.ZipCode}");
            }
        }

        private void LogEntityState(Employee employee)
        {
            var entry = _context.Entry(employee);
            _output.WriteLine($"\nEmployee State: {entry.State}");
            _output.WriteLine($"CompanyId: Current={entry.CurrentValues["CompanyId"]}, Original={entry.OriginalValues["CompanyId"]}");
            
            if (employee.Address != null)
            {
                var addressEntry = _context.Entry(employee.Address);
                _output.WriteLine($"Address State: {addressEntry.State}");
            }
            
            if (employee.ContactInfo != null)
            {
                var contactEntry = _context.Entry(employee.ContactInfo);
                _output.WriteLine($"ContactInfo State: {contactEntry.State}");
            }
        }

        private void LogDetailedState(Employee employee)
        {
            var entry = _context.Entry(employee);
            _output.WriteLine($"Employee: State={entry.State}, CompanyId={entry.CurrentValues["CompanyId"]}");
            
            // Log all tracked entities
            _output.WriteLine("All tracked entities:");
            foreach (var trackedEntry in _context.ChangeTracker.Entries())
            {
                _output.WriteLine($"  {trackedEntry.Entity.GetType().Name}: State={trackedEntry.State}");
                if (trackedEntry.Entity is Address || trackedEntry.Entity is ContactInfo || trackedEntry.Entity is ManagerProfile)
                {
                    foreach (var prop in trackedEntry.Properties)
                    {
                        _output.WriteLine($"    {prop.Metadata.Name}: {prop.CurrentValue}");
                    }
                }
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}