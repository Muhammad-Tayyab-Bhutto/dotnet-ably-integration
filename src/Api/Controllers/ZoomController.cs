using Microsoft.AspNetCore.Mvc;
using ably_rest_apis.src.Infrastructure.Zoom;

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
        private readonly ILogger<ZoomController> _logger;

        public ZoomController(IZoomJwtService zoomJwtService, ILogger<ZoomController> logger)
        {
            _zoomJwtService = zoomJwtService;
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
    }
}
