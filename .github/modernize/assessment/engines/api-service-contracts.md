# API & Service Communication Contracts

PhotoAlbum exposes a small set of Razor Pages-based HTTP endpoints (5 handlers across 3 pages) using synchronous server-side request/response patterns with no inter-service communication.

## Service Catalog

| Service | Port | Category | Purpose |
|---------|------|----------|---------|
| PhotoAlbum Web | 5000 (HTTP) / 5001 (HTTPS) | Business | Serves the photo gallery UI, handles photo upload/retrieval/deletion, and serves binary image files |

## API Endpoints Inventory

| Service | Method | Path | Request Type | Response Type |
|---------|--------|------|-------------|--------------|
| PhotoAlbum Web | GET | `/` | — | HTML page (gallery grid with all photos) |
| PhotoAlbum Web | POST | `/?handler=Upload` | Multipart form-data (`List<IFormFile> files`) | JSON (`{ success, uploadedPhotos[], failedUploads[] }`) |
| PhotoAlbum Web | GET | `/Detail?id={id}` | Path query param `id: int` | HTML page (full-size photo + metadata + navigation) |
| PhotoAlbum Web | POST | `/Detail?handler=Delete&id={id}` | Form post with `id: int` | Redirect to `/` (302) |
| PhotoAlbum Web | GET | `/PhotoFile?id={id}` | Path query param `id: int` | Binary image file (Content-Type from stored MimeType) |

## Management & Observability Endpoints

| Service | Endpoint | Custom Metrics |
|---------|----------|---------------|
| PhotoAlbum Web | None configured | None |

No health check endpoints (`/health`, `/healthz`), Swagger UI, or metrics endpoints are configured. Observability relies solely on the built-in ASP.NET Core console logging via `ILogger<T>`.

## DTOs & Contracts

**Service-level models (single service, no gateway aggregation):**

- **`Photo`** (entity/response model) — Represents a stored photo returned to Razor Pages for display. Contains identity, file metadata, and image dimensions. Not exposed directly as a JSON API type; rendered server-side into HTML. Full field details are in `data-architecture.md`.
- **`UploadResult`** (internal result DTO) — Carries the outcome of a single file upload operation (success flag, created photo ID, error message) from `PhotoService` back to the `IndexModel` page handler. Not a C# record; mutable class. Not directly serialized to the client — the page handler projects it into an anonymous JSON object.

No OpenAPI/Swagger specification, protobuf schemas, or GraphQL schemas are present. The single JSON response (upload handler) uses `System.Text.Json` via ASP.NET Core's default `JsonResult` serializer with no custom configuration.

## Communication Patterns

**Synchronous (only):** All communication is in-process and synchronous (async/await over I/O). There is no inter-service HTTP communication, message broker, or event bus.

Request flow: Browser → ASP.NET Core Razor Pages pipeline → `IPhotoService` (scoped DI) → `PhotoAlbumContext` (EF Core) → SQL Server LocalDB + local file system.

**Resilience:** No circuit breaker, retry policy, or timeout configuration is applied beyond the default ASP.NET Core request timeout. On database save failure after a file write, the service performs a compensating delete of the written file (manual rollback pattern).

**Service discovery:** Not applicable — single-process application with no remote service calls.

**Security posture:** No authentication or authorization is configured. All endpoints are publicly accessible with no login requirement, no JWT/OAuth2, no API keys, and no RBAC. HTTPS redirection is enabled in production via `app.UseHttpsRedirection()`, and HSTS headers are set in non-development environments. There is no CSRF protection explicitly configured beyond the default Razor Pages antiforgery tokens included on form POST handlers.

## Service Technology Matrix

| Service | Web Framework | Data Access | Discovery | Gateway | Health Checks | Cache | Metrics |
|---------|--------------|-------------|-----------|---------|--------------|-------|---------|
| PhotoAlbum Web | ASP.NET Core Razor Pages 9.0 | EF Core 9.0 (SQL Server) | None | None | None | None | None |

## Service Communication Sequence

```mermaid
sequenceDiagram
    participant Browser as "Web Browser"
    participant Pages as "Razor Pages\n(IndexModel / DetailModel)"
    participant Svc as "PhotoService"
    participant EF as "PhotoAlbumContext\n(EF Core)"
    participant DB as "SQL Server\n(LocalDB)"
    participant FS as "File System\n(wwwroot/uploads)"
    participant Img as "ImageSharp"

    Note over Browser,FS: Photo Upload Flow
    Browser->>Pages: POST /?handler=Upload (multipart files)
    Pages->>Svc: UploadPhotoAsync(IFormFile)
    Svc->>Svc: Validate MIME type and file size
    Svc->>Img: Image.LoadAsync(stream) - extract dimensions
    Img-->>Svc: width, height
    Svc->>FS: FileStream write (GUID filename)
    alt File write succeeds
        FS-->>Svc: OK
        Svc->>EF: Photos.AddAsync(photo) + SaveChangesAsync()
        alt DB save succeeds
            EF->>DB: INSERT INTO Photos
            DB-->>EF: photo.Id assigned
            EF-->>Svc: OK
            Svc-->>Pages: UploadResult { Success=true, PhotoId }
            Pages-->>Browser: 200 JSON { success, uploadedPhotos[] }
        else DB save fails
            EF-->>Svc: Exception
            Svc->>FS: File.Delete(GUID file) - compensating rollback
            Svc-->>Pages: UploadResult { Success=false, ErrorMessage }
            Pages-->>Browser: 200 JSON { success=false, failedUploads[] }
        end
    else File write fails
        FS-->>Svc: Exception
        Svc-->>Pages: UploadResult { Success=false, ErrorMessage }
        Pages-->>Browser: 200 JSON { success=false, failedUploads[] }
    end

    Note over Browser,DB: Photo Gallery / Detail Flow
    Browser->>Pages: GET / or GET /Detail?id=N
    Pages->>Svc: GetAllPhotosAsync() or GetPhotoByIdAsync(id)
    Svc->>EF: Photos.OrderByDescending(UploadedAt)
    EF->>DB: SELECT FROM Photos
    DB-->>EF: Photo rows
    EF-->>Svc: List[Photo]
    Svc-->>Pages: photos
    Pages-->>Browser: 200 HTML (rendered Razor view)

    Note over Browser,FS: File Serving Flow
    Browser->>Pages: GET /PhotoFile?id=N
    Pages->>Svc: GetPhotoByIdAsync(id)
    Svc->>EF: Photos.FindAsync(id)
    EF->>DB: SELECT WHERE Id=N
    DB-->>EF: Photo row
    EF-->>Svc: Photo
    Svc-->>Pages: Photo metadata
    Pages->>FS: File.ReadAllBytesAsync(storedFileName)
    FS-->>Pages: byte[]
    Pages-->>Browser: 200 image/jpeg (or png/gif/webp) with cache headers
```
