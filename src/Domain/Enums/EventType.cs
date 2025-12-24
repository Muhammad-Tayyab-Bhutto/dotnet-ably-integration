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
        FLAG_ACCEPTED,
        FLAG_REJECTED,
        USER_KICKED,
        STUDENT_WAITING,
        CALLED_TO_ROOM,
        RETURNED_FROM_BREAK,
        CALL_NEXT_STUDENTS,
        ROOM_CREATED,
        SESSION_ENDED
    }
}
