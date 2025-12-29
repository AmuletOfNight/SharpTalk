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
- [ ] **Domain & Data Layer**
    - [x] Define EF Core Entities (User, Workspace, WorkspaceMember, Channel, Message)
    - [x] Add remaining entities (ChannelMember, Attachment, Reaction)
    - [x] Implement Identity (Sign Up, Login, JWT)
    - [x] Create PostgreSQL Migrations (Initial)
- [ ] **Core API Implementation**
    - [x] Workspace Management (Create, List, Join/Invite)
    - [x] Channel Management (Create, List)
    - [x] Functional Private Channels (requires ChannelMember)
- [ ] **Real-time Messaging (Backend)**
    - [ ] Configure SignalR Hubs
    - [ ] Implement Message sending logic (Persistence + Broadcast)
- [x] **Frontend Foundation (Blazor)**
    - [x] Set up MainLayout with Sidebar
    - [x] Implement Auth State Provider (JWT handling)
    - [x] Create basic Chat Interface Components

## Phase 2: The "Slack" Feel (Enhanced UX)
- [ ] **Direct Messages**
    - [ ] 1:1 DM support
    - [ ] Group DM support
- [ ] **Presence & Indicators**
    - [ ] Online/Offline Status (Connection tracking with Redis)
    - [ ] Typing Indicators ("User is typing...")
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

## Current Task
- [ ] **Real-time Messaging (Backend)**
    - [ ] Configure SignalR Hubs
    - [ ] Implement Message sending logic (Persistence + Broadcast)


