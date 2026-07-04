# .NET Upgrade Plan: net9.0 → net10.0

## Overview

Upgrade the PhotoAlbum solution from **.NET 9.0** to **.NET 10.0 LTS**.

.NET 9 is a Standard-Term Support (STS) release with a shorter support lifecycle. .NET 10 is the next Long-Term Support (LTS) release, providing 3 years of mainstream support. The user has explicitly requested this upgrade.

## Source Version

- **Current framework**: `net9.0`

## Target Version

- **Target framework**: `net10.0`

## Projects in Solution

| Project | Path | Type |
|---------|------|------|
| PhotoAlbum | `PhotoAlbum/PhotoAlbum.csproj` | ASP.NET Core 9.0 Razor Pages Web Application |
| PhotoAlbum.Tests | `PhotoAlbum.Tests/PhotoAlbum.Tests.csproj` | xUnit Test Project |

## Upgrade Scope

1. **Target Framework Moniker (TFM)**: Update `<TargetFramework>` from `net9.0` to `net10.0` in both `.csproj` files.
2. **NuGet Package Updates**: Bump all `9.0.x` versioned packages (Entity Framework Core, ASP.NET Core MVC Testing, etc.) to their `10.0.x` equivalents.
3. **API Compatibility**: Review and fix any breaking API changes between .NET 9 and .NET 10.
4. **Build Validation**: Ensure the solution compiles successfully after the upgrade.
5. **Test Validation**: Ensure all existing xUnit tests pass after the upgrade.

## Tasks

- `001-upgrade-dotnet-to-net10` — Upgrade PhotoAlbum solution from .NET 9.0 to .NET 10.0
