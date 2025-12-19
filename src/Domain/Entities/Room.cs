namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents an assessment room within a session
    /// </summary>
    public class Room
    {
        public Guid Id { get; set; }
        public Guid SessionInstanceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public DateTime? ClosedAt { get; set; }

        // Navigation properties
        public virtual SessionInstance? SessionInstance { get; set; }
        public virtual ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
    }
}
