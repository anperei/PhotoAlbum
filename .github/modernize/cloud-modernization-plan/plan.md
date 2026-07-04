# Modernization Plan: Cloud Modernization Plan

**Project**: PhotoAlbum

---

## Technical Framework

- **Language**: C# / .NET 10 (`net10.0`)
- **Framework**: ASP.NET Core Razor Pages
- **Build Tool**: dotnet CLI
- **Database**: SQL Server (Entity Framework Core)
- **Key Dependencies**: EF Core SQL Server, ImageSharp

---

## Overview

> This migration modernizes PhotoAlbum for Azure by replacing local photo file storage with Azure Blob Storage and preparing the application for secure cloud-aligned dependency hygiene.
>
> The new architecture will:
>
> - Move photo binary storage from local disk paths to Azure Blob Storage
> - Use Azure-native authentication for storage access to reduce secret-based access patterns
> - Include dependency vulnerability remediation before rollout
>
> The migration follows a phased approach: implement storage modernization first, then complete security remediation and validation.

---

## Migration Impact Summary

| Application | Original Service | New Azure Service | Authentication | Comments |
|-------------|------------------|-------------------|----------------|----------|
| PhotoAlbum  | Local file system | Azure Blob Storage | Managed Identity | Migrate photo upload/read/delete storage flow |
| PhotoAlbum  | Existing dependencies | Patched dependencies | N/A | Remediate dependency CVEs before release |
