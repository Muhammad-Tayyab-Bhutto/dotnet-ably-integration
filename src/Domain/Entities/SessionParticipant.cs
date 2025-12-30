using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents a participant in a session instance
    /// </summary>
    public class SessionParticipant
    {
        public Guid Id { get; set; }
        public Guid SessionInstanceId { get; set; }
        public Guid UserId { get; set; }
        public Role Role { get; set; }
        public ParticipantStatus Status { get; set; } = ParticipantStatus.Waiting;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LeftAt { get; set; }
        public bool IsKicked { get; set; } = false;
        public string? KickReason { get; set; }
        public bool IsConnected { get; set; } = true;
        public Guid? CurrentRoomId { get; set; }
        public int DisconnectCount { get; set; } = 0;
        public bool HasRejoinPermission { get; set; } = false;

        // Navigation properties
        public virtual SessionInstance? SessionInstance { get; set; }
        public virtual User? User { get; set; }
        public virtual Room? CurrentRoom { get; set; }
    }
}
