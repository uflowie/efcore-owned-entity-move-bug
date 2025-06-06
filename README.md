# Owned Entity Collection Demo

This project demonstrates a bug in Entity Framework Core where owned entity data is deleted when moving an entity between parents.

## Issue Description

When moving an entity with owned data from one parent to another in Entity Framework Core, the owned data is unexpectedly deleted during `SaveChanges()`. The owned data should be preserved when moving an entity between parents.

See [BugReport.md](BugReport.md) for detailed information about the issue.

## Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for multi-database testing)

## Running the Tests

### Option 1: SQLite Only (Local - Easy)

For quick local reproduction of the bug:

```bash
dotnet test --filter "SqliteTests"
```

This runs only the SQLite test which requires no external dependencies.

### Option 2: All Database Providers (Docker - Complete)

To verify the bug across all providers:

```bash
docker-compose up --build tests
```

This will:
- Start PostgreSQL and SQL Server containers
- Build the test project with all database providers
- Run the test 3 times (once per database provider)
- Show the failing assertion for owned data preservation

### Option 3: All Providers Locally (Advanced)

Run all tests locally with your own database setup:

```bash
# Set up databases first (example with Docker)
docker run -d --name postgres -e POSTGRES_PASSWORD=password -p 5432:5432 postgres:15
docker run -d --name sqlserver -e SA_PASSWORD=YourStrong@Passw0rd -e ACCEPT_EULA=Y -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

# Then run all tests
dotnet test
```

## Project Structure

- `SqliteTests.cs` - SQLite-only test for easy local reproduction
- `MultiProviderTests.cs` - Tests against SQLite, PostgreSQL, and SQL Server
- `BugReport.md` - Detailed bug report with minimal reproduction code
- `docker-compose.yml` - Multi-database test environment
- `Dockerfile` - Container setup for running tests

## Expected Results

The test `Moving_Entity_Between_Parents_Should_Preserve_OwnedData` will **fail** on the final assertion:

```csharp
Assert.NotNull(entity.Data); // ❌ Fails - data is null after moving
```

This demonstrates the bug where owned entity data is lost when moving entities between parents.

## Database Providers Tested

- **SQLite** - In-memory database
- **PostgreSQL** - Containerized instance
- **SQL Server** - Containerized instance

The bug is consistent across all three providers, indicating it's a core EF issue rather than provider-specific.