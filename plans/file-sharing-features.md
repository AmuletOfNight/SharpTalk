# File Sharing Features Implementation Plan

## Overview
Implement file sharing functionality allowing users to upload and share files in channels, including images, documents, and other file types.

## Current State Analysis

### Existing Infrastructure
- **Attachment Entity**: Already defined with all necessary fields
  - Id, MessageId, FileName, FileUrl, FileType, FileSize, CreatedAt
- **Message Entity**: Has relationship to Attachment (via MessageId)
- **Avatar Upload**: Existing implementation in UserController for reference
- **Static File Serving**: Already configured in Program.cs for wwwroot/uploads

### Current Limitations
- No API endpoints for file upload
- No UI for file attachment in chat
- No file preview/display in messages
- No file download functionality
- Message entity doesn't have Attachments collection

## Proposed Features

### 1. File Upload API
Endpoint to upload files and associate them with messages.

**Features**:
- Support multiple file types (images, documents, archives)
- File size validation (max 10MB for documents, 5MB for images)
- File type validation (whitelist approach)
- Secure file storage with unique filenames
- Automatic thumbnail generation for images (optional)

### 2. File Attachment in Messages
Allow users to attach files when sending messages.

**Features**:
- File picker in chat input area
- Drag-and-drop support
- Multiple file selection
- File preview before sending
- Progress indicator during upload

### 3. File Display in Chat
Show attached files in message bubbles.

**Features**:
- Image preview (inline for images)
- File icon for non-image files
- File name and size display
- Download button
- Click to open/download

### 4. File Download
Secure file download endpoint.

**Features**:
- Authorization check (user must be in channel)
- Proper content-type headers
- Content-disposition for download

## Technical Implementation

### 1. Database Updates

#### Update Message Entity
Add Attachments collection:

```csharp
public class Message
{
    // ... existing fields
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
```

#### Update ApplicationDbContext
Configure relationship:

```csharp
modelBuilder.Entity<Attachment>()
    .HasOne(a => a.Message)
    .WithMany(m => m.Attachments)
    .HasForeignKey(a => a.MessageId)
    .OnDelete(DeleteBehavior.Cascade);
```

#### Migration Required
Create migration to add the relationship.

### 2. DTOs Required

#### AttachmentDto.cs
```csharp
public class AttachmentDto
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### Update MessageDto.cs
Add Attachments property:

```csharp
public class MessageDto
{
    // ... existing fields
    public List<AttachmentDto> Attachments { get; set; } = new List<AttachmentDto>();
}
```

#### SendMessageRequest.cs (Update)
Add optional file attachments:

```csharp
public class SendMessageRequest
{
    public int ChannelId { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<IFormFile>? Attachments { get; set; }
}
```

### 3. API Endpoints to Add

#### MessageController.cs Updates

```csharp
// POST api/message/upload
// Upload files and return their URLs
[HttpPost("upload")]
public async Task<ActionResult<List<AttachmentDto>>> UploadFiles(List<IFormFile> files)

// GET api/message/{messageId}/attachments
// Get all attachments for a message
[HttpGet("{messageId}/attachments")]
public async Task<ActionResult<List<AttachmentDto>>> GetMessageAttachments(int messageId)

// GET api/message/attachment/{attachmentId}
// Download a specific attachment
[HttpGet("attachment/{attachmentId}")]
public async Task<IActionResult> DownloadAttachment(int attachmentId)
```

### 4. File Upload Service (New)

**File**: `SharpTalk.Api/Services/FileUploadService.cs`

```csharp
public class FileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _context;

    public Task<List<AttachmentDto>> UploadFilesAsync(List<IFormFile> files, int messageId)
    public Task<AttachmentDto?> GetAttachmentAsync(int attachmentId)
    public Task<bool> CanAccessAttachmentAsync(int attachmentId, int userId)
}
```

### 5. Frontend Service Updates

#### ChatService.cs Updates

Add methods for file handling:

```csharp
public async Task<List<AttachmentDto>> UploadFilesAsync(List<IBrowserFile> files, int channelId)
public async Task<string> DownloadAttachmentAsync(int attachmentId)
```

### 6. Frontend Components

#### Update ChatArea.razor

Add file attachment UI:

```razor
<div class="message-input">
    <div class="file-attachments" @if="selectedFiles.Any()">
        @foreach (var file in selectedFiles)
        {
            <div class="file-preview">
                <span>@file.Name</span>
                <button @onclick="() => RemoveFile(file)">×</button>
            </div>
        }
    </div>
    
    <div class="input-actions">
        <label class="attach-btn">
            📎
            <InputFile OnChange="HandleFileSelect" multiple />
        </label>
        <input type="text" @bind="newMessage" placeholder="Message #Channel" />
        <button @onclick="SendMessage">Send</button>
    </div>
</div>
```

Add file display in messages:

```razor
@if (message.Attachments.Any())
{
    <div class="message-attachments">
        @foreach (var attachment in message.Attachments)
        {
            <div class="attachment-item">
                @if (IsImage(attachment.FileType))
                {
                    <img src="@attachment.FileUrl" class="attachment-image" @onclick="() => OpenImage(attachment)" />
                }
                else
                {
                    <div class="attachment-file">
                        <span class="file-icon">📄</span>
                        <div class="file-info">
                            <span class="file-name">@attachment.FileName</span>
                            <span class="file-size">@FormatFileSize(attachment.FileSize)</span>
                        </div>
                        <button @onclick="() => DownloadFile(attachment)">Download</button>
                    </div>
                }
            </div>
        }
    </div>
}
```

#### Create ImagePreviewModal.razor (New)

Modal for viewing images in full size:

```razor
<div class="image-preview-modal">
    <img src="@ImageUrl" />
    <button @onclick="Close">Close</button>
    <button @onclick="Download">Download</button>
</div>
```

### 7. CSS Updates

Add styles for file attachments:

```css
/* File Attachments */
.file-attachments {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    padding: 8px;
    background-color: #2f3136;
    border-radius: 4px;
    margin-bottom: 8px;
}

.file-preview {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 4px 8px;
    background-color: #40444b;
    border-radius: 4px;
    font-size: 12px;
}

.file-preview button {
    background: none;
    border: none;
    color: #dcddde;
    cursor: pointer;
    font-size: 16px;
}

.input-actions {
    display: flex;
    align-items: center;
    gap: 8px;
}

.attach-btn {
    padding: 8px 12px;
    cursor: pointer;
    font-size: 18px;
}

/* Message Attachments */
.message-attachments {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 8px;
}

.attachment-item {
    max-width: 300px;
}

.attachment-image {
    max-width: 100%;
    max-height: 200px;
    border-radius: 4px;
    cursor: pointer;
    transition: transform 0.2s;
}

.attachment-image:hover {
    transform: scale(1.02);
}

.attachment-file {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px;
    background-color: #2f3136;
    border-radius: 4px;
    cursor: pointer;
}

.file-icon {
    font-size: 24px;
}

.file-info {
    flex: 1;
    display: flex;
    flex-direction: column;
}

.file-name {
    font-size: 13px;
    font-weight: 500;
}

.file-size {
    font-size: 11px;
    color: #b9bbbe;
}

/* Image Preview Modal */
.image-preview-modal {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.9);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 9999;
}

.image-preview-modal img {
    max-width: 90%;
    max-height: 90%;
    object-fit: contain;
}
```

## Implementation Order

1. **Backend - Database**: Update Message entity, create migration
2. **Backend - DTOs**: Create AttachmentDto, update MessageDto
3. **Backend - Service**: Create FileUploadService
4. **Backend - API**: Add file upload/download endpoints to MessageController
5. **Frontend - Service**: Update ChatService with file methods
6. **Frontend - Component**: Update ChatArea with file attachment UI
7. **Frontend - Component**: Create ImagePreviewModal
8. **Frontend - CSS**: Add styles for file attachments
9. **Testing**: Test file upload, display, and download
10. **Documentation**: Update README with file sharing features

## Security Considerations

- File type validation (whitelist allowed types)
- File size limits (prevent DoS attacks)
- Authorization checks (user must be in channel to access files)
- Secure file storage (outside web root or with proper access controls)
- Sanitize filenames (prevent path traversal)
- Virus scanning (optional, for production)

## File Type Support

### Images (max 5MB)
- JPEG, PNG, GIF, WebP, SVG

### Documents (max 10MB)
- PDF, DOC, DOCX, XLS, XLSX, PPT, PPTX

### Archives (max 10MB)
- ZIP, RAR, 7Z, TAR, GZ

### Other (max 5MB)
- TXT, CSV, JSON, XML

## User Experience Considerations

- Show upload progress for large files
- Preview images before sending
- Allow removing files before sending
- Display file size and type
- Show error messages for invalid files
- Smooth animations for file attachments
- Responsive design for mobile devices
- Keyboard shortcuts (Ctrl+U to upload)

## Performance Considerations

- Stream file uploads (don't load entire file into memory)
- Use async operations for file I/O
- Implement file size limits on client and server
- Consider CDN for file serving in production
- Cache file metadata
- Lazy load images in chat history

## Future Enhancements (Out of Scope)

- Video file support with preview
- Audio file support with player
- Image editing before upload
- File compression
- File sharing expiration
- File versioning
- Collaborative document editing
- Screen capture and share
