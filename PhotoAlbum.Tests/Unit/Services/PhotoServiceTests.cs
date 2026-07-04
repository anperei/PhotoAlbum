using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PhotoAlbum.Data;
using PhotoAlbum.Models;
using PhotoAlbum.Services;
using System.Text;

namespace PhotoAlbum.Tests.Unit.Services;

public class PhotoServiceTests : IDisposable
{
    private readonly PhotoAlbumContext _context;
    private readonly IPhotoService _photoService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PhotoService> _logger;
    private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;

    public PhotoServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<PhotoAlbumContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PhotoAlbumContext(options);

        // Setup configuration
        var inMemorySettings = new Dictionary<string, string>
        {
            {"FileUpload:MaxFileSizeBytes", "10485760"},
            {"FileUpload:AllowedMimeTypes:0", "image/jpeg"},
            {"FileUpload:AllowedMimeTypes:1", "image/png"},
            {"FileUpload:AllowedMimeTypes:2", "image/gif"},
            {"FileUpload:AllowedMimeTypes:3", "image/webp"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        // Setup logger
        _logger = new LoggerFactory().CreateLogger<PhotoService>();

        // Setup Azure Blob Storage mocks
        _mockBlobContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();

        // GetBlobClient always returns the same mock BlobClient
        _mockBlobContainerClient
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(_mockBlobClient.Object);

        // UploadAsync succeeds
        _mockBlobClient
            .Setup(b => b.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobUploadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        // DeleteIfExistsAsync succeeds and returns true
        var mockDeleteResponse = new Mock<Response<bool>>();
        mockDeleteResponse.SetupGet(r => r.Value).Returns(true);
        _mockBlobClient
            .Setup(b => b.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResponse.Object);

        // Create PhotoService instance
        _photoService = new PhotoService(_context, _configuration, _logger, _mockBlobContainerClient.Object);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithValidImage_ReturnsSuccess()
    {
        // Arrange
        var file = CreateMockFormFile("test.jpg", "image/jpeg", 1024);

        // Act
        var result = await _photoService.UploadPhotoAsync(file);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.PhotoId);
        Assert.Equal("test.jpg", result.FileName);
        Assert.Null(result.ErrorMessage);

        // Verify photo was saved to database
        var photo = await _context.Photos.FindAsync(result.PhotoId);
        Assert.NotNull(photo);
        Assert.Equal("test.jpg", photo.OriginalFileName);
        Assert.Equal("image/jpeg", photo.MimeType);
        Assert.Equal(1024, photo.FileSize);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithInvalidMimeType_ReturnsError()
    {
        // Arrange
        var file = CreateMockFormFile("document.pdf", "application/pdf", 1024);

        // Act
        var result = await _photoService.UploadPhotoAsync(file);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.PhotoId);
        Assert.Contains("not supported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadPhotoAsync_WithOversizedFile_ReturnsError()
    {
        // Arrange
        var file = CreateMockFormFile("huge.jpg", "image/jpeg", 11 * 1024 * 1024); // 11MB

        // Act
        var result = await _photoService.UploadPhotoAsync(file);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.PhotoId);
        Assert.Contains("exceeds", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadPhotoAsync_CreatesFileInUploadsDirectory()
    {
        // Arrange
        var file = CreateMockFormFile("test.png", "image/png", 2048);

        // Act
        var result = await _photoService.UploadPhotoAsync(file);

        // Assert
        Assert.True(result.Success);

        // Verify blob was uploaded to Azure Blob Storage
        var photo = await _context.Photos.FindAsync(result.PhotoId);
        Assert.NotNull(photo);
        _mockBlobContainerClient.Verify(c => c.GetBlobClient(photo.StoredFileName), Times.AtLeastOnce);
        _mockBlobClient.Verify(b => b.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobUploadOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadPhotoAsync_SavesMetadataToDatabase()
    {
        // Arrange
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", 3072);

        // Act
        var result = await _photoService.UploadPhotoAsync(file);

        // Assert
        Assert.True(result.Success);

        var photo = await _context.Photos.FindAsync(result.PhotoId);
        Assert.NotNull(photo);
        Assert.Equal("photo.jpg", photo.OriginalFileName);
        Assert.NotEmpty(photo.StoredFileName);
        Assert.NotEmpty(photo.FilePath);
        Assert.True(photo.UploadedAt <= DateTime.UtcNow);
        Assert.True(photo.UploadedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task GetAllPhotosAsync_ReturnsPhotosOrderedByDate()
    {
        // Arrange
        var photo1 = new Photo
        {
            OriginalFileName = "first.jpg",
            StoredFileName = "guid1.jpg",
            FilePath = "/uploads/guid1.jpg",
            FileSize = 1024,
            MimeType = "image/jpeg",
            UploadedAt = DateTime.UtcNow.AddHours(-2)
        };
        var photo2 = new Photo
        {
            OriginalFileName = "second.jpg",
            StoredFileName = "guid2.jpg",
            FilePath = "/uploads/guid2.jpg",
            FileSize = 2048,
            MimeType = "image/jpeg",
            UploadedAt = DateTime.UtcNow.AddHours(-1)
        };
        var photo3 = new Photo
        {
            OriginalFileName = "third.jpg",
            StoredFileName = "guid3.jpg",
            FilePath = "/uploads/guid3.jpg",
            FileSize = 3072,
            MimeType = "image/jpeg",
            UploadedAt = DateTime.UtcNow
        };

        await _context.Photos.AddRangeAsync(photo1, photo2, photo3);
        await _context.SaveChangesAsync();

        // Act
        var photos = await _photoService.GetAllPhotosAsync();

        // Assert
        Assert.Equal(3, photos.Count);
        Assert.Equal("third.jpg", photos[0].OriginalFileName); // Most recent first
        Assert.Equal("second.jpg", photos[1].OriginalFileName);
        Assert.Equal("first.jpg", photos[2].OriginalFileName);
    }

    [Fact]
    public async Task DeletePhotoAsync_RemovesFileAndDatabaseRecord()
    {
        // Arrange
        var file = CreateMockFormFile("todelete.jpg", "image/jpeg", 1024);
        var uploadResult = await _photoService.UploadPhotoAsync(file);
        var photoId = uploadResult.PhotoId!.Value;

        var photo = await _context.Photos.FindAsync(photoId);
        Assert.NotNull(photo);

        // Reset mock invocations so we can verify delete-specific calls cleanly
        _mockBlobClient.Invocations.Clear();
        _mockBlobContainerClient.Invocations.Clear();

        // Act
        var result = await _photoService.DeletePhotoAsync(photoId);

        // Assert
        Assert.True(result);
        Assert.Null(await _context.Photos.FindAsync(photoId));

        // Verify blob was deleted from Azure Blob Storage
        _mockBlobClient.Verify(b => b.DeleteIfExistsAsync(
            It.IsAny<DeleteSnapshotsOption>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private IFormFile CreateMockFormFile(string fileName, string contentType, long size)
    {
        var content = new byte[size];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, size, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
