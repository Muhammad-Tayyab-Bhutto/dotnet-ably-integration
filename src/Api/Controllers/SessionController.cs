using Microsoft.AspNetCore.Mvc;
using ably_rest_apis.src.Api.DTOs;
using ably_rest_apis.src.Application.Abstractions.Services;
using ably_rest_apis.src.Domain.Entities;

namespace ably_rest_apis.src.Api.Controllers
{
    /// <summary>
    /// Session management controller - handles all session-related operations
    /// </summary>
    [ApiController]
    [Route("api/sessions")]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<SessionController> _logger;

        public SessionController(ISessionService sessionService, ILogger<SessionController> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new exam session
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<SessionResponse>>> CreateSession(
            [FromBody] CreateSessionRequest request,
            [FromHeader(Name = "X-User-Id")] Guid userId)
        {
            try
            {
                var session = new Session
                {
                    Name = request.Name,
                    Description = request.Description,
                    ScheduledStartTime = request.ScheduledStartTime,
                    ScheduledEndTime = request.ScheduledEndTime,
                    ReportingWindowStart = request.ReportingWindowStart,
                    ReportingWindowEnd = request.ReportingWindowEnd,
                    MaxStudentsPerRoom = request.MaxStudentsPerRoom,
                    NumberOfRooms = request.NumberOfRooms
                };

                // Set assigned users
                session.SetAssignedStudents(request.AssignedStudentIds);
                session.SetAssignedAssessors(request.AssignedAssessorIds);
                session.SetAssignedModerators(request.AssignedModeratorIds);

                var created = await _sessionService.CreateSessionAsync(session, userId);

                return Ok(new ApiResponse<SessionResponse>
                {
                    Success = true,
                    Message = "Session created successfully",
                    Data = new SessionResponse
                    {
                        Id = created.Id,
                        Name = created.Name,
                        Description = created.Description,
                        ScheduledStartTime = created.ScheduledStartTime,
                        ScheduledEndTime = created.ScheduledEndTime,
                        MaxStudentsPerRoom = created.MaxStudentsPerRoom,
                        NumberOfRooms = created.NumberOfRooms,
                        Status = "Pending",
                        CreatedAt = created.CreatedAt,
                        AssignedStudentIds = created.GetAssignedStudents(),
                        AssignedAssessorIds = created.GetAssignedAssessors(),
                        AssignedModeratorIds = created.GetAssignedModerators()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session");
                return BadRequest(new ApiResponse<SessionResponse>
                {
                    Success = false,
                    Message = "Failed to create session",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets a session by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<SessionResponse>>> GetSession(Guid id)
        {
            try
            {
                var session = await _sessionService.GetSessionAsync(id);
                if (session == null)
                {
                    return NotFound(new ApiResponse<SessionResponse>
                    {
                        Success = false,
                        Message = "Session not found"
                    });
                }

                var activeInstance = await _sessionService.GetActiveInstanceAsync(id);
                var status = activeInstance?.Status.ToString() ?? "Pending";

                return Ok(new ApiResponse<SessionResponse>
                {
                    Success = true,
                    Data = new SessionResponse
                    {
                        Id = session.Id,
                        Name = session.Name,
                        Description = session.Description,
                        ScheduledStartTime = session.ScheduledStartTime,
                        ScheduledEndTime = session.ScheduledEndTime,
                        MaxStudentsPerRoom = session.MaxStudentsPerRoom,
                        NumberOfRooms = session.NumberOfRooms,
                        Status = status,
                        CreatedAt = session.CreatedAt,
                        AssignedStudentIds = session.GetAssignedStudents(),
                        AssignedAssessorIds = session.GetAssignedAssessors(),
                        AssignedModeratorIds = session.GetAssignedModerators()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session {SessionId}", id);
                return StatusCode(500, new ApiResponse<SessionResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets all sessions
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SessionResponse>>>> GetAllSessions()
        {
            try
            {
                var sessions = await _sessionService.GetAllSessionsAsync();
                var responseList = new List<SessionResponse>();

                foreach (var s in sessions)
                {
                    var activeInstance = await _sessionService.GetActiveInstanceAsync(s.Id);
                    var status = activeInstance?.Status.ToString() ?? "Pending";

                    responseList.Add(new SessionResponse
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        ScheduledStartTime = s.ScheduledStartTime,
                        ScheduledEndTime = s.ScheduledEndTime,
                        MaxStudentsPerRoom = s.MaxStudentsPerRoom,
                        NumberOfRooms = s.NumberOfRooms,
                        Status = status,
                        CreatedAt = s.CreatedAt,
                        AssignedStudentIds = s.GetAssignedStudents(),
                        AssignedAssessorIds = s.GetAssignedAssessors(),
                        AssignedModeratorIds = s.GetAssignedModerators()
                    });
                }

                return Ok(new ApiResponse<List<SessionResponse>>
                {
                    Success = true,
                    Data = responseList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sessions");
                return StatusCode(500, new ApiResponse<List<SessionResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Updates an existing session (Admin only)
        /// PUT /api/sessions/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<SessionResponse>>> UpdateSession(
            Guid id,
            [FromBody] UpdateSessionRequest request,
            [FromHeader(Name = "X-User-Id")] Guid userId)
        {
            try
            {
                var sessionUpdate = new Session
                {
                    Name = request.Name,
                    Description = request.Description,
                    ScheduledStartTime = request.ScheduledStartTime,
                    ScheduledEndTime = request.ScheduledEndTime,
                    ReportingWindowStart = request.ReportingWindowStart,
                    ReportingWindowEnd = request.ReportingWindowEnd,
                    MaxStudentsPerRoom = request.MaxStudentsPerRoom,
                    NumberOfRooms = request.NumberOfRooms
                };

                // Set assigned users
                sessionUpdate.SetAssignedStudents(request.AssignedStudentIds);
                sessionUpdate.SetAssignedAssessors(request.AssignedAssessorIds);
                sessionUpdate.SetAssignedModerators(request.AssignedModeratorIds);

                var updated = await _sessionService.UpdateSessionAsync(id, sessionUpdate, userId);

                return Ok(new ApiResponse<SessionResponse>
                {
                    Success = true,
                    Message = "Session updated successfully",
                    Data = new SessionResponse
                    {
                        Id = updated.Id,
                        Name = updated.Name,
                        Description = updated.Description,
                        ScheduledStartTime = updated.ScheduledStartTime,
                        ScheduledEndTime = updated.ScheduledEndTime,
                        MaxStudentsPerRoom = updated.MaxStudentsPerRoom,
                        NumberOfRooms = updated.NumberOfRooms,
                        Status = "Pending",
                        CreatedAt = updated.CreatedAt,
                        AssignedStudentIds = updated.GetAssignedStudents(),
                        AssignedAssessorIds = updated.GetAssignedAssessors(),
                        AssignedModeratorIds = updated.GetAssignedModerators()
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<SessionResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<SessionResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<SessionResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session {SessionId}", id);
                return StatusCode(500, new ApiResponse<SessionResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Deletes a session (Admin only)
        /// DELETE /api/sessions/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteSession(
            Guid id,
            [FromHeader(Name = "X-User-Id")] Guid userId)
        {
            try
            {
                await _sessionService.DeleteSessionAsync(id, userId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Session deleted successfully",
                    Data = new { SessionId = id }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Starts a session (Admin only)
        /// POST /api/sessions/{id}/start
        /// </summary>
        [HttpPost("{id}/start")]
        public async Task<ActionResult<ApiResponse<object>>> StartSession(
            Guid id,
            [FromBody] StartEndSessionRequest request)
        {
            try
            {
                var instance = await _sessionService.StartSessionAsync(id, request.AdminId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Session started successfully",
                    Data = new
                    {
                        SessionId = id,
                        InstanceId = instance.Id,
                        StartedAt = instance.StartedAt,
                        Status = instance.Status.ToString()
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting session {SessionId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Joins a session
        /// POST /api/sessions/{id}/join
        /// </summary>
        [HttpPost("{id}/join")]
        public async Task<ActionResult<ApiResponse<ParticipantResponse>>> JoinSession(
            Guid id,
            [FromBody] JoinSessionRequest request)
        {
            try
            {
                var participant = await _sessionService.JoinSessionAsync(id, request.UserId);

                return Ok(new ApiResponse<ParticipantResponse>
                {
                    Success = true,
                    Message = "Joined session successfully",
                    Data = new ParticipantResponse
                    {
                        Id = participant.Id,
                        UserId = participant.UserId,
                        Name = participant.User?.Name ?? "",
                        Role = participant.Role.ToString(),
                        JoinedAt = participant.JoinedAt,
                        IsConnected = participant.IsConnected
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining session {SessionId}", id);
                return StatusCode(500, new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Student requests a break
        /// POST /api/sessions/{id}/break-request
        /// </summary>
        [HttpPost("{id}/break-request")]
        public async Task<ActionResult<ApiResponse<BreakRequestResponse>>> RequestBreak(
            Guid id,
            [FromBody] BreakRequestDto request)
        {
            try
            {
                var breakRequest = await _sessionService.RequestBreakAsync(id, request.StudentId, request.Reason);

                return Ok(new ApiResponse<BreakRequestResponse>
                {
                    Success = true,
                    Message = "Break requested successfully",
                    Data = new BreakRequestResponse
                    {
                        Id = breakRequest.Id,
                        StudentId = breakRequest.StudentId,
                        Status = breakRequest.Status.ToString(),
                        Reason = breakRequest.Reason,
                        RequestedAt = breakRequest.RequestedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting break in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Moderator approves a break
        /// POST /api/sessions/{id}/break-approve
        /// </summary>
        [HttpPost("{id}/break-approve")]
        public async Task<ActionResult<ApiResponse<BreakRequestResponse>>> ApproveBreak(
            Guid id,
            [FromBody] ApproveBreakRequest request)
        {
            try
            {
                var breakRequest = await _sessionService.ApproveBreakAsync(id, request.BreakRequestId, request.ModeratorId);

                return Ok(new ApiResponse<BreakRequestResponse>
                {
                    Success = true,
                    Message = "Break approved successfully",
                    Data = new BreakRequestResponse
                    {
                        Id = breakRequest.Id,
                        StudentId = breakRequest.StudentId,
                        Status = breakRequest.Status.ToString(),
                        RequestedAt = breakRequest.RequestedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving break in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Moderator rejects a break
        /// POST /api/sessions/{id}/break-reject
        /// </summary>
        [HttpPost("{id}/break-reject")]
        public async Task<ActionResult<ApiResponse<BreakRequestResponse>>> RejectBreak(
            Guid id,
            [FromBody] ApproveBreakRequest request) // Reusing ApproveBreakRequest as it has the same fields + we can add reason if needed
        {
            try
            {
                // Default reason if not provided
                string reason = "Request denied by moderator";
                
                var breakRequest = await _sessionService.DenyBreakAsync(id, request.BreakRequestId, request.ModeratorId, reason);

                return Ok(new ApiResponse<BreakRequestResponse>
                {
                    Success = true,
                    Message = "Break rejected successfully",
                    Data = new BreakRequestResponse
                    {
                        Id = breakRequest.Id,
                        StudentId = breakRequest.StudentId,
                        Status = breakRequest.Status.ToString(),
                        RequestedAt = breakRequest.RequestedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting break in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<BreakRequestResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets pending break requests
        /// </summary>
        [HttpGet("{id}/break-requests")]
        public async Task<ActionResult<ApiResponse<List<BreakRequestResponse>>>> GetPendingBreakRequests(Guid id)
        {
            try
            {
                var requests = await _sessionService.GetPendingBreakRequestsAsync(id);
                var response = requests.Select(r => new BreakRequestResponse
                {
                    Id = r.Id,
                    StudentId = r.StudentId,
                    StudentName = r.Student?.Name ?? "",
                    Status = r.Status.ToString(),
                    Reason = r.Reason,
                    RequestedAt = r.RequestedAt
                }).ToList();

                return Ok(new ApiResponse<List<BreakRequestResponse>>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting break requests for session {SessionId}", id);
                return StatusCode(500, new ApiResponse<List<BreakRequestResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Assessor flags a student
        /// POST /api/sessions/{id}/flag
        /// </summary>
        [HttpPost("{id}/flag")]
        public async Task<ActionResult<ApiResponse<FlagResponse>>> FlagUser(
            Guid id,
            [FromBody] FlagUserRequest request)
        {
            try
            {
                var flag = await _sessionService.FlagUserAsync(id, request.StudentId, request.AssessorId, request.Reason);

                return Ok(new ApiResponse<FlagResponse>
                {
                    Success = true,
                    Message = "User flagged successfully",
                    Data = new FlagResponse
                    {
                        Id = flag.Id,
                        StudentId = flag.StudentId,
                        Reason = flag.Reason,
                        IsEscalated = flag.IsEscalated,
                        CreatedAt = flag.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flagging user in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Moderator escalates a flag
        /// POST /api/sessions/{id}/flag/escalate
        /// </summary>
        [HttpPost("{id}/flag/escalate")]
        public async Task<ActionResult<ApiResponse<FlagResponse>>> EscalateFlag(
            Guid id,
            [FromBody] EscalateFlagRequest request)
        {
            try
            {
                var flag = await _sessionService.EscalateFlagAsync(id, request.FlagId, request.ModeratorId);

                return Ok(new ApiResponse<FlagResponse>
                {
                    Success = true,
                    Message = "Flag escalated successfully",
                    Data = new FlagResponse
                    {
                        Id = flag.Id,
                        StudentId = flag.StudentId,
                        StudentName = flag.Student?.Name ?? "",
                        Reason = flag.Reason,
                        IsEscalated = flag.IsEscalated,
                        CreatedAt = flag.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error escalating flag in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets active flags for a session
        /// </summary>
        [HttpGet("{id}/flags")]
        public async Task<ActionResult<ApiResponse<List<FlagResponse>>>> GetActiveFlags(Guid id)
        {
            try
            {
                var flags = await _sessionService.GetActiveFlagsAsync(id);
                var response = flags.Select(f => new FlagResponse
                {
                    Id = f.Id,
                    StudentId = f.StudentId,
                    StudentName = f.Student?.Name ?? "",
                    Reason = f.Reason,
                    IsEscalated = f.IsEscalated,
                    CreatedAt = f.CreatedAt
                }).ToList();

                return Ok(new ApiResponse<List<FlagResponse>>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting flags for session {SessionId}", id);
                return StatusCode(500, new ApiResponse<List<FlagResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Assessor calls next students (creates a room)
        /// POST /api/sessions/{id}/call-next
        /// </summary>
        [HttpPost("{id}/call-next")]
        public async Task<ActionResult<ApiResponse<RoomResponse>>> CallNextStudents(
            Guid id,
            [FromBody] CallNextStudentsRequest request)
        {
            try
            {
                var room = await _sessionService.CallNextStudentsAsync(id, request.AssessorId, request.StudentIds);
                var activeRooms = await _sessionService.GetActiveRoomsAsync(id);
                var createdRoom = activeRooms.FirstOrDefault(r => r.Id == room.Id);

                return Ok(new ApiResponse<RoomResponse>
                {
                    Success = true,
                    Message = "Room created successfully",
                    Data = new RoomResponse
                    {
                        Id = room.Id,
                        Name = room.Name,
                        Participants = createdRoom?.Participants.Select(p => new ParticipantResponse
                        {
                            Id = p.Id,
                            UserId = p.UserId,
                            Name = p.User?.Name ?? "",
                            Role = p.Role.ToString()
                        }).ToList() ?? new List<ParticipantResponse>(),
                        CreatedAt = room.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<RoomResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<RoomResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling next students in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<RoomResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Assessor manualy creates a room
        /// POST /api/sessions/{id}/rooms
        /// </summary>
        [HttpPost("{id}/rooms")]
        public async Task<ActionResult<ApiResponse<RoomResponse>>> CreateRoom(
            Guid id,
            [FromBody] CreateRoomRequest request)
        {
            try
            {
                var room = await _sessionService.CreateRoomAsync(id, request.AssessorId, request.Name);
                
                // Fetch room with participants to ensure DTO is complete
                var activeRooms = await _sessionService.GetActiveRoomsAsync(id);
                var createdRoom = activeRooms.FirstOrDefault(r => r.Id == room.Id);

                return Ok(new ApiResponse<RoomResponse>
                {
                    Success = true,
                    Message = "Room created successfully",
                    Data = new RoomResponse
                    {
                        Id = room.Id,
                        Name = room.Name,
                        Participants = createdRoom?.Participants.Select(p => new ParticipantResponse
                        {
                            Id = p.Id,
                            UserId = p.UserId,
                            Name = p.User?.Name ?? "",
                            Role = p.Role.ToString()
                        }).ToList() ?? new List<ParticipantResponse>(),
                        CreatedAt = room.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<RoomResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<RoomResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<RoomResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets active rooms for a session
        /// </summary>
        [HttpGet("{id}/rooms")]
        public async Task<ActionResult<ApiResponse<List<RoomResponse>>>> GetActiveRooms(Guid id)
        {
            try
            {
                var rooms = await _sessionService.GetActiveRoomsAsync(id);
                var response = rooms.Select(r => new RoomResponse
                {
                    Id = r.Id,
                    Name = r.Name,
                    Participants = r.Participants.Select(p => new ParticipantResponse
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Name = p.User?.Name ?? "",
                        Role = p.Role.ToString()
                    }).ToList(),
                    CreatedAt = r.CreatedAt
                }).ToList();

                return Ok(new ApiResponse<List<RoomResponse>>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rooms for session {SessionId}", id);
                return StatusCode(500, new ApiResponse<List<RoomResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Grants permission for a student to rejoin after max disconnects
        /// POST /api/sessions/{id}/participants/{studentId}/permit-rejoin
        /// </summary>
        [HttpPost("{id}/participants/{studentId}/permit-rejoin")]
        public async Task<ActionResult<ApiResponse<object>>> PermitRejoin(
            Guid id,
            Guid studentId,
            [FromHeader(Name = "X-User-Id")] Guid moderatorId)
        {
            try
            {
                var result = await _sessionService.GrantRejoinPermissionAsync(id, studentId, moderatorId);
                if (result)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Rejoin permission granted"
                    });
                }
                
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Session or participant not found"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting rejoin permission for student {StudentId} in session {SessionId}", studentId, id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Ends a session (Admin only)
        /// POST /api/sessions/{id}/end
        /// </summary>
        [HttpPost("{id}/end")]
        public async Task<ActionResult<ApiResponse<object>>> EndSession(
            Guid id,
            [FromBody] StartEndSessionRequest request)
        {
            try
            {
                var instance = await _sessionService.EndSessionAsync(id, request.AdminId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Session ended successfully",
                    Data = new
                    {
                        SessionId = id,
                        InstanceId = instance.Id,
                        EndedAt = instance.EndedAt,
                        Status = instance.Status.ToString()
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending session {SessionId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets waiting students in a session
        /// GET /api/sessions/{id}/waiting-students
        /// </summary>
        [HttpGet("{id}/waiting-students")]
        public async Task<ActionResult<ApiResponse<WaitingStudentsResponse>>> GetWaitingStudents(Guid id)
        {
            try
            {
                var waitingStudents = await _sessionService.GetWaitingStudentsAsync(id);
                var response = new WaitingStudentsResponse
                {
                    TotalWaiting = waitingStudents.Count,
                    Students = waitingStudents.Select(p => new ParticipantResponse
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Name = p.User?.Name ?? "",
                        Role = p.Role.ToString(),
                        Status = p.Status.ToString(),
                        JoinedAt = p.JoinedAt,
                        IsConnected = p.IsConnected
                    }).ToList()
                };

                return Ok(new ApiResponse<WaitingStudentsResponse>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting waiting students for session {SessionId}", id);
                return StatusCode(500, new ApiResponse<WaitingStudentsResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets all participants in a session with optional status filter
        /// GET /api/sessions/{id}/participants?status=Waiting
        /// </summary>
        [HttpGet("{id}/participants")]
        public async Task<ActionResult<ApiResponse<List<ParticipantResponse>>>> GetParticipants(
            Guid id,
            [FromQuery] string? status)
        {
            try
            {
                ably_rest_apis.src.Domain.Enums.ParticipantStatus? statusEnum = null;
                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<ably_rest_apis.src.Domain.Enums.ParticipantStatus>(status, true, out var parsed))
                    {
                        statusEnum = parsed;
                    }
                }

                var participants = await _sessionService.GetParticipantsByStatusAsync(id, statusEnum);
                var response = participants.Select(p => new ParticipantResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    Name = p.User?.Name ?? "",
                    Role = p.Role.ToString(),
                    Status = p.Status.ToString(),
                    JoinedAt = p.JoinedAt,
                    IsConnected = p.IsConnected
                }).ToList();

                return Ok(new ApiResponse<List<ParticipantResponse>>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting participants for session {SessionId}", id);
                return StatusCode(500, new ApiResponse<List<ParticipantResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Moderator accepts a flag (auto-kicks the student)
        /// POST /api/sessions/{id}/flag/accept
        /// </summary>
        [HttpPost("{id}/flag/accept")]
        public async Task<ActionResult<ApiResponse<FlagResponse>>> AcceptFlag(
            Guid id,
            [FromBody] AcceptRejectFlagRequest request)
        {
            try
            {
                var flag = await _sessionService.AcceptFlagAsync(id, request.FlagId, request.ModeratorId);

                return Ok(new ApiResponse<FlagResponse>
                {
                    Success = true,
                    Message = "Flag accepted - Student kicked",
                    Data = new FlagResponse
                    {
                        Id = flag.Id,
                        StudentId = flag.StudentId,
                        StudentName = flag.Student?.Name ?? "",
                        Reason = flag.Reason,
                        Status = flag.Status.ToString(),
                        IsEscalated = flag.IsEscalated,
                        IsAccepted = true,
                        CreatedAt = flag.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting flag in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Moderator rejects a flag
        /// POST /api/sessions/{id}/flag/reject
        /// </summary>
        [HttpPost("{id}/flag/reject")]
        public async Task<ActionResult<ApiResponse<FlagResponse>>> RejectFlag(
            Guid id,
            [FromBody] AcceptRejectFlagRequest request)
        {
            try
            {
                var flag = await _sessionService.RejectFlagAsync(id, request.FlagId, request.ModeratorId);

                return Ok(new ApiResponse<FlagResponse>
                {
                    Success = true,
                    Message = "Flag rejected",
                    Data = new FlagResponse
                    {
                        Id = flag.Id,
                        StudentId = flag.StudentId,
                        StudentName = flag.Student?.Name ?? "",
                        Reason = flag.Reason,
                        Status = flag.Status.ToString(),
                        IsEscalated = flag.IsEscalated,
                        IsAccepted = false,
                        CreatedAt = flag.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting flag in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Moderator flags a student directly
        /// POST /api/sessions/{id}/moderator-flag
        /// </summary>
        [HttpPost("{id}/moderator-flag")]
        public async Task<ActionResult<ApiResponse<FlagResponse>>> ModeratorFlagUser(
            Guid id,
            [FromBody] ModeratorFlagUserRequest request)
        {
            try
            {
                var flag = await _sessionService.ModeratorFlagUserAsync(id, request.StudentId, request.ModeratorId, request.Reason);

                return Ok(new ApiResponse<FlagResponse>
                {
                    Success = true,
                    Message = "User flagged by moderator",
                    Data = new FlagResponse
                    {
                        Id = flag.Id,
                        StudentId = flag.StudentId,
                        Reason = flag.Reason,
                        Status = flag.Status.ToString(),
                        IsEscalated = flag.IsEscalated,
                        IsAccepted = false,
                        CreatedAt = flag.CreatedAt
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moderator flagging user in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<FlagResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Kicks a student from the session
        /// POST /api/sessions/{id}/kick
        /// </summary>
        [HttpPost("{id}/kick")]
        public async Task<ActionResult<ApiResponse<ParticipantResponse>>> KickStudent(
            Guid id,
            [FromBody] KickStudentRequest request)
        {
            try
            {
                var participant = await _sessionService.KickStudentAsync(id, request.StudentId, request.ModeratorId, request.Reason);

                return Ok(new ApiResponse<ParticipantResponse>
                {
                    Success = true,
                    Message = "Student kicked from session",
                    Data = new ParticipantResponse
                    {
                        Id = participant.Id,
                        UserId = participant.UserId,
                        Name = participant.User?.Name ?? "",
                        Role = participant.Role.ToString(),
                        Status = participant.Status.ToString(),
                        JoinedAt = participant.JoinedAt,
                        IsConnected = participant.IsConnected
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error kicking student from session {SessionId}", id);
                return StatusCode(500, new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Student returns from break
        /// POST /api/sessions/{id}/return-from-break
        /// </summary>
        [HttpPost("{id}/return-from-break")]
        public async Task<ActionResult<ApiResponse<ParticipantResponse>>> ReturnFromBreak(
            Guid id,
            [FromBody] ReturnFromBreakRequest request)
        {
            try
            {
                var participant = await _sessionService.ReturnFromBreakAsync(id, request.StudentId);

                return Ok(new ApiResponse<ParticipantResponse>
                {
                    Success = true,
                    Message = "Returned from break successfully",
                    Data = new ParticipantResponse
                    {
                        Id = participant.Id,
                        UserId = participant.UserId,
                        Name = participant.User?.Name ?? "",
                        Role = participant.Role.ToString(),
                        Status = participant.Status.ToString(),
                        JoinedAt = participant.JoinedAt,
                        IsConnected = participant.IsConnected
                    }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning from break in session {SessionId}", id);
                return StatusCode(500, new ApiResponse<ParticipantResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}

