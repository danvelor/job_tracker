# Jobs Domain and Application Implementation Plan (Plan 3A)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the .NET solution skeleton, the shared kernel, and the `Jobs` domain and application layers, with every business rule enforced in the aggregate and every convention enforced by an architecture test — all without a database, a container, or a running service.

**Architecture:** A modular monolith. `Jobs.Domain` holds the `Job` aggregate and knows only `Common.Domain`; `Jobs.Application` holds commands, queries and validators and never names `Infrastructure`. Handlers return `Result`, never throw for an expected failure. The current instant is a parameter, so `BR-1` is testable without touching a global clock.

**Tech Stack:** .NET 9 (9.0.121), C# 13, MediatR 12.x pinned, FluentValidation 11, xUnit, Moq, FluentAssertions 7.x pinned, NetArchTest.

**Spec:** `context/design.md` B5 (the class map); `context/prd.md` sections 4-6 (`FR-*`, `BR-*`); `context/architecture.md` 3.1-3.3, 3.6, 8.3, 9.1-9.2

**Split:** This is plan 3A, everything that needs no database. Plan 3B adds `Jobs.Infrastructure` with EF Core, the schema, migrations, indexes, the HTTP endpoints, the Testcontainers integration suite and `HttpJobsAdapter`. The seam is where PostgreSQL enters, which is also where the environment risk lives.

**Rubric reach:** Aggregate Design (7) and CQRS + MediatR (6) complete here, plus Backend unit tests (5). Repository + UoW, Clean Architecture layers and all of section 4 land in 3B.

## Global Constraints

- **.NET 9.** `backend/global.json` pins the SDK and fails loudly on the wrong one. `dotnet@9` is keg-only on this machine, so every command in this plan is prefixed:
  ```bash
  PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH" dotnet …
  ```
- **No public setters on an aggregate.** State changes go through intention-named methods (`CLAUDE.md` non-negotiable 4)
- **Handlers return `Result` / `Result<T>`.** No exception is thrown for an expected failure; exceptions are for defects
- **The current time is a parameter**, never read inside a domain method (architecture 3.4)
- Naming and modifiers exactly as architecture 9.1 fixes them — `public sealed` commands and queries, `internal sealed` handlers and validators
- `Domain` references only `Common.Domain`. `Application` never references `Infrastructure`
- **Test-driven**: no production code without a test watched failing first (D-28). Scaffolding and `.csproj` files are exempt under architecture 8.3 condition (a)
- **Architecture tests get two mechanisms** (architecture 8.3): a permanent non-emptiness guard on every rule, and a one-time deliberate red per rule. NetArchTest is unusually easy to write so that it can never fail
- **Licence pins are not exempt from testing.** MediatR below 13, FluentAssertions below 8, asserted on the loaded assembly (D-20)
- Every commit is green: `dotnet build` with warnings as errors, and `dotnet test`

---

## File Structure

```
backend/
├── global.json                                   pins the SDK, already written
├── Directory.Packages.props                      central version pins, incl. D-20
├── Directory.Build.props                         nullable, warnings as errors, langversion
├── JobTracker.sln
├── src/
│   ├── Common/
│   │   ├── JobTracker.Common.Domain/
│   │   │   ├── Entity.cs                         identity equality
│   │   │   ├── AggregateRoot.cs                  Entity + domain events
│   │   │   ├── ValueObject.cs                    structural equality, Template Method
│   │   │   ├── Error.cs                          code, message, type
│   │   │   ├── Result.cs                         Result and Result<T>
│   │   │   └── IDomainEvent.cs                   marker, a MediatR INotification
│   │   └── JobTracker.Common.Application/
│   │       ├── IUnitOfWork.cs
│   │       ├── PagedList.cs                      cursor-shaped, no TotalCount
│   │       ├── IEventBus.cs
│   │       └── Behaviors/ValidationBehavior.cs
│   └── Modules/Jobs/
│       ├── JobTracker.Modules.Jobs.Domain/
│       │   ├── Job.cs                            the aggregate
│       │   ├── JobStatus.cs
│       │   ├── Address.cs                        value object
│       │   ├── JobPhoto.cs   NewJobPhoto.cs
│       │   ├── Notification.cs                   D-22, lives inside Jobs
│       │   ├── Assignee.cs   Customer.cs         read-only rosters (D-26)
│       │   ├── JobErrors.cs                      one Error per business rule
│       │   ├── Events/*.cs                       three domain events
│       │   ├── IJobRepository.cs   IPartyRepository.cs   INotificationRepository.cs
│       │   └── JobSearchResult.cs  JobSearchCriteria.cs  (D-25)
│       └── JobTracker.Modules.Jobs.Application/
│           ├── Jobs/CreateJob/       Command, Handler, Validator
│           ├── Jobs/StartJob/        Command, Handler
│           ├── Jobs/CompleteJob/     Command, Handler, Validator
│           ├── Jobs/CancelJob/       Command, Handler, Validator
│           ├── Jobs/RescheduleJob/   Command, Handler, Validator
│           ├── Jobs/SearchJobs/      Query, Handler, JobResponse
│           ├── Jobs/GetJobById/      Query, Handler, JobDetailResponse
│           └── Parties/              ListAssignees, ListCustomers
└── tests/
    ├── JobTracker.Modules.Jobs.Domain.UnitTests/
    ├── JobTracker.Modules.Jobs.Application.UnitTests/
    └── JobTracker.ArchitectureTests/
```

Billing's three projects and its test project arrive with plan 4, when there is an integration event for them to consume. Creating them now would add three empty projects the architecture tests would have to skip.

---

### Task 1: Solution skeleton and the build gate

**Files:**
- Create: `backend/Directory.Build.props`, `backend/Directory.Packages.props`, `backend/JobTracker.sln`
- Create: the eight project files listed above (four source, three test, plus the solution)

**Interfaces:**
- Consumes: `backend/global.json`, already written
- Produces: `dotnet build backend/JobTracker.sln` and `dotnet test backend/JobTracker.sln` — the two commands every later task runs

**No red phase.** Scaffolding is the architecture 8.3 condition (a) exemption: a test lives inside a `.csproj`, so none can exist before the projects do.

- [ ] **Step 1: Write `backend/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- A warning that never fails a build is a suggestion. The CI job in
         architecture 10.2 builds with warnings as errors, so local builds do
         too rather than discovering them late. -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Write `backend/Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- D-20. MediatR 13 and FluentAssertions 8 moved to commercial licences,
         and an unbuildable deliverable scores nothing regardless of its
         contents. The pins are asserted by tests in task 3, because a comment
         is not a check. -->
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />

    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="FluentAssertions" Version="7.0.0" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create the solution and the source projects**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"

dotnet new sln -n JobTracker

dotnet new classlib -o src/Common/JobTracker.Common.Domain
dotnet new classlib -o src/Common/JobTracker.Common.Application
dotnet new classlib -o src/Modules/Jobs/JobTracker.Modules.Jobs.Domain
dotnet new classlib -o src/Modules/Jobs/JobTracker.Modules.Jobs.Application

rm src/Common/JobTracker.Common.Domain/Class1.cs
rm src/Common/JobTracker.Common.Application/Class1.cs
rm src/Modules/Jobs/JobTracker.Modules.Jobs.Domain/Class1.cs
rm src/Modules/Jobs/JobTracker.Modules.Jobs.Application/Class1.cs
```

- [ ] **Step 4: Create the test projects**

```bash
dotnet new xunit -o tests/JobTracker.Modules.Jobs.Domain.UnitTests
dotnet new xunit -o tests/JobTracker.Modules.Jobs.Application.UnitTests
dotnet new xunit -o tests/JobTracker.ArchitectureTests

rm tests/JobTracker.Modules.Jobs.Domain.UnitTests/UnitTest1.cs
rm tests/JobTracker.Modules.Jobs.Application.UnitTests/UnitTest1.cs
rm tests/JobTracker.ArchitectureTests/UnitTest1.cs
```

- [ ] **Step 5: Add every project to the solution**

```bash
dotnet sln add $(find src tests -name '*.csproj')
```

- [ ] **Step 6: Wire the project references**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"

# Domain references only Common.Domain (architecture 9.2).
dotnet add src/Modules/Jobs/JobTracker.Modules.Jobs.Domain \
  reference src/Common/JobTracker.Common.Domain

# Application references its own Domain and Common.Application — never
# Infrastructure, which does not exist yet and must not when it does.
dotnet add src/Common/JobTracker.Common.Application \
  reference src/Common/JobTracker.Common.Domain
dotnet add src/Modules/Jobs/JobTracker.Modules.Jobs.Application \
  reference src/Modules/Jobs/JobTracker.Modules.Jobs.Domain
dotnet add src/Modules/Jobs/JobTracker.Modules.Jobs.Application \
  reference src/Common/JobTracker.Common.Application

dotnet add tests/JobTracker.Modules.Jobs.Domain.UnitTests \
  reference src/Modules/Jobs/JobTracker.Modules.Jobs.Domain
dotnet add tests/JobTracker.Modules.Jobs.Application.UnitTests \
  reference src/Modules/Jobs/JobTracker.Modules.Jobs.Application
dotnet add tests/JobTracker.ArchitectureTests \
  reference src/Modules/Jobs/JobTracker.Modules.Jobs.Application
dotnet add tests/JobTracker.ArchitectureTests \
  reference src/Common/JobTracker.Common.Application
```

- [ ] **Step 7: Add the package references**

Add to each test project's `.csproj` (versions come from `Directory.Packages.props`):

```xml
  <ItemGroup>
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Moq" />
  </ItemGroup>
```

And to `JobTracker.ArchitectureTests.csproj` additionally:

```xml
  <ItemGroup>
    <PackageReference Include="NetArchTest.Rules" />
    <PackageReference Include="MediatR" />
  </ItemGroup>
```

And to `JobTracker.Common.Application.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
  </ItemGroup>
```

And to `JobTracker.Modules.Jobs.Application.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
  </ItemGroup>
```

`Jobs.Domain` gets **no** package reference at all. A domain that needs a library is usually a domain that has leaked.

- [ ] **Step 8: Verify the build**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet build JobTracker.sln
```

Expected: build succeeded, zero warnings. If a warning appears it is an error here, which is the point.

- [ ] **Step 9: Add a `.gitignore` for the backend**

Create `backend/.gitignore`:

```
bin/
obj/
*.user
TestResults/
```

- [ ] **Step 10: Commit**

```bash
git add backend
git commit -m "chore: scaffold the .NET solution and its build gate

Seven projects and a solution, targeting net9.0 with C# 13, nullable
enabled and warnings as errors. A warning that never fails a build is a
suggestion, and architecture 10.2 has CI building this way, so local
builds match rather than discovering the difference late.

global.json pins the SDK and fails loudly on the wrong one. dotnet@9 is
keg-only on this machine and the dotnet@6 host cannot see the 9 SDK, so
without the pin a build would silently target .NET 6 — where the C# 12
collection expressions design B5 uses do not compile.

Versions are managed centrally. MediatR is pinned below 13 and
FluentAssertions below 8 (D-20): both moved to commercial licences, and
an unbuildable deliverable scores nothing regardless of its contents.
Task 3 asserts the pins, because a comment is not a check.

Jobs.Domain has no package reference at all. A domain that needs a
library is usually a domain that has leaked."
```

---

### Task 2: The shared kernel

**Files:**
- Create: `src/Common/JobTracker.Common.Domain/{Entity,AggregateRoot,ValueObject,Error,Result,IDomainEvent}.cs`
- Create: `src/Common/JobTracker.Common.Application/{IUnitOfWork,PagedList,IEventBus}.cs`
- Test: `tests/JobTracker.Modules.Jobs.Domain.UnitTests/Common/{ValueObjectTests,ResultTests,AggregateRootTests}.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `Entity`, `AggregateRoot`, `ValueObject`, `Error`, `ErrorType`, `Result`, `Result<T>`, `IDomainEvent`, `IUnitOfWork`, `PagedList<T>`, `IEventBus`

- [ ] **Step 1: Write the failing ValueObject test**

Create `tests/JobTracker.Modules.Jobs.Domain.UnitTests/Common/ValueObjectTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests.Common;

public sealed class ValueObjectTests
{
    private sealed class Money : ValueObject
    {
        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public decimal Amount { get; }
        public string Currency { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Two_values_with_the_same_components_are_equal()
    {
        new Money(10m, "USD").Should().Be(new Money(10m, "USD"));
    }

    [Fact]
    public void Two_values_differing_in_any_component_are_not_equal()
    {
        new Money(10m, "USD").Should().NotBe(new Money(10m, "EUR"));
        new Money(10m, "USD").Should().NotBe(new Money(11m, "USD"));
    }

    [Fact]
    public void Equal_values_share_a_hash_code()
    {
        // Without this, two equal values land in different dictionary buckets
        // and structural equality stops meaning anything in a HashSet.
        new Money(10m, "USD").GetHashCode()
            .Should().Be(new Money(10m, "USD").GetHashCode());
    }

    [Fact]
    public void A_value_is_not_equal_to_null_or_to_another_type()
    {
        new Money(10m, "USD").Equals(null).Should().BeFalse();
        new Money(10m, "USD").Equals("USD").Should().BeFalse();
    }

    [Fact]
    public void The_equality_operators_agree_with_Equals()
    {
        var left = new Money(10m, "USD");
        var right = new Money(10m, "USD");

        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }

    [Fact]
    public void Two_nulls_are_equal_and_a_null_differs_from_a_value()
    {
        Money? nothing = null;
        Money? alsoNothing = null;

        (nothing == alsoNothing).Should().BeTrue();
        (nothing == new Money(10m, "USD")).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet test JobTracker.sln
```

Expected: compilation error — `ValueObject` does not exist.

- [ ] **Step 3: Write `ValueObject.cs`**

```csharp
namespace JobTracker.Common.Domain;

/// <summary>
/// Structural equality by comparing an ordered projection of components.
///
/// A Template Method: <see cref="Equals(object?)"/> and
/// <see cref="GetHashCode"/> write the algorithm once, and a subclass supplies
/// only what it is made of.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null
        && other.GetType() == GetType()
        && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
```

- [ ] **Step 4: Write the remaining kernel types**

`IDomainEvent.cs`:

```csharp
using MediatR;

namespace JobTracker.Common.Domain;

/// <summary>A marker. It is a MediatR notification so the outbox can publish it.</summary>
public interface IDomainEvent : INotification;
```

This is the one type in `Common.Domain` that references a package. `IDomainEvent` has to be a `INotification` for the dispatcher to reach it, and the alternative — a marker of our own plus an adapter — buys nothing.

`Entity.cs`:

```csharp
namespace JobTracker.Common.Domain;

public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    // EF materialises through this; nothing else should.
    protected Entity() { }

    public Guid Id { get; protected init; }

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}
```

`AggregateRoot.cs`:

```csharp
namespace JobTracker.Common.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id) { }

    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Called by the outbox interceptor after it has copied the events into
    /// the same transaction as the state change (architecture 4.2).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

`Error.cs`:

```csharp
namespace JobTracker.Common.Domain;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Failure,
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    /// <summary>
    /// An invariant refused the operation. Presentation maps this to 409, not
    /// 400: the request was well-formed and the state refused it (design B6).
    /// </summary>
    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);
}
```

`Result.cs`:

```csharp
namespace JobTracker.Common.Domain;

/// <summary>
/// Success or failure as a value. Expected failures are returned; exceptions
/// are reserved for defects (architecture 9.2).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) =>
        _value = value;

    /// <summary>
    /// Throws when read from a failure. That is a defect — the caller did not
    /// check IsSuccess — not an expected failure, so an exception is right.
    /// </summary>
    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("A failed result has no value.");
}
```

`IUnitOfWork.cs`:

```csharp
namespace JobTracker.Common.Application;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`PagedList.cs`:

```csharp
namespace JobTracker.Common.Application;

/// <summary>
/// Cursor-shaped, not page-shaped: no Page, no PageSize and above all no
/// TotalCount. A total would need a second aggregate query per keystroke,
/// which is the cost NFR-5 rejects, and design A2 shows loaded rows rather
/// than matches. Assessment line 207 mandates the type name and says nothing
/// about its members.
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public bool HasMore => NextCursor is not null;

    public static PagedList<T> Empty() => new([], null);
}
```

`IEventBus.cs`:

```csharp
namespace JobTracker.Common.Application;

/// <summary>
/// Publishes integration events. It exists so a module publishing a contract
/// does not name the mediator, which is the piece that would change if the
/// modules ever became separate deployables (D-03).
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class;
}
```

- [ ] **Step 5: Write the failing Result and AggregateRoot tests**

Create `tests/JobTracker.Modules.Jobs.Domain.UnitTests/Common/ResultTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void A_success_carries_its_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void A_failure_carries_its_error()
    {
        var error = Error.Conflict("job.terminal", "A terminal job cannot change state");

        var result = Result.Failure<int>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws_because_that_is_a_defect()
    {
        var result = Result.Failure<int>(Error.NotFound("job.missing", "No such job"));

        var read = () => result.Value;

        // The caller did not check IsSuccess. That is a programming mistake,
        // not an expected failure, so an exception is the right answer.
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_success_cannot_be_constructed_with_an_error()
    {
        var build = () => new TestableResult(true, Error.Validation("x", "y"));

        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_failure_cannot_be_constructed_without_an_error()
    {
        var build = () => new TestableResult(false, Error.None);

        build.Should().Throw<InvalidOperationException>();
    }

    private sealed class TestableResult(bool isSuccess, Error error)
        : Result(isSuccess, error);
}
```

Create `tests/JobTracker.Modules.Jobs.Domain.UnitTests/Common/AggregateRootTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests.Common;

public sealed class AggregateRootTests
{
    private sealed record ThingHappened(Guid Id) : IDomainEvent;

    private sealed class Thing : AggregateRoot
    {
        public Thing() : base(Guid.NewGuid()) { }

        public void DoSomething() => Raise(new ThingHappened(Id));
    }

    [Fact]
    public void A_raised_event_is_visible_on_the_aggregate()
    {
        var thing = new Thing();

        thing.DoSomething();

        thing.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void The_event_collection_is_exposed_read_only()
    {
        // A caller that could add to this collection could fabricate a
        // consequence the aggregate never decided on.
        typeof(AggregateRoot)
            .GetProperty(nameof(AggregateRoot.DomainEvents))!
            .PropertyType.Should().Be(typeof(IReadOnlyCollection<IDomainEvent>));
    }

    [Fact]
    public void Clearing_empties_the_collection()
    {
        var thing = new Thing();
        thing.DoSomething();

        thing.ClearDomainEvents();

        thing.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Two_entities_of_the_same_type_with_the_same_id_are_equal()
    {
        var id = Guid.NewGuid();

        new TestEntity(id).Should().Be(new TestEntity(id));
    }

    private sealed class TestEntity(Guid id) : Entity(id);
}
```

- [ ] **Step 6: Run the tests to verify green**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet test JobTracker.sln
```

Expected: fifteen passing tests.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "feat: add the shared kernel

Entity, AggregateRoot, ValueObject, Error, Result and IDomainEvent —
the primitives every module's domain needs, and nothing that expresses
policy. Nothing about jobs, invoices, tenancy or scheduling belongs
here (architecture 3.3).

ValueObject is a Template Method: Equals and GetHashCode write the
algorithm once and a subclass supplies only its components. The hash
test is the one that matters — without it two equal values land in
different dictionary buckets and structural equality stops meaning
anything in a HashSet.

Result throws when Value is read from a failure, and that is deliberate:
the caller did not check IsSuccess, which is a defect rather than an
expected failure. Its constructor also refuses a success carrying an
error and a failure carrying none, so an impossible Result cannot be
built.

PagedList is cursor-shaped: no TotalCount, because a total needs a
second aggregate query per keystroke and NFR-5 rejects that cost. Line
207 mandates the type name and says nothing about its members.

IDomainEvent is the one kernel type with a package reference. It has to
be a MediatR notification for the outbox to publish it, and a marker of
our own plus an adapter would buy nothing."
```

---

### Task 3: Architecture tests, with a deliberate red per rule

**Files:**
- Create: `tests/JobTracker.ArchitectureTests/{LayerRules,NamingRules,LicenceRules}.cs`
- Create: `tests/JobTracker.ArchitectureTests/ArchitectureTestBase.cs`

**Interfaces:**
- Consumes: the assemblies of `Jobs.Domain`, `Jobs.Application`, `Common.Domain`, `Common.Application`
- Produces: the enforcement behind every rule in architecture 9.1 and 9.2

Architecture 8.3 requires two mechanisms here, because NetArchTest is unusually easy to write so that it can never fail: a wrong suffix, a case mismatch, or an assertion over an empty set all pass.

- [ ] **Step 1: Write the base with the non-emptiness guard**

Create `tests/JobTracker.ArchitectureTests/ArchitectureTestBase.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace JobTracker.ArchitectureTests;

public abstract class ArchitectureTestBase
{
    protected static readonly Assembly JobsDomain =
        typeof(Modules.Jobs.Domain.Job).Assembly;

    protected static readonly Assembly JobsApplication =
        typeof(Modules.Jobs.Application.Jobs.CreateJob.CreateJobCommand).Assembly;

    protected static readonly Assembly CommonDomain =
        typeof(Common.Domain.Entity).Assembly;

    /// <summary>
    /// Asserts a rule holds AND that it examined something.
    ///
    /// This is the permanent half of architecture 8.3's two mechanisms. A
    /// NetArchTest assertion over an empty set passes, so a renamed suffix or
    /// a moved namespace turns a rule into a rule about nothing — silently.
    /// Requiring a non-empty subject means a rule can never quietly stop
    /// applying.
    /// </summary>
    protected static void ShouldHold(ConditionList condition, PredicateList subject)
    {
        subject.GetTypes().Should().NotBeEmpty(
            "an architecture rule over an empty set passes without checking anything");

        var result = condition.GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these types break the rule: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
```

- [ ] **Step 2: Write the naming rules**

Create `tests/JobTracker.ArchitectureTests/NamingRules.cs`:

```csharp
using NetArchTest.Rules;

namespace JobTracker.ArchitectureTests;

public sealed class NamingRules : ArchitectureTestBase
{
    [Fact]
    public void Commands_are_public_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication)
            .That().HaveNameEndingWith("Command");

        ShouldHold(subject.Should().BePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Command_handlers_are_internal_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication)
            .That().HaveNameEndingWith("CommandHandler");

        ShouldHold(subject.Should().NotBePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Queries_are_public_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication)
            .That().HaveNameEndingWith("Query");

        ShouldHold(subject.Should().BePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Query_handlers_are_internal_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication)
            .That().HaveNameEndingWith("QueryHandler");

        ShouldHold(subject.Should().NotBePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Validators_are_internal_sealed_and_suffixed()
    {
        var subject = Types.InAssembly(JobsApplication)
            .That().HaveNameEndingWith("Validator");

        ShouldHold(subject.Should().NotBePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Domain_events_are_sealed_records_in_the_domain()
    {
        var subject = Types.InAssembly(JobsDomain)
            .That().HaveNameEndingWith("DomainEvent");

        ShouldHold(subject.Should().BePublic().And().BeSealed(), subject);
    }

    [Fact]
    public void Repository_interfaces_live_in_the_domain()
    {
        var subject = Types.InAssembly(JobsDomain)
            .That().AreInterfaces().And().HaveNameEndingWith("Repository");

        ShouldHold(subject.Should().BePublic(), subject);
    }

    [Fact]
    public void No_repository_interface_leaked_into_the_application_layer()
    {
        Types.InAssembly(JobsApplication)
            .That().AreInterfaces().And().HaveNameEndingWith("Repository")
            .GetTypes().Should().BeEmpty(
                "a repository interface belongs to the domain that owns the aggregate");
    }
}
```

- [ ] **Step 3: Write the layer rules**

Create `tests/JobTracker.ArchitectureTests/LayerRules.cs`:

```csharp
using FluentAssertions;
using NetArchTest.Rules;

namespace JobTracker.ArchitectureTests;

public sealed class LayerRules : ArchitectureTestBase
{
    [Fact]
    public void Domain_does_not_reference_application()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Application"),
            subject);
    }

    [Fact]
    public void Domain_does_not_reference_infrastructure()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Infrastructure"),
            subject);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        var subject = Types.InAssembly(JobsApplication);

        ShouldHold(
            subject.ShouldNot().HaveDependencyOn("JobTracker.Modules.Jobs.Infrastructure"),
            subject);
    }

    [Fact]
    public void Domain_does_not_reference_entity_framework()
    {
        var subject = Types.InAssembly(JobsDomain);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore"), subject);
    }

    [Fact]
    public void Common_domain_knows_nothing_about_jobs()
    {
        // The rule that keeps a shared kernel a kernel: if a type would only
        // ever be used by one module, it lives in that module (architecture 3.3).
        var subject = Types.InAssembly(CommonDomain);

        ShouldHold(subject.ShouldNot().HaveDependencyOn("JobTracker.Modules"), subject);
    }

    [Fact]
    public void Aggregates_expose_no_public_setter()
    {
        var offenders = Types.InAssembly(JobsDomain)
            .That().Inherit(typeof(Common.Domain.AggregateRoot))
            .GetTypes()
            .SelectMany(type => type.GetProperties())
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        // NetArchTest cannot express this, and it is the rule that separates a
        // domain model from a data bag, so it is written by hand rather than
        // left unchecked.
        offenders.Should().BeEmpty(
            "state changes go through intention-named methods (architecture 9.2)");
    }
}
```

- [ ] **Step 4: Write the licence rules**

Create `tests/JobTracker.ArchitectureTests/LicenceRules.cs`:

```csharp
using FluentAssertions;
using MediatR;

namespace JobTracker.ArchitectureTests;

/// <summary>
/// D-20. Nothing fails today if MediatR moves to 13 or FluentAssertions to 8:
/// it compiles, the tests pass, and the defect appears when a reviewer runs
/// dotnet restore without a licence and cannot build the deliverable.
///
/// Asserting on the loaded assembly rather than on Directory.Packages.props
/// checks what was actually restored, and catches a transitive bump the file
/// would not mention.
/// </summary>
public sealed class LicenceRules
{
    [Fact]
    public void MediatR_stays_below_the_commercially_licensed_major()
    {
        typeof(IMediator).Assembly.GetName().Version!.Major.Should().Be(12);
    }

    [Fact]
    public void FluentAssertions_stays_below_the_commercially_licensed_major()
    {
        typeof(FluentAssertions.AssertionExtensions).Assembly.GetName().Version!.Major
            .Should().BeLessThan(8);
    }
}
```

- [ ] **Step 5: Run the tests — they must fail because nothing exists yet**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet test JobTracker.sln
```

Expected: compilation errors — `Job` and `CreateJobCommand` do not exist. That is the correct red for this task: the rules reference the types they police, so they cannot compile before the module does. Tasks 4 and 5 make them compile, and **task 5 step 12 performs the deliberate red for each naming rule.**

- [ ] **Step 6: Do NOT commit yet**

This task's tests cannot pass until task 5. Leave the files uncommitted and continue; task 5 commits them together with the code they police. That is a deliberate exception to the every-commit-is-green rule, and the alternative — committing rules that reference nothing — would be worse.

---

### Task 4: The `Job` aggregate

**Files:**
- Create: `src/Modules/Jobs/JobTracker.Modules.Jobs.Domain/{JobStatus,Address,JobPhoto,NewJobPhoto,Job,JobErrors}.cs`
- Create: `src/Modules/Jobs/JobTracker.Modules.Jobs.Domain/Events/{JobCreated,JobCompleted,JobCancelled}DomainEvent.cs`
- Test: `tests/JobTracker.Modules.Jobs.Domain.UnitTests/{AddressTests,JobTests}.cs`

**Interfaces:**
- Consumes: the kernel from task 2
- Produces: `Job` with `Create`, `Reschedule`, `Start`, `Complete`, `Cancel`; `Address`; `JobPhoto`; `NewJobPhoto`; the three domain events; `JobErrors`

- [ ] **Step 1: Write the failing Address test**

Create `tests/JobTracker.Modules.Jobs.Domain.UnitTests/AddressTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests;

public sealed class AddressTests
{
    private static Address AnAddress(string street = "12 Elm St") =>
        Address.Create(street, "Springfield", "IL", "62701", 39.78m, -89.65m).Value;

    [Fact]
    public void Two_addresses_with_the_same_components_are_equal()
    {
        AnAddress().Should().Be(AnAddress());
    }

    [Fact]
    public void Addresses_differing_in_any_component_are_not_equal()
    {
        AnAddress().Should().NotBe(AnAddress("8 Oak Ave"));
    }

    [Fact]
    public void Equal_addresses_share_a_hash_code()
    {
        AnAddress().GetHashCode().Should().Be(AnAddress().GetHashCode());
    }

    [Fact]
    public void Equality_is_structural_rather_than_by_reference()
    {
        var left = AnAddress();
        var right = AnAddress();

        ReferenceEquals(left, right).Should().BeFalse();
        (left == right).Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Springfield", "IL", "62701")]
    [InlineData("12 Elm St", "", "IL", "62701")]
    [InlineData("12 Elm St", "Springfield", "", "62701")]
    [InlineData("12 Elm St", "Springfield", "IL", "")]
    public void Every_textual_component_is_required(
        string street, string city, string state, string zip)
    {
        Address.Create(street, city, state, zip, 39.78m, -89.65m)
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void Coordinates_outside_the_globe_are_refused(decimal latitude, decimal longitude)
    {
        // An address is a place. A latitude of 91 is not a place, and letting
        // one through would put a pin in the sea on a map nobody checks.
        Address.Create("12 Elm St", "Springfield", "IL", "62701", latitude, longitude)
            .IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: compilation error — `Address` does not exist.

- [ ] **Step 3: Write `Address.cs` and `JobErrors.cs`**

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public sealed class Address : ValueObject
{
    private Address(
        string street, string city, string state, string zipCode,
        decimal latitude, decimal longitude)
    {
        Street = street;
        City = city;
        State = state;
        ZipCode = zipCode;
        Latitude = latitude;
        Longitude = longitude;
    }

    // EF materialises an owned type through this.
    private Address() { }

    public string Street { get; private init; } = string.Empty;
    public string City { get; private init; } = string.Empty;
    public string State { get; private init; } = string.Empty;
    public string ZipCode { get; private init; } = string.Empty;
    public decimal Latitude { get; private init; }
    public decimal Longitude { get; private init; }

    public static Result<Address> Create(
        string street, string city, string state, string zipCode,
        decimal latitude, decimal longitude)
    {
        if (string.IsNullOrWhiteSpace(street)) return Result.Failure<Address>(JobErrors.AddressIncomplete);
        if (string.IsNullOrWhiteSpace(city)) return Result.Failure<Address>(JobErrors.AddressIncomplete);
        if (string.IsNullOrWhiteSpace(state)) return Result.Failure<Address>(JobErrors.AddressIncomplete);
        if (string.IsNullOrWhiteSpace(zipCode)) return Result.Failure<Address>(JobErrors.AddressIncomplete);
        if (latitude is < -90m or > 90m) return Result.Failure<Address>(JobErrors.CoordinatesOffGlobe);
        if (longitude is < -180m or > 180m) return Result.Failure<Address>(JobErrors.CoordinatesOffGlobe);

        return Result.Success(new Address(street, city, state, zipCode, latitude, longitude));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return ZipCode;
        yield return Latitude;
        yield return Longitude;
    }
}
```

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>One error per business rule, named after the rule it enforces.</summary>
public static class JobErrors
{
    public static readonly Error TitleRequired =
        Error.Validation("job.title-required", "A title is required");

    public static readonly Error AddressIncomplete =
        Error.Validation("job.address-incomplete", "Every address component is required");

    public static readonly Error CoordinatesOffGlobe =
        Error.Validation("job.coordinates-off-globe", "The coordinates are not a place on Earth");

    /// <summary>BR-1.</summary>
    public static readonly Error ScheduledInThePast =
        Error.Validation("job.scheduled-in-the-past", "A job cannot be scheduled in the past");

    /// <summary>BR-2.</summary>
    public static readonly Error Terminal =
        Error.Conflict("job.terminal", "A job in a terminal state cannot change state");

    /// <summary>BR-3.</summary>
    public static readonly Error NotScheduled =
        Error.Conflict("job.not-scheduled", "Only a Scheduled job can start");

    public static readonly Error NotInProgress =
        Error.Conflict("job.not-in-progress", "Only a job in progress can be completed");

    /// <summary>BR-4.</summary>
    public static readonly Error SignatureRequired =
        Error.Validation("job.signature-required", "A customer signature is required");

    /// <summary>BR-5.</summary>
    public static readonly Error ReasonRequired =
        Error.Validation("job.reason-required", "A cancellation reason is required");

    public static readonly Error NotFound =
        Error.NotFound("job.not-found", "No job with that identifier");
}
```

- [ ] **Step 4: Run the Address tests to verify green**

Expected: eleven passing tests in `AddressTests`.

- [ ] **Step 5: Write the failing Job test**

Create `tests/JobTracker.Modules.Jobs.Domain.UnitTests/JobTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;

namespace JobTracker.Modules.Jobs.Domain.UnitTests;

public sealed class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Future = new(2026, 3, 14);
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();

    private static Address AnAddress() =>
        Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value;

    private static Job AScheduledJob() =>
        Job.Create("Roof repair", null, AnAddress(), Future, Assignee, Customer, Organization, Now)
            .Value;

    private static Job AnInProgressJob()
    {
        var job = AScheduledJob();
        job.Start(Now.AddHours(1));
        return job;
    }

    private static IEnumerable<NewJobPhoto> NoPhotos() => [];

    // ---- creation -------------------------------------------------------

    [Fact]
    public void A_created_job_is_Scheduled_rather_than_Draft()
    {
        // D-14: the creation form collects exactly what Scheduled requires,
        // and the acceptance walkthrough cannot complete a job never scheduled.
        AScheduledJob().Status.Should().Be(JobStatus.Scheduled);
    }

    [Fact]
    public void Creation_records_the_organization_the_job_belongs_to()
    {
        AScheduledJob().OrganizationId.Should().Be(Organization);
    }

    [Fact]
    public void Creation_raises_JobCreatedDomainEvent()
    {
        AScheduledJob().DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCreatedDomainEvent>();
    }

    [Fact]
    public void A_job_cannot_be_created_without_a_title()
    {
        Job.Create("   ", null, AnAddress(), Future, Assignee, Customer, Organization, Now)
            .Error.Should().Be(JobErrors.TitleRequired);
    }

    [Fact]
    public void A_job_cannot_be_scheduled_in_the_past()
    {
        var yesterday = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1);

        Job.Create("Roof repair", null, AnAddress(), yesterday, Assignee, Customer, Organization, Now)
            .Error.Should().Be(JobErrors.ScheduledInThePast);
    }

    [Fact]
    public void A_job_scheduled_for_today_is_accepted()
    {
        // BR-1 says "in the past", and today is not past. An off-by-one here
        // would refuse every same-day job, which is most of them.
        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        Job.Create("Roof repair", null, AnAddress(), today, Assignee, Customer, Organization, Now)
            .IsSuccess.Should().BeTrue();
    }

    // ---- start ----------------------------------------------------------

    [Fact]
    public void A_Scheduled_job_starts_and_records_when()
    {
        var job = AScheduledJob();
        var startedAt = Now.AddHours(1);

        job.Start(startedAt).IsSuccess.Should().BeTrue();

        job.Status.Should().Be(JobStatus.InProgress);
        job.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public void A_job_that_already_started_cannot_start_again()
    {
        var job = AnInProgressJob();

        job.Start(Now.AddHours(2)).Error.Should().Be(JobErrors.NotScheduled);
    }

    // ---- complete -------------------------------------------------------

    [Fact]
    public void An_InProgress_job_completes_with_a_signature()
    {
        var job = AnInProgressJob();
        var completedAt = Now.AddHours(6);

        job.Complete(completedAt, "data:image/png;base64,AAA", NoPhotos())
            .IsSuccess.Should().BeTrue();

        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void Completion_without_a_signature_is_refused()
    {
        var job = AnInProgressJob();

        job.Complete(Now.AddHours(6), "   ", NoPhotos())
            .Error.Should().Be(JobErrors.SignatureRequired);
    }

    [Fact]
    public void A_job_that_never_started_cannot_be_completed()
    {
        var job = AScheduledJob();

        job.Complete(Now.AddHours(6), "sig", NoPhotos())
            .Error.Should().Be(JobErrors.NotInProgress);
    }

    [Fact]
    public void Completion_attaches_the_photos_it_was_given()
    {
        var job = AnInProgressJob();
        var photos = new[] { new NewJobPhoto("p1.jpg", Now, "ridge") };

        job.Complete(Now.AddHours(6), "sig", photos);

        job.Photos.Should().ContainSingle().Which.Url.Should().Be("p1.jpg");
    }

    [Fact]
    public void Completion_raises_JobCompletedDomainEvent()
    {
        var job = AnInProgressJob();
        job.ClearDomainEvents();

        job.Complete(Now.AddHours(6), "sig", NoPhotos());

        job.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCompletedDomainEvent>();
    }

    // ---- cancel ---------------------------------------------------------

    [Fact]
    public void A_Scheduled_job_cancels_with_a_reason()
    {
        var job = AScheduledJob();

        job.Cancel(Now.AddHours(1), "Weather").IsSuccess.Should().BeTrue();

        job.Status.Should().Be(JobStatus.Cancelled);
        job.CancellationReason.Should().Be("Weather");
    }

    [Fact]
    public void An_InProgress_job_can_also_be_cancelled()
    {
        AnInProgressJob().Cancel(Now.AddHours(2), "Customer withdrew")
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Cancellation_without_a_reason_is_refused()
    {
        AScheduledJob().Cancel(Now.AddHours(1), "  ")
            .Error.Should().Be(JobErrors.ReasonRequired);
    }

    [Fact]
    public void Cancellation_raises_JobCancelledDomainEvent()
    {
        var job = AScheduledJob();
        job.ClearDomainEvents();

        job.Cancel(Now.AddHours(1), "Weather");

        job.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCancelledDomainEvent>();
    }

    // ---- BR-2, terminal states -------------------------------------------

    [Fact]
    public void A_completed_job_refuses_every_further_transition()
    {
        var job = AnInProgressJob();
        job.Complete(Now.AddHours(6), "sig", NoPhotos());

        job.Start(Now.AddHours(7)).Error.Should().Be(JobErrors.Terminal);
        job.Cancel(Now.AddHours(7), "changed our mind").Error.Should().Be(JobErrors.Terminal);
        job.Reschedule(Future.AddDays(1), Assignee, Now).Error.Should().Be(JobErrors.Terminal);
    }

    [Fact]
    public void A_cancelled_job_refuses_every_further_transition()
    {
        var job = AScheduledJob();
        job.Cancel(Now.AddHours(1), "Weather");

        job.Start(Now.AddHours(2)).Error.Should().Be(JobErrors.Terminal);
        job.Complete(Now.AddHours(2), "sig", NoPhotos()).Error.Should().Be(JobErrors.Terminal);
    }

    // ---- reschedule ------------------------------------------------------

    [Fact]
    public void A_Scheduled_job_can_be_rescheduled()
    {
        var job = AScheduledJob();
        var later = Future.AddDays(3);

        job.Reschedule(later, Assignee, Now).IsSuccess.Should().BeTrue();

        job.ScheduledDate.Should().Be(later);
    }

    [Fact]
    public void Rescheduling_into_the_past_is_refused()
    {
        var yesterday = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1);

        AScheduledJob().Reschedule(yesterday, Assignee, Now)
            .Error.Should().Be(JobErrors.ScheduledInThePast);
    }

    // ---- reachability -----------------------------------------------------

    [Fact]
    public void Photos_are_exposed_read_only_and_have_no_other_way_in()
    {
        // Line 185: JobPhoto is reachable only through the aggregate root.
        // There is no AddPhoto: photos arrive through Complete, which is the
        // only moment the business produces them.
        typeof(Job).GetProperty(nameof(Job.Photos))!.PropertyType
            .Should().Be(typeof(IReadOnlyCollection<JobPhoto>));

        typeof(Job).GetMethods().Select(method => method.Name)
            .Should().NotContain("AddPhoto");
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Expected: compilation error — `Job` does not exist.

- [ ] **Step 7: Write the domain events**

`Events/JobCreatedDomainEvent.cs`:

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>
/// FR-8: triggers a notification to the assigned crew. It never becomes an
/// integration event, because notifying stays inside Jobs (D-22) — which is
/// what makes it the counter-example architecture 4.1 needs.
/// </summary>
public sealed record JobCreatedDomainEvent(Guid JobId, Guid AssigneeId, Guid OrganizationId)
    : IDomainEvent;
```

`Events/JobCompletedDomainEvent.cs`:

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>FR-9 and FR-10: triggers invoice generation and a customer notification.</summary>
public sealed record JobCompletedDomainEvent(
    Guid JobId, Guid CustomerId, Guid OrganizationId, DateTimeOffset CompletedAt)
    : IDomainEvent;
```

`Events/JobCancelledDomainEvent.cs`:

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>
/// Nothing outside Jobs consumes this: cancelling neither bills nor notifies.
/// It is the second internal-only event, and the pair with JobCompleted is
/// what makes the domain-versus-integration distinction demonstrable.
/// </summary>
public sealed record JobCancelledDomainEvent(Guid JobId, string Reason) : IDomainEvent;
```

- [ ] **Step 8: Write `JobStatus`, `JobPhoto`, `NewJobPhoto` and `Job`**

```csharp
namespace JobTracker.Modules.Jobs.Domain;

public enum JobStatus
{
    Draft,
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}
```

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// Public because Job.Photos is a public member and a public member cannot
/// expose an internal type (CS0053). Reachability is enforced by the internal
/// constructor and by the absence of an AddPhoto on the aggregate, not by the
/// class modifier.
/// </summary>
public sealed class JobPhoto : Entity
{
    internal JobPhoto(Guid id, string url, DateTimeOffset capturedAt, string? caption)
        : base(id)
    {
        Url = url;
        CapturedAt = capturedAt;
        Caption = caption;
    }

    private JobPhoto() { }

    public string Url { get; private init; } = string.Empty;
    public DateTimeOffset CapturedAt { get; private init; }
    public string? Caption { get; private init; }
    public Guid JobId { get; private init; }
}
```

```csharp
namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// The input to <see cref="Job.Complete"/>. A parameter shape, not an entity:
/// the aggregate turns each into a JobPhoto and assigns its identity.
/// </summary>
public readonly record struct NewJobPhoto(string Url, DateTimeOffset CapturedAt, string? Caption);
```

```csharp
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain.Events;

namespace JobTracker.Modules.Jobs.Domain;

public sealed class Job : AggregateRoot
{
    private readonly List<JobPhoto> _photos = [];

    private Job(Guid id) : base(id) { }

    // EF only.
    private Job() { }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Address Address { get; private set; } = null!;
    public JobStatus Status { get; private set; }
    public DateOnly? ScheduledDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? SignatureUrl { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OrganizationId { get; private set; }

    public IReadOnlyCollection<JobPhoto> Photos => _photos.AsReadOnly();

    private bool IsTerminal => Status is JobStatus.Completed or JobStatus.Cancelled;

    /// <summary>
    /// <paramref name="now"/> is a parameter rather than a read of
    /// DateTimeOffset.UtcNow, so BR-1 is testable without freezing a global
    /// clock. Handlers supply it from TimeProvider.
    /// </summary>
    public static Result<Job> Create(
        string title,
        string? description,
        Address address,
        DateOnly scheduledDate,
        Guid assigneeId,
        Guid customerId,
        Guid organizationId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Job>(JobErrors.TitleRequired);
        }

        if (scheduledDate < DateOnly.FromDateTime(now.UtcDateTime))
        {
            return Result.Failure<Job>(JobErrors.ScheduledInThePast);
        }

        var job = new Job(Guid.NewGuid())
        {
            Title = title,
            Description = description,
            Address = address,
            // D-14: creation produces a Scheduled job. Draft stays in the
            // model for a job captured without a date, unreachable from here.
            Status = JobStatus.Scheduled,
            ScheduledDate = scheduledDate,
            AssigneeId = assigneeId,
            CustomerId = customerId,
            OrganizationId = organizationId,
        };

        job.Raise(new JobCreatedDomainEvent(job.Id, assigneeId, organizationId));

        return Result.Success(job);
    }

    /// <summary>FR-2. BR-1 and BR-2.</summary>
    public Result Reschedule(DateOnly scheduledDate, Guid assigneeId, DateTimeOffset now)
    {
        if (IsTerminal) return Result.Failure(JobErrors.Terminal);
        if (scheduledDate < DateOnly.FromDateTime(now.UtcDateTime))
        {
            return Result.Failure(JobErrors.ScheduledInThePast);
        }

        ScheduledDate = scheduledDate;
        AssigneeId = assigneeId;
        Status = JobStatus.Scheduled;

        return Result.Success();
    }

    /// <summary>FR-3. BR-2 and BR-3.</summary>
    public Result Start(DateTimeOffset startedAt)
    {
        if (IsTerminal) return Result.Failure(JobErrors.Terminal);
        if (Status != JobStatus.Scheduled) return Result.Failure(JobErrors.NotScheduled);

        Status = JobStatus.InProgress;
        StartedAt = startedAt;

        return Result.Success();
    }

    /// <summary>FR-4. BR-2 and BR-4.</summary>
    public Result Complete(
        DateTimeOffset completedAt, string signatureUrl, IEnumerable<NewJobPhoto> photos)
    {
        if (IsTerminal) return Result.Failure(JobErrors.Terminal);
        if (string.IsNullOrWhiteSpace(signatureUrl))
        {
            return Result.Failure(JobErrors.SignatureRequired);
        }

        if (Status != JobStatus.InProgress) return Result.Failure(JobErrors.NotInProgress);

        Status = JobStatus.Completed;
        CompletedAt = completedAt;
        SignatureUrl = signatureUrl;

        foreach (var photo in photos)
        {
            _photos.Add(new JobPhoto(Guid.NewGuid(), photo.Url, photo.CapturedAt, photo.Caption));
        }

        Raise(new JobCompletedDomainEvent(Id, CustomerId, OrganizationId, completedAt));

        return Result.Success();
    }

    /// <summary>FR-5. BR-2 and BR-5.</summary>
    public Result Cancel(DateTimeOffset cancelledAt, string reason)
    {
        if (IsTerminal) return Result.Failure(JobErrors.Terminal);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure(JobErrors.ReasonRequired);

        Status = JobStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancellationReason = reason;

        Raise(new JobCancelledDomainEvent(Id, reason));

        return Result.Success();
    }
}
```

`Title`, `Status` and the rest use `private set` rather than `private init` because the intention-named methods assign them after construction. The architecture test in task 3 asserts none of them is public.

- [ ] **Step 9: Run the domain tests to verify green**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet test tests/JobTracker.Modules.Jobs.Domain.UnitTests
```

Expected: every test in `AddressTests` and `JobTests` passing.

- [ ] **Step 10: Commit**

```bash
git add backend
git commit -m "feat: add the Job aggregate and its invariants

Every rule in prd.md section 6 is enforced inside an intention-named
method and returns a Result rather than throwing. BR-1 to BR-5 each have
a test, and BR-2 has one per terminal state asserting that every other
transition is refused.

now is a parameter rather than a read of DateTimeOffset.UtcNow, so BR-1
is testable without freezing a global clock. One test covers the
boundary the rule turns on: a job scheduled for today is accepted,
because BR-1 says 'in the past' and today is not past. An off-by-one
there would refuse most real jobs.

Address is a value object with structural equality over all six
components, and it refuses coordinates that are not a place on Earth —
an address is a location, and a latitude of 91 puts a pin in the sea on
a map nobody checks.

JobPhoto is public with an internal constructor: Job.Photos is a public
member and a public member cannot expose an internal type (CS0053).
Reachability comes from the constructor and from the absence of an
AddPhoto, which a test asserts by reflection — photos arrive only
through Complete, the one moment the business produces them."
```

---

### Task 5: The application layer

**Files:**
- Create: the seven use-case folders under `src/Modules/Jobs/JobTracker.Modules.Jobs.Application/Jobs/`
- Create: `src/Modules/Jobs/JobTracker.Modules.Jobs.Domain/{IJobRepository,IPartyRepository,JobSearchResult,JobSearchCriteria,Assignee,Customer}.cs`
- Create: `src/Common/JobTracker.Common.Application/Behaviors/ValidationBehavior.cs`
- Test: `tests/JobTracker.Modules.Jobs.Application.UnitTests/{CreateJobCommandHandlerTests,StartJobCommandHandlerTests,CompleteJobCommandHandlerTests,SearchJobsQueryHandlerTests,ValidationBehaviorTests}.cs`

**Interfaces:**
- Consumes: `Job`, the kernel, MediatR, FluentValidation
- Produces: six commands, two queries, their handlers and validators, `IJobRepository`, `JobSearchResult`, `JobSearchCriteria`, and `ValidationBehavior`

- [ ] **Step 1: Write the failing CreateJob handler test**

Create `tests/JobTracker.Modules.Jobs.Application.UnitTests/CreateJobCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Jobs.CreateJob;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using Moq;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

public sealed class CreateJobCommandHandlerTests
{
    private readonly Mock<IJobRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private CreateJobCommandHandler Handler() =>
        new(_repository.Object, _unitOfWork.Object, _time);

    private static CreateJobCommand AValidCommand() => new(
        "Roof repair", null,
        "12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m,
        new DateOnly(2026, 3, 14), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task It_persists_the_job_and_returns_its_identifier()
    {
        var result = await Handler().Handle(AValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _repository.Verify(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_persisted_job_carries_the_JobCreatedDomainEvent()
    {
        Job? captured = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Callback<Job, CancellationToken>((job, _) => captured = job);

        await Handler().Handle(AValidCommand(), CancellationToken.None);

        captured!.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<JobCreatedDomainEvent>();
    }

    [Fact]
    public async Task An_invalid_address_fails_without_touching_the_repository()
    {
        var command = AValidCommand() with { City = "" };

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Error.Should().Be(JobErrors.AddressIncomplete);
        _repository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_past_date_fails_and_nothing_is_saved()
    {
        var command = AValidCommand() with { ScheduledDate = new DateOnly(2020, 1, 1) };

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Error.Should().Be(JobErrors.ScheduledInThePast);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task It_reads_the_clock_from_TimeProvider_rather_than_from_the_system()
    {
        // The handler supplies `now`; the aggregate never reads it. This is
        // what makes BR-1 testable without freezing a global clock.
        var command = AValidCommand() with { ScheduledDate = new DateOnly(2026, 3, 1) };

        var result = await Handler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: compilation error — `CreateJobCommand` does not exist.

- [ ] **Step 3: Write the domain contracts**

`IJobRepository.cs`:

```csharp
namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// Exactly the three members assessment line 220 names. There is deliberately
/// no generic IRepository&lt;T&gt; with Update, Delete, Count and GetAll: a Job
/// is never deleted (BR-2 says a closed job is corrected by a new job), and a
/// Delete a caller can see is a Delete someone eventually calls.
/// </summary>
public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a projection rather than aggregates (D-25). Lines 220 and 208
    /// contradict each other — one puts SearchAsync in the domain, the other
    /// demands projections without tracking — and a projected read model is
    /// what satisfies both without leaving a dead method behind.
    /// </summary>
    Task<IReadOnlyList<JobSearchResult>> SearchAsync(
        JobSearchCriteria criteria, CancellationToken cancellationToken = default);
}
```

`JobSearchResult.cs` and `JobSearchCriteria.cs`:

```csharp
namespace JobTracker.Modules.Jobs.Domain;

/// <summary>A read model. Not an aggregate: no identity, no behaviour.</summary>
public sealed record JobSearchResult(
    Guid Id,
    string Title,
    JobStatus Status,
    DateOnly? ScheduledDate,
    Guid? AssigneeId,
    string? AssigneeName,
    string Street,
    string City,
    string State,
    int PhotoCount);

public enum JobSortField
{
    ScheduledDate,
    Title,
}

/// <summary>A Specification: the query expressed in domain terms.</summary>
public sealed record JobSearchCriteria(
    Guid OrganizationId,
    string? Text,
    IReadOnlyList<JobStatus>? Statuses,
    DateOnly? From,
    DateOnly? To,
    Guid? AssigneeId,
    JobSortField Sort,
    string? Cursor,
    int Limit);
```

`Assignee.cs`, `Customer.cs`, `IPartyRepository.cs`:

```csharp
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// A read-only roster (D-26). No factory and no mutating method: EF
/// materialises one and nothing else writes one. It exists because three
/// controls in the interface need something to offer and a row showing a UUID
/// is useless.
/// </summary>
public sealed class Assignee : Entity
{
    private Assignee() { }

    public Guid OrganizationId { get; private init; }
    public string Name { get; private init; } = string.Empty;
}

public sealed class Customer : Entity
{
    private Customer() { }

    public Guid OrganizationId { get; private init; }
    public string Name { get; private init; } = string.Empty;
    public string Email { get; private init; } = string.Empty;
}
```

```csharp
namespace JobTracker.Modules.Jobs.Domain;

/// <summary>Two reads, no writes, because there is no write path.</summary>
public interface IPartyRepository
{
    Task<IReadOnlyList<Assignee>> ListAssigneesAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> ListCustomersAsync(
        Guid organizationId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Write the CreateJob use case**

`Jobs/CreateJob/CreateJobCommand.cs`:

```csharp
using JobTracker.Common.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

public sealed record CreateJobCommand(
    string Title,
    string? Description,
    string Street,
    string City,
    string State,
    string ZipCode,
    decimal Latitude,
    decimal Longitude,
    DateOnly ScheduledDate,
    Guid AssigneeId,
    Guid CustomerId,
    Guid OrganizationId) : IRequest<Result<Guid>>;
```

`Jobs/CreateJob/CreateJobCommandHandler.cs`:

```csharp
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

internal sealed class CreateJobCommandHandler(
    IJobRepository jobs,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<CreateJobCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateJobCommand command, CancellationToken cancellationToken)
    {
        var address = Address.Create(
            command.Street, command.City, command.State,
            command.ZipCode, command.Latitude, command.Longitude);

        if (address.IsFailure) return Result.Failure<Guid>(address.Error);

        // The handler supplies the instant; the aggregate never reads a clock.
        var job = Job.Create(
            command.Title, command.Description, address.Value,
            command.ScheduledDate, command.AssigneeId, command.CustomerId,
            command.OrganizationId, time.GetUtcNow());

        if (job.IsFailure) return Result.Failure<Guid>(job.Error);

        await jobs.AddAsync(job.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(job.Value.Id);
    }
}
```

`Jobs/CreateJob/CreateJobCommandValidator.cs`:

```csharp
using FluentValidation;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

/// <summary>
/// Shape only. Whether a date is in the past is BR-1 and belongs to the
/// aggregate: a validator that duplicated it would be a second place to
/// change when the rule changes.
/// </summary>
internal sealed class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Street).NotEmpty();
        RuleFor(command => command.City).NotEmpty();
        RuleFor(command => command.State).NotEmpty();
        RuleFor(command => command.ZipCode).NotEmpty();
        RuleFor(command => command.AssigneeId).NotEmpty();
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.OrganizationId).NotEmpty();
    }
}
```

- [ ] **Step 5: Run the CreateJob tests to verify green**

Expected: five passing tests.

- [ ] **Step 6: Write the remaining commands, following the same shape**

`StartJob`: `StartJobCommand(Guid JobId, Guid OrganizationId) : IRequest<Result>`, handler loads through `GetByIdAsync`, returns `JobErrors.NotFound` when absent, calls `job.Start(time.GetUtcNow())`, saves on success. **No validator**: the command carries only identifiers and `BR-3` is the aggregate's.

`CompleteJob`: `CompleteJobCommand(Guid JobId, Guid OrganizationId, string SignatureUrl, IReadOnlyList<NewPhotoInput> Photos) : IRequest<Result>` with `NewPhotoInput(string Url, string? Caption)`, handler maps to `NewJobPhoto` stamping `CapturedAt` from `TimeProvider`, plus a validator requiring a non-empty `SignatureUrl`.

`CancelJob`: `CancelJobCommand(Guid JobId, Guid OrganizationId, string Reason) : IRequest<Result>`, plus a validator requiring a non-empty `Reason`.

`RescheduleJob`: `RescheduleJobCommand(Guid JobId, Guid OrganizationId, DateOnly ScheduledDate, Guid AssigneeId) : IRequest<Result>`, plus a validator requiring a non-empty `AssigneeId`.

Each handler is the same five lines: load, refuse if absent, call the aggregate method, return its `Result` if it failed, save and return success. Write each one's test first.

- [ ] **Step 7: Write the failing SearchJobs test**

Create `tests/JobTracker.Modules.Jobs.Application.UnitTests/SearchJobsQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using JobTracker.Modules.Jobs.Application.Jobs.SearchJobs;
using JobTracker.Modules.Jobs.Domain;
using Moq;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

public sealed class SearchJobsQueryHandlerTests
{
    private readonly Mock<IJobRepository> _repository = new();

    private static JobSearchResult ARow(Guid id, string title) =>
        new(id, title, JobStatus.Scheduled, new DateOnly(2026, 3, 14),
            Guid.NewGuid(), "J. Ortiz", "12 Elm St", "Springfield", "IL", 0);

    private SearchJobsQueryHandler Handler() => new(_repository.Object);

    private static SearchJobsQuery AQuery(int limit = 2) =>
        new(Guid.NewGuid(), null, null, null, null, null, JobSortField.ScheduledDate, null, limit);

    [Fact]
    public async Task It_asks_the_repository_for_one_row_more_than_the_page()
    {
        JobSearchCriteria? captured = null;
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<JobSearchCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync([]);

        await Handler().Handle(AQuery(2), CancellationToken.None);

        // The extra row is how the handler learns whether another page exists
        // without a count. The repository runs the query; the handler builds
        // the envelope (D-25).
        captured!.Limit.Should().Be(3);
    }

    [Fact]
    public async Task A_full_page_reports_the_last_row_as_the_next_cursor()
    {
        var second = Guid.NewGuid();
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ARow(Guid.NewGuid(), "a"), ARow(second, "b"), ARow(Guid.NewGuid(), "c")]);

        var result = await Handler().Handle(AQuery(2), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.NextCursor.Should().Be(second.ToString());
        result.Value.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task A_partial_page_reports_no_next_cursor()
    {
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ARow(Guid.NewGuid(), "a")]);

        var result = await Handler().Handle(AQuery(2), CancellationToken.None);

        result.Value.NextCursor.Should().BeNull();
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task An_empty_result_is_a_success_rather_than_a_not_found()
    {
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await Handler().Handle(AQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}
```

- [ ] **Step 8: Write the SearchJobs use case**

```csharp
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.SearchJobs;

public sealed record JobResponse(
    Guid Id, string Title, string Status, DateOnly? ScheduledDate,
    Guid? AssigneeId, string? AssigneeName,
    string Street, string City, string State, int PhotoCount);

public sealed record SearchJobsQuery(
    Guid OrganizationId,
    string? Text,
    IReadOnlyList<JobStatus>? Statuses,
    DateOnly? From,
    DateOnly? To,
    Guid? AssigneeId,
    JobSortField Sort,
    string? Cursor,
    int Limit) : IRequest<Result<PagedList<JobResponse>>>;

internal sealed class SearchJobsQueryHandler(IJobRepository jobs)
    : IRequestHandler<SearchJobsQuery, Result<PagedList<JobResponse>>>
{
    public async Task<Result<PagedList<JobResponse>>> Handle(
        SearchJobsQuery query, CancellationToken cancellationToken)
    {
        // One row more than the page: that extra row is how the handler learns
        // another page exists without asking for a count, which is the cost
        // NFR-5 rejects.
        var criteria = new JobSearchCriteria(
            query.OrganizationId, query.Text, query.Statuses, query.From, query.To,
            query.AssigneeId, query.Sort, query.Cursor, query.Limit + 1);

        var rows = await jobs.SearchAsync(criteria, cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = hasMore ? rows.Take(query.Limit).ToList() : rows;

        // The repository runs the query; the handler builds the envelope (D-25).
        return Result.Success(new PagedList<JobResponse>(
            page.Select(Map).ToList(),
            hasMore ? page[^1].Id.ToString() : null));
    }

    private static JobResponse Map(JobSearchResult row) => new(
        row.Id, row.Title, row.Status.ToString(), row.ScheduledDate,
        row.AssigneeId, row.AssigneeName, row.Street, row.City, row.State, row.PhotoCount);
}
```

`GetJobById` follows the same shape, returning `Result<JobDetailResponse>` and `JobErrors.NotFound` when the repository returns null.

- [ ] **Step 9: Write the failing ValidationBehavior test**

```csharp
using FluentAssertions;
using FluentValidation;
using JobTracker.Common.Application.Behaviors;
using JobTracker.Common.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

public sealed class ValidationBehaviorTests
{
    private sealed record Probe(string Name) : IRequest<Result>;

    private sealed class ProbeValidator : AbstractValidator<Probe>
    {
        public ProbeValidator() => RuleFor(probe => probe.Name).NotEmpty();
    }

    [Fact]
    public async Task A_valid_request_reaches_the_handler()
    {
        var reached = false;
        var behavior = new ValidationBehavior<Probe, Result>([new ProbeValidator()]);

        await behavior.Handle(
            new Probe("ok"),
            () => { reached = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task An_invalid_request_never_reaches_the_handler()
    {
        var reached = false;
        var behavior = new ValidationBehavior<Probe, Result>([new ProbeValidator()]);

        var result = await behavior.Handle(
            new Probe(""),
            () => { reached = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        reached.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task With_no_validator_registered_the_request_passes_through()
    {
        var behavior = new ValidationBehavior<Probe, Result>([]);

        var result = await behavior.Handle(
            new Probe(""),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
```

- [ ] **Step 10: Write `ValidationBehavior.cs`**

```csharp
using FluentValidation;
using JobTracker.Common.Domain;
using MediatR;

namespace JobTracker.Common.Application.Behaviors;

/// <summary>
/// The Open/Closed example: validation applies to every handler in every
/// module and not one handler mentions it. Adding an authorisation behaviour
/// is a registration, not an edit.
///
/// It returns a failed Result rather than throwing, because a validation
/// failure is expected (architecture 9.2).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = validators
            .Select(validator => validator.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var error = Error.Validation(
            "request.validation",
            string.Join("; ", failures.Select(failure => failure.ErrorMessage)));

        return (TResponse)Result.Failure(error);
    }
}
```

- [ ] **Step 11: Run the whole suite to verify green**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet build JobTracker.sln
dotnet test JobTracker.sln
```

Expected: every test passing, including the architecture tests from task 3, which now compile.

- [ ] **Step 12: The deliberate red, once per naming rule**

Architecture 8.3's second mechanism. For each of the five naming rules, break the convention on one type, watch the rule fail, and restore it. This is the only thing that proves the assertion is wired to what its name claims.

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
```

For each of these, make the edit, run `dotnet test tests/JobTracker.ArchitectureTests`, confirm the named test fails, then revert:

| Rule | Break it by | Expect |
|---|---|---|
| Commands are public sealed | making `CreateJobCommand` non-sealed | `Commands_are_public_sealed_and_suffixed` fails |
| Handlers are internal sealed | making `CreateJobCommandHandler` public | `Command_handlers_are_internal_sealed_and_suffixed` fails |
| Queries are public sealed | making `SearchJobsQuery` internal | `Queries_are_public_sealed_and_suffixed` fails |
| Query handlers are internal sealed | making `SearchJobsQueryHandler` public | `Query_handlers_are_internal_sealed_and_suffixed` fails |
| Validators are internal sealed | making `CreateJobCommandValidator` public | `Validators_are_internal_sealed_and_suffixed` fails |

If a rule does **not** fail when its convention is broken, it is not wired — fix the rule before continuing. A rule that cannot fail is worse than no rule, because it reads as a guarantee.

Also verify the non-emptiness guard: temporarily rename `CreateJobCommand` to `CreateJobRequest` and confirm `Commands_are_public_sealed_and_suffixed` fails on the empty-set assertion rather than passing. Then revert.

- [ ] **Step 13: Run everything one last time and commit**

```bash
cd backend
export PATH="/opt/homebrew/opt/dotnet@9/bin:$PATH"
dotnet build JobTracker.sln
dotnet test JobTracker.sln
```

```bash
git add backend
git commit -m "feat: add the Jobs application layer and the architecture tests

Six commands and two queries, each handler doing one thing: load, call
the aggregate, map its Result, save. The rules stay in the domain — a
validator that re-checked BR-1 would be a second place to change when
the rule changes, so the validators check shape only.

SearchJobsQueryHandler asks for one row more than the page. That extra
row is how it learns another page exists without a count, which is the
cost NFR-5 rejects; the repository runs the query and the handler builds
the envelope (D-25).

ValidationBehavior is the Open/Closed example: validation reaches every
handler in every module and not one handler mentions it. It returns a
failed Result rather than throwing, because a validation failure is
expected.

The architecture tests carry both mechanisms architecture 8.3 requires.
Every rule asserts its subject is non-empty before asserting anything
about it, because a NetArchTest assertion over an empty set passes — a
renamed suffix would otherwise turn a rule into a rule about nothing,
silently. And each naming rule was watched failing once, by breaking the
convention on a real type and restoring it, which is the only thing that
proves the assertion is wired to what its name claims.

The no-public-setter rule is written by hand: NetArchTest cannot express
it, and it is the rule that separates a domain model from a data bag."
```

---

## Self-Review

**Spec coverage.** Assessment 3.1 is task 4: the aggregate with all its properties and all three invariants, `Address` as a value object with structural equality, `JobPhoto` reachable only through the root, and the three domain events. 3.2 is task 5: `CreateJobCommand` returning `Result<Guid>`, `CompleteJobCommand` returning `Result`, `SearchJobsQuery` returning `Result<PagedList<JobResponse>>` with pagination and every filter, and the naming conventions of lines 210-215 enforced rather than followed. Assessment 5.2 items 1, 2, 3 and 4 are tasks 2, 4 and 5.

**Not in this plan, by design.** 3.3's repository *implementation*, the EF configuration and the `jobs` schema are plan 3B — the interface is here, the EF Core behind it is not. 3.4's outbox and Hangfire are plan 4. Billing's three projects arrive with plan 4 too, when there is an integration event for them to consume; creating them now would add three empty projects the architecture tests would have to skip.

**Placeholder scan.** Step 6 of task 5 describes four commands by their shape rather than reproducing four near-identical handlers. That is the one place this plan compresses, and it is a judgement call: the CreateJob handler above is the worked example and the four others differ only in which aggregate method they call. If the executing session finds the description insufficient, that is a plan defect and the fix is to expand it, not to improvise.

**Type consistency.** `Result` and `Result<T>` come from `Common.Domain` throughout. `JobErrors` is the single source of every business-rule error, so a handler test asserting `JobErrors.ScheduledInThePast` and the aggregate returning it cannot drift. `JobSearchCriteria.Limit` is the page size **plus one**, set by the handler and consumed by the repository in 3B — a mismatch there would page wrongly, so 3B's integration test asserts the boundary.

**Two things this review found.** `Job.Create` takes eight parameters, which is past the point where a parameter object would read better; it is left as is because design B5 fixes the signature and changing it would put the plan and the design out of step, but it is worth revisiting once the endpoints exist. And `ValidationBehavior` casts `Result.Failure(error)` to `TResponse`, which works only because the constraint is `where TResponse : Result` and every response in this codebase is a `Result` or a `Result<T>` — a `Result<T>` failure built this way loses its type parameter, so the cast is safe at runtime but the compiler cannot prove it. Task 5 should add a test that a failing `Result<Guid>` request returns a well-formed failure rather than throwing at the cast.
