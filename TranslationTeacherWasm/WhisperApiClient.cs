using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;

namespace TranslationTeacherWasm;


public class WhisperApiClient
{
    private readonly HttpClient _httpClient;
    private ILogger<WhisperApiClient> Logger { get; set; }

    public WhisperApiClient(string baseAddress, ILoggerFactory? loggerFactory) 
        : this(new HttpClient { BaseAddress = new Uri(baseAddress) }, loggerFactory)
    {
    }
    
    public WhisperApiClient(HttpClient httpClient, ILoggerFactory? loggerFactory)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AthenaCLI");
        Logger = loggerFactory?.CreateLogger<WhisperApiClient>() ?? NullLogger<WhisperApiClient>.Instance;
    }

    public async Task<string> TranscribeAsync<T>(
        T audio,
        bool encode = true,
        string task = "transcribe",
        string language = "en",
        string initialPrompt = "",
        bool diarize = true,
        int minSpeakers = 1,
        int maxSpeakers = 10,
        string output = "text"
    )
    {
        Logger.LogInformation("Starting transcription process...");

        HttpContent audioContent = audio switch
        {
            Stream stream => new StreamContent(stream),
            byte[] bytes => new ByteArrayContent(bytes),
            _ => throw new ArgumentException($"Unsupported audio type: {typeof(T)}", nameof(audio))
        };
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        audioContent.Headers.ContentDisposition =  new ContentDispositionHeaderValue("form-data")
        {
            Name = "audio_file",
            FileName = "audio.wav"
        };
        
        var formData = new MultipartFormDataContent();
        formData.Add(audioContent);
        
        // Build query parameters
        var queryParams = $"?encode={encode}&task={task}&language={language}&diarize={diarize}&min_speakers={minSpeakers}&max_speakers={maxSpeakers}&output={output}";
        if (!string.IsNullOrEmpty(initialPrompt))
        {
            queryParams += $"&initial_prompt={Uri.EscapeDataString(initialPrompt)}";
        }

        // Make the POST request with query parameters
        var response = await _httpClient.PostAsync($"/asr{queryParams}", formData);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError($"Failed to transcribe audio: {response.StatusCode}");
            throw new HttpRequestException($"Error: {response.StatusCode}");
        }
        
        return await response.Content.ReadAsStringAsync();
    }
}