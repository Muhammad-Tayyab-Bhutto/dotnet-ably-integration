using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ably_rest_apis.src.Application.Abstractions.Messaging;
using ably_rest_apis.src.Application.Abstractions.Services;
using ably_rest_apis.src.Domain.Entities;
using ably_rest_apis.src.Domain.Enums;
using ably_rest_apis.src.Infrastructure.Persistence.DbContext;
using ably_rest_apis.src.Shared.Contracts;

namespace ably_rest_apis.src.Features.Sessions
{
    /// <summary>
    /// Session service implementation with race-condition safety
    /// </summary>
    public class SessionService : ISessionService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAblyPublisher _ablyPublisher;
        private readonly ILogger<SessionService> _logger;
        private static readonly SemaphoreSlim _sessionLock = new(1, 1);

        public SessionService(
            ApplicationDbContext dbContext,
            IAblyPublisher ablyPublisher,
            ILogger<SessionService> logger)
        {
            _dbContext = dbContext;
            _ablyPublisher = ablyPublisher;
            _logger = logger;
        }

        #region Session CRUD

        public async Task<Session> CreateSessionAsync(Session session, Guid createdById)
        {
            session.Id = Guid.NewGuid();
            session.CreatedById = createdById;
            session.CreatedAt = DateTime.UtcNow;

            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created session {SessionId} by user {UserId}", session.Id, createdById);
            return session;
        }

        public async Task<Session?> GetSessionAsync(Guid sessionId)
        {
            return await _dbContext.Sessions
                .Include(s => s.CreatedBy)
                .Include(s => s.Instances)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<List<Session>> GetAllSessionsAsync()
        {
            return await _dbContext.Sessions
                .Include(s => s.CreatedBy)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        #endregion

        #region Session Instance Operations

        public async Task<SessionInstance> StartSessionAsync(Guid sessionId, Guid adminId)
        {
            // Validate admin role
            var admin = await _dbContext.Users.FindAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can start sessions");

            // Validate session exists
            var session = await _dbContext.Sessions.FindAsync(sessionId);
            if (session == null)
                throw new KeyNotFoundException($"Session {sessionId} not found");

            // Check for existing active instance
            var existingInstance = await _dbContext.SessionInstances
                .FirstOrDefaultAsync(si => si.SessionId == sessionId && si.Status == SessionStatus.Active);

            if (existingInstance != null)
                throw new InvalidOperationException("Session already has an active instance");

            // Validate time window
            var now = DateTime.UtcNow;
            if (now < session.ScheduledStartTime.AddMinutes(-30))
                throw new InvalidOperationException("Cannot start session more than 30 minutes before scheduled time");

            // Create session instance
            var instance = new SessionInstance
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Status = SessionStatus.Active,
                StartedAt = now,
                StartedById = adminId
            };

            _dbContext.SessionInstances.Add(instance);

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.SESSION_STARTED,
                EmittedByUserId = adminId,
                EmittedByRole = Role.System,
                PayloadJson = "{}",
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.SESSION_STARTED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = adminId.ToString(), Role = "system" },
                Payload = new { },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Started session {SessionId} instance {InstanceId}", sessionId, instance.Id);
            return instance;
        }

        public async Task<SessionInstance> EndSessionAsync(Guid sessionId, Guid adminId)
        {
            var admin = await _dbContext.Users.FindAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can end sessions");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                throw new InvalidOperationException("No active session instance found");

            var now = DateTime.UtcNow;
            instance.Status = SessionStatus.Ended;
            instance.EndedAt = now;
            instance.EndedById = adminId;

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.SESSION_ENDED,
                EmittedByUserId = adminId,
                EmittedByRole = Role.System,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new SessionEndedPayload
                {
                    EndedBy = admin.Name,
                    Reason = "Session completed"
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.SESSION_ENDED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = adminId.ToString(), Role = "system" },
                Payload = new SessionEndedPayload { EndedBy = admin.Name, Reason = "Session completed" },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Ended session {SessionId}", sessionId);
            return instance;
        }

        public async Task<SessionInstance?> GetActiveInstanceAsync(Guid sessionId)
        {
            return await _dbContext.SessionInstances
                .Include(si => si.Participants)
                    .ThenInclude(p => p.User)
                .Include(si => si.Rooms)
                .FirstOrDefaultAsync(si => si.SessionId == sessionId && si.Status == SessionStatus.Active);
        }

        #endregion

        #region Participant Operations

        public async Task<SessionParticipant> JoinSessionAsync(Guid sessionId, Guid userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User {userId} not found");

            var session = await _dbContext.Sessions.FindAsync(sessionId);
            if (session == null)
                throw new KeyNotFoundException($"Session {sessionId} not found");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                throw new InvalidOperationException("Session is not active");

            // Check reporting window (for students)
            var now = DateTime.UtcNow;
            if (user.Role == Role.Student)
            {
                if (now < session.ReportingWindowStart || now > session.ReportingWindowEnd)
                    throw new InvalidOperationException("Outside reporting window");
            }

            // Check if already joined
            var existingParticipant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == userId);

            if (existingParticipant != null)
            {
                if (existingParticipant.IsKicked)
                    throw new InvalidOperationException("You have been removed from this session");

                // Reconnect
                existingParticipant.IsConnected = true;
                existingParticipant.LeftAt = null;
                await _dbContext.SaveChangesAsync();
                return existingParticipant;
            }

            // Create participant
            var participant = new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                UserId = userId,
                Role = user.Role,
                JoinedAt = now,
                IsConnected = true
            };

            _dbContext.SessionParticipants.Add(participant);

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.USER_JOINED,
                EmittedByUserId = userId,
                EmittedByRole = Role.System,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new UserJoinedPayload
                {
                    UserId = userId.ToString(),
                    Role = user.Role.ToString(),
                    Name = user.Name
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.USER_JOINED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = userId.ToString(), Role = "system" },
                Payload = new UserJoinedPayload
                {
                    UserId = userId.ToString(),
                    Role = user.Role.ToString(),
                    Name = user.Name
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
            return participant;
        }

        public async Task<bool> LeaveSessionAsync(Guid sessionId, Guid userId)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return false;

            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == userId);

            if (participant == null)
                return false;

            participant.LeftAt = DateTime.UtcNow;
            participant.IsConnected = false;
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DisconnectUserAsync(Guid sessionId, Guid userId)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return false;

            var participant = await _dbContext.SessionParticipants
                .Include(sp => sp.User)
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == userId);

            if (participant == null)
                return false;

            participant.IsConnected = false;
            var now = DateTime.UtcNow;

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.USER_DISCONNECTED,
                EmittedByUserId = userId,
                EmittedByRole = Role.System,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { UserId = userId.ToString() }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.USER_DISCONNECTED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = userId.ToString(), Role = "system" },
                Payload = new { UserId = userId.ToString(), Name = participant.User?.Name ?? "" },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            return true;
        }

        #endregion

        #region Break Request Operations

        public async Task<BreakRequest> RequestBreakAsync(Guid sessionId, Guid studentId, string? reason)
        {
            var student = await _dbContext.Users.FindAsync(studentId);
            if (student == null || student.Role != Role.Student)
                throw new UnauthorizedAccessException("Only students can request breaks");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                throw new InvalidOperationException("Session is not active");

            // Check if student is in session
            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == studentId);

            if (participant == null || participant.IsKicked)
                throw new InvalidOperationException("Student is not in this session");

            var now = DateTime.UtcNow;

            // Create break request
            var breakRequest = new BreakRequest
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                StudentId = studentId,
                Reason = reason,
                Status = BreakRequestStatus.Pending,
                RequestedAt = now
            };

            _dbContext.BreakRequests.Add(breakRequest);

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.BREAK_REQUESTED,
                EmittedByUserId = studentId,
                EmittedByRole = Role.Student,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new BreakRequestPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    Reason = reason
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.BREAK_REQUESTED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = studentId.ToString(), Role = "student" },
                Payload = new BreakRequestPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    Reason = reason
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Break requested by student {StudentId} in session {SessionId}", studentId, sessionId);
            return breakRequest;
        }

        public async Task<BreakRequest> ApproveBreakAsync(Guid sessionId, Guid breakRequestId, Guid moderatorId)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || moderator.Role != Role.Moderator)
                throw new UnauthorizedAccessException("Only moderators can approve breaks");

            var breakRequest = await _dbContext.BreakRequests
                .Include(br => br.Student)
                .FirstOrDefaultAsync(br => br.Id == breakRequestId);

            if (breakRequest == null)
                throw new KeyNotFoundException("Break request not found");

            if (breakRequest.Status != BreakRequestStatus.Pending)
                throw new InvalidOperationException("Break request is not pending");

            var now = DateTime.UtcNow;
            breakRequest.Status = BreakRequestStatus.Approved;
            breakRequest.ApprovedById = moderatorId;
            breakRequest.ApprovedAt = now;

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = breakRequest.SessionInstanceId,
                Type = EventType.BREAK_APPROVED,
                EmittedByUserId = moderatorId,
                EmittedByRole = Role.Moderator,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new BreakApprovedPayload
                {
                    StudentId = breakRequest.StudentId.ToString(),
                    ApprovedBy = moderator.Name
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.BREAK_APPROVED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = "moderator" },
                Payload = new BreakApprovedPayload
                {
                    StudentId = breakRequest.StudentId.ToString(),
                    ApprovedBy = moderator.Name
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Break approved for student {StudentId} by moderator {ModeratorId}", 
                breakRequest.StudentId, moderatorId);
            return breakRequest;
        }

        public async Task<BreakRequest> DenyBreakAsync(Guid sessionId, Guid breakRequestId, Guid moderatorId, string reason)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || moderator.Role != Role.Moderator)
                throw new UnauthorizedAccessException("Only moderators can deny breaks");

            var breakRequest = await _dbContext.BreakRequests.FindAsync(breakRequestId);
            if (breakRequest == null)
                throw new KeyNotFoundException("Break request not found");

            if (breakRequest.Status != BreakRequestStatus.Pending)
                throw new InvalidOperationException("Break request is not pending");

            breakRequest.Status = BreakRequestStatus.Denied;
            breakRequest.DenialReason = reason;
            await _dbContext.SaveChangesAsync();

            return breakRequest;
        }

        public async Task<List<BreakRequest>> GetPendingBreakRequestsAsync(Guid sessionId)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return new List<BreakRequest>();

            return await _dbContext.BreakRequests
                .Include(br => br.Student)
                .Where(br => br.SessionInstanceId == instance.Id && br.Status == BreakRequestStatus.Pending)
                .OrderBy(br => br.RequestedAt)
                .ToListAsync();
        }

        #endregion

        #region Flag Operations

        public async Task<Flag> FlagUserAsync(Guid sessionId, Guid studentId, Guid assessorId, string reason)
        {
            var assessor = await _dbContext.Users.FindAsync(assessorId);
            if (assessor == null || assessor.Role != Role.Assessor)
                throw new UnauthorizedAccessException("Only assessors can flag users");

            var student = await _dbContext.Users.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Student not found");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                throw new InvalidOperationException("Session is not active");

            var now = DateTime.UtcNow;

            var flag = new Flag
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                StudentId = studentId,
                FlaggedById = assessorId,
                Reason = reason,
                CreatedAt = now
            };

            _dbContext.Flags.Add(flag);

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.FLAG_USER,
                EmittedByUserId = assessorId,
                EmittedByRole = Role.Assessor,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new FlagUserPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    Reason = reason,
                    FlaggedBy = assessor.Name
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.FLAG_USER.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = assessorId.ToString(), Role = "assessor" },
                Payload = new FlagUserPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    Reason = reason,
                    FlaggedBy = assessor.Name
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Student {StudentId} flagged by assessor {AssessorId} in session {SessionId}", 
                studentId, assessorId, sessionId);
            return flag;
        }

        public async Task<Flag> EscalateFlagAsync(Guid sessionId, Guid flagId, Guid moderatorId)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || moderator.Role != Role.Moderator)
                throw new UnauthorizedAccessException("Only moderators can escalate flags");

            var flag = await _dbContext.Flags
                .Include(f => f.Student)
                .FirstOrDefaultAsync(f => f.Id == flagId);

            if (flag == null)
                throw new KeyNotFoundException("Flag not found");

            if (flag.IsEscalated)
                throw new InvalidOperationException("Flag is already escalated");

            var now = DateTime.UtcNow;
            flag.IsEscalated = true;
            flag.EscalatedById = moderatorId;
            flag.EscalatedAt = now;

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = flag.SessionInstanceId,
                Type = EventType.FLAG_ESCALATED,
                EmittedByUserId = moderatorId,
                EmittedByRole = Role.Moderator,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new FlagEscalatedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    FlagId = flagId.ToString(),
                    EscalatedBy = moderator.Name
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(sessionEvent);

            await _dbContext.SaveChangesAsync();

            // Publish to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = sessionEvent.Id.ToString(),
                Type = EventType.FLAG_ESCALATED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = "moderator" },
                Payload = new FlagEscalatedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    FlagId = flagId.ToString(),
                    EscalatedBy = moderator.Name
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Flag {FlagId} escalated by moderator {ModeratorId}", flagId, moderatorId);
            return flag;
        }

        public async Task<List<Flag>> GetActiveFlagsAsync(Guid sessionId)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return new List<Flag>();

            return await _dbContext.Flags
                .Include(f => f.Student)
                .Include(f => f.FlaggedBy)
                .Where(f => f.SessionInstanceId == instance.Id && !f.IsResolved)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        #endregion

        #region Room Operations

        public async Task<Room> CallNextStudentsAsync(Guid sessionId, Guid assessorId, List<Guid> studentIds)
        {
            // Use lock for race-condition safety
            await _sessionLock.WaitAsync();
            try
            {
                var assessor = await _dbContext.Users.FindAsync(assessorId);
                if (assessor == null || assessor.Role != Role.Assessor)
                    throw new UnauthorizedAccessException("Only assessors can call next students");

                var instance = await GetActiveInstanceAsync(sessionId);
                if (instance == null)
                    throw new InvalidOperationException("Session is not active");

                // Validate students are available
                var participants = await _dbContext.SessionParticipants
                    .Include(sp => sp.User)
                    .Where(sp => sp.SessionInstanceId == instance.Id && 
                                 studentIds.Contains(sp.UserId) &&
                                 sp.CurrentRoomId == null &&
                                 !sp.IsKicked)
                    .ToListAsync();

                if (participants.Count != studentIds.Count)
                    throw new InvalidOperationException("Some students are not available");

                var now = DateTime.UtcNow;
                var roomNumber = await _dbContext.Rooms.CountAsync(r => r.SessionInstanceId == instance.Id) + 1;

                // Create room
                var room = new Room
                {
                    Id = Guid.NewGuid(),
                    SessionInstanceId = instance.Id,
                    Name = $"Room {roomNumber}",
                    CreatedAt = now,
                    IsActive = true
                };
                _dbContext.Rooms.Add(room);

                // Assign participants to room
                foreach (var participant in participants)
                {
                    participant.CurrentRoomId = room.Id;
                }

                // Get/create assessor participant and assign to room
                var assessorParticipant = await _dbContext.SessionParticipants
                    .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == assessorId);

                if (assessorParticipant != null)
                {
                    assessorParticipant.CurrentRoomId = room.Id;
                }

                var participantIds = participants.Select(p => p.UserId.ToString()).ToList();
                participantIds.Add(assessorId.ToString());

                // Create CALL_NEXT_STUDENTS event
                var callEvent = new SessionEvent
                {
                    Id = Guid.NewGuid(),
                    SessionInstanceId = instance.Id,
                    Type = EventType.CALL_NEXT_STUDENTS,
                    EmittedByUserId = assessorId,
                    EmittedByRole = Role.Assessor,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new CallNextStudentsPayload
                    {
                        StudentIds = studentIds.Select(s => s.ToString()).ToList(),
                        RoomId = room.Id.ToString(),
                        AssessorId = assessorId.ToString()
                    }),
                    Timestamp = now,
                    IsPublished = false
                };
                _dbContext.SessionEvents.Add(callEvent);

                // Create ROOM_CREATED event
                var roomEvent = new SessionEvent
                {
                    Id = Guid.NewGuid(),
                    SessionInstanceId = instance.Id,
                    Type = EventType.ROOM_CREATED,
                    EmittedByUserId = assessorId,
                    EmittedByRole = Role.System,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new RoomCreatedPayload
                    {
                        RoomId = room.Id.ToString(),
                        RoomName = room.Name,
                        Participants = participantIds
                    }),
                    Timestamp = now,
                    IsPublished = false
                };
                _dbContext.SessionEvents.Add(roomEvent);

                await _dbContext.SaveChangesAsync();

                // Publish ROOM_CREATED to Ably
                var ablyEvent = new SessionEventDto
                {
                    EventId = roomEvent.Id.ToString(),
                    Type = EventType.ROOM_CREATED.ToString(),
                    SessionId = sessionId.ToString(),
                    EmittedBy = new EmittedByDto { UserId = assessorId.ToString(), Role = "system" },
                    Payload = new RoomCreatedPayload
                    {
                        RoomId = room.Id.ToString(),
                        RoomName = room.Name,
                        Participants = participantIds
                    },
                    Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
                };

                var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
                if (published)
                {
                    roomEvent.IsPublished = true;
                    callEvent.IsPublished = true;
                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Room {RoomId} created with {ParticipantCount} participants", 
                    room.Id, participantIds.Count);
                return room;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task<List<Room>> GetActiveRoomsAsync(Guid sessionId)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return new List<Room>();

            return await _dbContext.Rooms
                .Include(r => r.Participants)
                    .ThenInclude(p => p.User)
                .Where(r => r.SessionInstanceId == instance.Id && r.IsActive)
                .ToListAsync();
        }

        #endregion
    }
}
