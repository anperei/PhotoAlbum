# Architecture Diagram

PhotoAlbum is an ASP.NET Core 9.0 Razor Pages web application for photo gallery management, using SQL Server for persistence and local file storage for uploaded images.

## Application Architecture

```mermaid
flowchart TD
    subgraph Client["Client Layer"]
        Browser["Web Browser"]
    end
    subgraph App["Application Layer - ASP.NET Core 9.0"]
        Pages["Razor Pages\n(Index, Detail, PhotoFile)"]
        Service["PhotoService\nBusiness Logic"]
        IService["IPhotoService\nAbstraction"]
        Middleware["Static Files + Routing\nMiddleware Pipeline"]
    end
    subgraph Data["Data Layer"]
        EF["Entity Framework Core 9.0\nSQL Server Provider"]
        DB[("SQL Server LocalDB\nPhotoAlbumDb")]
        FS[("Local File System\nwwwroot/uploads/")]
    end
    subgraph Libraries["Libraries"]
        ImageSharp["SixLabors.ImageSharp 3.1\nImage Processing"]
    end

    Browser -->|"HTTP requests"| Middleware
    Middleware -->|"routes"| Pages
    Pages -->|"calls via interface"| IService
    IService -->|"implemented by"| Service
    Service -->|"CRUD operations"| EF
    EF -->|"SQL queries"| DB
    Service -->|"file read/write"| FS
    Service -->|"dimension extraction"| ImageSharp
```

### Technology Stack Summary

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| Presentation | ASP.NET Core Razor Pages | 9.0 | Server-side web UI rendering |
| Business Logic | PhotoService (custom) | — | Photo upload, retrieval, deletion |
| Data Access | Entity Framework Core | 9.0.9 | ORM for SQL Server |
| Database | SQL Server LocalDB | — | Persistent photo metadata storage |
| File Storage | Local File System | — | Binary image file storage (wwwroot/uploads) |
| Image Processing | SixLabors.ImageSharp | 3.1.11 | Image dimension extraction |

### Data Storage & External Services

The application uses SQL Server LocalDB for storing photo metadata (filename, size, MIME type, dimensions, upload timestamp) and the local file system (`wwwroot/uploads/`) for storing the actual image binary files. There are no external service integrations — all storage is local. File names use GUID-based identifiers to prevent collisions and avoid exposing original file names on disk.

### Key Architectural Decisions

- **Service layer abstraction**: `IPhotoService` interface decouples Razor Pages from storage implementation, enabling a future swap from local file storage to Azure Blob Storage without touching presentation code.
- **Transactional consistency**: On database save failure after file write, the file is deleted to prevent orphaned files on disk.
- **Configuration-driven file handling**: Upload path, max size, and allowed MIME types are all read from `appsettings.json`, enabling environment-specific overrides.

## Component Relationships

```mermaid
flowchart LR
    subgraph Presentation["Presentation Layer"]
        IndexPage["IndexModel\nGallery + Upload"]
        DetailPage["DetailModel\nPhoto Detail + Delete"]
        PhotoFilePage["PhotoFileModel\nFile Serving"]
    end
    subgraph Business["Business Logic"]
        IPhotoSvc["IPhotoService\nInterface"]
        PhotoSvc["PhotoService\nImplementation"]
    end
    subgraph DataAccess["Data Access"]
        DbCtx["PhotoAlbumContext\nDbContext"]
        PhotoEntity["Photo\nEntity Model"]
        UploadResult["UploadResult\nResult DTO"]
    end
    subgraph Infra["Infrastructure"]
        EFCore["EF Core + SQL Server"]
        FileSystem["File System\nwwwroot/uploads"]
        ImageSharp["ImageSharp\nImage Processing"]
        Config["IConfiguration\nappsettings.json"]
    end

    IndexPage -->|"delegates upload/list"| IPhotoSvc
    DetailPage -->|"delegates get/delete"| IPhotoSvc
    PhotoFilePage -->|"delegates get by ID"| IPhotoSvc
    IPhotoSvc -->|"implemented by"| PhotoSvc
    PhotoSvc -->|"queries/persists"| DbCtx
    PhotoSvc -->|"reads/writes"| FileSystem
    PhotoSvc -->|"extracts dimensions"| ImageSharp
    PhotoSvc -->|"reads settings"| Config
    PhotoSvc -->|"returns"| UploadResult
    DbCtx -->|"maps"| PhotoEntity
    DbCtx -->|"uses"| EFCore
```

### Component Inventory

| Component | Layer | Type | Responsibility |
|-----------|-------|------|---------------|
| IndexModel | Presentation | Razor PageModel | Displays photo gallery grid; handles multi-file upload via POST handler |
| DetailModel | Presentation | Razor PageModel | Displays single photo full-size with metadata; handles photo deletion and pagination navigation |
| PhotoFileModel | Presentation | Razor PageModel | Serves binary photo files by ID with caching headers; provides indirect file access |
| IPhotoService | Business Logic | Interface | Defines contract for photo operations (get all, get by ID, upload, delete) |
| PhotoService | Business Logic | Service | Implements photo upload (with validation, dimension extraction, file write, DB save), retrieval, and deletion with transactional rollback |
| PhotoAlbumContext | Data Access | EF Core DbContext | Manages Photos DbSet; configures entity mappings and descending index on UploadedAt |
| Photo | Data Access | Entity Model | Represents a stored photo with metadata (original/stored filename, path, size, MIME type, dimensions, timestamp) |
| UploadResult | Data Access | DTO | Carries upload operation result (success flag, photo ID, error message) back to calling page |
