using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Shared.Contracts
{
    /// <summary>
    /// Standard event contract for Ably messages
    /// Matches the exact specification: eventId, type, sessionId, emittedBy, payload, timestamp
    /// </summary>
    public class SessionEventDto
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public EmittedByDto EmittedBy { get; set; } = new EmittedByDto();
        public object Payload { get; set; } = new { };
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public class EmittedByDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = "system";
    }

    // Specific payload DTOs for type safety

    public class UserJoinedPayload
    {
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class BreakRequestPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class BreakApprovedPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
    }

    public class FlagUserPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string FlaggedBy { get; set; } = string.Empty;
    }

    public class FlagEscalatedPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string FlagId { get; set; } = string.Empty;
        public string EscalatedBy { get; set; } = string.Empty;
    }

    public class FlagAcceptedPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string FlagId { get; set; } = string.Empty;
        public string AcceptedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class FlagRejectedPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string FlagId { get; set; } = string.Empty;
        public string RejectedBy { get; set; } = string.Empty;
    }

    public class UserKickedPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string KickedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class StudentWaitingPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public int Position { get; set; }
    }

    public class CalledToRoomPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
    }

    public class ReturnedFromBreakPayload
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
    }

    public class RoomCreatedPayload
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new List<string>();
    }

    public class CallNextStudentsPayload
    {
        public List<string> StudentIds { get; set; } = new List<string>();
        public string RoomId { get; set; } = string.Empty;
        public string AssessorId { get; set; } = string.Empty;
    }

    public class SessionEndedPayload
    {
        public string EndedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = "Session completed";
    }
}

