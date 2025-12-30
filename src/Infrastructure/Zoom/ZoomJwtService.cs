using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ably_rest_apis.src.Infrastructure.Zoom
{
    /// <summary>
    /// Interface for Zoom Video SDK JWT generation
    /// </summary>
    public interface IZoomJwtService
    {
        /// <summary>
        /// Generates a JWT token for Zoom Video SDK session
        /// </summary>
        /// <param name="sessionName">The session/topic name</param>
        /// <param name="userId">Unique user identifier</param>
        /// <param name="role">0 = participant, 1 = host</param>
        /// <returns>JWT token string</returns>
        string GenerateSessionToken(string sessionName, string userId, int role = 0);
    }

    /// <summary>
    /// Zoom Video SDK JWT generation service
    /// </summary>
    public class ZoomJwtService : IZoomJwtService
    {
        private readonly string _sdkKey;
        private readonly string _sdkSecret;
        private readonly ILogger<ZoomJwtService> _logger;

        public ZoomJwtService(IConfiguration configuration, ILogger<ZoomJwtService> logger)
        {
            _sdkKey = configuration["Zoom:SdkKey"] 
                ?? throw new InvalidOperationException("Zoom SDK Key not configured");
            _sdkSecret = configuration["Zoom:SdkSecret"] 
                ?? throw new InvalidOperationException("Zoom SDK Secret not configured");
            _logger = logger;
        }

        public string GenerateSessionToken(string sessionName, string userId, int role = 0)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var iat = now.ToUnixTimeSeconds();
                var exp = now.AddHours(24).ToUnixTimeSeconds(); // Token valid for 24 hours

                // JWT Header
                var header = new
                {
                    alg = "HS256",
                    typ = "JWT"
                };

                // JWT Payload for Video SDK
                var payload = new
                {
                    app_key = _sdkKey,
                    tpc = sessionName, // Topic/session name
                    role_type = role, // 0 = participant, 1 = host
                    user_identity = userId,
                    version = 1,
                    iat = iat,
                    exp = exp
                };

                // Create JWT
                var headerJson = JsonSerializer.Serialize(header);
                var payloadJson = JsonSerializer.Serialize(payload);

                var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
                var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

                var signatureInput = $"{headerBase64}.{payloadBase64}";
                var signature = ComputeHmacSha256(signatureInput, _sdkSecret);

                var jwt = $"{headerBase64}.{payloadBase64}.{signature}";

                _logger.LogInformation(
                    "Generated Zoom JWT for session {SessionName}, user {UserId}, role {Role}",
                    sessionName, userId, role);

                return jwt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Zoom JWT");
                throw;
            }
        }

        private static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input);
            output = output.Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return output;
        }

        private static string ComputeHmacSha256(string input, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Base64UrlEncode(hash);
        }
    }
}
