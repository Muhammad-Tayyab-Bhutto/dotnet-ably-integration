using ably_rest_apis.src.Domain.Enums;
using Newtonsoft.Json;

namespace ably_rest_apis.src.Shared.Contracts
{
    /// <summary>
    /// Standard event contract for Ably messages
    /// Matches the exact specification: eventId, type, sessionId, emittedBy, payload, timestamp
    /// </summary>
    /// <summary>
    /// Standard event contract for Ably messages
    /// Matches the exact specification: eventId, type, sessionId, emittedBy, payload, timestamp
    /// </summary>
    public class SessionEventDto
    {
        [JsonProperty("eventId")]
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("emittedBy")]
        public EmittedByDto EmittedBy { get; set; } = new EmittedByDto();

        [JsonProperty("payload")]
        public object Payload { get; set; } = new { };

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public class EmittedByDto
    {
        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string Role { get; set; } = "system";
    }

    // Specific payload DTOs for type safety

    public class UserJoinedPayload
    {
        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("role")]
        public string Role { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class BreakRequestPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string? Reason { get; set; }
    }

    public class BreakApprovedPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("approvedBy")]
        public string ApprovedBy { get; set; } = string.Empty;
    }

    public class FlagUserPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonProperty("flaggedBy")]
        public string FlaggedBy { get; set; } = string.Empty;
    }

    public class FlagEscalatedPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("flagId")]
        public string FlagId { get; set; } = string.Empty;

        [JsonProperty("escalatedBy")]
        public string EscalatedBy { get; set; } = string.Empty;
    }

    public class FlagAcceptedPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("flagId")]
        public string FlagId { get; set; } = string.Empty;

        [JsonProperty("acceptedBy")]
        public string AcceptedBy { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class FlagRejectedPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("flagId")]
        public string FlagId { get; set; } = string.Empty;

        [JsonProperty("rejectedBy")]
        public string RejectedBy { get; set; } = string.Empty;
    }

    public class UserKickedPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("kickedBy")]
        public string KickedBy { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public class StudentWaitingPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("position")]
        public int Position { get; set; }
    }

    public class CalledToRoomPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("roomId")]
        public string RoomId { get; set; } = string.Empty;

        [JsonProperty("roomName")]
        public string RoomName { get; set; } = string.Empty;
    }

    public class ReturnedFromBreakPayload
    {
        [JsonProperty("studentId")]
        public string StudentId { get; set; } = string.Empty;

        [JsonProperty("studentName")]
        public string StudentName { get; set; } = string.Empty;

        [JsonProperty("newStatus")]
        public string NewStatus { get; set; } = string.Empty;
    }

    public class RoomCreatedPayload
    {
        [JsonProperty("roomId")]
        public string RoomId { get; set; } = string.Empty;

        [JsonProperty("roomName")]
        public string RoomName { get; set; } = string.Empty;

        [JsonProperty("participants")]
        public List<string> Participants { get; set; } = new List<string>();
    }

    public class CallNextStudentsPayload
    {
        [JsonProperty("studentIds")]
        public List<string> StudentIds { get; set; } = new List<string>();

        [JsonProperty("roomId")]
        public string RoomId { get; set; } = string.Empty;

        [JsonProperty("assessorId")]
        public string AssessorId { get; set; } = string.Empty;
    }

    public class SessionEndedPayload
    {
        [JsonProperty("endedBy")]
        public string EndedBy { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string Reason { get; set; } = "Session completed";
    }
}

