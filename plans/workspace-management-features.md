# Workspace Management Features Plan

## Overview
Add comprehensive management features to the workspace management dialog, enabling owners to fully manage their workspaces and members, while providing appropriate actions for non-owner members.

## Current State Analysis

### Existing Components
- **WorkspaceSettingsModal.razor**: Has two tabs (General, Invite Users)
  - General tab: Shows workspace name (read-only), disabled rename button
  - Invite Users tab: Allows inviting users by username
- **WorkspaceController.cs**: Endpoints for get, create, invite, get members
- **WorkspaceMember entity**: Id, WorkspaceId, UserId, Role, JoinedAt
- **WorkspaceDto**: Id, Name, OwnerId, MemberCount
- **UserStatusDto**: UserId, Username, Status

### Current Limitations
- Cannot remove members from workspace
- Cannot rename workspace
- Cannot delete workspace
- Cannot leave workspace (for non-owners)
- Cannot transfer ownership
- No visibility into member roles
- No member management interface

## Proposed Features

### 1. Members Management Tab (Owner Only)
**Purpose**: View and manage all workspace members

**Features**:
- Display list of all members with:
  - Username
  - Role (Owner, Member)
  - Join date
  - Online status indicator
- Actions for each member:
  - Remove member (with confirmation)
  - Change role (Member ↔ Owner) - optional
- Visual distinction for current user
- Search/filter members
- Refresh member list

**UI Layout**:
```
┌─────────────────────────────────────────┐
│ Members Management                      │
├─────────────────────────────────────────┤
│ [Search members...]                     │
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │ 👤 john_doe (Owner) • Online        │ │
│ │    Joined: Dec 28, 2024             │ │
│ │    [You]                            │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │ 👤 jane_smith (Member) • Offline    │ │
│ │    Joined: Dec 29, 2024             │ │
│ │    [Remove] [Make Owner]            │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

### 2. Enhanced General Tab (Owner Only)
**Purpose**: Manage workspace basic settings

**Features**:
- Enable workspace rename functionality
- Add workspace description field
- Display workspace statistics:
  - Total members
  - Total channels
  - Created date
- Add "Delete Workspace" button (danger zone)
- Add "Transfer Ownership" button

**UI Layout**:
```
┌─────────────────────────────────────────┐
│ General Settings                        │
├─────────────────────────────────────────┤
│ Workspace Name                          │
│ [My Workspace____________] [Rename]     │
│                                         │
│ Description                             │
│ [Team collaboration space_______]      │
│                                         │
│ Statistics                              │
│ • Members: 5                            │
│ • Channels: 3                           │
│ • Created: Dec 28, 2024                 │
│                                         │
│ Danger Zone                             │
│ [Transfer Ownership]                    │
│ [Delete Workspace]                      │
└─────────────────────────────────────────┘
```

### 3. Member View Tab (Non-Owner)
**Purpose**: Allow non-owners to view members and leave workspace

**Features**:
- View all members (read-only)
- See member roles
- See online status
- "Leave Workspace" button (with confirmation)

**UI Layout**:
```
┌─────────────────────────────────────────┐
│ Workspace Members                       │
├─────────────────────────────────────────┤
│ 👤 john_doe (Owner) • Online            │
│    Joined: Dec 28, 2024                 │
│                                         │
│ 👤 jane_smith (Member) • Offline        │
│    Joined: Dec 29, 2024                 │
│                                         │
│ 👤 you (Member) • Online                │
│    Joined: Dec 29, 2024                 │
│                                         │
│ [Leave Workspace]                       │
└─────────────────────────────────────────┘
```

## Technical Implementation

### New DTOs Required

#### WorkspaceMemberDto.cs
```csharp
public class WorkspaceMemberDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; }
    public bool IsOnline { get; set; }
    public bool IsCurrentUser { get; set; }
}
```

#### RenameWorkspaceRequest.cs
```csharp
public class RenameWorkspaceRequest
{
    public int WorkspaceId { get; set; }
    public string NewName { get; set; } = string.Empty;
}
```

#### UpdateWorkspaceDescriptionRequest.cs
```csharp
public class UpdateWorkspaceDescriptionRequest
{
    public int WorkspaceId { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

#### RemoveMemberRequest.cs
```csharp
public class RemoveMemberRequest
{
    public int WorkspaceId { get; set; }
    public int UserId { get; set; }
}
```

#### TransferOwnershipRequest.cs
```csharp
public class TransferOwnershipRequest
{
    public int WorkspaceId { get; set; }
    public int NewOwnerId { get; set; }
}
```

#### UpdateMemberRoleRequest.cs
```csharp
public class UpdateMemberRoleRequest
{
    public int WorkspaceId { get; set; }
    public int UserId { get; set; }
    public string NewRole { get; set; } = "Member";
}
```

### API Endpoints to Add

#### WorkspaceController.cs

```csharp
// GET api/workspace/{workspaceId}/members-detailed
// Returns detailed member information with roles and join dates
[HttpGet("{workspaceId}/members-detailed")]
public async Task<ActionResult<List<WorkspaceMemberDto>>> GetWorkspaceMembersDetailed(int workspaceId)

// PUT api/workspace/{workspaceId}/rename
// Rename workspace (owner only)
[HttpPut("{workspaceId}/rename")]
public async Task<IActionResult> RenameWorkspace(int workspaceId, RenameWorkspaceRequest request)

// PUT api/workspace/{workspaceId}/description
// Update workspace description (owner only)
[HttpPut("{workspaceId}/description")]
public async Task<IActionResult> UpdateDescription(int workspaceId, UpdateWorkspaceDescriptionRequest request)

// DELETE api/workspace/{workspaceId}/members/{userId}
// Remove member from workspace (owner only)
[HttpDelete("{workspaceId}/members/{userId}")]
public async Task<IActionResult> RemoveMember(int workspaceId, int userId)

// DELETE api/workspace/{workspaceId}/leave
// Leave workspace (member only)
[HttpDelete("{workspaceId}/leave")]
public async Task<IActionResult> LeaveWorkspace(int workspaceId)

// DELETE api/workspace/{workspaceId}
// Delete workspace (owner only)
[HttpDelete("{workspaceId}")]
public async Task<IActionResult> DeleteWorkspace(int workspaceId)

// POST api/workspace/{workspaceId}/transfer-ownership
// Transfer ownership to another member (owner only)
[HttpPost("{workspaceId}/transfer-ownership")]
public async Task<IActionResult> TransferOwnership(int workspaceId, TransferOwnershipRequest request)

// PUT api/workspace/{workspaceId}/members/{userId}/role
// Update member role (owner only)
[HttpPut("{workspaceId}/members/{userId}/role")]
public async Task<IActionResult> UpdateMemberRole(int workspaceId, int userId, UpdateMemberRoleRequest request)
```

### WorkspaceService.cs Updates

Add new methods:
```csharp
public async Task<List<WorkspaceMemberDto>> GetWorkspaceMembersDetailedAsync(int workspaceId)
public async Task<bool> RenameWorkspaceAsync(int workspaceId, string newName)
public async Task<bool> UpdateWorkspaceDescriptionAsync(int workspaceId, string description)
public async Task<bool> RemoveMemberAsync(int workspaceId, int userId)
public async Task<bool> LeaveWorkspaceAsync(int workspaceId)
public async Task<bool> DeleteWorkspaceAsync(int workspaceId)
public async Task<bool> TransferOwnershipAsync(int workspaceId, int newOwnerId)
public async Task<bool> UpdateMemberRoleAsync(int workspaceId, int userId, string newRole)
```

### WorkspaceSettingsModal.razor Enhancements

#### New Tabs Structure
- **Owner Tabs**: General, Invite Users, Members, Danger Zone
- **Member Tabs**: Members (read-only)

#### Tab Content Updates

**General Tab (Owner)**:
- Enable rename input and button
- Add description field
- Add workspace statistics
- Add "Transfer Ownership" button
- Add "Delete Workspace" button (in danger zone)

**Members Tab (Owner)**:
- Display member list with roles
- Show online status
- Add remove button for each member
- Add role change button for each member
- Add search/filter
- Highlight current user

**Members Tab (Non-Owner)**:
- Display member list (read-only)
- Show roles and online status
- Add "Leave Workspace" button

**Invite Users Tab**:
- Keep existing functionality
- Add success/error feedback

### Database Changes

#### Update Workspace Entity
Add Description field:
```csharp
public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Channel> Channels { get; set; } = new List<Channel>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

#### Migration Required
Create migration to add Description and CreatedAt fields to Workspace table.

## Implementation Order

1. **Backend - DTOs**: Create new DTOs for workspace management operations
2. **Backend - Database**: Add Description and CreatedAt to Workspace entity, create migration
3. **Backend - API**: Add new endpoints to WorkspaceController
4. **Frontend - Service**: Update WorkspaceService with new methods
5. **Frontend - Modal**: Enhance WorkspaceSettingsModal with new tabs and features
6. **Frontend - Integration**: Update ChannelList to handle workspace events (member removed, workspace deleted, etc.)
7. **Testing**: Test all new features

## Security Considerations

- All owner-only endpoints must verify ownership
- Non-owners cannot remove other members
- Owner cannot remove themselves (must transfer ownership first)
- Owner cannot delete workspace without confirmation
- Transfer ownership requires confirmation
- Leave workspace requires confirmation
- Rate limiting on invite/remove operations

## User Experience Considerations

- Clear visual distinction between owner and member actions
- Confirmation dialogs for destructive actions (delete, remove, leave, transfer)
- Loading states for async operations
- Error messages for failed operations
- Success notifications for completed operations
- Refresh member list after changes
- Redirect to home page after leaving/deleting workspace

## Future Enhancements (Out of Scope)

- Member permissions system (read-only, admin, etc.)
- Workspace templates
- Workspace analytics
- Member activity logs
- Bulk member operations
- Member invitation links
- Workspace export/import