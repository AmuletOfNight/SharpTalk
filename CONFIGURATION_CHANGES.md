# Configuration Changes Summary

## File Upload Bug Fix

### Problem
When uploading files in the chat, the application threw an `ArgumentException` because `file.ContentType` was empty, causing `MediaTypeHeaderValue` to fail.

### Solution
Added a check to handle empty or null ContentType values by using a default MIME type (`application/octet-stream`) when the browser doesn't provide one.

### Files Modified
- `SharpTalk.Web/Services/ChatService.cs` - Line 120-127
- `SharpTalk.Web/Services/UserService.cs` - Line 116-123

## Configuration Improvements

### Hardcoded Values Moved to Configuration

#### SharpTalk.Web (Blazor WebAssembly)
**Created:** `SharpTalk.Web/wwwroot/appsettings.json`
```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5298",
    "SignalRHubUrl": "http://localhost:5298/chatHub"
  },
  "FileUploadSettings": {
    "MaxChatFileSizeBytes": 10485760,
    "MaxAvatarFileSizeBytes": 2097152
  }
}
```

**Modified:** `SharpTalk.Web/Program.cs`
- Added configuration loading
- API Base URL now reads from `ApiSettings:BaseUrl`
- SignalR Hub URL now reads from `ApiSettings:SignalRHubUrl`

**Modified:** `SharpTalk.Web/Services/ChatService.cs`
- Added `IConfiguration` dependency
- SignalR Hub URL reads from configuration
- Max chat file size reads from configuration
- Added empty ContentType handling

**Modified:** `SharpTalk.Web/Services/UserService.cs`
- Added `IConfiguration` dependency
- Max avatar file size reads from configuration
- Added empty ContentType handling

#### SharpTalk.Api
**Modified:** `SharpTalk.Api/appsettings.json`
```json
{
  "FileUploadSettings": {
    "MaxImageFileSizeBytes": 5242880,
    "MaxOtherFileSizeBytes": 10485760,
    "MaxAvatarFileSizeBytes": 2097152
  },
  "MessageSettings": {
    "MaxMessagesToRetrieve": 50
  }
}
```

**Modified:** `SharpTalk.Api/Services/FileUploadService.cs`
- Added `IConfiguration` dependency
- Max image file size reads from configuration
- Max other file size reads from configuration

**Modified:** `SharpTalk.Api/Controllers/UserController.cs`
- Added `IConfiguration` dependency
- Max avatar file size reads from configuration

**Modified:** `SharpTalk.Api/Controllers/MessageController.cs`
- Added `IConfiguration` dependency
- Max messages to retrieve reads from configuration

## Configuration Values

### File Size Limits
- **Max Chat File Size:** 10MB (10,485,760 bytes)
- **Max Avatar File Size:** 2MB (2,097,152 bytes)
- **Max Image File Size:** 5MB (5,242,880 bytes)
- **Max Other File Size:** 10MB (10,485,760 bytes)

### API Settings
- **API Base URL:** http://localhost:5298
- **SignalR Hub URL:** http://localhost:5298/chatHub

### Message Settings
- **Max Messages to Retrieve:** 50

## Benefits

1. **Maintainability:** Configuration values are now centralized and easy to modify
2. **Flexibility:** Different environments can have different configurations
3. **Consistency:** File size limits are consistent across client and server
4. **Bug Fix:** File uploads now work even when browser doesn't detect file type
5. **Best Practices:** Following .NET configuration patterns

## Testing

To test the file upload fix:
1. Upload a file in the chat
2. The file should upload successfully even if the browser doesn't detect its MIME type
3. Check the browser console for debug messages showing the file details

## Next Steps

Consider updating the API Base URL to use HTTPS to avoid mixed content warnings:
- Change `BaseUrl` to `https://localhost:7107` in `SharpTalk.Web/wwwroot/appsettings.json`
- Change `SignalRHubUrl` to `https://localhost:7107/chatHub`
- Ensure the API is running with HTTPS enabled
