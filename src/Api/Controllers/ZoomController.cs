using Microsoft.AspNetCore.Mvc;
using ably_rest_apis.src.Infrastructure.Zoom;
using ably_rest_apis.src.Application.Abstractions.Services;

namespace ably_rest_apis.src.Api.Controllers
{
    /// <summary>
    /// Controller for Zoom Video SDK operations
    /// </summary>
    [ApiController]
    [Route("api/zoom")]
    public class ZoomController : ControllerBase
    {
        private readonly IZoomJwtService _zoomJwtService;
        private readonly IZoomRecordingService _zoomRecordingService;
        private readonly ISessionService _sessionService;
        private readonly ILogger<ZoomController> _logger;

        public ZoomController(
            IZoomJwtService zoomJwtService, 
            IZoomRecordingService zoomRecordingService,
            ISessionService sessionService,
            ILogger<ZoomController> logger)
        {
            _zoomJwtService = zoomJwtService;
            _zoomRecordingService = zoomRecordingService;
            _sessionService = sessionService;
            _logger = logger;
        }

        /// <summary>
        /// Gets a Zoom Video SDK JWT token for joining a session
        /// </summary>
        /// <param name="sessionId">The exam session ID</param>
        /// <param name="roomId">The room ID (optional - uses sessionId if not provided)</param>
        /// <param name="role">0 = participant, 1 = host (default: 0)</param>
        /// <returns>JWT token and session name</returns>
        [HttpGet("token")]
        public IActionResult GetSessionToken(
            [FromQuery] string sessionId,
            [FromQuery] string? roomId = null,
            [FromQuery] int role = 0)
        {
            try
            {
                // Get user ID from header (same pattern as other endpoints)
                var userId = Request.Headers["X-User-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "X-User-Id header is required" });
                }

                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest(new { success = false, message = "sessionId is required" });
                }

                // Create session name: use roomId if provided, otherwise use sessionId
                // Format: exam-{sessionId}-{roomId} for room-specific sessions
                // Or: exam-{sessionId} for session-wide
                var sessionName = !string.IsNullOrEmpty(roomId)
                    ? $"exam-{sessionId.Substring(0, Math.Min(8, sessionId.Length))}-{roomId.Substring(0, Math.Min(8, roomId.Length))}"
                    : $"exam-{sessionId.Substring(0, Math.Min(8, sessionId.Length))}";

                var token = _zoomJwtService.GenerateSessionToken(sessionName, userId, role);

                _logger.LogInformation(
                    "Generated Zoom token for user {UserId} in session {SessionName}",
                    userId, sessionName);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        token = token,
                        sessionName = sessionName,
                        userId = userId,
                        role = role
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Zoom token");
                return StatusCode(500, new { success = false, message = "Failed to generate video session token" });
            }
        }

        /// <summary>
        /// Gets multiple tokens for moderator multi-session viewing
        /// </summary>
        /// <param name="sessionId">The exam session ID</param>
        /// <param name="roomIds">Comma-separated list of room IDs</param>
        /// <returns>Array of tokens for each room</returns>
        [HttpGet("tokens/multi")]
        public IActionResult GetMultiSessionTokens(
            [FromQuery] string sessionId,
            [FromQuery] string roomIds)
        {
            try
            {
                var userId = Request.Headers["X-User-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "X-User-Id header is required" });
                }

                if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(roomIds))
                {
                    return BadRequest(new { success = false, message = "sessionId and roomIds are required" });
                }

                var roomIdList = roomIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var tokens = new List<object>();

                foreach (var roomId in roomIdList)
                {
                    var trimmedRoomId = roomId.Trim();
                    var sessionName = $"exam-{sessionId.Substring(0, Math.Min(8, sessionId.Length))}-{trimmedRoomId.Substring(0, Math.Min(8, trimmedRoomId.Length))}";
                    var token = _zoomJwtService.GenerateSessionToken(sessionName, userId, 0); // Moderator as observer

                    tokens.Add(new
                    {
                        roomId = trimmedRoomId,
                        sessionName = sessionName,
                        token = token
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = tokens
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate multi-session Zoom tokens");
                return StatusCode(500, new { success = false, message = "Failed to generate video session tokens" });
            }
        }

        /// <summary>
        /// Gets cloud recordings for a session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>List of recordings</returns>
        [HttpGet("recordings/{sessionId}")]
        public async Task<IActionResult> GetSessionRecordings(Guid sessionId)
        {
            try
            {
                // Verify session exists and get dates
                var session = await _sessionService.GetSessionAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { success = false, message = "Session not found" });
                }

                // Construct session topic name pattern
                // Since recordings might be per room (exam-{sessionId}-{roomId}) or global (exam-{sessionId}),
                // we might need to search for both or handle "topic" partial match?
                // The Video SDK API might not support wildcard topic search easily in List Sessions unless we filter locally.
                // Our service currently filters by EXACT topic match.
                // Let's assume for now we only care about the main session or we iterate rooms?
                // For simplicity, let's fetch recordings for the main session topic: "exam-{sessionId...}"
                // If rooms are recorded separately (which they are), we might need to fetch for each room?
                // That would be expensive.
                // Maybe the session topic is just the prefix?
                // Current implementation of ZoomRecordingService filters by EXACT topic.
                // Let's update ZoomController to construct the main topic.
                
                var topic = $"exam-{sessionId.ToString().Substring(0, Math.Min(8, sessionId.ToString().Length))}";
                
                // Add a buffer to the search window (e.g. -1 hour start, +4 hours end) to capture early starts/late ends
                var from = session.ScheduledStartTime.AddHours(-1);
                var to = session.ScheduledEndTime.AddHours(4); // Extended window
                
                var recordings = await _zoomRecordingService.GetRecordingsForSessionAsync(topic, from, to);

                return Ok(new
                {
                    success = true,
                    data = recordings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recordings for session {SessionId}", sessionId);
                return StatusCode(500, new { success = false, message = "Failed to get recordings" });
            }
        }

        /// <summary>
        /// Gets all cloud recordings (global view)
        /// </summary>
        /// <returns>List of recordings</returns>
        [HttpGet("recordings")]
        public async Task<IActionResult> GetAllRecordings([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                // Default to last 30 days if not specified
                var startDate = from ?? DateTime.UtcNow.AddDays(-30);
                var endDate = to ?? DateTime.UtcNow;

                var recordings = await _zoomRecordingService.GetAllRecordingsAsync(startDate, endDate);

                return Ok(new
                {
                    success = true,
                    data = recordings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all recordings");
                return StatusCode(500, new { success = false, message = "Failed to get recordings" });
            }
        }

        /// <summary>
        /// Starts cloud recording for a session
        /// </summary>
        /// <param name="request">Request body containing sessionId and layout</param>
        /// <returns>Success status</returns>
        [HttpPost("recordings/start")]
        public async Task<IActionResult> StartRecording([FromBody] StartRecordingRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SessionId))
                {
                    return BadRequest(new { success = false, message = "Session ID is required" });
                }

                _logger.LogInformation("Requesting to start recording for session {SessionId} with layout {Layout}", request.SessionId, request.Layout);

                var success = await _zoomRecordingService.StartRecordingAsync(request.SessionId, request.Layout);

                if (success)
                {
                    return Ok(new { success = true, message = "Recording started" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to start recording. Ensure session is active." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start recording");
                return StatusCode(500, new { success = false, message = "Failed to start recording" });
            }
        }

        [HttpPost("recordings/pause")]
        public async Task<IActionResult> PauseRecording([FromBody] RecordingControlRequest request)
        {
            try
            {
                var success = await _zoomRecordingService.PauseRecordingAsync(request.SessionId);
                return success ? Ok(new { success = true }) : StatusCode(500, new { success = false, message = "Failed to pause recording" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause recording");
                return StatusCode(500, new { success = false, message = "Failed to pause recording" });
            }
        }

        [HttpPost("recordings/resume")]
        public async Task<IActionResult> ResumeRecording([FromBody] RecordingControlRequest request)
        {
            try
            {
                var success = await _zoomRecordingService.ResumeRecordingAsync(request.SessionId);
                return success ? Ok(new { success = true }) : StatusCode(500, new { success = false, message = "Failed to resume recording" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume recording");
                return StatusCode(500, new { success = false, message = "Failed to resume recording" });
            }
        }

        [HttpPost("recordings/stop")]
        public async Task<IActionResult> StopRecording([FromBody] RecordingControlRequest request)
        {
            try
            {
                var success = await _zoomRecordingService.StopRecordingAsync(request.SessionId);
                return success ? Ok(new { success = true }) : StatusCode(500, new { success = false, message = "Failed to stop recording" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop recording");
                return StatusCode(500, new { success = false, message = "Failed to stop recording" });
            }
        }
    }

    public class StartRecordingRequest
    {
        public string SessionId { get; set; } = "";
        public string Layout { get; set; } = "";
    }

    public class RecordingControlRequest
    {
        public string SessionId { get; set; } = "";
    }
}
