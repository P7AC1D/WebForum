# Web Forum API

A RESTful API backend for a web forum built with ASP.NET Core, Entity Framework Core, and PostgreSQL.

## Features

- **User Authentication**: JWT-based authentication with refresh tokens
- **User Management**: User registration, profile management, and role-based access
- **Posts**: Create and retrieve forum posts with optional tagging
- **Comments**: Comment on posts with threaded discussions
- **Likes**: Like/unlike posts and comments
- **Moderation**: Content moderation with post tagging and management
- **Interactive Documentation**: Scalar UI for API exploration

## Tech Stack

- **Backend**: ASP.NET Core (.NET 9)
- **Database**: PostgreSQL 15
- **ORM**: Entity Framework Core
- **Authentication**: JWT Bearer tokens with BCrypt password hashing
- **Documentation**: OpenAPI/Swagger with Scalar UI
- **Containerization**: Docker & Docker Compose
- **Validation**: FluentValidation
- **Testing**: xUnit (Unit & Integration tests)

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)

### 1. Clone the Repository

```bash
git clone <repository-url>
cd web-forum
```

### 2. Start the Database

#### Windows (PowerShell)
```powershell
.\db.ps1 start
```

#### Linux/macOS (Bash)
```bash
./db.sh start
```

This will:
- Start PostgreSQL and pgAdmin containers
- Wait for the database to be ready
- Automatically create initial migrations if none exist
- Apply all EF Core migrations to the database

### 3. Run the API

```bash
cd src/WebForum.Api
dotnet run
```

The API will be available at:
- **API**: https://localhost:7094 (or http://localhost:5163)
- **Scalar UI**: https://localhost:7094/scalar/v1
- **Swagger UI**: https://localhost:7094/swagger

### 4. Access pgAdmin (Optional)

- **URL**: http://localhost:5050
- **Email**: admin@webforum.com
- **Password**: admin123

## Database Management Scripts

Convenient scripts are provided for database operations:

### Commands Available

| Command   | Description |
|-----------|-------------|
| `start`   | Start PostgreSQL and apply migrations |
| `stop`    | Stop all services |
| `restart` | Restart the database |
| `reset`   | Reset database and migrations (⚠️ **deletes all data and migrations**) |
| `migrate` | Apply EF Core migrations (creates initial migration if none exist) |
| `logs`    | Show database logs |
| `connect` | Connect to database via psql |
| `backup`  | Create a database backup |
| `status`  | Show service status |

### Examples

```bash
# Start database with migrations
.\db.ps1 start          # Windows
./db.sh start           # Linux/macOS

# Apply migrations only (auto-creates initial migration if needed)
.\db.ps1 migrate        # Windows
./db.sh migrate         # Linux/macOS

# Reset database (careful!)
.\db.ps1 reset          # Windows
./db.sh reset           # Linux/macOS

# Check status
.\db.ps1 status         # Windows
./db.sh status          # Linux/macOS
```

## ✅ Verified Working Status

This API has been successfully tested and verified:

- ✅ **Database Setup**: PostgreSQL runs in Docker with automatic migration creation
- ✅ **Initial Migration**: Auto-created on first run if no migrations exist
- ✅ **User Registration**: Working with JWT token generation
- ✅ **Authentication**: JWT-based auth with proper token validation
- ✅ **Flexible User Roles**: Supports string and integer role values during registration
- ✅ **API Documentation**: Scalar UI available at `/scalar/v1`
- ✅ **Database Management**: Convenient PowerShell and Bash scripts

### Quick Test

To verify the setup is working:

1. Start the database: `.\db.ps1 start`
2. Run the API: `cd src/WebForum.Api && dotnet run`
3. Test registration:
   ```bash
   curl -X POST http://localhost:5043/api/auth/register \
        -H "Content-Type: application/json" \
        -d '{"username":"testuser","email":"test@example.com","password":"password123","role":"User"}'
   ```

Expected response: JSON with JWT token and user information.

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register a new user (supports flexible role assignment)
- `POST /api/auth/login` - User login
- `POST /api/auth/refresh` - Refresh JWT token

### Users
- `GET /api/users/me` - Get current user profile
- `PUT /api/users/me` - Update current user profile
- `GET /api/users/me/posts` - Get current user's posts

### Posts
- `GET /api/posts` - Get all posts (paginated)
- `GET /api/posts/{id}` - Get specific post
- `POST /api/posts` - Create new post
- `GET /api/posts/{id}/comments` - Get post comments
- `POST /api/posts/{id}/comments` - Create new comment on a post
- `POST /api/posts/{id}/like` - Like a post
- `DELETE /api/posts/{id}/like` - Unlike a post

### Moderation
- `POST /api/moderation/posts/{id}/tag` - Tag a post
- `DELETE /api/moderation/posts/{id}/tag` - Remove tag from post

For detailed API documentation, visit the Scalar UI at `/scalar/v1` when the API is running.

## Development

### Project Structure

```
├── src/
│   └── WebForum.Api/           # Main API project
│       ├── Controllers/        # API controllers
│       ├── Data/              # Database context
│       ├── DTOs/              # Data transfer objects  
│       ├── Models/            # Entity models
│       ├── Services/          # Business logic services
│       │   ├── Interfaces/    # Service interfaces
│       │   └── Implementations/ # Service implementations
│       └── Migrations/        # EF Core migrations
├── tests/
│   ├── WebForum.UnitTests/    # Unit tests
│   └── WebForum.IntegrationTests/ # Integration tests
├── docs/                      # Documentation
├── docker-compose.yml         # Docker services
└── db.ps1 / db.sh            # Database management scripts
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/WebForum.UnitTests/

# Run integration tests only
dotnet test tests/WebForum.IntegrationTests/
```

### Entity Framework Migrations

The project includes automatic migration management. When you run `.\db.ps1 migrate` or `.\db.ps1 start`, the scripts will:

1. **Auto-detect**: Check if any migrations exist
2. **Auto-create**: Create an "InitialCreate" migration if none exist
3. **Auto-apply**: Apply all pending migrations to the database

Manual migration commands (if needed):

```bash
cd src/WebForum.Api

# Create new migration (only needed for schema changes)
dotnet ef migrations add MigrationName

# Apply migrations manually
dotnet ef database update

# Remove last migration (if not applied)
dotnet ef migrations remove

# List all migrations
dotnet ef migrations list
```

## Architecture

This project follows **SOLID principles** with:

- **Controllers**: Handle HTTP requests/responses, minimal logic
- **Services**: Contain business logic, implement interfaces
- **Models**: Entity definitions and DTOs
- **Repository Pattern**: Via Entity Framework Core DbContext
- **Dependency Injection**: For loose coupling and testability

### Key Design Decisions

1. **Strict RESTful API**: No edit/delete for posts and comments (forum best practice)
2. **JWT Authentication**: Stateless authentication with refresh tokens
3. **Service Layer**: Clean separation of concerns from controllers
4. **Validation**: FluentValidation for robust input validation
5. **Error Handling**: Consistent error responses across the API
6. **Security**: BCrypt password hashing, JWT token security
7. **Automatic Migrations**: Database schema automatically managed on startup
8. **Flexible Enums**: Support for both string and integer enum values in JSON

## Configuration

### Database Connection

The database connection string is configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=webforum;Username=postgres;Password=password"
  }
}
```

### JWT Settings

JWT configuration in `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "WebForum.Api",
    "Audience": "WebForum.Client",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  }
}
```