# SharpTalk Implementation Plan

## Project Overview
SharpTalk is a real-time team collaboration tool designed to facilitate communication through persistent chat rooms, direct messages, and private groups.

## Tech Stack
- **Backend**: ASP.NET Core 10 Web API + SignalR
- **Database**: PostgreSQL (EF Core)
- **Frontend**: Blazor WebAssembly
- **Infrastructure**: Docker, Redis
- **Styling**: Vanilla CSS (Premium, Dark Mode, Glassmorphism)

## Phase 1: The MVP (Foundation)
- [x] **Project Setup**
    - [x] Initialize Git repository
    - [x] Set up solution structure (Backend, Frontend, Shared)
    - [x] Configure Docker Compose (PostgreSQL, Redis)
- [x] **Domain & Data Layer**
    - [x] Define EF Core Entities (User, Workspace, WorkspaceMember, Channel, Message)
    - [x] Add remaining entities (ChannelMember, Attachment, Reaction)
    - [x] Implement Identity (Sign Up, Login, JWT)
    - [x] Create PostgreSQL Migrations (Initial)
- [x] **Core API Implementation**
    - [x] Workspace Management (Create, List, Join/Invite)
    - [x] Channel Management (Create, List)
    - [x] Functional Private Channels (requires ChannelMember)
- [x] **Real-time Messaging (Backend)**
    - [x] Configure SignalR Hubs
    - [x] Implement Message sending logic (Persistence + Broadcast)
- [x] **Frontend Foundation (Blazor)**
    - [x] Set up MainLayout with Sidebar
    - [x] Implement Auth State Provider (JWT handling)
    - [x] Create basic Chat Interface Components

## Phase 1.5: Stability & Refinements (Completed)
- [x] **Authentication & Security**
    - [x] Case-insensitive email handling
    - [x] Redirect non-existent users (invalid cookie handling)
    - [x] Fix logout functionality
- [x] **UI/UX Polish**
    - [x] Enhance User Settings UI (Consistency, Layout)
    - [x] Optimize Avatar Cropper UI
    - [x] Improve Loading Experience (Splash screen, Ghost loaders)
    - [x] Add visual boxes to Login/Register pages
- [x] **Infrastructure**
    - [x] Migrate to Blazor.LocalStorage (v9)

## Phase 2: The "Slack" Feel (Enhanced UX)
- [/] **Direct Messages**
    - [x] 1:1 DM support (Global DMs across workspaces)
    - [x] Group DM support (Feature Complete, Pending Tests)
- [x] **Presence & Indicators**
    - [x] Online/Offline Status (Connection tracking with Redis)
    - [x] Calling/Typing Indicators ("User is typing...")
- [ ] **Notifications & Unread**
    - [ ] Unread message counters per channel
    - [ ] "Edited" status for messages

## Phase 3: Advanced Features
- [ ] **File Sharing**
    - [ ] File upload endpoint
    - [ ] Drag-and-drop in UI
- [ ] **Threading**
    - [ ] Message replies/threads UI
- [ ] **Search**
    - [ ] Global search (Users, Messages)
    - [ ] Quick Switcher (Ctrl+K)
- [ ] **Configuration**
    - [ ] Move hardcoded values to appsettings.json

## Current Task
- [ ] **Group Direct Messages (Testing & Polish)**
    - [x] **Data Layer**
        - [x] Add `Group` value to ChannelType enum
        - [x] Create CreateGroupDMRequest DTO
        - [x] Create GroupMemberDto DTO
        - [x] Create AddGroupMemberRequest DTO
        - [x] Create RemoveGroupMemberRequest DTO
        - [x] Create UpdateGroupNameRequest DTO
        - [x] Update ChannelDto with group properties (Members, MemberCount, IsGroup)
        - [x] Add GroupDM constants to ChatConstants.cs
    - [x] **Backend - API**
        - [x] Add CreateGroupDM POST endpoint
        - [x] Add AddGroupMember POST endpoint
        - [x] Add RemoveGroupMember DELETE endpoint
        - [x] Add UpdateGroupName PUT endpoint
        - [x] Add GetGroupMembers GET endpoint
        - [x] Update GetDirectMessages to include group DMs
        - [x] Update SendMessage validation for group DMs
    - [x] **Backend - SignalR Hub**
        - [x] Add AddGroupMember method with notifications
        - [x] Add RemoveGroupMember method with notifications
        - [x] Add UpdateGroupName method with notifications
    - [x] **Frontend - Services**
        - [x] Add CreateGroupDMAsync method
        - [x] Add AddGroupMemberAsync method
        - [x] Add RemoveGroupMemberAsync method
        - [x] Add UpdateGroupNameAsync method
        - [x] Add GetGroupMembersAsync method
    - [x] **Frontend - Components**
        - [x] Create StartGroupDMModal with multi-select UI
        - [x] Create GroupDMSettingsModal for member management
        - [x] Create GroupAvatarStack component for stacked avatars
        - [x] Update DirectMessagesList to display group DMs
        - [x] Update ChatArea with group DM header and settings
    - [x] **Styling**
        - [x] Add CSS for stacked avatar display
        - [x] Add CSS for group DM list items
        - [x] Add CSS for multi-select user interface
    - [ ] **Testing**
        - [ ] Write unit tests for Group DM controller endpoints
        - [ ] Write unit tests for ChannelService methods
        - [ ] Write integration tests for SignalR group operations
    - [ ] **Documentation**
        - [ ] Update API documentation
        - [ ] Add user documentation

