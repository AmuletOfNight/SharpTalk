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
- [x] **Direct Messages**
    - [x] 1:1 DM support (Global DMs across workspaces)
    - [ ] Group DM support
- [x] **Presence & Indicators**
    - [x] Online/Offline Status (Connection tracking with Redis)
    - [ ] Calling/Typing Indicators ("User is typing...")
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
- [ ] **Group Direct Messages**
    - [ ] Add `GroupDM` ChannelType enum value
    - [ ] Create Group DM endpoint with shared workspace validation
    - [ ] Create multi-select user modal in frontend
    - [ ] Update DM list to display Group DMs with stacked avatars
    - [ ] Add unit tests for Group DM functionality

