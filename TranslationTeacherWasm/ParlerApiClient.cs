using System.Text;
using System.Text.Json;

namespace TranslationTeacherWasm;

public class ParlerApiClient
{    
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly TimeSpan Timeout;

    public ParlerApiClient(string baseUrl, TimeSpan timeout)
    {
        _httpClient = new HttpClient();
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient.Timeout = timeout;
    }

    public async Task<byte[]> TextToSpeechAsync(string text, string speaker = null)
    {
        var payload = new
        {
            text,
            speaker = speaker ?? "A female speaker delivers a slightly expressive and animated speech with a moderate speed and pitch. The recording is of very high quality, with the speaker's voice sounding clear and very close up."
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/tts", content);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsByteArrayAsync();
        }
        else
        {
            throw new HttpRequestException($"Error: {response.StatusCode}");
        }
    }
}