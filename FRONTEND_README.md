# Session Management Frontend Integration Guide

## Overview
This document provides guidance for integrating a frontend application with the Session Management API.

---

## API Base URL
```
http://localhost:5000/api
```

---

## Authentication
Include user ID in request headers:
```
X-User-Id: {user-guid}
```

---

## API Endpoints

### Session Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/sessions` | Create session with room config |
| GET | `/sessions` | Get all sessions |
| GET | `/sessions/{id}` | Get session by ID |
| PUT | `/sessions/{id}` | Update session (Admin) |
| DELETE | `/sessions/{id}` | Delete session (Admin) |
| POST | `/sessions/{id}/start` | Start session (Admin) → Ably notification |
| POST | `/sessions/{id}/end` | End session (Admin) → Ably notification |
| POST | `/sessions/{id}/join` | Join session |

### Participant Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/sessions/{id}/participants` | Get all participants |
| GET | `/sessions/{id}/participants?status=Waiting` | Filter by status |
| GET | `/sessions/{id}/waiting-students` | Get waiting students |
| POST | `/sessions/{id}/kick` | Kick student (Moderator) |

### Break Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/sessions/{id}/break-request` | Request break (Student) |
| POST | `/sessions/{id}/break-approve` | Approve break (Moderator) |
| GET | `/sessions/{id}/break-requests` | Get pending breaks |
| POST | `/sessions/{id}/return-from-break` | Return from break |

### Flag Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/sessions/{id}/flag` | Flag student (Assessor) |
| POST | `/sessions/{id}/moderator-flag` | Flag student (Moderator) |
| POST | `/sessions/{id}/flag/accept` | Accept flag → auto-kick |
| POST | `/sessions/{id}/flag/reject` | Reject flag |
| GET | `/sessions/{id}/flags` | Get active flags |

### Room Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/sessions/{id}/rooms` | Get active rooms |
| POST | `/sessions/{id}/call-next` | Call students to room |

---

## Request/Response Examples

### Create Session
```json
POST /api/sessions
Header: X-User-Id: {admin-guid}

{
  "name": "Assessment Session 1",
  "description": "Morning assessment",
  "scheduledStartTime": "2024-12-25T09:00:00Z",
  "scheduledEndTime": "2024-12-25T12:00:00Z",
  "reportingWindowStart": "2024-12-25T08:30:00Z",
  "reportingWindowEnd": "2024-12-25T09:30:00Z",
  "maxStudentsPerRoom": 5,
  "numberOfRooms": 3
}
```

### Join Session
```json
POST /api/sessions/{sessionId}/join

{
  "userId": "{student-guid}"
}

// Response shows assigned status
{
  "success": true,
  "data": {
    "id": "...",
    "status": "Waiting" | "InRoom",
    ...
  }
}
```

### Accept Flag (Auto-kicks student)
```json
POST /api/sessions/{sessionId}/flag/accept

{
  "flagId": "{flag-guid}",
  "moderatorId": "{moderator-guid}"
}
```

---

## Real-Time Events (Ably)

### Channel Pattern
```
session:{sessionId}
```

### Event Types
| Event | Description |
|-------|-------------|
| `SESSION_STARTED` | Session has started |
| `SESSION_ENDED` | Session has ended |
| `USER_JOINED` | User joined session |
| `STUDENT_WAITING` | Student added to waiting queue |
| `CALLED_TO_ROOM` | Student called to a room |
| `USER_KICKED` | Student was kicked |
| `BREAK_REQUESTED` | Student requested break |
| `BREAK_APPROVED` | Break was approved |
| `RETURNED_FROM_BREAK` | Student returned from break |
| `FLAG_USER` | Student was flagged |
| `FLAG_ACCEPTED` | Flag accepted, student kicked |
| `FLAG_REJECTED` | Flag rejected |
| `ROOM_CREATED` | New room created |

### Event Payload Structure
```json
{
  "eventId": "guid",
  "type": "EVENT_TYPE",
  "sessionId": "guid",
  "emittedBy": {
    "userId": "guid",
    "role": "admin|moderator|assessor|student|system"
  },
  "payload": { ... },
  "timestamp": 1703433600
}
```

---

## Participant Statuses
| Status | Description |
|--------|-------------|
| `Waiting` | In waiting queue, no room assigned |
| `InRoom` | Currently in an assessment room |
| `OnBreak` | On approved break |
| `Left` | Voluntarily left session |
| `Kicked` | Removed from session (cannot rejoin) |

---

## Role Permissions
-----------------------------------------------------------------
| Action                | Admin | Moderator | Assessor | Student |
|--------|:-----:       |:-----:|:--------:|:-------:|
| Create Session        | ✅  :| ❌       | ❌       | ❌       |
| Start/End Session     | ✅   | ❌       | ❌       | ❌       |
| Join Session          | ✅   | ✅       | ✅       | ✅       |
| View Waiting Students | ✅   | ✅       | ✅       | ❌       |
| Call Students to Room | ❌   | ❌       | ✅       | ❌       |
| Flag Student          | ❌   | ✅       | ✅       | ❌       |
| Accept/Reject Flag    | ❌   | ✅       | ❌       | ❌       |
| Kick Student          | ✅   | ✅       | ❌       | ❌       |
| Request Break         | ❌   | ❌       | ❌       | ✅       |
| Approve Break         | ❌   | ✅       | ❌       | ❌       |           
-------------------------------------------------------------------
---

## Frontend Implementation Tips

1. **Subscribe to Ably channel** on session join
2. **Update UI reactively** based on real-time events
3. **Handle kicked status** - show message and prevent rejoin
4. **Show waiting count** to assessors for calling students
5. **Disable actions** based on current participant status
