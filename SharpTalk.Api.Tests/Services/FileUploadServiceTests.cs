using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SharpTalk.Api.Entities;
using SharpTalk.Api.Services;
using SharpTalk.Api.Tests.Helpers;

namespace SharpTalk.Api.Tests.Services;

public class FileUploadServiceTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly FileUploadService _service;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<FileUploadService>> _loggerMock;
    private readonly string _testUploadPath;

    public FileUploadServiceTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        
        // Create a temporary directory for uploads
        _testUploadPath = Path.Combine(Path.GetTempPath(), $"SharpTalkTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testUploadPath);
        Directory.CreateDirectory(Path.Combine(_testUploadPath, "wwwroot", "uploads", "files"));
        
        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(x => x.ContentRootPath).Returns(_testUploadPath);
        
        _configurationMock = new Mock<IConfiguration>();
        
        _loggerMock = new Mock<ILogger<FileUploadService>>();

        _service = new FileUploadService(
            _dbHelper.Context,
            _environmentMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
        
        // Clean up test directory
        if (Directory.Exists(_testUploadPath))
        {
            try
            {
                Directory.Delete(_testUploadPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private IFormFile CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, c) => stream.CopyTo(s))
            .Returns(Task.CompletedTask);
        return fileMock.Object;
    }

    #region UploadFilesAsync Tests

    [Fact]
    public async Task UploadFilesAsync_ValidFile_CreatesAttachment()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        // Create a message first
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var fileContent = "Test file content"u8.ToArray();
        var file = CreateMockFile("test.txt", "text/plain", fileContent);

        // Act
        var result = await _service.UploadFilesAsync(new List<IFormFile> { file }, message.Id, 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].FileName.Should().Be("test.txt");
        result[0].FileType.Should().Be("text/plain");
    }

    [Fact]
    public async Task UploadFilesAsync_InvalidMimeType_ThrowsArgumentException()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var fileContent = "Test content"u8.ToArray();
        var file = CreateMockFile("test.exe", "application/x-executable", fileContent);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.UploadFilesAsync(new List<IFormFile> { file }, message.Id, 1));
    }

    [Fact]
    public async Task UploadFilesAsync_EmptyFile_SkipsFile()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var file = CreateMockFile("empty.txt", "text/plain", Array.Empty<byte>());

        // Act
        var result = await _service.UploadFilesAsync(new List<IFormFile> { file }, message.Id, 1);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CanAccessAttachmentAsync Tests

    [Fact]
    public async Task CanAccessAttachmentAsync_WorkspaceMember_ReturnsTrue()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var attachment = new Attachment
        {
            MessageId = message.Id,
            FileName = "test.txt",
            FileUrl = "/uploads/files/test.txt",
            FileType = "text/plain",
            FileSize = 100,
            
        };
        _dbHelper.Context.Attachments.Add(attachment);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _service.CanAccessAttachmentAsync(attachment.Id, 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAttachmentAsync_NotMember_ReturnsFalse()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var attachment = new Attachment
        {
            MessageId = message.Id,
            FileName = "test.txt",
            FileUrl = "/uploads/files/test.txt",
            FileType = "text/plain",
            FileSize = 100,
            
        };
        _dbHelper.Context.Attachments.Add(attachment);
        await _dbHelper.Context.SaveChangesAsync();

        // Act - User 2 is not a workspace member
        var result = await _service.CanAccessAttachmentAsync(attachment.Id, 2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAttachmentAsync_PrivateChannel_RequiresChannelMembership()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        // Create a private channel
        var privateChannel = new Channel
        {
            WorkspaceId = 1,
            Name = "private-channel",
            IsPrivate = true,
            
        };
        _dbHelper.Context.Channels.Add(privateChannel);
        await _dbHelper.Context.SaveChangesAsync();

        // Add user 1 as channel member
        _dbHelper.Context.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = privateChannel.Id,
            UserId = 1,
            JoinedAt = DateTime.UtcNow
        });

        // Add user 2 as workspace member but NOT channel member
        _dbHelper.Context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = 2,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 1
        });
        await _dbHelper.Context.SaveChangesAsync();

        var message = new Message
        {
            ChannelId = privateChannel.Id,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var attachment = new Attachment
        {
            MessageId = message.Id,
            FileName = "test.txt",
            FileUrl = "/uploads/files/test.txt",
            FileType = "text/plain",
            FileSize = 100,
            
        };
        _dbHelper.Context.Attachments.Add(attachment);
        await _dbHelper.Context.SaveChangesAsync();

        // Act - User 2 is workspace member but not channel member
        var result = await _service.CanAccessAttachmentAsync(attachment.Id, 2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAttachmentAsync_NonexistentAttachment_ReturnsFalse()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();

        // Act
        var result = await _service.CanAccessAttachmentAsync(999, 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAttachmentAsync Tests

    [Fact]
    public async Task GetAttachmentAsync_ExistingAttachment_ReturnsDto()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        var attachment = new Attachment
        {
            MessageId = message.Id,
            FileName = "test.txt",
            FileUrl = "/uploads/files/test.txt",
            FileType = "text/plain",
            FileSize = 100,
            
        };
        _dbHelper.Context.Attachments.Add(attachment);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _service.GetAttachmentAsync(attachment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.FileName.Should().Be("test.txt");
    }

    [Fact]
    public async Task GetAttachmentAsync_NonexistentAttachment_ReturnsNull()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();

        // Act
        var result = await _service.GetAttachmentAsync(999);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
