using ably_rest_apis.src.Shared.Contracts;

namespace ably_rest_apis.src.Application.Abstractions.Messaging
{
    /// <summary>
    /// Interface for publishing events to Ably
    /// </summary>
    public interface IAblyPublisher
    {
        /// <summary>
        /// Publishes an event to the session channel
        /// </summary>
        /// <param name="sessionId">The session ID (channel will be session:{sessionId})</param>
        /// <param name="eventData">The event data to publish</param>
        /// <returns>True if published successfully</returns>
        Task<bool> PublishAsync(string sessionId, SessionEventDto eventData);

        /// <summary>
        /// Publishes an event to a specific channel
        /// </summary>
        /// <param name="channelName">Full channel name</param>
        /// <param name="eventName">Event name/type</param>
        /// <param name="eventData">The event data to publish</param>
        /// <returns>True if published successfully</returns>
        Task<bool> PublishToChannelAsync(string channelName, string eventName, object eventData);
    }
}
