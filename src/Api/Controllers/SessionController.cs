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
                    ReportingWindowEnd = request.ReportingWindowEnd
                };

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
                        Status = "Pending",
                        CreatedAt = created.CreatedAt
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
                        Status = status,
                        CreatedAt = session.CreatedAt
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
                var response = sessions.Select(s => new SessionResponse
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    ScheduledStartTime = s.ScheduledStartTime,
                    ScheduledEndTime = s.ScheduledEndTime,
                    CreatedAt = s.CreatedAt
                }).ToList();

                return Ok(new ApiResponse<List<SessionResponse>>
                {
                    Success = true,
                    Data = response
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
    }
}
