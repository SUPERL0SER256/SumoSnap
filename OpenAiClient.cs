using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public class OpenAiClient : IAiClient
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly string _apiKey;

    public OpenAiClient()
    {
        var settings = SettingsManager.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
        {
            throw new MissingKeyException("OpenAI");
        }
        _apiKey = settings.OpenAiApiKey;
    }

        public async Task<AiResponse> ChatAsync(string userMessage, string systemPrompt)
    {
        var requestBody = new
        {
            model = "gpt-4o",
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions") { Content = content };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        using var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new Exception($"OpenAI API Error: {responseJson}");
        return ExtractTextFromResponse(responseJson);
    }

    public async Task<AiResponse> ChatWithImageAsync(BitmapSource image, string userMessage)
    {
        string base64Image = ImageToBase64(image);

        var requestBody = new
        {
            model = "gpt-4o",
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a helpful AI screenshot companion. Keep answers extremely brief, direct, and actionable. Do not use conversational filler. IMPORTANT: Do NOT use LaTeX, Markdown math ($ or $$), or complex formatting. Write all math, symbols, and equations in plain, simple text (e.g., use the word 'alpha' instead of Greek symbols). Return exactly what the user needs to know instantly."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = userMessage },
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                    }
                }
            },
            max_tokens = 500
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI API failed: {response.StatusCode} - {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        return ExtractTextFromResponse(responseJson);
    }

    public async Task<string> TranscribeAudioAsync(byte[] wavData)
    {
        using var content = new MultipartFormDataContent();
        using var audioContent = new ByteArrayContent(wavData);
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        content.Add(audioContent, "file", "audio.wav");
        content.Add(new StringContent("whisper-1"), "model");
        
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI Whisper failed: {response.StatusCode} - {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("text").GetString() ?? "";
    }

    private string ImageToBase64(BitmapSource image)
    {
        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private AiResponse ExtractTextFromResponse(string responseJson)
    {
        var response = new AiResponse();
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var choices = root.GetProperty("choices");
            response.Text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "No response received.";

            if (root.TryGetProperty("usage", out var usageObj) && usageObj.TryGetProperty("total_tokens", out var tokenProp))
            {
                response.TokensUsed = tokenProp.GetInt32();
            }
        }
        catch
        {
            response.Text = "Failed to parse OpenAI response.";
        }
        return response;
    }
}




