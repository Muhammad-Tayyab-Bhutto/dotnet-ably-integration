namespace ably_rest_apis.src.Api.DTOs
{
    // Request DTOs
    public class CreateSessionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ScheduledStartTime { get; set; }
        public DateTime ScheduledEndTime { get; set; }
        public DateTime ReportingWindowStart { get; set; }
        public DateTime ReportingWindowEnd { get; set; }
    }

    public class JoinSessionRequest
    {
        public Guid UserId { get; set; }
    }

    public class BreakRequestDto
    {
        public Guid StudentId { get; set; }
        public string? Reason { get; set; }
    }

    public class ApproveBreakRequest
    {
        public Guid BreakRequestId { get; set; }
        public Guid ModeratorId { get; set; }
    }

    public class FlagUserRequest
    {
        public Guid StudentId { get; set; }
        public Guid AssessorId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class EscalateFlagRequest
    {
        public Guid FlagId { get; set; }
        public Guid ModeratorId { get; set; }
    }

    public class CallNextStudentsRequest
    {
        public Guid AssessorId { get; set; }
        public List<Guid> StudentIds { get; set; } = new List<Guid>();
    }

    public class StartEndSessionRequest
    {
        public Guid AdminId { get; set; }
    }

    // Response DTOs
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    public class SessionResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ScheduledStartTime { get; set; }
        public DateTime ScheduledEndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ParticipantResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public bool IsConnected { get; set; }
    }

    public class BreakRequestResponse
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    public class FlagResponse
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool IsEscalated { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RoomResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ParticipantResponse> Participants { get; set; } = new List<ParticipantResponse>();
        public DateTime CreatedAt { get; set; }
    }
}
