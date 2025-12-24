namespace ably_rest_apis.src.Domain.Enums
{
    /// <summary>
    /// Status of a participant in a session
    /// </summary>
    public enum ParticipantStatus
    {
        Waiting,    // In waiting queue
        InRoom,     // Currently in a room
        OnBreak,    // Approved break
        Left,       // Voluntarily left
        Kicked      // Removed by moderator/flag acceptance
    }
}
