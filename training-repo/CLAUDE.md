# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

OrderHub is a training ASP.NET Core 8 MVC application for order management (customers, products, orders). It follows a three-project clean-architecture layout on .NET 8, EF Core 8 (SQL Server), and xUnit.

## Commands

Run from the repo root (`OrderHub.sln`):

```powershell
dotnet build                                  # build all projects
dotnet run --project src/OrderHub.Web         # run the web app (http://localhost:5150, https://localhost:7147)
dotnet test                                   # run the full xUnit suite
dotnet test --filter "FullyQualifiedName~OrderServicePricingTests"   # run one test class
dotnet test --filter "Name=CalculateTotal_WithoutCustomer_UsesStandardRate"  # run one test
```

EF Core migrations (design-time package lives in `OrderHub.Web`, DbContext in `OrderHub.Infrastructure`):

```powershell
dotnet ef migrations add <Name> --project src/OrderHub.Infrastructure --startup-project src/OrderHub.Web
dotnet ef database update --project src/OrderHub.Infrastructure --startup-project src/OrderHub.Web
```

Note: you rarely need to run migrations/DB setup manually — on startup `Program.cs` calls `db.Database.Migrate()` and `DbSeeder.SeedAsync(db)`, so launching the app creates the schema and seeds sample data (20 customers, 50 products, 200 orders) into an empty database.

## Architecture

Dependency flow is strictly one-directional: **Web → Infrastructure → Core**, and Web → Core. Core references nothing else.

- **OrderHub.Core** — domain and business logic, no framework dependencies.
  - `Domain/` — POCO entities (`Order`, `OrderItem`, `Product`, `Customer`) and enums (`OrderStatus`, `CustomerTier`).
  - `Interfaces/` — repository contracts (`IOrderRepository`, etc.), implemented in Infrastructure.
  - `Services/` — business logic (`OrderService`, `ProductService`, `CustomerService`) and their interfaces. Services depend on repository interfaces, never on EF Core.
  - `Common/` — `ServiceResult<T>` and `PagedResult<T>`.
- **OrderHub.Infrastructure** — EF Core persistence.
  - `Data/OrderHubDbContext.cs` — DbSets + `OnModelCreating` (constraints, indexes, delete behaviors).
  - `Data/DbSeeder.cs` — idempotent seeding with a fixed `Random` seed for reproducible data.
  - `Repositories/` — `IxxxRepository` implementations; handle `Include`/query composition. `SaveChangesAsync` is exposed on repositories and called by services (unit-of-work-ish, no separate UoW type).
  - `Migrations/` — generated EF migrations.
- **OrderHub.Web** — ASP.NET Core MVC (controllers + Razor views), the composition root.
  - `Program.cs` wires all DI (`AddScoped` for every repository and service) and the `Default` connection string.
  - `Controllers/` map service results to `ViewModels/` and Razor `Views/`. Controllers hold no business logic.
  - `Helpers/DisplayHelper.cs` — presentation formatting (status/tier labels, badge classes, money, local time).

### Key conventions

- **`ServiceResult<T>`** is the return type for mutating service operations. It carries `Success`, `Value`, and an `Errors` list (joined via `ErrorMessage`). Controllers surface failures through `ModelState.AddModelError` (form redisplay) or `TempData["Error"]`/`TempData["Success"]` (redirects). Do not throw for expected validation failures — return `ServiceResult<T>.Fail(...)`.
- **`PagedResult<T>`** wraps list queries with paging metadata; list endpoints use a fixed page size (`OrdersController.PageSize = 20`).
- **Price snapshotting** — `OrderItem.UnitPriceSnapshot` freezes the unit price at order-creation time so later product price changes don't alter historical orders.
- **Tier discounts** live in `OrderService.GetDiscountRate` (Gold 10%, Silver 5%, Standard 0%) and drive `CalculateSubtotal`/`CalculateTotal`.
- **UI-facing strings are Traditional Chinese (zh-TW)** — error messages, labels, `TempData` messages, and seed data. Match this when adding user-visible text.

### Testing

- xUnit; tests live in `tests/OrderHub.Tests`.
- `TestSetup.cs` builds an **EF Core InMemory** `OrderHubDbContext` (unique DB name per test) plus factory helpers (`CreateOrderService`, `AddCustomer`, `AddProduct`) — tests do **not** require a real SQL Server. Follow this pattern for new service tests.

## Code style

Enforced via `.editorconfig`: file-scoped namespaces, `var` when the type is apparent, `System` usings sorted first, 4-space indent for `.cs`/`.cshtml`, 2-space for `.json`/`.js`/`.css`. Nullable reference types and implicit usings are enabled across all projects.

## Configuration

The `Default` connection string (in `appsettings.json` / `appsettings.Development.json`) points at a shared training SQL Server (`JPOSDEV158\SQL2022`, database `OrderHubTraining`) using Windows integrated auth. Change it to target a local instance if needed.
