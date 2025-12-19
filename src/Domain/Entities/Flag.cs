namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents a flag raised against a student
    /// </summary>
    public class Flag
    {
        public Guid Id { get; set; }
        public Guid SessionInstanceId { get; set; }
        public Guid StudentId { get; set; }
        public Guid FlaggedById { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool IsEscalated { get; set; } = false;
        public Guid? EscalatedById { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; } = false;
        public string? Resolution { get; set; }

        // Navigation properties
        public virtual SessionInstance? SessionInstance { get; set; }
        public virtual User? Student { get; set; }
        public virtual User? FlaggedBy { get; set; }
        public virtual User? EscalatedBy { get; set; }
    }
}
