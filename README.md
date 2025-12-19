# .NET Ably Integration - Exam Session Management API

A .NET backend API that integrates with Ably for real-time exam session coordination across Admin, Moderator, Assessor, and Student roles.

## Features

- **Backend-controlled events** - Clients request, backend decides
- **Race-condition safety** - SemaphoreSlim for room creation
- **Complete audit trail** - All events stored in database
- **Role-based authorization** - Validated at service layer
- **Ably integration** - Real-time event publishing
- **Standard event contract** - Auditable, replayable, debuggable

## API Endpoints

| Method | Endpoint | Role | Description |
|--------|----------|------|-------------|
| POST | `/api/sessions/{id}/start` | Admin | Start a session |
| POST | `/api/sessions/{id}/join` | Any | Join a session |
| POST | `/api/sessions/{id}/break-request` | Student | Request a break |
| POST | `/api/sessions/{id}/break-approve` | Moderator | Approve a break |
| POST | `/api/sessions/{id}/flag` | Assessor | Flag a student |
| POST | `/api/sessions/{id}/flag/escalate` | Moderator | Escalate a flag |
| POST | `/api/sessions/{id}/call-next` | Assessor | Call next students |
| POST | `/api/sessions/{id}/end` | Admin | End a session |

## Event Types

- `SESSION_STARTED` - Admin starts session
- `USER_JOINED` - User joins session
- `BREAK_REQUESTED` / `BREAK_APPROVED`
- `FLAG_USER` / `FLAG_ESCALATED`
- `ROOM_CREATED` - Room assigned
- `SESSION_ENDED` - Session closed

## Configuration

Update `src/Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=techsol360;User=root;Password=YOUR_PASSWORD;"
  },
  "Ably": {
    "ApiKey": "YOUR_ABLY_API_KEY"
  }
}
```

## Running

```bash
dotnet run --urls "http://localhost:5000"
```

- Swagger UI: http://localhost:5000/swagger
- Health Check: http://localhost:5000/health

## Tech Stack

- .NET 10.0
- Entity Framework Core with MySQL (Pomelo)
- Ably.IO SDK
- Swashbuckle (Swagger)
