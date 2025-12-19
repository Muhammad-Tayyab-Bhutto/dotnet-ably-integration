using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents a user in the exam session system
    /// </summary>
    public class User
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Role Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<SessionParticipant> SessionParticipations { get; set; } = new List<SessionParticipant>();
        public virtual ICollection<BreakRequest> BreakRequests { get; set; } = new List<BreakRequest>();
        public virtual ICollection<Flag> FlagsReceived { get; set; } = new List<Flag>();
        public virtual ICollection<Flag> FlagsCreated { get; set; } = new List<Flag>();
    }
}
