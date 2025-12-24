namespace ably_rest_apis.src.Domain.Enums
{
    /// <summary>
    /// Status of a flag in the review workflow
    /// </summary>
    public enum FlagStatus
    {
        Pending,    // Awaiting moderator review
        Accepted,   // Moderator accepted - student kicked
        Rejected    // Moderator rejected - no action
    }
}
