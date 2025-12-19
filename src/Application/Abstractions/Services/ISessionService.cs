using ably_rest_apis.src.Domain.Entities;
using ably_rest_apis.src.Domain.Enums;

namespace ably_rest_apis.src.Application.Abstractions.Services
{
    /// <summary>
    /// Interface for session management operations
    /// </summary>
    public interface ISessionService
    {
        // Session CRUD
        Task<Session> CreateSessionAsync(Session session, Guid createdById);
        Task<Session?> GetSessionAsync(Guid sessionId);
        Task<List<Session>> GetAllSessionsAsync();

        // Session Instance Operations
        Task<SessionInstance> StartSessionAsync(Guid sessionId, Guid adminId);
        Task<SessionInstance> EndSessionAsync(Guid sessionId, Guid adminId);
        Task<SessionInstance?> GetActiveInstanceAsync(Guid sessionId);

        // Participant Operations
        Task<SessionParticipant> JoinSessionAsync(Guid sessionId, Guid userId);
        Task<bool> LeaveSessionAsync(Guid sessionId, Guid userId);
        Task<bool> DisconnectUserAsync(Guid sessionId, Guid userId);

        // Break Request Operations
        Task<BreakRequest> RequestBreakAsync(Guid sessionId, Guid studentId, string? reason);
        Task<BreakRequest> ApproveBreakAsync(Guid sessionId, Guid breakRequestId, Guid moderatorId);
        Task<BreakRequest> DenyBreakAsync(Guid sessionId, Guid breakRequestId, Guid moderatorId, string reason);
        Task<List<BreakRequest>> GetPendingBreakRequestsAsync(Guid sessionId);

        // Flag Operations
        Task<Flag> FlagUserAsync(Guid sessionId, Guid studentId, Guid assessorId, string reason);
        Task<Flag> EscalateFlagAsync(Guid sessionId, Guid flagId, Guid moderatorId);
        Task<List<Flag>> GetActiveFlagsAsync(Guid sessionId);

        // Room Operations
        Task<Room> CallNextStudentsAsync(Guid sessionId, Guid assessorId, List<Guid> studentIds);
        Task<List<Room>> GetActiveRoomsAsync(Guid sessionId);
    }
}
