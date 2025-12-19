using IO.Ably;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ably_rest_apis.src.Application.Abstractions.Messaging;
using ably_rest_apis.src.Shared.Contracts;

namespace ably_rest_apis.src.Infrastructure.Messaging
{
    /// <summary>
    /// Ably publisher implementation for real-time event publishing
    /// </summary>
    public class AblyPublisher : IAblyPublisher
    {
        private readonly AblyRest _ablyClient;
        private readonly ILogger<AblyPublisher> _logger;

        public AblyPublisher(IConfiguration configuration, ILogger<AblyPublisher> logger)
        {
            var apiKey = configuration["Ably:ApiKey"] 
                ?? throw new InvalidOperationException("Ably API key not configured");
            
            _ablyClient = new AblyRest(apiKey);
            _logger = logger;
        }

        public async Task<bool> PublishAsync(string sessionId, SessionEventDto eventData)
        {
            var channelName = $"session:{sessionId}";
            return await PublishToChannelAsync(channelName, eventData.Type, eventData);
        }

        public async Task<bool> PublishToChannelAsync(string channelName, string eventName, object eventData)
        {
            try
            {
                var channel = _ablyClient.Channels.Get(channelName);
                var jsonData = JsonConvert.SerializeObject(eventData);
                
                await channel.PublishAsync(eventName, jsonData);
                
                _logger.LogInformation(
                    "Published event {EventName} to channel {ChannelName}",
                    eventName,
                    channelName);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to publish event {EventName} to channel {ChannelName}",
                    eventName,
                    channelName);
                
                return false;
            }
        }
    }
}
