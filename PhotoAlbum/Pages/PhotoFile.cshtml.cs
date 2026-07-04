using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhotoAlbum.Services;

namespace PhotoAlbum.Pages;

/// <summary>
/// Page model for serving photo files from Azure Blob Storage
/// </summary>
public class PhotoFileModel : PageModel
{
    private readonly IPhotoService _photoService;
    private readonly ILogger<PhotoFileModel> _logger;
    private readonly BlobContainerClient _blobContainerClient;

    /// <summary>
    /// Initializes a new instance of the PhotoFileModel class
    /// </summary>
    /// <param name="photoService">Service for photo metadata operations</param>
    /// <param name="blobContainerClient">Azure Blob Storage container client</param>
    /// <param name="logger">Logger instance</param>
    public PhotoFileModel(
        IPhotoService photoService,
        BlobContainerClient blobContainerClient,
        ILogger<PhotoFileModel> logger)
    {
        _photoService = photoService;
        _blobContainerClient = blobContainerClient;
        _logger = logger;
    }

    /// <summary>
    /// Serves a photo file by ID, downloading the binary from Azure Blob Storage.
    /// </summary>
    /// <param name="id">The ID of the photo to serve</param>
    /// <returns>File result with the photo content, or NotFound if the photo doesn't exist</returns>
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null)
        {
            _logger.LogWarning("Photo file request with null ID");
            return NotFound();
        }

        try
        {
            var photo = await _photoService.GetPhotoByIdAsync(id.Value);

            if (photo == null)
            {
                _logger.LogWarning("Photo with ID {PhotoId} not found", id);
                return NotFound();
            }

            // Download the photo binary from Azure Blob Storage.
            // Rule 18: dispose BlobDownloadStreamingResult (response.Value) via using;
            // copy content to a MemoryStream before returning so the stream is fully read
            // and the connection is released before the response is written to the client.
            var blobClient = _blobContainerClient.GetBlobClient(photo.StoredFileName);
            var downloadResponse = await blobClient.DownloadStreamingAsync();
            var ms = new MemoryStream();
            using (downloadResponse.Value)
            {
                await downloadResponse.Value.Content.CopyToAsync(ms);
            }
            ms.Position = 0;

            _logger.LogDebug("Serving photo ID {PhotoId} ({FileName}, {FileSize} bytes)",
                id, photo.OriginalFileName, ms.Length);

            // Return the file with appropriate content type and enable caching
            Response.Headers.CacheControl = "public,max-age=31536000"; // Cache for 1 year
            Response.Headers.ETag = $"\"{photo.Id}-{photo.UploadedAt.Ticks}\"";

            return File(ms, photo.MimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving photo with ID {PhotoId}", id);
            return StatusCode(500);
        }
    }
}
