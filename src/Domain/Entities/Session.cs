namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents an exam session configuration
    /// </summary>
    public class Session
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ScheduledStartTime { get; set; }
        public DateTime ScheduledEndTime { get; set; }
        public DateTime ReportingWindowStart { get; set; }
        public DateTime ReportingWindowEnd { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual User? CreatedBy { get; set; }
        public virtual ICollection<SessionInstance> Instances { get; set; } = new List<SessionInstance>();
    }
}
