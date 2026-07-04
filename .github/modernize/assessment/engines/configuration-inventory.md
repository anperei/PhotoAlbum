# Configuration & Externalized Settings Inventory

PhotoAlbum uses three configuration sources (two `appsettings` JSON files and `launchSettings.json`) with no external config server, secret store, or feature flag framework.

## Configuration Sources

| Source | Type | Path / Location | Notes |
|--------|------|----------------|-------|
| `appsettings.json` | JSON config file | `PhotoAlbum/appsettings.json` | Base configuration loaded in all environments |
| `appsettings.Development.json` | JSON config file | `PhotoAlbum/appsettings.Development.json` | Development overrides; merged on top of base when `ASPNETCORE_ENVIRONMENT=Development` |
| `launchSettings.json` | Developer launch config | `PhotoAlbum/Properties/launchSettings.json` | Local dev only; sets `ASPNETCORE_ENVIRONMENT` and launch URLs; not deployed |
| `.env.example` | Environment variable template | `.env.example` | Documents Azure deployment variables (`RESOURCE_GROUP`); not loaded by the application at runtime |
| User Secrets | ASP.NET Core User Secrets | Machine-local (identified by `UserSecretsId: 28fdd5b1-4b72-4763-98cc-ac5ebb3f280d`) | Developer-only override store; not committed to source control |

No Spring Cloud Config, Azure App Configuration, HashiCorp Vault, Kubernetes ConfigMaps, or any remote configuration server is in use.

## Build Profiles

| Profile | Activation | Purpose | Key Changes |
|---------|-----------|---------|------------|
| Debug | Default in Visual Studio / `dotnet build` | Local development and testing | No optimisations; debug symbols included |
| Release | Manual via `dotnet build -c Release` or `dotnet publish` | Container image build and deployment | Full optimisations; `UseAppHost=false` in Dockerfile publish step |

No MSBuild property-level conditional compilation symbols or additional build profiles are defined in the `.csproj` file.

## Runtime Profiles

| Profile | Activation Method | Config Files Loaded | Key Overrides |
|---------|-----------------|-------------------|--------------|
| Development | `ASPNETCORE_ENVIRONMENT=Development` (set in `launchSettings.json`) | `appsettings.json` + `appsettings.Development.json` | Log level `Default=Debug`, EF Core logging enabled (`Microsoft.EntityFrameworkCore=Information`), detailed error pages |
| Production | `ASPNETCORE_ENVIRONMENT` not set (or set to `Production`) | `appsettings.json` only | Standard logging (`Default=Information`), HSTS enabled, error handler page `/Error` |
| Test | `IsTestEnvironment=true` in test host configuration | `appsettings.json` (in-memory DB replaces SQL Server) | Skips EF Core `MigrateAsync()` on startup |

## Properties Inventory

### PhotoAlbum Web

| Property Key | Default Value | Profile Override | Source |
|-------------|--------------|-----------------|--------|
| `ConnectionStrings:DefaultConnection` | `Server=(localdb)\mssqllocaldb;Database=PhotoAlbumDb;Trusted_Connection=true;MultipleActiveResultSets=true` | None | `appsettings.json` |
| `FileUpload:MaxFileSizeBytes` | `10485760` (10 MB) | None | `appsettings.json` |
| `FileUpload:AllowedMimeTypes[0]` | `image/jpeg` | None | `appsettings.json` |
| `FileUpload:AllowedMimeTypes[1]` | `image/png` | None | `appsettings.json` |
| `FileUpload:AllowedMimeTypes[2]` | `image/gif` | None | `appsettings.json` |
| `FileUpload:AllowedMimeTypes[3]` | `image/webp` | None | `appsettings.json` |
| `FileUpload:MaxFilesPerUpload` | `10` | None | `appsettings.json` (read by config but not enforced in `PhotoService`) |
| `FileUpload:UploadPath` | `wwwroot/uploads` | None | `appsettings.json` |
| `Logging:LogLevel:Default` | `Information` | `Debug` (Development) | `appsettings.json` / `appsettings.Development.json` |
| `Logging:LogLevel:Microsoft.AspNetCore` | `Warning` | `Information` (Development) | `appsettings.json` / `appsettings.Development.json` |
| `Logging:LogLevel:Microsoft.EntityFrameworkCore` | _(not set)_ | `Information` (Development) | `appsettings.Development.json` |
| `DetailedErrors` | _(not set)_ | `true` (Development) | `appsettings.Development.json` |
| `AllowedHosts` | `*` | None | `appsettings.json` |
| `IsTestEnvironment` | `false` (implicit) | `true` (Test — set programmatically in test host) | Test host configuration |

### Form Options (set programmatically in `Program.cs`)

| Setting | Value | Notes |
|---------|-------|-------|
| `FormOptions.MultipartBodyLengthLimit` | `10485760` (10 MB) | Hard-coded; mirrors `FileUpload:MaxFileSizeBytes` |
| `FormOptions.ValueLengthLimit` | `10485760` (10 MB) | Hard-coded |
| `FormOptions.MultipartBoundaryLengthLimit` | `128` | Hard-coded |

## Startup Parameters & Resource Requirements

| Service | Runtime Options | Memory | Instance Count |
|---------|----------------|--------|---------------|
| PhotoAlbum Web (local) | None specified — `dotnet run` defaults | No limit configured | 1 |
| PhotoAlbum Web (Docker) | `ENTRYPOINT ["dotnet", "PhotoAlbum.dll"]` — no JVM, no `-Xms`/`-Xmx` | No `mem_limit` in Dockerfile | 1 |

No Kubernetes resource requests/limits, Docker Compose `mem_limit`, or cloud deployment scaling configuration is present in the repository.

## Startup Dependency Chain

1. **`Program.cs` starts** → creates uploads directory (`wwwroot/uploads`) if absent.
2. **EF Core `MigrateAsync()`** is called (unless `IsTestEnvironment=true`) — the application will fail fast and rethrow if migration fails, preventing the web server from accepting requests.
3. **ASP.NET Core pipeline** becomes ready and begins serving requests.

The application has a hard dependency on SQL Server being reachable at startup (due to the eager migration call). There are no `dockerize` wait scripts, Kubernetes readiness probes, or health-check endpoints configured to signal readiness to an orchestrator.

## Secrets & Sensitive Configuration

| Secret Reference | Type | Storage |
|----------------|------|---------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | `appsettings.json` (trusted Windows auth — no password embedded for LocalDB dev use) |
| User Secrets (`UserSecretsId: 28fdd5b1-4b72-4763-98cc-ac5ebb3f280d`) | Developer override store | Machine-local `~/.microsoft/usersecrets/` — not in source control |
| `RESOURCE_GROUP` | Azure resource group name | `.env` file (not committed; `.env.example` is the template) |

The development connection string uses Windows Integrated Authentication (`Trusted_Connection=true`) to LocalDB and contains no password. No encryption (DPAPI, Jasypt, Azure KeyVault references) is applied to any configuration value.

### Secrets Provisioning Workflow

No automated secrets provisioning workflow is configured in the repository. Development secrets are managed via ASP.NET Core User Secrets (machine-local, not committed). Azure deployment targets use environment variables (e.g., `RESOURCE_GROUP`) sourced from a local `.env` file that must be created manually by the developer from `.env.example`. There is no integration with Azure KeyVault, GitHub Actions secrets injection, or any managed identity binding in the current codebase.

## Feature Flags

No feature flag framework (e.g., .NET `Microsoft.FeatureManagement`, LaunchDarkly, Unleash) is configured. No `@ConditionalOnProperty`-style or `IFeatureManager`-style conditional activation is present.

The `IsTestEnvironment` boolean in `appsettings.json`/test host configuration acts as a single informal toggle to skip EF Core migration at startup, but is not a feature flag in the conventional sense.

| Flag Name | Default | Controlled By |
|-----------|---------|--------------|
| `IsTestEnvironment` | `false` | Test host configuration (`WebApplicationFactory`) |

## Framework & Runtime Versions

| Component | Version | Source |
|-----------|---------|--------|
| .NET Runtime | 9.0 | `<TargetFramework>net9.0</TargetFramework>` in `.csproj` |
| ASP.NET Core | 9.0 | Included via `Microsoft.NET.Sdk.Web` SDK |
| Entity Framework Core | 9.0.9 | `PhotoAlbum.csproj` package reference |
| EF Core SQL Server | 9.0.9 | `PhotoAlbum.csproj` package reference |
| EF Core Design | 9.0.9 | `PhotoAlbum.csproj` package reference (build-only) |
| SixLabors.ImageSharp | 3.1.11 | `PhotoAlbum.csproj` package reference |
| xUnit | 2.9.2 | `PhotoAlbum.Tests.csproj` package reference |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.9 | `PhotoAlbum.Tests.csproj` package reference |
| Microsoft.EntityFrameworkCore.InMemory | 9.0.9 | `PhotoAlbum.Tests.csproj` package reference |
| Docker base image (runtime) | `mcr.microsoft.com/dotnet/aspnet:9.0` | `Dockerfile` |
| Docker base image (build) | `mcr.microsoft.com/dotnet/sdk:9.0` | `Dockerfile` |
| Build tool | .NET CLI (`dotnet build` / `dotnet publish`) | `Dockerfile`, project convention |
