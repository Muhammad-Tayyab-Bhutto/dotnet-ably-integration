using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents an event in the session for audit trail and replay
    /// </summary>
    public class SessionEvent
    {
        public Guid Id { get; set; }
        public Guid SessionInstanceId { get; set; }
        public EventType Type { get; set; }
        public Guid? EmittedByUserId { get; set; }
        public Role EmittedByRole { get; set; }
        public string PayloadJson { get; set; } = "{}";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsPublished { get; set; } = false;

        // Navigation properties
        public virtual SessionInstance? SessionInstance { get; set; }
        public virtual User? EmittedByUser { get; set; }
    }
}
