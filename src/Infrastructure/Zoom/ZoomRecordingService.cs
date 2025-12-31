using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ably_rest_apis.src.Infrastructure.Zoom
{
    public interface IZoomRecordingService
    {
        /// <summary>
        /// Gets cloud recordings for a specific session (by topic name)
        /// </summary>
        /// <param name="sessionName">The session topic name (e.g. exam-{sessionId})</param>
        /// <param name="from">Start date for search</param>
        /// <param name="to">End date for search</param>
        /// <returns>List of recordings</returns>
        Task<List<ZoomRecording>> GetRecordingsForSessionAsync(string sessionName, DateTime from, DateTime to);
    }

    public class ZoomRecordingService : IZoomRecordingService
    {
        private readonly HttpClient _httpClient;
        private readonly IZoomJwtService _zoomJwtService;
        private readonly ILogger<ZoomRecordingService> _logger;
        private const string BaseUrl = "https://api.zoom.us/v2";

        public ZoomRecordingService(HttpClient httpClient, IZoomJwtService zoomJwtService, ILogger<ZoomRecordingService> logger)
        {
            _httpClient = httpClient;
            _zoomJwtService = zoomJwtService;
            _logger = logger;
        }

        public async Task<List<ZoomRecording>> GetRecordingsForSessionAsync(string sessionName, DateTime from, DateTime to)
        {
            try
            {
                // 1. Get API Token
                var token = _zoomJwtService.GenerateApiToken();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // 2. List Sessions in date range to find the correct Session ID(s)
                // Video SDK uses /videosdk/sessions
                var fromStr = from.ToString("yyyy-MM-dd");
                var toStr = to.ToString("yyyy-MM-dd");
                
                var sessionsUrl = $"{BaseUrl}/videosdk/sessions?from={fromStr}&to={toStr}";
                var sessionResponse = await _httpClient.GetAsync(sessionsUrl);
                
                if (!sessionResponse.IsSuccessStatusCode)
                {
                    var error = await sessionResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to list Zoom sessions: {StatusCode} {Error}", sessionResponse.StatusCode, error);
                    return new List<ZoomRecording>();
                }

                var sessionsJson = await sessionResponse.Content.ReadAsStringAsync();
                var sessionsList = JsonSerializer.Deserialize<ZoomSessionListResponse>(sessionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (sessionsList?.Sessions == null || !sessionsList.Sessions.Any())
                {
                    _logger.LogInformation("No Zoom sessions found between {From} and {To}", fromStr, toStr);
                    return new List<ZoomRecording>();
                }

                // 3. Filter sessions by topic matches our sessionName
                var matchingSessions = sessionsList.Sessions
                    .Where(s => s.Topic == sessionName)
                    .ToList();

                if (!matchingSessions.Any())
                {
                    _logger.LogWarning("Found sessions but none matched topic: {Topic}", sessionName);
                    return new List<ZoomRecording>();
                }

                var allRecordings = new List<ZoomRecording>();

                // 4. For each matching session (could be multiple if restarted), get recordings
                foreach (var session in matchingSessions)
                {
                    // Video SDK recording endpoint: /videosdk/sessions/{sessionId}/recordings
                    // Note: Use session.Id
                    var recordingsUrl = $"{BaseUrl}/videosdk/sessions/{session.Id}/recordings";
                    var recordingResponse = await _httpClient.GetAsync(recordingsUrl);

                    if (!recordingResponse.IsSuccessStatusCode)
                    {
                        var error = await recordingResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to get recordings for session {SessionId}: {StatusCode} {Error}", session.Id, recordingResponse.StatusCode, error);
                        continue;
                    }

                    var recordingJson = await recordingResponse.Content.ReadAsStringAsync();
                    var recordingData = JsonSerializer.Deserialize<ZoomRecordingListResponse>(recordingJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (recordingData?.RecordingFiles != null)
                    {
                        foreach (var file in recordingData.RecordingFiles)
                        {
                            allRecordings.Add(new ZoomRecording
                            {
                                SessionId = session.Id,
                                StartTime = session.StartTime, // Session start, not file start (though file has recording_start)
                                DownloadUrl = file.DownloadUrl,
                                PlayUrl = file.PlayUrl, // Need to check if Video SDK returns play_url
                                FileType = file.FileType,
                                FileSize = file.FileSize,
                                Duration = file.Duration, // In seconds?
                                RecordingStart = file.RecordingStart,
                                RecordingEnd = file.RecordingEnd
                            });
                        }
                    }
                }

                return allRecordings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Zoom recordings for {SessionName}", sessionName);
                throw;
            }
        }
    }

    // DTOs for Zoom API Responses

    public class ZoomSessionListResponse
    {
        [JsonPropertyName("sessions")]
        public List<ZoomSessionItem> Sessions { get; set; } = new();
    }

    public class ZoomSessionItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = ""; // Session ID

        [JsonPropertyName("topic")]
        public string Topic { get; set; } = "";

        [JsonPropertyName("start_time")]
        public DateTime StartTime { get; set; }
        
        [JsonPropertyName("end_time")]
        public DateTime? EndTime { get; set; }
    }

    public class ZoomRecordingListResponse
    {
        [JsonPropertyName("recording_files")]
        public List<ZoomRecordingFile> RecordingFiles { get; set; } = new();
    }

    public class ZoomRecordingFile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("play_url")]
        public string PlayUrl { get; set; } = "";

        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = ""; // MP4, M4A

        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }

        [JsonPropertyName("recording_start")]
        public DateTime RecordingStart { get; set; }

        [JsonPropertyName("recording_end")]
        public DateTime RecordingEnd { get; set; }
        
        // Sometimes duration is not directly on file object in all APIs, but let's assume it is or calculate
        [JsonPropertyName("duration")] // Documentation says duration exists
        public int Duration { get; set; }
    }

    public class ZoomRecording
    {
        public string SessionId { get; set; } = "";
        public DateTime StartTime { get; set; } // Session start
        public DateTime RecordingStart { get; set; }
        public DateTime RecordingEnd { get; set; }
        public string DownloadUrl { get; set; } = "";
        public string PlayUrl { get; set; } = "";
        public string FileType { get; set; } = "";
        public long FileSize { get; set; }
        public int Duration { get; set; }
    }
}
