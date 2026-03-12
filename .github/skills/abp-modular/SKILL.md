---
name: abp-modular
description: "ABP Framework modular monolith implementation skill for this MVCAllOptions project. Use when: creating a new module, wiring a module into the main application, adding entities and EF Core mappings, building application services and DTOs, exposing Auto API Controllers, creating Razor Pages UI, wiring module menus, implementing integration services for cross-module data access, publishing/subscribing to distributed events for cross-module notifications, or performing cross-module JOIN queries in the main application. Covers the full lifecycle from module scaffold to production patterns. Triggers: 'add a module', 'create module', 'implement phase', 'integration service', 'cross-module', 'event bus', 'distributed event', 'module integration'."
argument-hint: "Describe which phase or feature to implement (e.g. 'entity + DB for Orders module', 'integration service between Orders and Catalog', 'distributed event on order placed', 'cross-module JOIN report')"
---

# ABP Modular Monolith Implementation Skill

## Purpose

Guide end-to-end implementation of features inside an ABP modular monolith solution following the `MVCAllOptions` project structure. The main solution lives under `src/` and modules live under their own folder (e.g. `mvcalloptions.orders/`). Each phase below maps to a tutorial part and a concrete set of files.

> **Key mapping for this project**:  
> Tutorial `Catalog` → `MVCAllOptions` (main app, `src/`)  
> Tutorial `Ordering` → `MVCAllOptions.Orders` module (`mvcalloptions.orders/`)

---

## When to Use

- Scaffolding a brand-new Standard Module and installing it into the main application
- Adding an entity, configuring EF Core, creating a migration
- Defining DTOs, service interfaces, permissions, and implementing app services
- Exposing HTTP API via Auto Controllers
- Building Razor Pages UI (page model, `.cshtml`, JavaScript, menu)
- Implementing **integration services** so one module can query data from another
- Publishing **distributed events** from a module and handling them in another
- Writing cross-module **JOIN queries** in the main application layer

---

## Step-by-Step Procedure

---

### Phase 1 — Module Setup (Parts 1–4)

> Creates the module scaffold and wires it into the main application.

**Standard module projects created by ABP Studio:**

| Project | Purpose |
|---------|---------|
| `*.Domain` | Entities, domain services, repository interfaces |
| `*.Domain.Shared` | Enums, constants, ETOs |
| `*.Application.Contracts` | DTOs, `IAppService` interfaces, permissions |
| `*.Application` | App service implementations, mappers |
| `*.EntityFrameworkCore` | `IModuleDbContext`, `ModuleDbContext`, `ModelCreatingExtensions` |
| `*.HttpApi` | (optional) HTTP API controllers |
| `*.Web` | Razor Pages UI, menu contributors |

**Installation into main app** — ABP Studio installs the module by:
1. Importing the module in ABP Studio → adds `[DependsOn]` attributes to host modules
2. Adding project references: `*.Application`, `*.Web` → referenced in host app
3. Adding `[ReplaceDbContext(typeof(IModuleDbContext))]` to host `DbContext`
4. Implementing `IModuleDbContext` on host `DbContext`
5. Calling `builder.ConfigureModule()` inside `OnModelCreating`

---

### Phase 2 — Entity + EF Core (Part 5)

#### 2a. Entity

Place entities in module's `*.Domain` project:

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.Domain/Orders/Order.cs
public class Order : CreationAuditedAggregateRoot<Guid>
{
    public Guid BookId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }

    protected Order() { }

    public Order(Guid id, Guid bookId, string customerName) : base(id)
    {
        BookId = bookId;
        CustomerName = customerName;
        State = OrderState.Placed;
    }
}
```

Place enums in module's `*.Domain.Shared` project:

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.Domain.Shared/Orders/OrderState.cs
public enum OrderState : byte
{
    Placed = 0,
    Delivered = 1,
    Canceled = 2
}
```

#### 2b. EF Core — Module DbContext Interface

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.EntityFrameworkCore/Orders/IOrdersDbContext.cs
[ConnectionStringName(OrdersDbProperties.ConnectionStringName)]
public interface IOrdersDbContext : IEfCoreDbContext
{
    DbSet<Order> Orders { get; set; }
}
```

#### 2c. EF Core — Module DbContext Implementation

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.EntityFrameworkCore/Orders/OrdersDbContext.cs
[ConnectionStringName(OrdersDbProperties.ConnectionStringName)]
public class OrdersDbContext : AbpDbContext<OrdersDbContext>, IOrdersDbContext
{
    public DbSet<Order> Orders { get; set; }

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureOrders();
    }
}
```

#### 2d. EF Core — Model Creating Extensions (table config)

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.EntityFrameworkCore/Orders/OrdersDbContextModelCreatingExtensions.cs
public static void ConfigureOrders(this ModelBuilder builder)
{
    builder.Entity<Order>(b =>
    {
        b.ToTable(OrdersDbProperties.DbTablePrefix + "Orders", OrdersDbProperties.DbSchema);
        b.ConfigureByConvention(); // ALWAYS call this
        b.Property(q => q.CustomerName).IsRequired().HasMaxLength(120);
    });
}
```

#### 2e. Host DbContext — wire in 3 steps

```csharp
// src/MVCAllOptions.EntityFrameworkCore/MVCAllOptionsDbContext.cs

[ReplaceDbContext(typeof(IOrdersDbContext))]         // Step 1: attribute
public class MVCAllOptionsDbContext :
    AbpDbContext<MVCAllOptionsDbContext>,
    IOrdersDbContext                                  // Step 2: implement interface
{
    public DbSet<Order> Orders { get; set; }          // Step 3: add DbSet

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureOrders();                    // Step 4: call extension
    }
}
```

#### 2f. Migration

```bash
# From EntityFrameworkCore project directory:
dotnet ef migrations add AddOrders

# Apply migration via DbMigrator (recommended):
dotnet run --project src/MVCAllOptions.DbMigrator
```

---

### Phase 3 — Application Service + API + UI (Part 5)

#### 3a. Permissions (Application.Contracts)

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.Application.Contracts/Permissions/OrdersPermissions.cs
public static class OrdersPermissions
{
    public const string GroupName = "Orders";
    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
        public const string Create  = Default + ".Create";
        public const string Delete  = Default + ".Delete";
    }
}
```

Register with a `PermissionDefinitionProvider`.

#### 3b. DTOs (Application.Contracts)

```csharp
// OrderDto — use CreationAuditedEntityDto for CreationTime
public class OrderDto : CreationAuditedEntityDto<Guid>
{
    public Guid BookId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }
}

// CreateOrderDto
public class CreateOrderDto
{
    [Required][StringLength(150)] public string CustomerName { get; set; } = null!;
    [Required] public Guid BookId { get; set; }
}
```

#### 3c. Service Interface (Application.Contracts)

```csharp
public interface IOrderAppService : IApplicationService
{
    Task<PagedResultDto<OrderDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task CreateAsync(CreateOrderDto input);
}
```

#### 3d. Mapper (Application) using Mapperly

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.Application/OrdersApplicationMappers.cs
[Mapper]
public partial class OrderToOrderDtoMapper : MapperBase<Order, OrderDto>
{
    public override partial OrderDto Map(Order source);
    public override partial void Map(Order source, OrderDto destination);
}
```

#### 3e. App Service Implementation (Application)

```csharp
public class OrderAppService : OrdersAppService, IOrderAppService
{
    private readonly IRepository<Order, Guid> _repository;
    private readonly OrderToOrderDtoMapper _mapper;

    public OrderAppService(IRepository<Order, Guid> repository, OrderToOrderDtoMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<OrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var orders = await _repository.GetListAsync();
        return new PagedResultDto<OrderDto>(orders.Count,
            ObjectMapper.Map<List<Order>, List<OrderDto>>(orders));
    }

    public async Task CreateAsync(CreateOrderDto input)
    {
        var order = new Order(GuidGenerator.Create(), input.BookId, input.CustomerName);
        await _repository.InsertAsync(order);
    }
}
```

#### 3f. Auto API Controllers

Register in the **host** web module (`src/MVCAllOptions.Web/MVCAllOptionsWebModule.cs`):

```csharp
private void ConfigureAutoApiControllers()
{
    Configure<AbpAspNetCoreMvcOptions>(options =>
    {
        options.ConventionalControllers.Create(
            typeof(OrdersApplicationModule).Assembly, settings =>
            {
                settings.RootPath = "orders";
            });
    });
}
```

> **Important**: Do NOT configure `ConventionalControllers` inside the module's own `HttpApiModule`. Always configure it in the host application's Web module.

#### 3g. Razor Page UI (Web)

```csharp
// Pages/Orders/Index.cshtml.cs
public class IndexModel : AbpPageModel
{
    private readonly IOrderAppService _orderAppService;
    public IndexModel(IOrderAppService orderAppService)
        => _orderAppService = orderAppService;

    public async Task OnGetAsync() { }
}
```

```html
<!-- Pages/Orders/Index.cshtml -->
@page
@model IndexModel
<abp-card>
    <abp-card-body>
        <abp-table id="OrdersTable" striped-rows="true" />
    </abp-card-body>
</abp-card>
```

```javascript
// wwwroot/Pages/Orders/index.js
$(function () {
    var dataTable = $('#OrdersTable').DataTable(
        abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            ajax: abp.libs.datatables.createAjax(
                mvcAllOptions.orders.orders.order.getList),
            columns: [
                { title: 'Customer', data: 'customerName' },
                { title: 'State', data: 'state' }
            ]
        })
    );
});
```

#### 3h. Menu Contributor

```csharp
private async Task ConfigureMainMenuAsync(MenuConfigurationContext context)
{
    if (!await context.IsGrantedAsync(OrdersPermissions.Orders.Default))
        return;

    context.Menu.AddItem(
        new ApplicationMenuItem(
            OrdersMenus.Prefix,
            context.GetLocalizer<OrdersResource>()["Menu:Orders"],
            "~/Orders",
            icon: "fa fa-basket-shopping"
        )
    );
}
```

---

### Phase 4 — Integration Services (Part 6)

> Request/response style cross-module communication. Module A defines `[IntegrationService]` interface in its `*.Application.Contracts/Integration/`; Module B references that contract project and calls the service.

#### 4a. Define the Integration Service Interface

Create an `Integration/` folder inside the **Contracts** project of the providing module.  
> The `Integration/` folder isolates cross-module contracts from business application services.

```csharp
// src/MVCAllOptions.Application.Contracts/Integration/IBookIntegrationService.cs
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace MVCAllOptions.Integration;

[IntegrationService]   // ← marks this as an integration service, NOT exposed as HTTP API
public interface IBookIntegrationService : IApplicationService
{
    Task<List<BookDto>> GetBooksByIdsAsync(List<Guid> ids);
}
```

Key points:
- `[IntegrationService]` attribute from `Volo.Abp` namespace
- Placed in `Integration/` subfolder of `*.Application.Contracts`
- NOT exposed as HTTP API by Auto Controllers (ABP filters it out)
- Returns existing `BookDto` (reuse is fine; split if DTOs will diverge)

#### 4b. Implement the Integration Service

Create an `Integration/` folder inside the **Application** project of the providing module.

```csharp
// src/MVCAllOptions.Application/Integration/BookIntegrationService.cs
namespace MVCAllOptions.Integration;

public class BookIntegrationService : MVCAllOptionsAppService, IBookIntegrationService
{
    private readonly IRepository<Book, Guid> _repository;

    public BookIntegrationService(IRepository<Book, Guid> repository)
        => _repository = repository;

    public async Task<List<BookDto>> GetBooksByIdsAsync(List<Guid> ids)
    {
        var books = await _repository.GetListAsync(b => ids.Contains(b.Id));
        return ObjectMapper.Map<List<Book>, List<BookDto>>(books);
    }
}
```

#### 4c. Add Project Reference in Consuming Module

The consuming module (`Orders.Application`) must reference the providing module's contracts:

```xml
<!-- mvcalloptions.orders/src/MVCAllOptions.Orders.Application/MVCAllOptions.Orders.Application.csproj -->
<ProjectReference Include="..\..\..\..\src\MVCAllOptions.Application.Contracts\MVCAllOptions.Application.Contracts.csproj" />
```

Also add `[DependsOn(typeof(MVCAllOptionsApplicationContractsModule))]` to the consuming module class if not already present.

#### 4d. Consume the Integration Service

Update `OrderDto` to include the enriched field:

```csharp
public class OrderDto : CreationAuditedEntityDto<Guid>
{
    public Guid BookId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }
    public string BookName { get; set; } = string.Empty;  // ← populated at runtime
}
```

Update the Mapperly mapper to ignore the extra property (it is filled manually, not from entity):

```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class OrderToOrderDtoMapper : MapperBase<Order, OrderDto>
{
    [MapperIgnoreTarget(nameof(OrderDto.BookName))]
    public override partial OrderDto Map(Order source);

    [MapperIgnoreTarget(nameof(OrderDto.BookName))]
    public override partial void Map(Order source, OrderDto destination);
}
```

Inject and use in `OrderAppService.GetListAsync`:

```csharp
public class OrderAppService : OrdersAppService, IOrderAppService
{
    private readonly IRepository<Order, Guid> _repository;
    private readonly IBookIntegrationService _bookIntegration;

    public OrderAppService(
        IRepository<Order, Guid> repository,
        IBookIntegrationService bookIntegration)
    {
        _repository = repository;
        _bookIntegration = bookIntegration;
    }

    public async Task<PagedResultDto<OrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var orders = await _repository.GetListAsync();

        var bookIds = orders.Select(o => o.BookId).Distinct().ToList();
        var books = (await _bookIntegration.GetBooksByIdsAsync(bookIds))
            .ToDictionary(b => b.Id, b => b.Name);

        var dtos = ObjectMapper.Map<List<Order>, List<OrderDto>>(orders);
        dtos.ForEach(dto => dto.BookName = books.GetValueOrDefault(dto.BookId, string.Empty));

        return new PagedResultDto<OrderDto>(orders.Count, dtos);
    }
}
```

#### 4e. Integration Folder Convention (Summary)

```
src/MVCAllOptions.Application.Contracts/
  Integration/
    IBookIntegrationService.cs   ← [IntegrationService] interface

src/MVCAllOptions.Application/
  Integration/
    BookIntegrationService.cs    ← implementation

mvcalloptions.orders/src/MVCAllOptions.Orders.Application/
  Orders/
    OrderAppService.cs           ← injects IBookIntegrationService
```

> **Design tip**: Keep cross-module integration calls minimal. Cache results if `GetListAsync` is called frequently to avoid N+1 style integration calls.

---

### Phase 5 — Distributed Events (Part 7)

> Publish-subscribe style cross-module notification. Use `IDistributedEventBus` (works in-process by default; can be switched to RabbitMQ/Kafka without code changes).

#### 5a. Define the ETO (Event Transfer Object)

Place in `*.Domain.Shared` or `*.Application.Contracts/Events/` of the **publishing** module.  
Using Contracts project makes it reachable by subscriber without pulling the full module.

```csharp
// mvcalloptions.orders/src/MVCAllOptions.Orders.Application.Contracts/Events/OrderPlacedEto.cs
namespace MVCAllOptions.Orders.Events;

public class OrderPlacedEto
{
    public string CustomerName { get; set; } = null!;
    public Guid BookId { get; set; }
}
```

#### 5b. Publish the Event

Inject `IDistributedEventBus` in the app service that triggers the action:

```csharp
private readonly IDistributedEventBus _eventBus;

public async Task CreateAsync(CreateOrderDto input)
{
    var order = new Order(GuidGenerator.Create(), input.BookId, input.CustomerName);
    await _repository.InsertAsync(order);

    await _eventBus.PublishAsync(new OrderPlacedEto
    {
        BookId = order.BookId,
        CustomerName = order.CustomerName
    });
}
```

#### 5c. Subscribe to the Event (in another module)

The subscribing module must reference `*.Application.Contracts` of the publishing module to access the ETO class.

```csharp
// src/MVCAllOptions.Application/EventHandlers/OrderEventHandler.cs
using MVCAllOptions.Orders.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

public class OrderEventHandler :
    IDistributedEventHandler<OrderPlacedEto>,  // ABP auto-subscribes
    ITransientDependency                        // register as transient
{
    private readonly IRepository<Book, Guid> _bookRepository;

    public OrderEventHandler(IRepository<Book, Guid> bookRepository)
        => _bookRepository = bookRepository;

    public async Task HandleEventAsync(OrderPlacedEto eventData)
    {
        var book = await _bookRepository.FindAsync(eventData.BookId);
        if (book == null) return;

        book.StockCount--;
        await _bookRepository.UpdateAsync(book);
    }
}
```

Key points:
- `IDistributedEventHandler<TEto>` — ABP auto-wires subscriptions
- `ITransientDependency` — required for ABP to find and register the handler
- In-process by default; no message broker needed for modular monolith
- Prefer `IDistributedEventBus` over `ILocalEventBus` to allow future extraction to microservices

#### 5d. Supporting Entity Field

When events alter entity state (e.g., `StockCount`), add the field to the entity and migration:

```csharp
// src/MVCAllOptions.Domain/Books/Book.cs
public int StockCount { get; set; }
```

```bash
dotnet ef migrations add AddBookStockCount
dotnet run --project src/MVCAllOptions.DbMigrator
```

---

### Phase 6 — Cross-Module JOIN (Part 8)

> When both modules share the same physical database, the **main application** (not any module) can perform LINQ JOIN queries spanning multiple modules' tables.

#### 6a. Define Reporting Interface + DTO (in main Application.Contracts)

```csharp
// src/MVCAllOptions.Application.Contracts/Reports/IOrderReportAppService.cs
public interface IOrderReportAppService : IApplicationService
{
    Task<List<OrderReportDto>> GetLatestOrdersAsync();
}

public class OrderReportDto
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }
    public Guid BookId { get; set; }
    public string BookName { get; set; } = null!;
}
```

#### 6b. Implement with LINQ JOIN (in main Application)

```csharp
// src/MVCAllOptions.Application/Reports/OrderReportAppService.cs
public class OrderReportAppService : MVCAllOptionsAppService, IOrderReportAppService
{
    private readonly IRepository<Order, Guid> _orderRepo;
    private readonly IRepository<Book, Guid> _bookRepo;

    public OrderReportAppService(
        IRepository<Order, Guid> orderRepo,
        IRepository<Book, Guid> bookRepo)
    {
        _orderRepo = orderRepo;
        _bookRepo = bookRepo;
    }

    public async Task<List<OrderReportDto>> GetLatestOrdersAsync()
    {
        var orders  = await _orderRepo.GetQueryableAsync();
        var books   = await _bookRepo.GetQueryableAsync();

        return (from o in orders
                join b in books on o.BookId equals b.Id
                orderby o.CreationTime descending
                select new OrderReportDto
                {
                    OrderId      = o.Id,
                    CustomerName = o.CustomerName,
                    State        = o.State,
                    BookId       = b.Id,
                    BookName     = b.Name
                })
               .Take(10)
               .ToList();
    }
}
```

> **Warning**: Cross-module JOINs couple modules at the database level. Acceptable for reporting; avoid for business logic. Cannot work across different DBMS.

---

## Integration Folder Pattern (Reference)

The `Integration/` folder is a convention for grouping cross-module contracts:

```
*.Application.Contracts/
  Integration/          ← interfaces with [IntegrationService]
    IFooIntegrationService.cs

*.Application/
  Integration/          ← implementations
    FooIntegrationService.cs

*.Application.Contracts/
  Events/               ← ETO classes for distributed events
    FooCreatedEto.cs

*.Application/
  EventHandlers/        ← IDistributedEventHandler<T> implementations
    FooEventHandler.cs
```

---

## Common Mistakes & Fixes

| Mistake | Fix |
|---------|-----|
| `ConventionalControllers` in module's own `HttpApiModule` | Move to host `MVCAllOptionsWebModule` |
| Entity ID set inside constructor `new Guid()` | Pass ID from `GuidGenerator.Create()` externally; use `base(id)` |
| `OrderDto` extends `EntityDto<Guid>` — missing `CreationTime` | Use `CreationAuditedEntityDto<Guid>` |
| Mapperly error: unmapped required target property `BookName` | Add `[MapperIgnoreTarget(nameof(OrderDto.BookName))]` |
| `DateTime.Now` in entity | Use `IClock` / `Clock.Now` |
| `DbContext` injected in app service | Use `IRepository<T, TKey>` |
| Calling another app service from same module | Use domain service or repository directly |
| Integration service exposed as HTTP API | Add `[IntegrationService]` attribute to the interface |
| `scrollX: true` causes double "Show" dropdown in DataTables | Remove `scrollX: true` from DataTable config |

---

## Quick Reference — File Locations

| What | Where |
|------|-------|
| Entity | `*.Domain/[Entity]/` |
| Enum / ETO | `*.Domain.Shared/` |
| Module DbContext interface | `*.EntityFrameworkCore/Data/` |
| Module DbContext impl | `*.EntityFrameworkCore/Data/` |
| Table mapping extensions | `*.EntityFrameworkCore/Data/` |
| Host DbContext | `src/MVCAllOptions.EntityFrameworkCore/` |
| Permissions | `*.Application.Contracts/Permissions/` |
| DTOs | `*.Application.Contracts/[Entity]/` |
| Service interface | `*.Application.Contracts/[Entity]/` |
| Integration contract | `*.Application.Contracts/Integration/` |
| Event ETO | `*.Application.Contracts/Events/` |
| App service impl | `*.Application/[Entity]/` |
| Mapper | `*.Application/[Module]ApplicationMappers.cs` |
| Integration impl | `*.Application/Integration/` |
| Event handler | `*.Application/EventHandlers/` |
| Razor Page model | `*.Web/Pages/[Entity]/Index.cshtml.cs` |
| Razor Page view | `*.Web/Pages/[Entity]/Index.cshtml` |
| Page JavaScript | `*.Web/wwwroot/Pages/[Entity]/index.js` |
| Menu contributor | `*.Web/Menus/[Module]MenuContributor.cs` |

---

## References

- ABP Modular CRM tutorial: https://abp.io/docs/latest/tutorials/modular-crm
- Integration Services docs: https://abp.io/docs/latest/framework/api-development/integration-services
- Distributed Event Bus: https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed
- Auto API Controllers: https://abp.io/docs/latest/framework/api-development/auto-controllers
- DDD Entities: https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities
