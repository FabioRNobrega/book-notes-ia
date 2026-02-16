# Scaffolding / Generate Guide (.NET 9 MVC + EF Core + Identity) — Docker-first

This project runs **inside Docker**. All `dotnet` commands below are executed **inside the `webapp` container**.

---

## Enter the container

```bash
docker compose exec webapp bash
```

> Assumes your app code is mounted at `/app` inside the container.

---

## Tooling: dotnet-ef + aspnet-codegenerator

### 1) Create/keep a local tool manifest (recommended)
Run once in `/app`:

```bash
cd /app
dotnet new tool-manifest
```

### 2) Install tools (pin major to 9.*)
```bash
dotnet tool install dotnet-ef --version 9.*
dotnet tool install dotnet-aspnet-codegenerator --version 9.*
dotnet tool restore
```

Verify:

```bash
dotnet ef --version
dotnet aspnet-codegenerator --help
```

### 3) Update tools later
```bash
dotnet tool update dotnet-ef --version 9.*
dotnet tool update dotnet-aspnet-codegenerator --version 9.*
dotnet tool restore
```

---

## EF Core migrations (Postgres)

### Add a migration
```bash
dotnet ef migrations add <MigrationName>
```

Example:

```bash
dotnet ef migrations add AddUserProfile
```

### Apply migrations immediately (optional)
```bash
dotnet ef database update
```

> If your app runs `db.Database.Migrate()` on startup, you can skip `database update` and just restart the app container.

### Remove the last migration (if not applied)
```bash
dotnet ef migrations remove
```

---

## Scaffold MVC Controller + Views (CRUD)

This uses **Microsoft.VisualStudio.Web CodeGeneration.Design** (package) + **dotnet-aspnet-codegenerator** (tool).

### Scaffold a CRUD controller + Razor views
For Postgres, **always** pass the database provider:

```bash
dotnet aspnet-codegenerator controller   -name <ControllerName>   -m <Full.Model.Type>   -dc <DbContextType>   -outDir Controllers   --useDefaultLayout   --referenceScriptLibraries   --databaseProvider postgres
```

Example:

```bash
dotnet aspnet-codegenerator controller   -name UserProfileController   -m WebApp.Models.UserProfile   -dc AppDbContext   -outDir Controllers   --useDefaultLayout   --referenceScriptLibraries   --databaseProvider postgres
```

What you get:
- `Controllers/<ControllerName>.cs`
- `Views/<ControllerNameWithoutController>/*` (Index/Create/Edit/Details/Delete)

### Common flags
- `--useAsyncActions` or `-async`: generate async controller actions
- `--noViews` or `-nv`: generate controller only (no Razor views)
- `--force` or `-f`: overwrite existing files
- `--relativeFolderPath` or `-outDir`: output folder (relative to project)

To see the exact options supported by your installed tool:
```bash
dotnet aspnet-codegenerator controller --help
```

---

## Scaffold Views only

If you want only a view (or to regenerate a specific view):

```bash
dotnet aspnet-codegenerator view <ViewName> <TemplateName>   -m <Full.Model.Type>   -dc <DbContextType>   -outDir Views/<FolderName>   --useDefaultLayout
```

Example (Create view):
```bash
dotnet aspnet-codegenerator view Create Create   -m WebApp.Models.UserProfile   -dc AppDbContext   -outDir Views/UserProfile   --useDefaultLayout
```

List available templates:
```bash
dotnet aspnet-codegenerator view --help
```

---

## Identity scaffolding (optional)

If you want to customize Identity UI pages (Login/Register/etc.):

```bash
dotnet aspnet-codegenerator identity --help
```

> The exact flags and file names vary by generator version—use `--help` output as the source of truth.

---

## Running commands without opening a shell (one-liners)

Example: add migration from host:
```bash
docker compose exec webapp bash -lc "dotnet tool restore && dotnet ef migrations add AddUserProfile"
```

Example: scaffold controller from host:
```bash
docker compose exec webapp bash -lc "dotnet tool restore && dotnet aspnet-codegenerator controller -name UserProfileController -m WebApp.Models.UserProfile -dc AppDbContext -outDir Controllers --useDefaultLayout --referenceScriptLibraries --databaseProvider postgres"
```

---

## Troubleshooting

### “To scaffold, install Microsoft.EntityFrameworkCore.SqlServer”
You forgot to specify Postgres as the generator provider:

✅ Fix:
```bash
--databaseProvider postgres
```

### “Run 'dotnet tool restore' to make the command available”
You are using local tools (tool manifest). Run:

```bash
dotnet tool restore
```

### Version warnings (tools older than runtime)
Update tools:

```bash
dotnet tool update dotnet-ef --version 9.*
dotnet tool restore
```

---

## Recommended workflow for new features

1. Create model + update `AppDbContext` (DbSet + mapping).
2. `dotnet ef migrations add ...`
3. Restart app (or `dotnet ef database update`)
4. Scaffold controller/views
5. Immediately harden generated CRUD to your security rules (e.g., per-user access).
