# SharpTalk

A modern, real-time team collaboration platform built with .NET, designed to facilitate seamless communication through persistent chat rooms, direct messages, and private groups. Inspired by Slack and Discord, SharpTalk provides a clean, intuitive interface for team collaboration.

## 🌟 Project Overview

SharpTalk is a multi-tenant collaboration tool organized around **Workspaces**, where teams can communicate through **Channels**, share files, and collaborate in real-time. The platform supports:

- **Multi-Workspace Architecture**: Users can belong to multiple independent workspaces
- **Real-Time Communication**: Instant message delivery using SignalR WebSockets
- **Role-Based Access Control**: Owner, Admin, and Member roles with appropriate permissions
- **Rich Messaging**: Support for markdown, reactions, and message threading
- **File Sharing**: Drag-and-drop file uploads with cloud storage integration
- **Presence Tracking**: Online/offline status and typing indicators

## 🛠 Tech Stack

### Backend
- **Framework**: ASP.NET Core 10 Web API
- **Real-Time**: SignalR (WebSocket-based communication)
- **Database**: PostgreSQL 15
- **ORM**: Entity Framework Core
- **Authentication**: JWT (JSON Web Tokens)
- **Caching/PubSub**: Redis (for presence tracking and SignalR scale-out)
- **File Storage**: Local storage (with Azure Blob Storage planned)

### Frontend
- **Framework**: Blazor WebAssembly
- **State Management**: Blazored.LocalStorage
- **Authentication**: ASP.NET Core Authentication State Provider
- **Styling**: Vanilla CSS with premium dark mode and glassmorphism design

### Infrastructure
- **Containerization**: Docker & Docker Compose
- **Database**: PostgreSQL 15 (Docker container)
- **Cache**: Redis (Docker container)

## 📋 Features

### ✅ Currently Implemented

#### Phase 1: Foundation (MVP)

**Authentication & Identity**
- ✅ User registration with email/password
- ✅ User login with JWT token generation
- ✅ Secure API endpoints with JWT authentication
- ✅ Custom authentication state provider for Blazor
- ✅ Case-insensitive email handling
- ✅ Automatic redirection for invalid sessions

**Workspace Management**
- ✅ Create and delete workspaces
- ✅ Rename workspaces and update descriptions
- ✅ Invite users and manage members
- ✅ Leave workspace functionality
- ✅ Workspace settings modal

**Channel Management**
- ✅ Create, list, and manage channels
- ✅ Public and private channel support
- ✅ Channel member tracking

**Real-Time Messaging**
- ✅ SignalR hub configuration with Redis backplane
- ✅ Instant message delivery and broadcasting
- ✅ Connection management and auto-reconnect

#### Phase 2: Enhanced UX (Partial)

**Direct Messaging**
- ✅ 1:1 private conversations (Global DMs)
- ✅ Group Direct Messages (Support for multiple members)
- ✅ Unified DM list across workspaces

**Presence & Activity**
- ✅ Real-time Online/Offline status tracking
- ✅ Integration with Redis for scalable presence
- ✅ Visual status indicators in UI

**User Experience**
- ✅ Modern "Slack-like" UI with Dark Mode
- ✅ User Settings and Profile Management
- ✅ Avatar uploading and cropping
- ✅ Responsive design for mobile/desktop

### 🚧 In Progress

**Notifications & Unread**
- ⏳ Unread message counters
- ⏳ Typing indicators
- ⏳ "Edited" message status

### 📅 Planned Features

#### Phase 3: Advanced Features

**File Sharing**
- File upload endpoint with validation
- Drag-and-drop file upload in UI
- Image and GIF auto-expansion in chat
- Cloud storage integration (Azure Blob Storage)

**Message Threading**
- Reply to specific messages
- Threaded conversation view
- Thread notifications

**Search Functionality**
- Global message search
- User search
- Quick Switcher (Ctrl+K) for navigation

**Future Enhancements**
- Workspace analytics
- Voice/video integration
- Third-party integrations

## 🏗 Architecture

### Project Structure

```
SharpTalk/
├── SharpTalk.Api/              # Backend Web API
│   ├── Controllers/            # API endpoints
│   │   ├── AuthController.cs
│   │   ├── ChannelController.cs
│   │   ├── MessageController.cs
│   │   ├── UserController.cs
│   │   └── WorkspaceController.cs
│   ├── Data/                   # Database context
│   │   └── ApplicationDbContext.cs
│   ├── Entities/               # EF Core entities
│   │   ├── Attachment.cs
│   │   ├── Channel.cs
│   │   ├── ChannelMember.cs
│   │   ├── Message.cs
│   │   ├── Reaction.cs
│   │   ├── User.cs
│   │   ├── Workspace.cs
│   │   └── WorkspaceMember.cs
│   ├── Hubs/                   # SignalR hubs
│   │   └── ChatHub.cs
│   ├── Migrations/             # Database migrations
│   └── Program.cs              # API startup configuration
│
├── SharpTalk.Web/              # Frontend Blazor WASM
│   ├── Auth/                   # Authentication
│   │   └── CustomAuthStateProvider.cs
│   ├── Layout/                 # Layout components
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── LandingLayout.razor
│   ├── Pages/                  # Page components
│   │   ├── Login.razor
│   │   ├── Register.razor
│   │   ├── Home.razor
│   │   └── Index.razor
│   ├── Services/               # Business logic services
│   │   ├── WorkspaceService.cs
│   │   ├── ChannelService.cs
│   │   ├── ChatService.cs
│   │   └── UserService.cs
│   ├── Shared/                 # Shared components
│   │   ├── Sidebar.razor
│   │   ├── ChannelList.razor
│   │   ├── ChatArea.razor
│   │   ├── CreateWorkspaceModal.razor
│   │   ├── CreateChannelModal.razor
│   │   ├── WorkspaceSettingsModal.razor
│   │   ├── UserSettingsModal.razor
│   │   └── UserProfile.razor
│   └── Program.cs              # Blazor startup configuration
│
├── SharpTalk.Shared/           # Shared DTOs and models
│   └── DTOs/                   # Data transfer objects
│       ├── AuthResponse.cs
│       ├── ChannelDto.cs
│       ├── CreateChannelRequest.cs
│       ├── CreateWorkspaceRequest.cs
│       ├── InviteUserRequest.cs
│       ├── LoginRequest.cs
│       ├── MessageDto.cs
│       ├── RegisterRequest.cs
│       ├── RemoveMemberRequest.cs
│       ├── RenameWorkspaceRequest.cs
│       ├── SendMessageRequest.cs
│       ├── TransferOwnershipRequest.cs
│       ├── UpdateMemberRoleRequest.cs
│       ├── UpdateWorkspaceDescriptionRequest.cs
│       ├── UserInfo.cs
│       ├── UserStatusDto.cs
│       ├── WorkspaceDto.cs
│       └── WorkspaceMemberDto.cs
│
├── plans/                      # Feature planning documents
│   ├── workspace-management-features.md
│   └── user-avatar-features.md
│
├── docker-compose.yml          # Docker services configuration
├── IMPLEMENTATION_PLAN.md      # Detailed implementation roadmap
├── Requirements Document.txt   # Original requirements specification
└── README.md                   # This file
```

### Database Schema

The application uses PostgreSQL with the following core entities:

- **User**: User accounts with authentication and profile data
- **Workspace**: Independent collaboration spaces
- **WorkspaceMember**: Join table linking users to workspaces with roles
- **Channel**: Communication channels within workspaces
- **ChannelMember**: Join table tracking channel membership
- **Message**: Chat messages with threading support
- **Attachment**: File attachments linked to messages
- **Reaction**: Emoji reactions on messages

### User Roles

- **Workspace Owner**: Creator of the workspace, can delete and manage all aspects
- **Workspace Admin**: Can manage users, channels, and moderation
- **Member**: Standard user, can send messages and join public channels
- **Guest** (Future): Restricted access to specific channels

## 🚀 Getting Started

### Prerequisites

- .NET 10 SDK
- Docker Desktop (for PostgreSQL and Redis)
- Git

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd SharpTalk
   ```

2. **Start infrastructure services**
   ```bash
   docker-compose up -d
   ```
   This starts:
   - PostgreSQL on port 5433
   - Redis on port 6379

3. **Configure connection strings**
   
   Update `SharpTalk.Api/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5433;Database=sharptalk;Username=postgres;Password=password"
     },
     "JwtSettings": {
       "Secret": "your-super-secret-key-at-least-32-characters-long",
       "Issuer": "SharpTalk",
       "Audience": "SharpTalkUsers"
     }
   }
   ```

4. **Run database migrations**
   ```bash
   cd SharpTalk.Api
   dotnet ef database update
   ```

5. **Start the API**
   ```bash
   dotnet run
   ```
   The API will be available at `http://localhost:5298`

6. **Start the Web client** (in a new terminal)
   ```bash
   cd SharpTalk.Web
   dotnet run
   ```
   The web application will be available at `http://localhost:5000`

### Development Setup

#### Running Tests
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

#### Creating New Migrations
```bash
cd SharpTalk.Api
dotnet ef migrations add MigrationName
dotnet ef database update
```

#### Building for Production
```bash
# Build API
cd SharpTalk.Api
dotnet publish -c Release

# Build Web
cd SharpTalk.Web
dotnet publish -c Release
```

## 📊 Current Implementation Status

### Phase 1: Foundation (MVP) - 85% Complete

- [x] Project setup and structure
- [x] Docker Compose configuration
- [x] EF Core entities and relationships
- [x] Database migrations
- [x] Authentication (JWT)
- [x] Workspace CRUD operations
- [x] Channel CRUD operations
- [x] Private channel support
- [x] Frontend layout and navigation
- [x] Authentication state management
- [ ] Real-time messaging (SignalR) - **In Progress**

### Phase 2: Enhanced UX - 0% Complete

- [ ] Direct Messages (1:1 and Group)
- [ ] Online/offline status tracking
- [ ] Typing indicators
- [ ] Unread message counters
- [ ] Message editing indicators

### Phase 3: Advanced Features - 0% Complete

- [ ] File upload and sharing
- [ ] Message threading
- [ ] Search functionality
- [ ] Workspace management enhancements
- [ ] User profile and avatar features

## 🔮 Future Roadmap

### Short Term (Next 1-2 months)
1. Complete real-time messaging implementation
2. Add presence tracking with Redis
3. Implement typing indicators
4. Add unread message counters
5. Implement message editing and deletion

### Medium Term (3-6 months)
1. Direct messaging (1:1 and group)
2. File upload and sharing
3. Message threading
4. Enhanced workspace management
5. User profile and avatar features

### Long Term (6+ months)
1. Global search functionality
2. Quick Switcher (Ctrl+K)
3. Notifications system
4. Workspace analytics
5. Mobile app (React Native or MAUI)
6. Voice/video integration
7. Integration with third-party services (GitHub, Jira, etc.)

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow C# coding conventions
- Write unit tests for new features
- Update documentation as needed
- Ensure all tests pass before submitting PR
- Follow the existing code style and structure

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- Inspired by Slack and Discord
- Built with Microsoft .NET ecosystem
- Uses SignalR for real-time communication
- Styled with modern CSS and glassmorphism design principles

## 📞 Support

For questions, issues, or suggestions:
- Open an issue on GitHub
- Check the [Requirements Document.txt](Requirements%20Document.txt) for detailed specifications
- Review the [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for development progress

## 🔗 Related Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor)
- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr)
- [Entity Framework Core Documentation](https://docs.microsoft.com/ef/core)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Redis Documentation](https://redis.io/documentation)

---

**Built with ❤️ using .NET 10**
