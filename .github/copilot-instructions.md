# Copilot Instructions — MVCAllOptions (ABP Framework, Layered DDD)

This project uses **ABP Framework** with a **Layered (DDD) solution template** and **MVC/Razor Pages UI**.
Apply all rules below for every code generation, edit, or review task.

> **Additional Rules Source**: Always read and apply every `.mdc` file found under `.cursor/rules/` and its subdirectories. The full rule set lives there, organised as:
> - `.cursor/rules/template/app.mdc` — Solution template & feature flow
> - `.cursor/rules/framework/common/abp-core.mdc` — ABP core conventions
> - `.cursor/rules/framework/common/application-layer.mdc` — Application layer patterns
> - `.cursor/rules/framework/common/authorization.mdc` — Permissions & authorization
> - `.cursor/rules/framework/common/cli-commands.mdc` — ABP CLI commands
> - `.cursor/rules/framework/common/ddd-patterns.mdc` — DDD entities, aggregates, repositories
> - `.cursor/rules/framework/common/dependency-rules.mdc` — Layer dependency guardrails
> - `.cursor/rules/framework/common/development-flow.mdc` — End-to-end development workflow
> - `.cursor/rules/framework/common/infrastructure.mdc` — Settings, cache, events, background jobs
> - `.cursor/rules/framework/common/multi-tenancy.mdc` — Multi-tenancy patterns
> - `.cursor/rules/framework/data/ef-core.mdc` — EF Core DbContext & migrations
> - `.cursor/rules/framework/testing/patterns.mdc` — Integration testing patterns
> - `.cursor/rules/framework/ui/mvc.mdc` — MVC / Razor Pages UI patterns
>
> When any `.mdc` rule conflicts with a section below, **the `.mdc` file takes precedence** as it is the canonical source of truth.

---

## 1. Solution Structure

```
src/
  MVCAllOptions.Domain.Shared/        # Constants, enums, localization, ETOs
  MVCAllOptions.Domain/               # Entities, repository interfaces, domain services
  MVCAllOptions.Application.Contracts/ # DTOs, service interfaces, permissions
  MVCAllOptions.Application/          # Application service implementations, mappers
  MVCAllOptions.EntityFrameworkCore/  # EF Core DbContext, repository implementations
  MVCAllOptions.HttpApi/              # REST API controllers (optional)
  MVCAllOptions.HttpApi.Client/       # Client proxies for remote calls
  MVCAllOptions.Web/                  # MVC/Razor Pages UI (admin)
  MVCAllOptions.Web.Public/           # Public-facing website (anonymous access)
  MVCAllOptions.DbMigrator/           # Database migration console app
test/
  MVCAllOptions.Domain.Tests/
  MVCAllOptions.Application.Tests/
  MVCAllOptions.EntityFrameworkCore.Tests/
```

### Layer Dependency Direction (STRICTLY ENFORCED)

| Project | Can Reference |
|---------|--------------|
| Domain.Shared | Nothing |
| Domain | Domain.Shared |
| Application.Contracts | Domain.Shared |
| Application | Domain, Application.Contracts |
| EntityFrameworkCore | Domain |
| HttpApi | Application.Contracts only |
| Web | Application.Contracts |
| DbMigrator | EntityFrameworkCore |

**Critical violations — NEVER do:**
```csharp
// ❌ Application accessing DbContext directly
private readonly MyDbContext _dbContext;

// ❌ Domain depending on Application
private readonly IBookAppService _appService;

// ❌ HttpApi depending on concrete Application implementation
private readonly BookAppService _bookAppService; // Use interface!

// ❌ Minimal APIs, MediatR, custom UoW, manual HTTP calls from UI
```

---

## 2. ABP Core Conventions

### Module System
Every module has an `AbpModule` class. Middleware (`OnApplicationInitialization`) only belongs in the final host app.

### Dependency Injection
Use marker interfaces — never `AddScoped/AddTransient/AddSingleton` manually:
- `ITransientDependency` → Transient
- `ISingletonDependency` → Singleton
- `IScopedDependency` → Scoped

Classes inheriting `ApplicationService`, `DomainService`, or `AbpController` are auto-registered.

### Base Class Properties (do NOT inject these separately)

| Property | Available In |
|----------|-------------|
| `GuidGenerator` | All base classes |
| `Clock` | All base classes |
| `CurrentUser` | All base classes |
| `CurrentTenant` | All base classes |
| `L` (StringLocalizer) | `ApplicationService`, `AbpController` |
| `AuthorizationService` | `ApplicationService`, `AbpController` |
| `Logger` | All base classes |
| `UnitOfWorkManager` | `ApplicationService`, `DomainService` |

### Time Handling
```csharp
// ✅ In base-class derived services
var now = Clock.Now;

// ✅ In other services — inject IClock
private readonly IClock _clock;

// ❌ Never
var now = DateTime.Now;
var now = DateTime.UtcNow;
```

### Async Rules
- All async methods end with `Async` suffix, use `async`/`await` all the way — never `.Result` or `.Wait()`

### Business Exceptions
```csharp
throw new BusinessException("MVCAllOptions:BookNameAlreadyExists")
    .WithData("Name", bookName);
```

### ABP Anti-Patterns (NEVER USE)
| Don't | Use Instead |
|-------|-------------|
| Minimal APIs | ABP Auto API Controllers |
| MediatR | Application Services |
| `DbContext` in App Services | `IRepository<T>` |
| `AddScoped/Transient/Singleton` | DI marker interfaces |
| `DateTime.Now` | `IClock` / `Clock.Now` |
| Custom Unit of Work | `IUnitOfWorkManager` |
| Manual HTTP calls from UI | ABP client proxies |
| Hardcoded role checks | Permission-based authorization |
| Business logic in Controllers | Application Services |

---

## 3. DDD Patterns

### Rich Domain Model (Entities)
Use **private setters + public methods** — never anemic entities with public setters.

```csharp
public class Book : AuditedAggregateRoot<Guid>
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }

    protected Book() { } // Required for ORM

    public Book(Guid id, string name, decimal price) : base(id)
    {
        SetName(name);
        SetPrice(price);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: BookConsts.MaxNameLength);
    }

    public void SetPrice(decimal price)
    {
        Price = Check.Range(price, nameof(price), 0, 9999);
    }
}
```

**Entity rules:**
- Protected parameterless constructor required for ORM
- Don't generate GUID inside constructor — pass from `GuidGenerator.Create()` externally
- Private setters, enforce invariants in methods
- Initialize collections in primary constructor
- Reference other aggregates by ID only, not navigation properties
- One repository per aggregate root — **never** for child entities

### Domain Events
```csharp
AddLocalEvent(new OrderCompletedEvent(Id));           // same transaction
AddDistributedEvent(new OrderCompletedEto { OrderId = Id }); // cross-service (async)
```

### Repository Pattern
```csharp
// Generic repo is fine for simple CRUD
private readonly IRepository<Book, Guid> _bookRepository;

// Custom interface only when custom queries needed (define in Domain, implement in EF Core)
public interface IBookRepository : IRepository<Book, Guid>
{
    Task<Book> FindByNameAsync(string name);
}
```

### Domain Services (`*Manager` naming)
- Accept/return domain objects, not DTOs
- No dependency on authenticated user — receive values from application layer
- Use `GuidGenerator`, `Clock` from base class

---

## 4. Application Layer Patterns

### Service Interface (Application.Contracts)
```csharp
public interface IBookAppService : IApplicationService
{
    Task<BookDto> GetAsync(Guid id);
    Task<PagedResultDto<BookListItemDto>> GetListAsync(GetBookListInput input);
    Task<BookDto> CreateAsync(CreateBookDto input);
    Task<BookDto> UpdateAsync(Guid id, UpdateBookDto input);
    Task DeleteAsync(Guid id);
}
```

### Service Implementation (Application)
```csharp
public class BookAppService : ApplicationService, IBookAppService
{
    // Use IRepository<Book,Guid> for simple CRUD, or IBookRepository for custom queries
}
```

**App service rules:**
- Accept/return DTOs only — never expose entities
- Don't repeat entity name in method names: `GetAsync` not `GetBookAsync`
- ID is NOT inside UpdateDto — pass separately
- Call `UpdateAsync` explicitly (don't rely on change tracking)
- Don't call other app services in the same module
- Don't use `IFormFile`/`Stream` — pass `byte[]` from controllers
- Use base class properties (`Clock`, `CurrentUser`, `GuidGenerator`, `L`)

### DTO Naming Conventions

| Purpose | Name |
|---------|------|
| Query input | `Get{Entity}Input` |
| List query input | `Get{Entity}ListInput` |
| Create input | `Create{Entity}Dto` |
| Update input | `Update{Entity}Dto` |
| Single output | `{Entity}Dto` |
| List item output | `{Entity}ListItemDto` |

### Object Mapping — Mapperly (default)
```csharp
[Mapper]
public partial class BookMapper
{
    public partial BookDto MapToDto(Book book);
    public partial List<BookDto> MapToDtoList(List<Book> books);
}
// Register: context.Services.AddSingleton<BookMapper>();
```
Check existing mapping files before deciding on Mapperly vs AutoMapper — use what's already in the solution.

### Validation
Use data annotations for simple rules; `IValidatableObject` or FluentValidation only for cross-field or complex application-level validation. Domain invariants belong in the entity/domain service.

---

## 5. Authorization (Permissions)

### Define Permissions (Application.Contracts)
```csharp
public static class MVCAllOptionsPermissions
{
    public const string GroupName = "MVCAllOptions";

    public static class Books
    {
        public const string Default = GroupName + ".Books";
        public const string Create = Default + ".Create";
        public const string Edit   = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }
}
```
Register via a `PermissionDefinitionProvider`.

### Using Permissions
```csharp
[Authorize(MVCAllOptionsPermissions.Books.Create)]
public async Task<BookDto> CreateAsync(CreateBookDto input) { ... }

// Programmatic
await CheckPolicyAsync(MVCAllOptionsPermissions.Books.Edit);
if (await IsGrantedAsync(MVCAllOptionsPermissions.Books.Delete)) { ... }
```

### Current User
Use `CurrentUser` from base class (or inject `ICurrentUser` in non-base services). Never trust client input for user identity.

---

## 6. Entity Framework Core

### DbContext
```csharp
[ConnectionStringName("Default")]
public class MVCAllOptionsDbContext : AbpDbContext<MVCAllOptionsDbContext>
{
    public DbSet<Book> Books { get; set; }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureMVCAllOptions();
    }
}
```

### Entity Configuration
```csharp
builder.Entity<Book>(b =>
{
    b.ToTable(MVCAllOptionsConsts.DbTablePrefix + "Books", MVCAllOptionsConsts.DbSchema);
    b.ConfigureByConvention(); // Always call this — sets up ABP conventions
    b.Property(x => x.Name).IsRequired().HasMaxLength(BookConsts.MaxNameLength);
});
```
Always call `b.ConfigureByConvention()`.

### Module Configuration
```csharp
context.Services.AddAbpDbContext<MVCAllOptionsDbContext>(options =>
{
    options.AddDefaultRepositories(); // ✅ Aggregate roots only
    // ❌ Never: options.AddDefaultRepositories(includeAllEntities: true)
});
```

### Migration Commands
```bash
cd src/MVCAllOptions.EntityFrameworkCore
dotnet ef migrations add MigrationName

# Run DbMigrator (recommended — also seeds data)
dotnet run --project ../MVCAllOptions.DbMigrator
```

---

## 7. Multi-Tenancy

```csharp
public class Product : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; } // null = Host
    // ...
}
```

- **Never** manually filter by `TenantId` — ABP does it automatically
- Use `CurrentTenant.Change(tenantId)` in a `using` block to switch tenant context
- Don't change `TenantId` after entity creation

---

## 8. Infrastructure Services

### Settings
Define via `SettingDefinitionProvider`. Read with `ISettingProvider` (inject) or `SettingProvider` (base class).

### Distributed Cache
```csharp
private readonly IDistributedCache<BookCacheItem> _cache;
// Use GetOrAddAsync; annotate cache class with [CacheName("...")]
```

### Event Bus
- **Local events** (`ILocalEventHandler<T>`) — same transaction, same process
- **Distributed events** (`IDistributedEventHandler<T>`) — cross-service/module, ETOs in Domain.Shared

### Background Jobs
Inherit `AsyncBackgroundJob<TArgs>`, implement `ExecuteAsync`.

---

## 9. MVC / Razor Pages UI

### Page Model
```csharp
public class IndexModel : AbpPageModel
{
    private readonly IBookAppService _bookAppService;
    // Inject via constructor, use AppService (through Application.Contracts)
}
```

Use ABP Tag Helpers (`<abp-card>`, `<abp-button>`, `<abp-dynamic-form>`, `<abp-modal>`, `<abp-table>`).

### Localization
```html
@* Razor *@
<h1>@L["Books"]</h1>
```
```javascript
// JS
var text = abp.localization.getResource('MVCAllOptions')('Books');
```

### JavaScript Patterns
```javascript
// Authorization check
abp.auth.isGranted('MVCAllOptions.Books.Create')

// Ajax
abp.ajax({ url: '...', type: 'POST', data: JSON.stringify(data) })

// Notifications
abp.notify.success('Created!');

// DataTables with ABP
abp.libs.datatables.normalizeConfiguration({ serverSide: true, ... })
```

---

## 10. Testing Patterns

Use **integration tests** (not unit tests with mocks) backed by SQLite in-memory.

```csharp
public class BookAppService_Tests : MVCAllOptionsApplicationTestBase
{
    private readonly IBookAppService _bookAppService;
    public BookAppService_Tests() => _bookAppService = GetRequiredService<IBookAppService>();

    [Fact]
    public async Task Should_Create_Book() { /* Arrange / Act / Assert with Shouldly */ }
}
```

**Test naming:** `Should_ExpectedBehavior_When_Condition`

**Assertions:** Use Shouldly (`ShouldBe`, `ShouldNotBeNull`, `ShouldContain`, `Should.ThrowAsync<T>`).

---

## 11. Adding a New Feature — Checklist

1. **Entity** → `*.Domain/`
2. **Constants/enums** → `*.Domain.Shared/`
3. **Repository interface** (only if custom queries) → `*.Domain/`
4. **EF Core config + repository impl** → `*.EntityFrameworkCore/`
5. **Migration** → `dotnet ef migrations add ...` then run DbMigrator
6. **DTOs + service interface + permissions** → `*.Application.Contracts/`
7. **Mapper** → `*.Application/` (Mapperly partial class)
8. **App service impl** → `*.Application/`
9. **Razor Page/View** → `*.Web/` (use `AbpPageModel`, tag helpers)
10. **Tests** → `*.Application.Tests/` or `*.Domain.Tests/`

---

## 12. ABP CLI Quick Reference

```bash
abp generate-proxy -t ng                          # Angular proxies
abp generate-proxy -t csharp -u https://localhost:44300  # C# proxies
abp install-libs                                  # Install JS libs (MVC/Blazor Server)
abp add-package-ref PackageName                   # Add module package reference
abp new-module ModuleName -t module:ddd           # New DDD module
abp install-module Volo.Blogging                  # Install published module
abp add-package Volo.Abp.Caching.StackExchangeRedis
abp update                                        # Update all ABP packages
abp clean                                         # Delete bin/obj folders
abp suite generate --entity .suite/entities/Book.json --solution ./MVCAllOptions.sln
```

---

## Skills

| Skill | Description | File |
|-------|-------------|------|
| `maf-workflows` | MAF agents, fan-out/fan-in workflows, DevUI, executor patterns | `.github/skills/maf-workflows/SKILL.md` |
| `code-review` | ABP-aware code review of unstaged/staged/branch/commit changes against all `.cursor/rules/` | `.github/skills/code-review/SKILL.md` |
| `abp-ai-management` | ABP AI Management module (Pro): installing, configuring workspaces, IChatClient usage, chat widgets (MVC), custom IChatClientFactory, IWorkspaceConfigurationStore, permissions, remote/microservice client scenarios, Semantic Kernel and MAF integration | `.github/skills/abp-ai-management/SKILL.md` |

---

## Key References
- ABP Docs: https://abp.io/docs/latest
- Layered Template: https://abp.io/docs/latest/solution-templates/layered-web-application
- DDD Guide: https://abp.io/docs/latest/framework/architecture/domain-driven-design
- Testing: https://abp.io/docs/latest/testing
