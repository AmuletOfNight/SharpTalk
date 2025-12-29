# User Avatar Features Implementation Plan

## Overview
Design and implement user avatars with:
1. User avatar display in the main UI (name + profile picture)
2. User settings modal for profile management
3. Avatar upload functionality
4. Message avatars in chat

## Current State Analysis

### Existing Components
- **User Entity**: Already has `AvatarUrl` and `Status` fields
- **AuthResponse DTO**: Already includes `AvatarUrl`
- **CustomAuthStateProvider**: Parses JWT tokens but doesn't store user profile
- **Login/Register**: Returns AvatarUrl but doesn't persist user data

### Current Limitations
- No user profile storage in localStorage (only JWT token)
- No user avatar/name display in the main layout
- No user settings/profile modal
- No avatar upload endpoint
- No message avatar display

## Proposed Features

### 1. User Profile Display
Display logged-in user's name and avatar in the main layout (bottom-left corner of sidebar).

**UI Layout**:
```
┌─────────────────────────────────────┐
│ Sidebar                             │
│  ┌─────────────────────────────┐    │
│  │ [Avatar] Username           │    │
│  │   Status: Online           ▼ │    │
│  └─────────────────────────────┘    │
│  ┌─────┐ ┌─────┐ ┌─────┐          │
│  │ WS1 │ │ WS2 │ │ WS3 │ [+ ]      │
│  └─────┘ └─────┘ └─────┘          │
└─────────────────────────────────────┘
```

### 2. User Settings Modal
A modal for managing user profile:
- Avatar upload
- Username display (read-only or editable)
- Email display (read-only)
- Status selection (Online, Away, Offline)
- Logout button

### 3. Avatar Upload
API endpoint to upload and update user avatar.

### 4. Message Avatars
Display user avatars next to messages in the chat area.

## Technical Implementation

### 1. User Service (New)
Create a centralized service for user data management.

**File**: `SharpTalk.Web/Services/UserService.cs`
```csharp
public class UserService
{
    public event Action<UserInfo>? OnUserInfoChanged;
    public UserInfo? CurrentUser { get; private set; }
    
    public Task<UserInfo?> GetCurrentUserAsync()
    public Task UpdateUserInfoAsync(UserInfo userInfo)
    public Task LogoutAsync()
}
```

### 2. UserInfo DTO (New)
Create a DTO for storing user profile data.

**File**: `SharpTalk.Shared/DTOs/UserInfo.cs`
```csharp
public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Online";
}
```

### 3. User Controller (New)
Add API endpoints for user management.

**File**: `SharpTalk.Api/Controllers/UserController.cs`
```csharp
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    // GET api/user/profile - Get current user profile
    // PUT api/user/profile - Update user profile
    // POST api/user/avatar - Upload avatar
    // PUT api/user/status - Update status
}
```

### 4. Update CustomAuthStateProvider
Store user profile in localStorage during authentication.

**File**: `SharpTalk.Web/Auth/CustomAuthStateProvider.cs`
- Store full user info in localStorage
- Add methods to retrieve/update user info

### 5. User Profile Component (New)
Display user info in the sidebar with dropdown menu.

**File**: `SharpTalk.Web/Shared/UserProfile.razor`
```razor
<div class="user-profile" @onclick="ToggleDropdown">
    <img src="@AvatarUrl ?? '/images/default-avatar.png'" class="user-avatar" />
    <div class="user-info">
        <span class="username">@Username</span>
        <span class="status">@Status</span>
    </div>
    @if (IsDropdownOpen)
    {
        <div class="user-dropdown">
            <button @onclick="OpenSettings">Settings</button>
            <button @onclick="Logout">Logout</button>
        </div>
    }
</div>
```

### 6. User Settings Modal (New)
Modal for managing user profile.

**File**: `SharpTalk.Web/Shared/UserSettingsModal.razor`
```razor
<div class="modal">
    <div class="modal-content">
        <h3>User Settings</h3>
        
        <!-- Avatar Section -->
        <div class="avatar-section">
            <img src="@AvatarUrl ?? '/images/default-avatar.png'" class="avatar-preview" />
            <InputFile OnChange="UploadAvatar" accept="image/*" />
        </div>
        
        <!-- Username -->
        <div class="form-group">
            <label>Username</label>
            <InputText @bind-Value="Username" />
        </div>
        
        <!-- Status -->
        <div class="form-group">
            <label>Status</label>
            <select @bind="Status">
                <option value="Online">Online</option>
                <option value="Away">Away</option>
                <option value="Offline">Offline</option>
            </select>
        </div>
        
        <!-- Actions -->
        <button @onclick="SaveSettings">Save</button>
        <button @onclick="Close">Cancel</button>
    </div>
</div>
```

### 7. Message Avatar Display
Update MessageDto and ChatArea to show avatars.

**File**: `SharpTalk.Shared/DTOs/MessageDto.cs`
```csharp
public class MessageDto
{
    // ... existing fields
    public string? AvatarUrl { get; set; }
}
```

**File**: `SharpTalk.Web/Shared/ChatArea.razor`
Update message rendering to include avatar:
```razor
<div class="message-item">
    <img src="@message.AvatarUrl ?? '/images/default-avatar.png'" class="message-avatar" />
    <div class="message-content">
        <span class="message-author">@message.Username</span>
        <span class="message-text">@message.Content</span>
    </div>
</div>
```

### 8. CSS Updates
Add styles for user profile and avatar components.

**File**: `SharpTalk.Web/wwwroot/css/app.css`
```css
/* User Profile */
.user-profile {
    display: flex;
    align-items: center;
    padding: 8px;
    cursor: pointer;
    background-color: #292b2f;
    border-radius: 4px;
    margin: 4px;
}

.user-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    object-fit: cover;
}

.user-info {
    margin-left: 8px;
    display: flex;
    flex-direction: column;
}

.username {
    font-weight: 600;
    font-size: 13px;
}

.status {
    font-size: 11px;
    color: #b9bbbe;
}

.user-dropdown {
    position: absolute;
    bottom: 60px;
    left: 4px;
    background-color: #36393f;
    border-radius: 4px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
    padding: 4px 0;
    min-width: 120px;
}

.user-dropdown button {
    display: block;
    width: 100%;
    padding: 8px 12px;
    text-align: left;
    background: none;
    border: none;
    color: #dcddde;
    cursor: pointer;
}

.user-dropdown button:hover {
    background-color: #40444b;
}

/* Message Avatar */
.message-avatar {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    object-fit: cover;
    margin-right: 12px;
}

/* User Settings Modal */
.avatar-section {
    text-align: center;
    margin-bottom: 20px;
}

.avatar-preview {
    width: 100px;
    height: 100px;
    border-radius: 50%;
    object-fit: cover;
    margin-bottom: 10px;
}
```

## Implementation Order

1. **Backend - DTOs**: Create UserInfo DTO
2. **Backend - API**: Add UserController with avatar upload endpoint
3. **Frontend - Service**: Create UserService for user data management
4. **Frontend - Auth**: Update CustomAuthStateProvider to store user profile
5. **Frontend - Component**: Create UserProfile component for sidebar
6. **Frontend - Modal**: Create UserSettingsModal
7. **Frontend - Chat**: Update MessageDto and ChatArea for message avatars
8. **Frontend - Layout**: Integrate UserProfile into MainLayout
9. **Frontend - CSS**: Add styles for all new components
10. **Testing**: Test all new features

## Security Considerations

- All user endpoints require authentication
- Avatar upload should validate file type and size
- Username changes should check for duplicates
- Status updates should be limited to valid values

## User Experience Considerations

- Loading states while avatar uploads
- Error messages for failed operations
- Preview of avatar before upload
- Confirmation for logout
- Smooth transitions and animations
