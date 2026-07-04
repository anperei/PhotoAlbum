using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using PhotoAlbum.Data;
using PhotoAlbum.Models;
using SixLabors.ImageSharp;

namespace PhotoAlbum.Services;

/// <summary>
/// Service for photo operations including upload, retrieval, and deletion.
/// Photo binaries are stored in Azure Blob Storage; metadata is persisted in the database.
/// </summary>
public class PhotoService : IPhotoService
{
    private readonly PhotoAlbumContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PhotoService> _logger;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly long _maxFileSizeBytes;
    private readonly string[] _allowedMimeTypes;

    public PhotoService(
        PhotoAlbumContext context,
        IConfiguration configuration,
        ILogger<PhotoService> logger,
        BlobContainerClient blobContainerClient)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _blobContainerClient = blobContainerClient;
        _maxFileSizeBytes = _configuration.GetValue<long>("FileUpload:MaxFileSizeBytes", 10485760);
        _allowedMimeTypes = _configuration.GetSection("FileUpload:AllowedMimeTypes").Get<string[]>()
            ?? new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
    }

    /// <summary>
    /// Get all photos ordered by upload date (newest first)
    /// </summary>
    public async Task<List<Photo>> GetAllPhotosAsync()
    {
        try
        {
            return await _context.Photos
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving photos from database");
            throw;
        }
    }

    /// <summary>
    /// Get a specific photo by ID
    /// </summary>
    public async Task<Photo?> GetPhotoByIdAsync(int id)
    {
        try
        {
            return await _context.Photos.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving photo with ID {PhotoId}", id);
            throw;
        }
    }

    /// <summary>
    /// Upload a photo file to Azure Blob Storage and persist its metadata to the database.
    /// </summary>
    public async Task<UploadResult> UploadPhotoAsync(IFormFile file)
    {
        var result = new UploadResult
        {
            FileName = file.FileName
        };

        try
        {
            // Validate file type
            if (!_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                result.Success = false;
                result.ErrorMessage = $"File type not supported. Please upload JPEG, PNG, GIF, or WebP images.";
                _logger.LogWarning("Upload rejected: Invalid file type {ContentType} for {FileName}",
                    file.ContentType, file.FileName);
                return result;
            }

            // Validate file size
            if (file.Length > _maxFileSizeBytes)
            {
                result.Success = false;
                result.ErrorMessage = $"File size exceeds {_maxFileSizeBytes / 1024 / 1024}MB limit.";
                _logger.LogWarning("Upload rejected: File size {FileSize} exceeds limit for {FileName}",
                    file.Length, file.FileName);
                return result;
            }

            // Validate file length
            if (file.Length <= 0)
            {
                result.Success = false;
                result.ErrorMessage = "File is empty.";
                return result;
            }

            // Generate unique blob name
            var extension = Path.GetExtension(file.FileName);
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var relativePath = $"/uploads/{storedFileName}";

            // Buffer the file content into a byte array so the stream can be consumed
            // twice: once for image dimension extraction and once for the blob upload.
            // (Rule 11: resetting or buffering is required after any stream read.)
            byte[] fileBytes;
            using (var bufferStream = new MemoryStream())
            {
                await file.CopyToAsync(bufferStream);
                fileBytes = bufferStream.ToArray();
            }

            // Extract image dimensions using ImageSharp
            int? width = null;
            int? height = null;
            try
            {
                using var imageStream = new MemoryStream(fileBytes);
                using var image = await Image.LoadAsync(imageStream);
                width = image.Width;
                height = image.Height;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract image dimensions for {FileName}", file.FileName);
                // Continue without dimensions — not critical
            }

            // Upload to Azure Blob Storage
            var blobClient = _blobContainerClient.GetBlobClient(storedFileName);
            try
            {
                using var uploadStream = new MemoryStream(fileBytes);
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
                    // Conditions intentionally omitted → unconditional overwrite
                };
                // MIGRATION NOTE: unconditional overwrite to preserve original FileMode.Create semantics
                // (local disk write always replaced the file if it existed).
                await blobClient.UploadAsync(uploadStream, uploadOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName} to Azure Blob Storage", file.FileName);
                result.Success = false;
                result.ErrorMessage = "Error saving file. Please try again.";
                return result;
            }

            // Create photo entity
            var photo = new Photo
            {
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                FilePath = relativePath,
                FileSize = file.Length,
                MimeType = file.ContentType,
                UploadedAt = DateTime.UtcNow,
                Width = width,
                Height = height
            };

            // Save to database
            try
            {
                await _context.Photos.AddAsync(photo);
                await _context.SaveChangesAsync();

                result.Success = true;
                result.PhotoId = photo.Id;

                _logger.LogInformation("Successfully uploaded photo {FileName} with ID {PhotoId}",
                    file.FileName, photo.Id);
            }
            catch (Exception ex)
            {
                // Rollback: delete blob if database save fails
                try
                {
                    await blobClient.DeleteIfExistsAsync();
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Error deleting blob {BlobName} during rollback", storedFileName);
                }

                _logger.LogError(ex, "Error saving photo metadata to database for {FileName}", file.FileName);
                result.Success = false;
                result.ErrorMessage = "Error saving photo information. Please try again.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during photo upload for {FileName}", file.FileName);
            result.Success = false;
            result.ErrorMessage = "An unexpected error occurred. Please try again.";
        }

        return result;
    }

    /// <summary>
    /// Delete a photo by ID — removes the blob from Azure Blob Storage and the metadata from the database.
    /// </summary>
    public async Task<bool> DeletePhotoAsync(int id)
    {
        try
        {
            var photo = await _context.Photos.FindAsync(id);
            if (photo == null)
            {
                _logger.LogWarning("Photo with ID {PhotoId} not found for deletion", id);
                return false;
            }

            // Delete blob from Azure Blob Storage
            var blobClient = _blobContainerClient.GetBlobClient(photo.StoredFileName);
            try
            {
                await blobClient.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob {BlobName} for photo ID {PhotoId}",
                    photo.StoredFileName, id);
                // Continue with database deletion even if blob deletion fails
            }

            // Delete from database
            _context.Photos.Remove(photo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted photo ID {PhotoId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting photo with ID {PhotoId}", id);
            throw;
        }
    }
}
