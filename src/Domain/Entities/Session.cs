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
        public int MaxStudentsPerRoom { get; set; } = 5;
        public int NumberOfRooms { get; set; } = 1;
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Assigned users - stored as comma-separated GUIDs
        public string? AssignedStudentIds { get; set; }
        public string? AssignedAssessorIds { get; set; }
        public string? AssignedModeratorIds { get; set; }

        // Navigation properties
        public virtual User? CreatedBy { get; set; }
        public virtual ICollection<SessionInstance> Instances { get; set; } = new List<SessionInstance>();

        // Helper methods for assigned users
        public List<Guid> GetAssignedStudents() => ParseIds(AssignedStudentIds);
        public List<Guid> GetAssignedAssessors() => ParseIds(AssignedAssessorIds);
        public List<Guid> GetAssignedModerators() => ParseIds(AssignedModeratorIds);

        public void SetAssignedStudents(IEnumerable<Guid>? ids) => AssignedStudentIds = ids != null ? string.Join(",", ids) : null;
        public void SetAssignedAssessors(IEnumerable<Guid>? ids) => AssignedAssessorIds = ids != null ? string.Join(",", ids) : null;
        public void SetAssignedModerators(IEnumerable<Guid>? ids) => AssignedModeratorIds = ids != null ? string.Join(",", ids) : null;

        private static List<Guid> ParseIds(string? ids)
        {
            if (string.IsNullOrWhiteSpace(ids)) return new List<Guid>();
            return ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : Guid.Empty)
                      .Where(id => id != Guid.Empty)
                      .ToList();
        }
    }
}
