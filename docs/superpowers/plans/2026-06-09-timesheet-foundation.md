# Timesheet Tracker — Foundation & Domain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the design-independent foundation of the Timesheet Tracker — project scaffolding, EF Core data model, Identity + Google auth wiring, and the domain/export services — all under TDD, ready for the Blazor UI (separate plan) once claude design completes.

**Architecture:** A .NET 10 solution with a `DataModel` class library (EF Core entities + DbContext + services), a `Web` Blazor Web App (Interactive Server) that hosts Identity, and two NUnit test projects. Domain calculations (durations, decimal hours, public holidays, exports) live as injectable services in `DataModel/Services` so they are unit-testable without the UI.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, ASP.NET Core Identity + Google auth, AustralianHolidays, ExcelsiorClosedXml, NUnit + Verify.NUnit + FluentAssertions + Testcontainers.PostgreSql + Respawn, bUnit + Verify.Bunit (UI plan).

**Conventions (from VendingMachineTracker):** central package management; `BaseEntity` (`Guid Id`, `CreatedDate`, `ModifiedDate`); nested `Configuration : IEntityTypeConfiguration<T>` per entity wired via `ApplyConfigurationsFromAssembly`; **entity classes in the global namespace, enums namespaced** (`TimesheetTracker.DataModel.Enums`); `ImplicitUsings` + `Nullable` enabled; `GlobalUsings` for `System.ComponentModel.DataAnnotations`.

**Scope note:** Blazor UI components (weekly grid, calendar, job editor, profile, export screen) are intentionally **out of scope here** — they get their own plan after the design handoff. This plan stops at services + a thin export surface the UI will call.

---

## File Structure

**`src/DataModel/`** (class library, `RootNamespace` = `TimesheetTracker.DataModel`)
- `DataModel.csproj` — EF Core, Npgsql, Identity, AustralianHolidays, Excelsior
- `GlobalUsings.cs` — `global using System.ComponentModel.DataAnnotations;`
- `Enums/AustralianState.cs`, `Enums/DaysOfWeek.cs`
- `Models/BaseEntity.cs`, `AppUser.cs`, `Job.cs`, `JobCustomField.cs`, `ProjectCode.cs`, `TimeEntry.cs`
- `TimesheetDbContext.cs`
- `Services/ITimeCalculationService.cs` + `TimeCalculationService.cs`
- `Services/IHolidayService.cs` + `HolidayService.cs`
- `Services/Export/` — `TimesheetRow.cs`, `IExportService.cs`, `ExportService.cs`
- `Services/StateMappingExtensions.cs` — maps `AustralianState` ↔ AustralianHolidays `State`
- `Migrations/` — generated

**`src/Web/`** (Blazor Web App, Interactive Server) — auth + DI host only in this plan
- `Web.csproj`, `Program.cs`, `appsettings.json`

**`tests/DataModel.Tests/`** — NUnit + Verify + Testcontainers
- `DataModel.Tests.csproj`, `GlobalUsings.cs`, `VerifyGlobalSettings.cs`
- `UnitTests/TimeCalculationServiceTests.cs`, `HolidayServiceTests.cs`, `ExportServiceTests.cs`
- `IntegrationTests/TestContainerBase.cs`, `DbContextModelTests.cs`

---

## Task 0: Solution scaffolding & central packages

**Files:**
- Create: `Directory.Packages.props`, `TimesheetTracker.slnx`
- Create: `src/DataModel/DataModel.csproj`, `src/Web/Web.csproj`, `tests/DataModel.Tests/DataModel.Tests.csproj`
- Existing: `.editorconfig`, `Directory.Build.props` (already copied)

- [ ] **Step 1: Create `Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>false</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageVersion>
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.8" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.8" />
    <PackageVersion Include="AustralianHolidays" Version="1.1.0" />
    <PackageVersion Include="ExcelsiorClosedXml" Version="1.7.2" />
    <PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
    <!-- Test -->
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="FluentAssertions" Version="8.10.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageVersion Include="NUnit" Version="4.6.1" />
    <PackageVersion Include="NUnit3TestAdapter" Version="6.2.0" />
    <PackageVersion Include="NUnit.Analyzers" Version="4.14.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageVersion>
    <PackageVersion Include="Verify.NUnit" Version="31.19.0" />
    <PackageVersion Include="Verify.EntityFramework" Version="15.2.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.12.0" />
    <PackageVersion Include="Respawn" Version="7.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the three project files**

`src/DataModel/DataModel.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>TimesheetTracker.DataModel</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="AustralianHolidays" />
    <PackageReference Include="ExcelsiorClosedXml" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="DataModel.Tests" />
  </ItemGroup>
</Project>
```

`src/Web/Web.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>TimesheetTracker.Web</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Google" />
    <PackageReference Include="Serilog.AspNetCore" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DataModel\DataModel.csproj" />
  </ItemGroup>
</Project>
```

`tests/DataModel.Tests/DataModel.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
    <PackageReference Include="NUnit.Analyzers" />
    <PackageReference Include="Verify.NUnit" />
    <PackageReference Include="Verify.EntityFramework" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Respawn" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DataModel\DataModel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `TimesheetTracker.slnx`**

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/DataModel/DataModel.csproj" />
    <Project Path="src/Web/Web.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/DataModel.Tests/DataModel.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 4: Add a minimal `src/Web/Program.cs` and `appsettings.json` so the solution builds**

`src/Web/Program.cs`:
```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "Timesheet Tracker");
app.Run();
```

`src/Web/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=timesheet;Username=postgres;Password=postgres"
  }
}
```

- [ ] **Step 5: Add `src/DataModel/GlobalUsings.cs`**

```csharp
global using System.ComponentModel.DataAnnotations;
```

- [ ] **Step 6: Restore & build**

Run: `dotnet build TimesheetTracker.slnx`
Expected: Build succeeded, 0 errors. (DataModel has no code yet beyond usings; Web returns a string.)

- [ ] **Step 7: Commit**

```bash
git checkout -b feat/foundation
git add -A
git commit -m "chore: scaffold solution, projects, and central packages"
```

---

## Task 1: Enums

**Files:**
- Create: `src/DataModel/Enums/AustralianState.cs`, `src/DataModel/Enums/DaysOfWeek.cs`

- [ ] **Step 1: Create `AustralianState.cs`**

```csharp
namespace TimesheetTracker.DataModel.Enums;

public enum AustralianState
{
    NSW,
    VIC,
    QLD,
    SA,
    WA,
    TAS,
    NT,
    ACT
}
```

- [ ] **Step 2: Create `DaysOfWeek.cs`**

```csharp
namespace TimesheetTracker.DataModel.Enums;

[Flags]
public enum DaysOfWeek
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,

    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday
}
```

- [ ] **Step 3: Build & commit**

Run: `dotnet build src/DataModel/DataModel.csproj` → Expected: success.
```bash
git add -A && git commit -m "feat: add AustralianState and DaysOfWeek enums"
```

---

## Task 2: BaseEntity & AppUser

**Files:**
- Create: `src/DataModel/Models/BaseEntity.cs`, `src/DataModel/Models/AppUser.cs`

- [ ] **Step 1: Create `BaseEntity.cs`** (entity classes use the global namespace per convention)

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }
}
```

- [ ] **Step 2: Create `AppUser.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using TimesheetTracker.DataModel.Enums;

public class AppUser : IdentityUser<Guid>
{
    [MaxLength(200)]
    public string DisplayName { get; set; } = null!;

    public AustralianState DefaultState { get; set; } = AustralianState.NSW;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
```

- [ ] **Step 3: Build & commit** (will not fully compile until `Job` exists — Task 3 follows immediately; build after Task 3)

```bash
git add -A && git commit -m "feat: add BaseEntity and AppUser"
```

---

## Task 3: Job entity + configuration

**Files:**
- Create: `src/DataModel/Models/Job.cs`

- [ ] **Step 1: Create `Job.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimesheetTracker.DataModel.Enums;

public class Job : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>Overrides the user's DefaultState for holiday calculation when set.</summary>
    public AustralianState? StateOverride { get; set; }

    /// <summary>Decimal places for the copyable decimal-hours output. Default 2.</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Scheduled work days; drives grid emphasis only, never blocks entry.</summary>
    public DaysOfWeek WorkDays { get; set; } = DaysOfWeek.Weekdays;

    public bool IsArchived { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<JobCustomField> CustomFields { get; set; } = new List<JobCustomField>();
    public ICollection<ProjectCode> ProjectCodes { get; set; } = new List<ProjectCode>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();

    public class Configuration : IEntityTypeConfiguration<Job>
    {
        public void Configure(EntityTypeBuilder<Job> builder)
        {
            builder
                .HasOne(j => j.User)
                .WithMany(u => u.Jobs)
                .HasForeignKey(j => j.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(j => j.UserId);
        }
    }
}
```

- [ ] **Step 2: Build & commit**

Run: `dotnet build src/DataModel/DataModel.csproj`
Expected: fails until `JobCustomField`, `ProjectCode`, `TimeEntry` exist (Tasks 4–6). Proceed to Task 4; build at end of Task 6.
```bash
git add -A && git commit -m "feat: add Job entity and configuration"
```

---

## Task 4: JobCustomField

**Files:**
- Create: `src/DataModel/Models/JobCustomField.cs`

- [ ] **Step 1: Create `JobCustomField.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class JobCustomField : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string Value { get; set; } = null!;

    /// <summary>When true, shown in the weekly grid and export header.</summary>
    public bool ShowOnTimesheet { get; set; }

    public int DisplayOrder { get; set; }

    public class Configuration : IEntityTypeConfiguration<JobCustomField>
    {
        public void Configure(EntityTypeBuilder<JobCustomField> builder)
        {
            builder
                .HasOne(f => f.Job)
                .WithMany(j => j.CustomFields)
                .HasForeignKey(f => f.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(f => f.JobId);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: add JobCustomField entity"
```

---

## Task 5: ProjectCode

**Files:**
- Create: `src/DataModel/Models/ProjectCode.cs`

- [ ] **Step 1: Create `ProjectCode.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ProjectCode : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    [MaxLength(100)]
    public string Code { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();

    public class Configuration : IEntityTypeConfiguration<ProjectCode>
    {
        public void Configure(EntityTypeBuilder<ProjectCode> builder)
        {
            builder
                .HasOne(p => p.Job)
                .WithMany(j => j.ProjectCodes)
                .HasForeignKey(p => p.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.JobId);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: add ProjectCode entity"
```

---

## Task 6: TimeEntry

**Files:**
- Create: `src/DataModel/Models/TimeEntry.cs`

- [ ] **Step 1: Create `TimeEntry.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TimeEntry : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    /// <summary>The day the entry is filed under. For a midnight-spanner, the day it starts.</summary>
    public DateOnly WorkDate { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>True when the shift ends after midnight on the following day.</summary>
    public bool EndsNextDay { get; set; }

    public int BreakMinutes { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid? ProjectCodeId { get; set; }
    public ProjectCode? ProjectCode { get; set; }

    public class Configuration : IEntityTypeConfiguration<TimeEntry>
    {
        public void Configure(EntityTypeBuilder<TimeEntry> builder)
        {
            builder
                .HasOne(e => e.Job)
                .WithMany(j => j.TimeEntries)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(e => e.ProjectCode)
                .WithMany(p => p.TimeEntries)
                .HasForeignKey(e => e.ProjectCodeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(e => new { e.JobId, e.WorkDate });
        }
    }
}
```

- [ ] **Step 2: Build the whole DataModel**

Run: `dotnet build src/DataModel/DataModel.csproj`
Expected: Build succeeded (all entity references now resolve).

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add TimeEntry entity"
```

---

## Task 7: DbContext

**Files:**
- Create: `src/DataModel/TimesheetDbContext.cs`

- [ ] **Step 1: Create `TimesheetDbContext.cs`**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TimesheetTracker.DataModel;

public class TimesheetDbContext(DbContextOptions<TimesheetDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Job> Jobs { get; set; }
    public DbSet<JobCustomField> JobCustomFields { get; set; }
    public DbSet<ProjectCode> ProjectCodes { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<AppUser>().ToTable("Users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles");
    }
}
```

- [ ] **Step 2: Build & commit**

Run: `dotnet build src/DataModel/DataModel.csproj` → Expected: success.
```bash
git add -A && git commit -m "feat: add TimesheetDbContext"
```

---

## Task 8: TimeCalculationService (TDD)

**Files:**
- Create: `src/DataModel/Services/ITimeCalculationService.cs`, `src/DataModel/Services/TimeCalculationService.cs`
- Test: `tests/DataModel.Tests/UnitTests/TimeCalculationServiceTests.cs`
- Test infra: `tests/DataModel.Tests/GlobalUsings.cs`

This is the core domain logic. Build it test-first.

- [ ] **Step 1: Add `tests/DataModel.Tests/GlobalUsings.cs`**

```csharp
global using NUnit.Framework;
global using FluentAssertions;
```

- [ ] **Step 2: Write the failing test file**

`tests/DataModel.Tests/UnitTests/TimeCalculationServiceTests.cs`:
```csharp
using TimesheetTracker.DataModel.Services;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class TimeCalculationServiceTests
{
    private readonly ITimeCalculationService _service = new TimeCalculationService();

    [Test]
    public void DurationMinutes_SimpleShift_SubtractsBreak()
    {
        var entry = Entry(start: new(9, 0), end: new(17, 0), breakMinutes: 30);
        _service.DurationMinutes(entry).Should().Be(450); // 8h - 30m
    }

    [Test]
    public void DurationMinutes_PastMidnight_AddsADay()
    {
        var entry = Entry(start: new(22, 0), end: new(6, 0), breakMinutes: 0, endsNextDay: true);
        _service.DurationMinutes(entry).Should().Be(480); // 8h
    }

    [Test]
    public void DecimalHours_RoundsToJobPrecision()
    {
        var entry = Entry(start: new(9, 0), end: new(17, 15), breakMinutes: 0); // 8.25h
        _service.DecimalHours(entry, decimalPlaces: 2).Should().Be(8.25m);
        _service.DecimalHours(entry, decimalPlaces: 1).Should().Be(8.3m);
    }

    [Test]
    public void HoursMinutes_FormatsAsHColonMM()
    {
        var entry = Entry(start: new(9, 0), end: new(16, 30), breakMinutes: 0);
        _service.HoursMinutes(entry).Should().Be("7:30");
    }

    [Test]
    public void DurationMinutes_NonPositive_ReturnsZero()
    {
        var entry = Entry(start: new(9, 0), end: new(9, 0), breakMinutes: 30);
        _service.DurationMinutes(entry).Should().Be(0);
    }

    private static TimeEntry Entry(TimeOnly start, TimeOnly end, int breakMinutes, bool endsNextDay = false) =>
        new()
        {
            StartTime = start,
            EndTime = end,
            BreakMinutes = breakMinutes,
            EndsNextDay = endsNextDay,
            WorkDate = new DateOnly(2026, 6, 8)
        };
}
```

- [ ] **Step 3: Create the interface so the test compiles to a failing (red) state**

`src/DataModel/Services/ITimeCalculationService.cs`:
```csharp
namespace TimesheetTracker.DataModel.Services;

public interface ITimeCalculationService
{
    int DurationMinutes(TimeEntry entry);
    decimal DecimalHours(TimeEntry entry, int decimalPlaces);
    string HoursMinutes(TimeEntry entry);
}
```

Create a stub `TimeCalculationService` that throws, so the build succeeds and tests fail:
```csharp
namespace TimesheetTracker.DataModel.Services;

public class TimeCalculationService : ITimeCalculationService
{
    public int DurationMinutes(TimeEntry entry) => throw new NotImplementedException();
    public decimal DecimalHours(TimeEntry entry, int decimalPlaces) => throw new NotImplementedException();
    public string HoursMinutes(TimeEntry entry) => throw new NotImplementedException();
}
```

- [ ] **Step 4: Run tests — verify they fail**

Run: `dotnet test tests/DataModel.Tests/DataModel.Tests.csproj --filter TimeCalculationServiceTests`
Expected: FAIL (NotImplementedException).

- [ ] **Step 5: Implement `TimeCalculationService`**

```csharp
namespace TimesheetTracker.DataModel.Services;

public class TimeCalculationService : ITimeCalculationService
{
    public int DurationMinutes(TimeEntry entry)
    {
        var start = entry.StartTime.ToTimeSpan();
        var end = entry.EndTime.ToTimeSpan();
        if (entry.EndsNextDay)
        {
            end += TimeSpan.FromDays(1);
        }

        var worked = (int)(end - start).TotalMinutes - entry.BreakMinutes;
        return worked > 0 ? worked : 0;
    }

    public decimal DecimalHours(TimeEntry entry, int decimalPlaces)
    {
        var hours = DurationMinutes(entry) / 60m;
        return Math.Round(hours, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public string HoursMinutes(TimeEntry entry)
    {
        var minutes = DurationMinutes(entry);
        return $"{minutes / 60}:{minutes % 60:D2}";
    }
}
```

- [ ] **Step 6: Run tests — verify they pass**

Run: `dotnet test tests/DataModel.Tests/DataModel.Tests.csproj --filter TimeCalculationServiceTests`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: add TimeCalculationService with duration/decimal/hh:mm logic"
```

---

## Task 9: HolidayService (TDD)

**Files:**
- Create: `src/DataModel/Services/StateMappingExtensions.cs`, `src/DataModel/Services/IHolidayService.cs`, `src/DataModel/Services/HolidayService.cs`
- Test: `tests/DataModel.Tests/UnitTests/HolidayServiceTests.cs`

> **Library note (verify at execution):** AustralianHolidays v1.1.0 exposes extension methods on a date value, e.g. `date.IsHoliday(State.NSW, out var name)`. Confirm whether the receiver is `DateOnly` or the package's `Date` type and adjust the single call site in `HolidayService` accordingly. The service interface below shields all callers from that detail.

- [ ] **Step 1: Create `StateMappingExtensions.cs`**

```csharp
using AustralianHolidays;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

internal static class StateMappingExtensions
{
    public static State ToHolidayState(this AustralianState state) => state switch
    {
        AustralianState.NSW => State.NSW,
        AustralianState.VIC => State.VIC,
        AustralianState.QLD => State.QLD,
        AustralianState.SA => State.SA,
        AustralianState.WA => State.WA,
        AustralianState.TAS => State.TAS,
        AustralianState.NT => State.NT,
        AustralianState.ACT => State.ACT,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
```

- [ ] **Step 2: Create `IHolidayService.cs`**

```csharp
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

public interface IHolidayService
{
    bool IsPublicHoliday(DateOnly date, AustralianState state, out string? name);
}
```

- [ ] **Step 3: Write failing test**

`tests/DataModel.Tests/UnitTests/HolidayServiceTests.cs`:
```csharp
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Services;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class HolidayServiceTests
{
    private readonly IHolidayService _service = new HolidayService();

    [Test]
    public void ChristmasDay_IsHoliday_InNsw()
    {
        var isHoliday = _service.IsPublicHoliday(new DateOnly(2026, 12, 25), AustralianState.NSW, out var name);
        isHoliday.Should().BeTrue();
        name.Should().Be("Christmas Day");
    }

    [Test]
    public void OrdinaryWeekday_IsNotHoliday()
    {
        var isHoliday = _service.IsPublicHoliday(new DateOnly(2026, 6, 9), AustralianState.NSW, out var name);
        isHoliday.Should().BeFalse();
        name.Should().BeNull();
    }
}
```

- [ ] **Step 4: Stub `HolidayService` (throws), run tests, verify red**

```csharp
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

public class HolidayService : IHolidayService
{
    public bool IsPublicHoliday(DateOnly date, AustralianState state, out string? name) =>
        throw new NotImplementedException();
}
```

Run: `dotnet test ... --filter HolidayServiceTests` → Expected: FAIL.

- [ ] **Step 5: Implement `HolidayService`** (adjust the `IsHoliday` call to the confirmed receiver type)

```csharp
using AustralianHolidays;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

public class HolidayService : IHolidayService
{
    public bool IsPublicHoliday(DateOnly date, AustralianState state, out string? name)
    {
        // AustralianHolidays exposes IsHoliday(state, out name) as an extension.
        // If the receiver is the package Date type, convert: new Date(date.Year, date.Month, date.Day).
        return date.IsHoliday(state.ToHolidayState(), out name);
    }
}
```

- [ ] **Step 6: Run tests — verify pass**

Run: `dotnet test ... --filter HolidayServiceTests` → Expected: PASS.
> If the holiday name differs (e.g. "Christmas Day" vs a regional variant), update the expected string to match the library's output — the library is the source of truth.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: add HolidayService over AustralianHolidays"
```

---

## Task 10: Export service (TDD with Verify)

**Files:**
- Create: `src/DataModel/Services/Export/TimesheetRow.cs`, `ExportPeriod.cs`, `IExportService.cs`, `ExportService.cs`
- Test: `tests/DataModel.Tests/UnitTests/ExportServiceTests.cs`, `tests/DataModel.Tests/VerifyGlobalSettings.cs`

The export service maps `TimeEntry` rows (already loaded + scoped by the caller) into an `.xlsx` workbook. It takes the rows and job precision; period selection (week/month/FY) is computed by a small helper and tested directly.

- [ ] **Step 1: Create `ExportPeriod.cs`** (financial-year + week/month boundary helper)

```csharp
namespace TimesheetTracker.DataModel.Services.Export;

public enum ExportScope { Week, Month, FinancialYear }

public static class ExportPeriod
{
    /// <summary>Returns the inclusive [start, end] date range for the scope containing <paramref name="anchor"/>.</summary>
    public static (DateOnly Start, DateOnly End) Range(ExportScope scope, DateOnly anchor) => scope switch
    {
        ExportScope.Week => WeekRange(anchor),
        ExportScope.Month => (new DateOnly(anchor.Year, anchor.Month, 1),
                              new DateOnly(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))),
        ExportScope.FinancialYear => FinancialYearRange(anchor),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static (DateOnly, DateOnly) WeekRange(DateOnly anchor)
    {
        var delta = ((int)anchor.DayOfWeek + 6) % 7; // Monday = 0
        var monday = anchor.AddDays(-delta);
        return (monday, monday.AddDays(6));
    }

    private static (DateOnly, DateOnly) FinancialYearRange(DateOnly anchor)
    {
        var startYear = anchor.Month >= 7 ? anchor.Year : anchor.Year - 1;
        return (new DateOnly(startYear, 7, 1), new DateOnly(startYear + 1, 6, 30));
    }
}
```

- [ ] **Step 2: Write failing tests for `ExportPeriod`**

`tests/DataModel.Tests/UnitTests/ExportServiceTests.cs`:
```csharp
using TimesheetTracker.DataModel.Services.Export;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class ExportPeriodTests
{
    [Test]
    public void Week_StartsMonday_EndsSunday()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.Week, new DateOnly(2026, 6, 10)); // Wed
        start.Should().Be(new DateOnly(2026, 6, 8));  // Mon
        end.Should().Be(new DateOnly(2026, 6, 14));   // Sun
    }

    [Test]
    public void Month_CoversWholeMonth()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.Month, new DateOnly(2026, 2, 15));
        start.Should().Be(new DateOnly(2026, 2, 1));
        end.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Test]
    public void FinancialYear_BeforeJuly_StartsPreviousJuly()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.FinancialYear, new DateOnly(2026, 3, 1));
        start.Should().Be(new DateOnly(2025, 7, 1));
        end.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Test]
    public void FinancialYear_FromJuly_StartsSameYearJuly()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.FinancialYear, new DateOnly(2026, 8, 1));
        start.Should().Be(new DateOnly(2026, 7, 1));
        end.Should().Be(new DateOnly(2027, 6, 30));
    }
}
```

- [ ] **Step 3: Run — verify pass** (this helper is implemented in Step 1)

Run: `dotnet test ... --filter ExportPeriodTests` → Expected: PASS (4 tests).

- [ ] **Step 4: Create the export row + interface**

`src/DataModel/Services/Export/TimesheetRow.cs`:
```csharp
using Excelsior;

namespace TimesheetTracker.DataModel.Services.Export;

public class TimesheetRow
{
    [Column(Heading = "Date", Order = 1)]
    public string Date { get; set; } = null!;

    [Column(Heading = "Day", Order = 2)]
    public string Day { get; set; } = null!;

    [Column(Heading = "Start", Order = 3)]
    public string Start { get; set; } = null!;

    [Column(Heading = "End", Order = 4)]
    public string End { get; set; } = null!;

    [Column(Heading = "Break (min)", Order = 5)]
    public int BreakMinutes { get; set; }

    [Column(Heading = "Hours (decimal)", Order = 6)]
    public decimal DecimalHours { get; set; }

    [Column(Heading = "Hours (h:mm)", Order = 7)]
    public string HoursMinutes { get; set; } = null!;

    [Column(Heading = "Project Code", Order = 8)]
    public string? ProjectCode { get; set; }

    [Column(Heading = "Public Holiday", Order = 9)]
    public string? PublicHoliday { get; set; }

    [Column(Heading = "Notes", Order = 10)]
    public string? Notes { get; set; }
}
```

`src/DataModel/Services/Export/IExportService.cs`:
```csharp
namespace TimesheetTracker.DataModel.Services.Export;

public interface IExportService
{
    IReadOnlyList<TimesheetRow> BuildRows(IEnumerable<TimeEntry> entries, AustralianStateContext context);
    Task<byte[]> ToWorkbookAsync(IReadOnlyList<TimesheetRow> rows);
}

/// <summary>The effective state + job precision needed to render rows.</summary>
public record AustralianStateContext(Enums.AustralianState State, int DecimalPlaces);
```

- [ ] **Step 5: Implement `ExportService`** (composes calc + holiday services)

`src/DataModel/Services/Export/ExportService.cs`:
```csharp
using Excelsior;

namespace TimesheetTracker.DataModel.Services.Export;

public class ExportService(ITimeCalculationService calc, IHolidayService holidays) : IExportService
{
    public IReadOnlyList<TimesheetRow> BuildRows(IEnumerable<TimeEntry> entries, AustralianStateContext context)
    {
        var rows = new List<TimesheetRow>();
        foreach (var e in entries.OrderBy(e => e.WorkDate).ThenBy(e => e.StartTime))
        {
            var isHoliday = holidays.IsPublicHoliday(e.WorkDate, context.State, out var holidayName);
            rows.Add(new TimesheetRow
            {
                Date = e.WorkDate.ToString("yyyy-MM-dd"),
                Day = e.WorkDate.DayOfWeek.ToString(),
                Start = e.StartTime.ToString("HH:mm"),
                End = e.EndTime.ToString("HH:mm"),
                BreakMinutes = e.BreakMinutes,
                DecimalHours = calc.DecimalHours(e, context.DecimalPlaces),
                HoursMinutes = calc.HoursMinutes(e),
                ProjectCode = e.ProjectCode?.Code,
                PublicHoliday = isHoliday ? holidayName : null,
                Notes = e.Notes
            });
        }
        return rows;
    }

    public async Task<byte[]> ToWorkbookAsync(IReadOnlyList<TimesheetRow> rows)
    {
        var builder = new BookBuilder();
        builder.AddSheet(rows);
        return await builder.ToBytes();
    }
}
```

- [ ] **Step 6: Write a Verify snapshot test for `BuildRows`**

Add to `ExportServiceTests.cs`:
```csharp
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Services;

[TestFixture]
public class ExportServiceTests
{
    [Test]
    public Task BuildRows_ProducesExpectedColumns()
    {
        var service = new ExportService(new TimeCalculationService(), new HolidayService());
        var entries = new[]
        {
            new TimeEntry
            {
                WorkDate = new DateOnly(2026, 12, 25),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                BreakMinutes = 30,
                Notes = "Worked the holiday"
            }
        };

        var rows = service.BuildRows(entries, new AustralianStateContext(AustralianState.NSW, 2));
        return Verify(rows);
    }
}
```

- [ ] **Step 7: Add `VerifyGlobalSettings.cs`**

```csharp
using System.Runtime.CompilerServices;

public static class VerifyGlobalSettings
{
    [ModuleInitializer]
    public static void Init() => VerifierSettings.InitializePlugins();
}
```

Add `global using VerifyNUnit;` and `global using static VerifyNUnit.Verifier;` to `tests/DataModel.Tests/GlobalUsings.cs`.

- [ ] **Step 8: Run — verify (first run creates the snapshot)**

Run: `dotnet test ... --filter ExportServiceTests`
Expected: First run FAILS and writes `*.received.txt`; review it, rename to `*.verified.txt` (or accept via the Verify diff tool), re-run → PASS.

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: add export service (rows + xlsx) with period helper"
```

---

## Task 11: DI registration + Web auth wiring

**Files:**
- Create: `src/DataModel/ServiceCollectionExtensions.cs`
- Modify: `src/Web/Program.cs`, `src/Web/appsettings.json`

- [ ] **Step 1: Create `ServiceCollectionExtensions.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;

namespace TimesheetTracker.DataModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetDataModel(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TimesheetDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IExportService, ExportService>();
        return services;
    }
}
```

- [ ] **Step 2: Wire `Program.cs`** (Identity + Google + DataModel)

```csharp
using Microsoft.AspNetCore.Identity;
using TimesheetTracker.DataModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTimesheetDataModel(builder.Configuration.GetConnectionString("Default")!);

builder.Services
    .AddIdentity<AppUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<TimesheetDbContext>()
    .AddDefaultTokenProviders();

var authBuilder = builder.Services.AddAuthentication();
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleSecret))
{
    authBuilder.AddGoogle(o =>
    {
        o.ClientId = googleClientId;
        o.ClientSecret = googleSecret;
    });
}

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Timesheet Tracker");
app.Run();
```

- [ ] **Step 3: Add the Google config keys to `appsettings.json`** (empty by default; real values via user-secrets/env)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=timesheet;Username=postgres;Password=postgres"
  },
  "Authentication": {
    "Google": {
      "ClientId": "",
      "ClientSecret": ""
    }
  }
}
```

- [ ] **Step 4: Build the solution**

Run: `dotnet build TimesheetTracker.slnx`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: register services and wire Identity + Google auth"
```

---

## Task 12: Initial migration + DbContext model snapshot test

**Files:**
- Create: `src/DataModel/Migrations/*` (generated), `tests/DataModel.Tests/IntegrationTests/DbContextModelTests.cs`

- [ ] **Step 1: Add EF design-time factory so `dotnet ef` works against the class library**

`src/DataModel/DesignTimeDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimesheetTracker.DataModel;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimesheetDbContext>
{
    public TimesheetDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql("Host=localhost;Database=timesheet;Username=postgres;Password=postgres")
            .Options;
        return new TimesheetDbContext(options);
    }
}
```

- [ ] **Step 2: Generate the initial migration**

Run: `dotnet ef migrations add InitialCreate --project src/DataModel/DataModel.csproj --startup-project src/DataModel/DataModel.csproj --output-dir Migrations`
Expected: `Migrations/<timestamp>_InitialCreate.cs` + snapshot created.

- [ ] **Step 3: Write a Verify snapshot test of the EF model** (catches accidental schema drift)

`tests/DataModel.Tests/IntegrationTests/DbContextModelTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;

namespace DataModel.Tests.IntegrationTests;

[TestFixture]
public class DbContextModelTests
{
    [Test]
    public Task Model_MatchesSnapshot()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=x;Password=x")
            .Options;
        using var context = new TimesheetDbContext(options);
        return Verify(context.Model);
    }
}
```

Add `global using VerifyTests;` if required by Verify.EntityFramework; ensure `VerifyEntityFramework.Initialize();` is called in `VerifyGlobalSettings.Init()`.

- [ ] **Step 4: Run — verify (creates snapshot first run)**

Run: `dotnet test ... --filter DbContextModelTests`
Expected: first run writes `*.received.txt`; review and accept → PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add initial EF migration and model snapshot test"
```

---

## Task 13: Optional — Testcontainers integration base (round-trip persistence)

**Files:**
- Create: `tests/DataModel.Tests/IntegrationTests/TestContainerBase.cs`, `JobPersistenceTests.cs`

> Requires Docker running. Mirrors the VendingMachineTracker `TestContainerBase` pattern (Testcontainers.PostgreSql + Respawn). Validates that the schema actually applies and entities round-trip.

- [ ] **Step 1: Create `TestContainerBase.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TimesheetTracker.DataModel;

namespace DataModel.Tests.IntegrationTests;

public abstract class TestContainerBase
{
    private PostgreSqlContainer _container = null!;
    protected TimesheetDbContext Db = null!;

    [SetUp]
    public async Task SetUp()
    {
        _container = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        Db = new TimesheetDbContext(options);
        await Db.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await Db.DisposeAsync();
        await _container.DisposeAsync();
    }
}
```

- [ ] **Step 2: Write a round-trip test**

`JobPersistenceTests.cs`:
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel.Enums;

namespace DataModel.Tests.IntegrationTests;

[TestFixture]
public class JobPersistenceTests : TestContainerBase
{
    [Test]
    public async Task Job_WithEntries_RoundTrips()
    {
        var user = new AppUser { Id = Guid.NewGuid(), UserName = "a@b.com", DisplayName = "A", DefaultState = AustralianState.VIC };
        var job = new Job { Id = Guid.NewGuid(), Name = "Acme", User = user, DecimalPlaces = 2 };
        job.TimeEntries.Add(new TimeEntry
        {
            Id = Guid.NewGuid(),
            WorkDate = new DateOnly(2026, 6, 8),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            BreakMinutes = 30
        });
        Db.Add(job);
        await Db.SaveChangesAsync();

        var loaded = await Db.Jobs.Include(j => j.TimeEntries).SingleAsync();
        loaded.TimeEntries.Should().HaveCount(1);
        loaded.WorkDays.Should().Be(DaysOfWeek.Weekdays);
    }
}
```

- [ ] **Step 3: Run — verify pass** (Docker required)

Run: `dotnet test ... --filter JobPersistenceTests`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "test: add Testcontainers persistence round-trip"
```

---

## Done criteria

- `dotnet build TimesheetTracker.slnx` succeeds.
- `dotnet test` passes for `TimeCalculationServiceTests`, `HolidayServiceTests`, `ExportPeriodTests`, `ExportServiceTests`, `DbContextModelTests` (and `JobPersistenceTests` when Docker is available).
- Initial migration committed.
- All domain logic (duration, midnight, decimal precision, financial-year, holidays, export rows) is covered by tests independent of the UI.

## Next plan (after design handoff)
`docs/superpowers/plans/<date>-timesheet-blazor-ui.md` — weekly grid, month calendar nav, job editor, profile, export screen, with bUnit + Verify.Bunit snapshots. Consumes the services built here.
