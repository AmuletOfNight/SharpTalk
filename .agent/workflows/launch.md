---
description: How to launch the SharpTalk application
---

To launch the SharpTalk application with all its dependencies, follow these steps:

### 1. Start Infrastructure (PostgreSQL & Redis)
Ensure Docker is running on your system, then execute the following command in the root directory:

// turbo
```powershell
docker-compose up -d
```

### 2. Update the Database
If this is your first time running the app or you've made changes to the data model, apply the migrations:

// turbo
```powershell
dotnet ef database update --project SharpTalk.Api
```

### 3. Launch the Backend API
Navigate to the API project (or run from root) to start the backend:

// turbo
```powershell
dotnet run --project SharpTalk.Api
```
The API will be available at `http://localhost:5298`.

### 4. Launch the Frontend
Start the Blazor WebAssembly client:

// turbo
```powershell
dotnet run --project SharpTalk.Web
```
The application will be available at `http://localhost:5032` (or similar, check the output).

### Troubleshooting
- **Redis Connection**: If the API fails to start, ensure `sharptalk-redis` is running in Docker.
- **Database Connection**: Ensure the `sharptalk-postgres` container is healthy.
