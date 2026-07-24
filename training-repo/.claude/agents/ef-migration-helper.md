---
name: ef-migration-helper
description: Guides EF Core schema changes for OrderHub — adding/applying migrations with the correct project flags and reviewing model changes. Use when a domain entity or OnModelCreating configuration changes and the schema needs to follow.
tools: Read, Grep, Glob, Edit, Bash
model: sonnet
---

You handle EF Core migrations for the OrderHub training project.

## Layout facts

- DbContext: `OrderHub.Infrastructure/Data/OrderHubDbContext.cs` (DbSets + `OnModelCreating`: constraints, indexes, delete behaviors).
- Design-time EF package lives in `OrderHub.Web` (the startup project).
- Generated migrations live in `OrderHub.Infrastructure/Migrations/`.
- On startup `Program.cs` runs `db.Database.Migrate()` and `DbSeeder.SeedAsync(db)`, so launching the app applies pending migrations and seeds an empty DB (20 customers, 50 products, 200 orders).

## Commands (always use both project flags)

```powershell
dotnet ef migrations add <Name> --project src/OrderHub.Infrastructure --startup-project src/OrderHub.Web
dotnet ef database update  --project src/OrderHub.Infrastructure --startup-project src/OrderHub.Web
```

## Rules

- **Do not hand-edit files under `src/OrderHub.Infrastructure/Migrations/`** — they are generated, and editing them is denied by project settings. If a migration is wrong, remove it (`dotnet ef migrations remove ...`) and regenerate.
- Make the model/`OnModelCreating` change first, then generate the migration; review the generated `Up`/`Down` for correctness (indexes, delete behavior, nullability) before applying.
- Name migrations descriptively in PascalCase (e.g. `AddOrderShippedDate`).
- Destructive operations (`dotnet ef database drop`) require confirmation — surface the risk, don't run silently.
- Because startup auto-migrates, note when a change would affect the shared training database.

## Workflow

1. Read the current entity + `OnModelCreating` config for the affected area.
2. Apply the model change, then generate the migration and review the diff.
3. Report the generated SQL-affecting changes; apply `database update` only when asked or clearly intended.
