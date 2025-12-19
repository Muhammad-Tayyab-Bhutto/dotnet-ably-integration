namespace ably_rest_apis.src.Domain.Enums
{
    /// <summary>
    /// Types of events that can be published
    /// </summary>
    public enum EventType
    {
        SESSION_STARTED,
        USER_JOINED,
        USER_DISCONNECTED,
        BREAK_REQUESTED,
        BREAK_APPROVED,
        FLAG_USER,
        FLAG_ESCALATED,
        CALL_NEXT_STUDENTS,
        ROOM_CREATED,
        SESSION_ENDED
    }
}
