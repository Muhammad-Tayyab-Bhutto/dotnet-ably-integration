using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents an active instance of a session
    /// </summary>
    public class SessionInstance
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Pending;
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public Guid? StartedById { get; set; }
        public Guid? EndedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Session? Session { get; set; }
        public virtual User? StartedBy { get; set; }
        public virtual User? EndedBy { get; set; }
        public virtual ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
        public virtual ICollection<BreakRequest> BreakRequests { get; set; } = new List<BreakRequest>();
        public virtual ICollection<Flag> Flags { get; set; } = new List<Flag>();
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
        public virtual ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();
    }
}
