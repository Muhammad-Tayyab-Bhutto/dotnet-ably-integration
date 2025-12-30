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

        public async Task<Session> UpdateSessionAsync(Guid sessionId, Session updatedSession, Guid adminId)
        {
            var admin = await _dbContext.Users.FindAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can update sessions");

            var session = await _dbContext.Sessions.FindAsync(sessionId);
            if (session == null)
                throw new KeyNotFoundException($"Session {sessionId} not found");

            // Check if session has an active instance
            var activeInstance = await _dbContext.SessionInstances
                .FirstOrDefaultAsync(si => si.SessionId == sessionId && si.Status == SessionStatus.Active);
            
            if (activeInstance != null)
                throw new InvalidOperationException("Cannot update a session that is currently active");

            // Update fields
            session.Name = updatedSession.Name;
            session.Description = updatedSession.Description;
            session.ScheduledStartTime = updatedSession.ScheduledStartTime;
            session.ScheduledEndTime = updatedSession.ScheduledEndTime;
            session.ReportingWindowStart = updatedSession.ReportingWindowStart;
            session.ReportingWindowEnd = updatedSession.ReportingWindowEnd;
            session.MaxStudentsPerRoom = updatedSession.MaxStudentsPerRoom;
            session.NumberOfRooms = updatedSession.NumberOfRooms;
            // Update assigned users
            session.AssignedStudentIds = updatedSession.AssignedStudentIds;
            session.AssignedAssessorIds = updatedSession.AssignedAssessorIds;
            session.AssignedModeratorIds = updatedSession.AssignedModeratorIds;
            session.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated session {SessionId} by admin {AdminId}", sessionId, adminId);
            return session;
        }

        public async Task<bool> DeleteSessionAsync(Guid sessionId, Guid adminId)
        {
            var admin = await _dbContext.Users.FindAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can delete sessions");

            var session = await _dbContext.Sessions
                .Include(s => s.Instances)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                throw new KeyNotFoundException($"Session {sessionId} not found");

            // Check if session has an active instance
            var activeInstance = session.Instances.FirstOrDefault(i => i.Status == SessionStatus.Active);
            if (activeInstance != null)
                throw new InvalidOperationException("Cannot delete a session that is currently active");

            _dbContext.Sessions.Remove(session);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted session {SessionId} by admin {AdminId}", sessionId, adminId);
            return true;
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

            // Auto-create rooms
            for (int i = 1; i <= session.NumberOfRooms; i++)
            {
                var room = new Room
                {
                    Id = Guid.NewGuid(),
                    SessionInstanceId = instance.Id,
                    Name = $"Room {i}",
                    CreatedAt = now,
                    IsActive = true
                };
                _dbContext.Rooms.Add(room);
                
                // Create ROOM_CREATED event (Internal only, no need to spam Ably yet or maybe we should?)
                // Let's publish it so UI updates immediately
                var roomEvent = new SessionEvent
                {
                    Id = Guid.NewGuid(),
                    SessionInstanceId = instance.Id,
                    Type = EventType.ROOM_CREATED,
                    EmittedByUserId = adminId,
                    EmittedByRole = Role.System,
                    PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new RoomCreatedPayload
                    {
                        RoomId = room.Id.ToString(),
                        RoomName = room.Name,
                        Participants = new List<string>()
                    }),
                    Timestamp = now,
                    IsPublished = false
                };
                _dbContext.SessionEvents.Add(roomEvent);
            }
            
            await _dbContext.SaveChangesAsync();

            // Publish session start to Ably
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

            _logger.LogInformation("Started session {SessionId} instance {InstanceId} with {RoomCount} auto-created rooms", 
                sessionId, instance.Id, session.NumberOfRooms);
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

            // Lazy creation of rooms if they don't exist (handle legacy sessions)
            if (session.NumberOfRooms > 0 && !await _dbContext.Rooms.AnyAsync(r => r.SessionInstanceId == instance.Id))
            {
                await _sessionLock.WaitAsync();
                try
                {
                    // Double-check inside lock
                    if (!await _dbContext.Rooms.AnyAsync(r => r.SessionInstanceId == instance.Id))
                    {
                        for (int i = 1; i <= session.NumberOfRooms; i++)
                        {
                            var room = new Room
                            {
                                Id = Guid.NewGuid(),
                                SessionInstanceId = instance.Id,
                                Name = $"Room {i}",
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            _dbContext.Rooms.Add(room);

                            // Create ROOM_CREATED event
                            var roomEvent = new SessionEvent
                            {
                                Id = Guid.NewGuid(),
                                SessionInstanceId = instance.Id,
                                Type = EventType.ROOM_CREATED,
                                EmittedByUserId = session.CreatedById, // Use creator ID as fallback
                                EmittedByRole = Role.System,
                                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new RoomCreatedPayload
                                {
                                    RoomId = room.Id.ToString(),
                                    RoomName = room.Name,
                                    Participants = new List<string>()
                                }),
                                Timestamp = DateTime.UtcNow,
                                IsPublished = false
                            };
                            _dbContext.SessionEvents.Add(roomEvent);
                        }
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Lazy-created {Count} rooms for session {SessionId}", session.NumberOfRooms, sessionId);
                    }
                }
                finally
                {
                    _sessionLock.Release();
                }
            }

            // Check reporting window (for students) - only block if session hasn't started yet
            // If session is active, allow students to join even outside the reporting window
            var now = DateTime.UtcNow;
            if (user.Role == Role.Student)
            {
                // Only block if we're before the reporting window start (too early)
                if (now < session.ReportingWindowStart)
                    throw new InvalidOperationException("Session has not started yet. Please wait for the reporting window.");
                
                // Log a warning if outside reporting window but session is active
                if (now > session.ReportingWindowEnd)
                {
                    _logger.LogWarning("Student {UserId} joining session {SessionId} after reporting window end", userId, sessionId);
                }
            }

            // Check if already joined
            var existingParticipant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == userId);

            if (existingParticipant != null)
            {
                if (existingParticipant.IsKicked)
                    throw new InvalidOperationException("You have been removed from this session");

                // Check 3-strike rule for students
                if (user.Role == Role.Student && existingParticipant.DisconnectCount >= 3 && !existingParticipant.HasRejoinPermission)
                {
                    _logger.LogWarning("Student {UserId} denied rejoin due to excessive disconnects ({Count})", userId, existingParticipant.DisconnectCount);
                    throw new InvalidOperationException("You have exceeded the maximum number of disconnects. Please contact a moderator to rejoin.");
                }

                // Retry assignment if waiting (e.g. if rooms were just created)
                if (user.Role == Role.Student && existingParticipant.Status == ParticipantStatus.Waiting)
                {
                    var room = await FindAvailableRoomAsync(instance.Id, session.MaxStudentsPerRoom);
                    if (room != null)
                    {
                        existingParticipant.Status = ParticipantStatus.InRoom;
                        existingParticipant.CurrentRoomId = room.Id;
                        _logger.LogInformation("Assigned waiting student {UserId} to room {RoomId} on rejoin", userId, room.Id);
                    }
                }

                // Reconnect
                existingParticipant.IsConnected = true;
                existingParticipant.LeftAt = null;
                await _dbContext.SaveChangesAsync();
                return existingParticipant;
            }

            // Determine participant status for students
            ParticipantStatus initialStatus = ParticipantStatus.Waiting;
            Room? assignedRoom = null;

            if (user.Role == Role.Student)
            {
                // Try to find an available room for the student
                assignedRoom = await FindAvailableRoomAsync(instance.Id, session.MaxStudentsPerRoom);
                if (assignedRoom != null)
                {
                    initialStatus = ParticipantStatus.InRoom;
                }
                else
                {
                    initialStatus = ParticipantStatus.Waiting;
                }
            }
            else
            {
                // Non-students (Assessors, Moderators, Admins) are in room by default
                initialStatus = ParticipantStatus.InRoom;
            }

            // Create participant
            var participant = new SessionParticipant
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                UserId = userId,
                Role = user.Role,
                Status = initialStatus,
                JoinedAt = now,
                IsConnected = true,
                CurrentRoomId = assignedRoom?.Id
            };

            _dbContext.SessionParticipants.Add(participant);

            // Create audit event
            var eventType = user.Role == Role.Student && initialStatus == ParticipantStatus.Waiting 
                ? EventType.STUDENT_WAITING 
                : EventType.USER_JOINED;

            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = eventType,
                EmittedByUserId = userId,
                EmittedByRole = Role.System,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new UserJoinedPayload
                {
                    UserId = userId.ToString(),
                    Role = user.Role.ToString(),
                    Name = user.Name,
                    Status = initialStatus.ToString()
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
                Type = eventType.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = userId.ToString(), Role = "system" },
                Payload = new UserJoinedPayload
                {
                    UserId = userId.ToString(),
                    Role = user.Role.ToString(),
                    Name = user.Name,
                    Status = initialStatus.ToString()
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("User {UserId} joined session {SessionId} with status {Status}", userId, sessionId, initialStatus);
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
            
            // Increment disconnect count if it's a student (or track for everyone, policy applies to students)
            participant.DisconnectCount++;

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
            participant.DisconnectCount++; // Increment on force disconnect too? Or just leave? User asked "if you will leave".
            // Assuming "DisconnectUserAsync" is system/timeout or explicit disconnect. Let's count it.

            var now = DateTime.UtcNow;
            // ... (rest of method)

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
            if (moderator == null || (moderator.Role != Role.Moderator && moderator.Role != Role.Admin))
                throw new UnauthorizedAccessException("Only moderators or admins can approve breaks");

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

            // Update participant status to OnBreak
            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == breakRequest.SessionInstanceId && sp.UserId == breakRequest.StudentId);
            
            if (participant != null)
            {
                participant.Status = ParticipantStatus.OnBreak;
                participant.CurrentRoomId = null; // Remove from room while on break
            }

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

        public async Task<bool> GrantRejoinPermissionAsync(Guid sessionId, Guid studentId, Guid moderatorId)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || (moderator.Role != Role.Moderator && moderator.Role != Role.Admin))
                throw new UnauthorizedAccessException("Only moderators or admins can grant rejoin permission");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return false;

            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == studentId);

            if (participant == null)
                return false;

            participant.HasRejoinPermission = true;
            // participant.DisconnectCount = 0; // Optional: Reset count

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Rejoin permission granted for student {StudentId} in session {SessionId} by {ModeratorId}", studentId, sessionId, moderatorId);
            return true;
        }

        public async Task<BreakRequest> DenyBreakAsync(Guid sessionId, Guid breakRequestId, Guid moderatorId, string reason)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || (moderator.Role != Role.Moderator && moderator.Role != Role.Admin))
                throw new UnauthorizedAccessException("Only moderators or admins can deny breaks");

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

        public async Task<Room> CreateRoomAsync(Guid sessionId, Guid assessorId, string name)
        {
            await _sessionLock.WaitAsync();
            try
            {
                var assessor = await _dbContext.Users.FindAsync(assessorId);
                if (assessor == null || assessor.Role != Role.Assessor)
                    throw new UnauthorizedAccessException("Only assessors can create rooms");

                var instance = await GetActiveInstanceAsync(sessionId);
                if (instance == null)
                    throw new InvalidOperationException("Session is not active");

                var now = DateTime.UtcNow;

                // Create room
                var room = new Room
                {
                    Id = Guid.NewGuid(),
                    SessionInstanceId = instance.Id,
                    Name = name,
                    CreatedAt = now,
                    IsActive = true
                };
                _dbContext.Rooms.Add(room);

                // Get assessor participant and assign to room
                var assessorParticipant = await _dbContext.SessionParticipants
                    .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == assessorId);

                if (assessorParticipant != null)
                {
                    assessorParticipant.CurrentRoomId = room.Id;
                }

                var participantIds = new List<string> { assessorId.ToString() };

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
                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Room {RoomId} created by assessor {AssessorId}", room.Id, assessorId);
                return room;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

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

        #region Waiting Students Operations

        public async Task<List<SessionParticipant>> GetWaitingStudentsAsync(Guid sessionId)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return new List<SessionParticipant>();

            return await _dbContext.SessionParticipants
                .Include(sp => sp.User)
                .Where(sp => sp.SessionInstanceId == instance.Id && 
                             sp.Status == ParticipantStatus.Waiting &&
                             sp.Role == Role.Student &&
                             !sp.IsKicked)
                .OrderBy(sp => sp.JoinedAt)
                .ToListAsync();
        }

        public async Task<List<SessionParticipant>> GetParticipantsByStatusAsync(Guid sessionId, ParticipantStatus? status)
        {
            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                return new List<SessionParticipant>();

            var query = _dbContext.SessionParticipants
                .Include(sp => sp.User)
                .Where(sp => sp.SessionInstanceId == instance.Id);

            if (status.HasValue)
            {
                query = query.Where(sp => sp.Status == status.Value);
            }

            return await query.OrderBy(sp => sp.JoinedAt).ToListAsync();
        }

        #endregion

        #region Kick Student Operations

        public async Task<SessionParticipant> KickStudentAsync(Guid sessionId, Guid studentId, Guid moderatorId, string reason)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || (moderator.Role != Role.Moderator && moderator.Role != Role.Admin))
                throw new UnauthorizedAccessException("Only moderators or admins can kick students");

            var student = await _dbContext.Users.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Student not found");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                throw new InvalidOperationException("Session is not active");

            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == studentId);

            if (participant == null)
                throw new KeyNotFoundException("Student is not in this session");

            if (participant.IsKicked)
                throw new InvalidOperationException("Student is already kicked");

            var now = DateTime.UtcNow;
            participant.IsKicked = true;
            participant.KickReason = reason;
            participant.Status = ParticipantStatus.Kicked;
            participant.IsConnected = false;
            participant.CurrentRoomId = null;

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.USER_KICKED,
                EmittedByUserId = moderatorId,
                EmittedByRole = moderator.Role,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new UserKickedPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    KickedBy = moderator.Name,
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
                Type = EventType.USER_KICKED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = moderator.Role.ToString().ToLower() },
                Payload = new UserKickedPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    KickedBy = moderator.Name,
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

            _logger.LogInformation("Student {StudentId} kicked by {ModeratorId} from session {SessionId}", 
                studentId, moderatorId, sessionId);
            return participant;
        }

        #endregion

        #region Return From Break Operations

        public async Task<SessionParticipant> ReturnFromBreakAsync(Guid sessionId, Guid studentId)
        {
            var student = await _dbContext.Users.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Student not found");

            var session = await _dbContext.Sessions.FindAsync(sessionId);
            if (session == null)
                throw new KeyNotFoundException("Session not found");

            var instance = await GetActiveInstanceAsync(sessionId);
            if (instance == null)
                throw new InvalidOperationException("Session is not active");

            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == instance.Id && sp.UserId == studentId);

            if (participant == null)
                throw new KeyNotFoundException("Student is not in this session");

            if (participant.Status != ParticipantStatus.OnBreak)
                throw new InvalidOperationException("Student is not on break");

            var now = DateTime.UtcNow;
            participant.IsConnected = true;

            // Try to assign to an available room
            var availableRoom = await FindAvailableRoomAsync(instance.Id, session.MaxStudentsPerRoom);
            
            if (availableRoom != null)
            {
                participant.Status = ParticipantStatus.InRoom;
                participant.CurrentRoomId = availableRoom.Id;
            }
            else
            {
                participant.Status = ParticipantStatus.Waiting;
            }

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.RETURNED_FROM_BREAK,
                EmittedByUserId = studentId,
                EmittedByRole = Role.Student,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new ReturnedFromBreakPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    NewStatus = participant.Status.ToString()
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
                Type = EventType.RETURNED_FROM_BREAK.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = studentId.ToString(), Role = "student" },
                Payload = new ReturnedFromBreakPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    NewStatus = participant.Status.ToString()
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);

            _logger.LogInformation("Student {StudentId} returned from break in session {SessionId}", studentId, sessionId);
            return participant;
        }

        private async Task<Room?> FindAvailableRoomAsync(Guid sessionInstanceId, int maxStudentsPerRoom)
        {
            var rooms = await _dbContext.Rooms
                .Include(r => r.Participants)
                .Where(r => r.SessionInstanceId == sessionInstanceId && r.IsActive)
                .ToListAsync();

            foreach (var room in rooms)
            {
                var studentCount = room.Participants.Count(p => p.Role == Role.Student && !p.IsKicked);
                if (studentCount < maxStudentsPerRoom)
                {
                    return room;
                }
            }

            return null;
        }

        #endregion

        #region Flag Accept/Reject Operations

        public async Task<Flag> AcceptFlagAsync(Guid sessionId, Guid flagId, Guid moderatorId)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || moderator.Role != Role.Moderator)
                throw new UnauthorizedAccessException("Only moderators can accept flags");

            var flag = await _dbContext.Flags
                .Include(f => f.Student)
                .FirstOrDefaultAsync(f => f.Id == flagId);

            if (flag == null)
                throw new KeyNotFoundException("Flag not found");

            if (flag.Status != FlagStatus.Pending)
                throw new InvalidOperationException("Flag is already processed");

            var now = DateTime.UtcNow;
            flag.Status = FlagStatus.Accepted;
            flag.RespondedById = moderatorId;
            flag.RespondedAt = now;
            flag.IsResolved = true;
            flag.Resolution = "Accepted - Student kicked";

            // Auto-kick the student
            var participant = await _dbContext.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.SessionInstanceId == flag.SessionInstanceId && sp.UserId == flag.StudentId);

            if (participant != null)
            {
                participant.IsKicked = true;
                participant.KickReason = $"Flag accepted: {flag.Reason}";
                participant.Status = ParticipantStatus.Kicked;
                participant.IsConnected = false;
                participant.CurrentRoomId = null;
            }

            // Create FLAG_ACCEPTED event
            var flagEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = flag.SessionInstanceId,
                Type = EventType.FLAG_ACCEPTED,
                EmittedByUserId = moderatorId,
                EmittedByRole = Role.Moderator,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new FlagAcceptedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    StudentName = flag.Student?.Name ?? "",
                    FlagId = flagId.ToString(),
                    AcceptedBy = moderator.Name,
                    Reason = flag.Reason
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(flagEvent);

            // Create USER_KICKED event
            var kickEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = flag.SessionInstanceId,
                Type = EventType.USER_KICKED,
                EmittedByUserId = moderatorId,
                EmittedByRole = Role.Moderator,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new UserKickedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    StudentName = flag.Student?.Name ?? "",
                    KickedBy = moderator.Name,
                    Reason = $"Flag accepted: {flag.Reason}"
                }),
                Timestamp = now,
                IsPublished = false
            };
            _dbContext.SessionEvents.Add(kickEvent);

            await _dbContext.SaveChangesAsync();

            // Publish FLAG_ACCEPTED to Ably
            var ablyEvent = new SessionEventDto
            {
                EventId = flagEvent.Id.ToString(),
                Type = EventType.FLAG_ACCEPTED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = "moderator" },
                Payload = new FlagAcceptedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    StudentName = flag.Student?.Name ?? "",
                    FlagId = flagId.ToString(),
                    AcceptedBy = moderator.Name,
                    Reason = flag.Reason
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);

            // Publish USER_KICKED to Ably
            var kickAblyEvent = new SessionEventDto
            {
                EventId = kickEvent.Id.ToString(),
                Type = EventType.USER_KICKED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = "moderator" },
                Payload = new UserKickedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    StudentName = flag.Student?.Name ?? "",
                    KickedBy = moderator.Name,
                    Reason = $"Flag accepted: {flag.Reason}"
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            await _ablyPublisher.PublishAsync(sessionId.ToString(), kickAblyEvent);

            _logger.LogInformation("Flag {FlagId} accepted by moderator {ModeratorId}, student {StudentId} kicked", 
                flagId, moderatorId, flag.StudentId);
            return flag;
        }

        public async Task<Flag> RejectFlagAsync(Guid sessionId, Guid flagId, Guid moderatorId)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || moderator.Role != Role.Moderator)
                throw new UnauthorizedAccessException("Only moderators can reject flags");

            var flag = await _dbContext.Flags
                .Include(f => f.Student)
                .FirstOrDefaultAsync(f => f.Id == flagId);

            if (flag == null)
                throw new KeyNotFoundException("Flag not found");

            if (flag.Status != FlagStatus.Pending)
                throw new InvalidOperationException("Flag is already processed");

            var now = DateTime.UtcNow;
            flag.Status = FlagStatus.Rejected;
            flag.RespondedById = moderatorId;
            flag.RespondedAt = now;
            flag.IsResolved = true;
            flag.Resolution = "Rejected by moderator";

            // Create FLAG_REJECTED event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = flag.SessionInstanceId,
                Type = EventType.FLAG_REJECTED,
                EmittedByUserId = moderatorId,
                EmittedByRole = Role.Moderator,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new FlagRejectedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    FlagId = flagId.ToString(),
                    RejectedBy = moderator.Name
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
                Type = EventType.FLAG_REJECTED.ToString(),
                SessionId = sessionId.ToString(),
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = "moderator" },
                Payload = new FlagRejectedPayload
                {
                    StudentId = flag.StudentId.ToString(),
                    FlagId = flagId.ToString(),
                    RejectedBy = moderator.Name
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);

            _logger.LogInformation("Flag {FlagId} rejected by moderator {ModeratorId}", flagId, moderatorId);
            return flag;
        }

        public async Task<Flag> ModeratorFlagUserAsync(Guid sessionId, Guid studentId, Guid moderatorId, string reason)
        {
            var moderator = await _dbContext.Users.FindAsync(moderatorId);
            if (moderator == null || moderator.Role != Role.Moderator)
                throw new UnauthorizedAccessException("Only moderators can flag users directly");

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
                FlaggedById = moderatorId,
                Reason = reason,
                Status = FlagStatus.Pending,
                CreatedAt = now
            };

            _dbContext.Flags.Add(flag);

            // Create audit event
            var sessionEvent = new SessionEvent
            {
                Id = Guid.NewGuid(),
                SessionInstanceId = instance.Id,
                Type = EventType.FLAG_USER,
                EmittedByUserId = moderatorId,
                EmittedByRole = Role.Moderator,
                PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(new FlagUserPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    Reason = reason,
                    FlaggedBy = moderator.Name
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
                EmittedBy = new EmittedByDto { UserId = moderatorId.ToString(), Role = "moderator" },
                Payload = new FlagUserPayload
                {
                    StudentId = studentId.ToString(),
                    StudentName = student.Name,
                    Reason = reason,
                    FlaggedBy = moderator.Name
                },
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            var published = await _ablyPublisher.PublishAsync(sessionId.ToString(), ablyEvent);
            if (published)
            {
                sessionEvent.IsPublished = true;
                await _dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Student {StudentId} flagged by moderator {ModeratorId} in session {SessionId}", 
                studentId, moderatorId, sessionId);
            return flag;
        }

        #endregion
    }
}

