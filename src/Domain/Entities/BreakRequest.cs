using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Domain.Entities
{
    /// <summary>
    /// Represents a break request from a student
    /// </summary>
    public class BreakRequest
    {
        public Guid Id { get; set; }
        public Guid SessionInstanceId { get; set; }
        public Guid StudentId { get; set; }
        public BreakRequestStatus Status { get; set; } = BreakRequestStatus.Pending;
        public string? Reason { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public Guid? ApprovedById { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? DenialReason { get; set; }

        // Navigation properties
        public virtual SessionInstance? SessionInstance { get; set; }
        public virtual User? Student { get; set; }
        public virtual User? ApprovedBy { get; set; }
    }
}
