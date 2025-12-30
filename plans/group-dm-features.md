# Group DM Implementation Plan

## Overview
Implement a group DM system allowing users to create and manage conversations with up to 8-9 participants (including the creator = 9 max members).

## Implementation Status: ✅ COMPLETED

All features have been fully implemented:

### ✅ Phase 1: Data Layer
- **DTOs Created**: `CreateGroupDMRequest`, `GroupMemberDto`, `AddGroupMemberRequest`, `RemoveGroupMemberRequest`, `UpdateGroupNameRequest`, `LeaveGroupRequest`

### ✅ Phase 2: Backend API
- **ChannelController**: All endpoints implemented
  - `POST api/channel/groupdm` - Create group DM
  - `POST api/channel/groupdm/members` - Add member
  - `DELETE api/channel/groupdm/{channelId}/members/{userId}` - Remove member
  - `PUT api/channel/groupdm/{channelId}/name` - Update group name
  - `GET api/channel/groupdm/{channelId}/members` - Get members
  - `POST api/channel/groupdm/{channelId}/leave` - Leave group

### ✅ Phase 3: Frontend Components
- **StartGroupDMModal.razor**: Multi-select user picker with validation
- **GroupDMSettingsModal.razor**: Member management, rename, leave group
- **GroupAvatarStack.razor**: Overlapping avatar display
- **ChatArea.razor**: Group header with member count and settings button
- **DirectMessagesList.razor**: Already had group badge and avatar support

### ✅ Phase 4: ChannelService Methods
- `CreateGroupDMAsync`, `AddGroupMemberAsync`, `RemoveGroupMemberAsync`, `UpdateGroupNameAsync`, `GetGroupMembersAsync`, `LeaveGroupAsync`

---

## Current State Analysis

### Existing Infrastructure (Before Implementation)
- **ChannelType enum**: Already included `Group` type
- **ChannelDto**: Had `GroupMemberDto? Members`, `MemberCount`, `IsGroup` properties
- **DirectMessagesList.razor**: Referenced `StartGroupDMModal` (not yet implemented)
- **ChannelType**: Already defined with Group member

### Gaps Identified (Before Implementation)
1. No `StartGroupDMModal.razor` component
2. No API endpoints for group DM creation with multiple members
3. No member management (add/remove) for group DMs
4. No `GroupMemberDto` class defined
5. No `CreateGroupDMRequest` DTO
6. No group settings modal for member management
7. No UI for adding/removing members from existing group DMs
8. Member limit enforcement not implemented

---

## Implementation Plan

### Phase 1: Data Layer

#### 1.1 Create DTOs
```csharp
// SharpTalk.Shared/DTOs/CreateGroupDMRequest.cs
public class CreateGroupDMRequest
{
    public int? WorkspaceId { get; set; }
    
    [Required]
    [MinLength(2, ErrorMessage = "Group DM requires at least 2 other members")]
    [MaxLength(8, ErrorMessage = "Group DM cannot exceed 8 other members (9 total)")]
    public List<int> TargetUserIds { get; set; } = new();
    
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters")]
    public string? Name { get; set; }
}

// SharpTalk.Shared/DTOs/GroupMemberDto.cs
public class GroupMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Offline"; // Online, Offline, Away
    public DateTime? JoinedAt { get; set; }
    public bool IsAdmin { get; set; } // Creator or designated admin
}

// SharpTalk.Shared/DTOs/AddGroupMemberRequest.cs
public class AddGroupMemberRequest
{
    [Required]
    public int ChannelId { get; set; }
    
    [Required]
    public int UserId { get; set; }
}

// SharpTalk.Shared/DTOs/RemoveGroupMemberRequest.cs
public class RemoveGroupMemberRequest
{
    [Required]
    public int ChannelId { get; set; }
    
    [Required]
    public int UserId { get; set; }
}

// SharpTalk.Shared/DTOs/UpdateGroupNameRequest.cs
public class UpdateGroupNameRequest
{
    [Required]
    public int ChannelId { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

// SharpTalk.Shared/DTOs/LeaveGroupRequest.cs
public class LeaveGroupRequest
{
    [Required]
    public int ChannelId { get; set; }
}
```

#### 1.2 Database Updates (if needed)
The current `ChannelMember` entity should already support group DMs. Verify:
- `ChannelId` relationship
- `UserId` relationship
- `JoinedAt` timestamp
- No additional schema changes required

### Phase 2: Backend API

#### 2.1 ChannelController Updates
Add these endpoints:

```csharp
// POST api/channel/create-group-dm
[HttpPost("create-group-dm")]
public async Task<ActionResult<ChannelDto>> CreateGroupDM(CreateGroupDMRequest request)

// POST api/channel/{channelId}/add-member
[HttpPost("{channelId}/add-member")]
public async Task<ActionResult<ChannelDto>> AddGroupMember(AddGroupMemberRequest request)

// POST api/channel/{channelId}/remove-member
[HttpPost("{channelId}/remove-member")]
public async Task<ActionResult<ChannelDto>> RemoveGroupMember(RemoveGroupMemberRequest request)

// POST api/channel/{channelId}/leave
[HttpPost("{channelId}/leave")]
public async Task<ActionResult> LeaveGroup(LeaveGroupRequest request)

// PUT api/channel/{channelId}/name
[HttpPut("{channelId}/name")]
public async Task<ActionResult<ChannelDto>> UpdateGroupName(UpdateGroupNameRequest request)

// GET api/channel/{channelId}/members
[HttpGet("{channelId}/members")]
public async Task<ActionResult<List<GroupMemberDto>>> GetGroupMembers(int channelId)
```

#### 2.2 ChannelService Updates (SharpTalk.Web.Services)
```csharp
public async Task<ChannelDto?> CreateGroupDMAsync(int? workspaceId, List<int> targetUserIds, string? name = null)
public async Task<ChannelDto?> AddMemberToGroupAsync(int channelId, int userId)
public async Task<ChannelDto?> RemoveMemberFromGroupAsync(int channelId, int userId)
public async Task LeaveGroupAsync(int channelId)
public async Task<ChannelDto?> UpdateGroupNameAsync(int channelId, string name)
public async Task<List<GroupMemberDto>> GetGroupMembersAsync(int channelId)
```

#### 2.3 Validation Rules
- Group DM: 2-8 other members (9 total max including creator)
- Creator automatically becomes admin
- Cannot remove yourself if you're the only admin (must leave instead)
- Cannot add user already in group
- Must be member of same workspace (if workspace-scoped) or any workspace (for global DMs)

### Phase 3: Frontend Components

#### 3.1 StartGroupDMModal.razor
**Location**: `SharpTalk.Web/Shared/StartGroupDMModal.razor`

**Features**:
- [x] Multi-select user picker from workspace members
- [x] Display selected users count (X/Y selected)
- [x] Optional group name input
- [x] Validation errors for member limits
- [x] Create button with loading state

**UI Layout**:
```
┌─────────────────────────────┐
│ Create Group DM         [X] │
├─────────────────────────────┤
│ Optional Group Name:         │
│ [_________________]          │
├─────────────────────────────┤
│ Select Members (2-8):        │
│ [Search...]                  │
│ □ @user1                     │
│ ✓ @user2                     │
│ □ @user3                     │
│ ...                          │
├─────────────────────────────┤
│ Selected: 3/8          [Create] │
└─────────────────────────────┘
```

#### 3.2 GroupDMSettingsModal.razor
**Location**: `SharpTalk.Web/Shared/GroupDMSettingsModal.razor`

**Features**:
- [x] Group name display and edit
- [x] Member list with avatars and names
- [x] Remove member buttons (admin only)
- [x] Add member button
- [x] Leave group button (with confirmation if last member)
- [ ] Copy invite link (future)

**UI Layout**:
```
┌─────────────────────────────────┐
│ Group Settings             [X] │
├─────────────────────────────────┤
│ Group Name:                      │
│ [__________________] [Rename]    │
├─────────────────────────────────┤
│ Members (5):                     │
│ ○ @admin      [Remove]           │
│ ○ @member1    [Remove]           │
│ ○ @member2    [Remove]           │
│ [+] Add Member                   │
├─────────────────────────────────┤
│ [           Leave Group          ]│
└─────────────────────────────────┘
```

#### 3.3 GroupAvatarStack.razor
**Location**: `SharpTalk.Web/Shared/GroupAvatarStack.razor`

**Purpose**: Display overlapping avatars for group DMs

**Features**:
- [x] Overlapping circular avatars (like Discord)
- [x] Configurable max visible avatars (default 4-5)
- [x] Hover tooltip showing all members
- [x] Responsive sizing

**Props**:
```csharp
[Parameter] public List<GroupMemberDto> Members { get; set; } = new()
[Parameter] public int MaxVisible { get; set; } = 4
[Parameter] public string Size { get; set; } = "32px"
```

#### 3.4 DirectMessagesList.razor Updates
**File**: `SharpTalk.Web/Shared/DirectMessagesList.razor`

**Updates needed**:
- [x] Button for "Create Group DM" already present (line 14-22)
- [x] Group badge with member count already implemented (line 74-77)
- [x] Group avatar already implemented (line 46-55)
- [x] Handle group channel selection differently (show member count)
- [x] Add group-specific styling

#### 3.5 ChatArea.razor Updates
**File**: `SharpTalk.Web/Shared/ChatArea.razor`

**Updates needed**:
- [x] Detect if active channel is group DM
- [x] Show group header with:
  - Group name
  - Member count
  - Settings button (opens GroupDMSettingsModal)
  - Member avatars
- [ ] Add "Members" sidebar toggle for group DMs

### Phase 4: Backend Implementation Details

#### 4.1 CreateGroupDM Logic
```csharp
public async Task<ChannelDto> CreateGroupDM(CreateGroupDMRequest request)
{
    // Validate member count (2-8 others = 3-9 total including creator)
    if (request.TargetUserIds.Count < 2 || request.TargetUserIds.Count > 8)
        throw new ValidationException("Group must have 2-8 other members");
    
    // Get creator
    var creatorId = GetCurrentUserId();
    
    // Get or create 1:1 DM channels for each pair, then convert to group
    // OR create new channel with all members
    
    // Create channel
    var channel = new Channel
    {
        Name = request.Name ?? GenerateGroupName(request.TargetUserIds),
        Type = ChannelType.Group,
        WorkspaceId = request.WorkspaceId,
        CreatedAt = DateTime.UtcNow
    };
    
    // Add creator
    channel.Members.Add(new ChannelMember
    {
        UserId = creatorId,
        JoinedAt = DateTime.UtcNow,
        IsAdmin = true
    });
    
    // Add other members
    foreach (var userId in request.TargetUserIds)
    {
        channel.Members.Add(new ChannelMember
        {
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
            IsAdmin = false
        });
    }
    
    await _context.Channels.AddAsync(channel);
    await _context.SaveChangesAsync();
    
    return MapToDto(channel);
}
```

#### 4.2 AddMember Logic
```csharp
public async Task<ChannelDto> AddMember(int channelId, int userId)
{
    var channel = await _context.Channels
        .Include(c => c.Members)
        .FirstOrDefaultAsync(c => c.Id == channelId);
    
    if (channel == null || channel.Type != ChannelType.Group)
        throw new NotFoundException("Group not found");
    
    if (channel.Members.Count >= 9)
        throw new ValidationException("Group is full (max 9 members)");
    
    if (channel.Members.Any(m => m.UserId == userId))
        throw new ValidationException("User already in group");
    
    channel.Members.Add(new ChannelMember
    {
        UserId = userId,
        JoinedAt = DateTime.UtcNow,
        IsAdmin = false
    });
    
    await _context.SaveChangesAsync();
    
    // Notify via SignalR
    await _hubContext.Clients.Group(channelId.ToString())
        .SendAsync("MemberAdded", userId);
    
    return MapToDto(channel);
}
```

### Phase 5: SignalR Hub Updates

#### 5.1 ChatHub.cs
```csharp
public async Task JoinGroupChannel(int channelId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
}

public async Task LeaveGroupChannel(int channelId)
{
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
}

// These are automatically called when joining a channel
public override async Task OnConnectedAsync()
{
    // Existing logic + auto-join group DM channels
}
```

### Phase 6: Frontend Service Updates

#### 6.1 ChannelService.cs
Add methods:
```csharp
public async Task<ChannelDto?> CreateGroupDMAsync(int? workspaceId, List<int> targetUserIds, string? name = null)
{
    var request = new CreateGroupDMRequest
    {
        WorkspaceId = workspaceId,
        TargetUserIds = targetUserIds,
        Name = name
    };
    
    var response = await _httpClient.PostAsJsonAsync("api/channel/create-group-dm", request);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<ChannelDto>();
}

public async Task<ChannelDto?> AddMemberToGroupAsync(int channelId, int userId)
{
    var request = new AddGroupMemberRequest { ChannelId = channelId, UserId = userId };
    var response = await _httpClient.PostAsJsonAsync($"api/channel/{channelId}/add-member", request);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<ChannelDto>();
}

public async Task<ChannelDto?> RemoveMemberFromGroupAsync(int channelId, int userId)
{
    var request = new RemoveGroupMemberRequest { ChannelId = channelId, UserId = userId };
    var response = await _httpClient.PostAsJsonAsync($"api/channel/{channelId}/remove-member", request);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<ChannelDto>();
}

public async Task LeaveGroupAsync(int channelId)
{
    var request = new LeaveGroupRequest { ChannelId = channelId };
    await _httpClient.PostAsJsonAsync($"api/channel/{channelId}/leave", request);
}

public async Task<ChannelDto?> UpdateGroupNameAsync(int channelId, string name)
{
    var request = new UpdateGroupNameRequest { ChannelId = channelId, Name = name };
    var response = await _httpClient.PutAsJsonAsync($"api/channel/{channelId}/name", request);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<ChannelDto>();
}
```

### Phase 7: UI/UX Polish

#### 7.1 Group Avatar Styling (CSS)
```css
.group-avatar-stack {
    display: flex;
    align-items: center;
}

.group-avatar-stack .avatar {
    width: var(--avatar-size, 32px);
    height: var(--avatar-size, 32px);
    border-radius: 50%;
    border: 2px solid var(--bg-primary, #36393f);
    margin-left: -8px;
}

.group-avatar-stack .avatar:first-child {
    margin-left: 0;
}

.group-avatar-stack .avatar-more {
    background: var(--accent-color, #5865f2);
    color: white;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.75rem;
    font-weight: bold;
}
```

#### 7.2 Group DM Header Styling
```css
.group-dm-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    border-bottom: 1px solid var(--border-color, #202225);
    background: var(--bg-secondary, #2f3136);
}

.group-dm-info {
    display: flex;
    align-items: center;
    gap: 0.75rem;
}

.group-dm-name {
    font-weight: 600;
    font-size: 1rem;
}

.group-dm-members {
    font-size: 0.875rem;
    color: var(--text-muted, #72767d);
}
```

### Phase 8: Testing

#### 8.1 Unit Tests
- CreateGroupDM: Valid/invalid member counts
- AddMember: At limit, already member, non-member
- RemoveMember: Last admin, self-removal
- LeaveGroup: Last member behavior

#### 8.2 Integration Tests
- Create group DM via API
- Add/remove members
- Verify channel members list
- SignalR notifications

#### 8.3 Frontend Tests
- StartGroupDMModal: Selection limits, validation
- GroupDMSettingsModal: Remove member, leave group
- GroupAvatarStack: Rendering, overflow

### Phase 9: Documentation

#### 9.1 Update README.md
- Document group DM feature
- Member limits: 2-8 others (9 total)
- Admin features
- Future enhancements

#### 9.2 API Documentation
- Endpoint descriptions
- Request/response schemas
- Error codes

---

## File Changes Summary

### New Files Created
| File | Purpose |
|------|---------|
| `SharpTalk.Shared/DTOs/CreateGroupDMRequest.cs` | Request DTO for group creation |
| `SharpTalk.Shared/DTOs/GroupMemberDto.cs` | Member info DTO |
| `SharpTalk.Shared/DTOs/AddGroupMemberRequest.cs` | Add member request |
| `SharpTalk.Shared/DTOs/RemoveGroupMemberRequest.cs` | Remove member request |
| `SharpTalk.Shared/DTOs/UpdateGroupNameRequest.cs` | Rename request |
| `SharpTalk.Shared/DTOs/LeaveGroupRequest.cs` | Leave group request |
| `SharpTalk.Web/Shared/StartGroupDMModal.razor` | Create group DM modal |
| `SharpTalk.Web/Shared/GroupDMSettingsModal.razor` | Group settings modal |
| `SharpTalk.Web/Shared/GroupAvatarStack.razor` | Avatar stack component |

### Files to Modify
| File | Changes |
|------|---------|
| `SharpTalk.Api/Controllers/ChannelController.cs` | Add group DM endpoints |
| `SharpTalk.Web/Services/ChannelService.cs` | Add group DM methods |
| `SharpTalk.Web/Shared/DirectMessagesList.razor` | Ensure group DM support |
| `SharpTalk.Web/Shared/ChatArea.razor` | Add group header |
| `SharpTalk.Web/wwwroot/css/app.css` | Add group DM styles |

---

## Priority Order

All priority items have been completed:

1. **High Priority**:
   - ✅ Create DTOs
   - ✅ Create StartGroupDMModal.razor
   - ✅ Add API endpoints
   - ✅ Add ChannelService methods

2. **Medium Priority**:
   - ✅ Create GroupDMSettingsModal.razor
   - ✅ Update ChatArea.razor with group header
   - ✅ Add member add/remove endpoints

3. **Low Priority**:
   - ✅ GroupAvatarStack.razor
   - [ ] SignalR notifications (partially implemented)
   - [ ] Testing
   - [ ] Documentation

---

## Future Enhancements (Post-MVP)

- [ ] Group DM avatars (auto-generated or custom)
- [ ] Invite links for sharing group DMs
- [ ] Pin messages in group DMs
- [ ] Group DM media gallery
- [ ] Admin role management (promote/demote)
- [ ] Mute notifications per group
- [ ] Group DM search functionality
- [ ] SignalR notifications for real-time member updates
- [ ] Comprehensive unit and integration tests
