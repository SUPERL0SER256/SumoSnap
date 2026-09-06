using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public class OllamaClient : IAiClient
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly string _url;
    private readonly string _model;

    public OllamaClient()
    {
        var settings = SettingsManager.LoadSettings();
        _url = string.IsNullOrWhiteSpace(settings.OllamaUrl) ? "http://localhost:11434" : settings.OllamaUrl;
        _model = string.IsNullOrWhiteSpace(settings.OllamaModel) ? "llava" : settings.OllamaModel;
    }

    public async Task<string> ChatWithImageAsync(BitmapSource image, string userMessage)
    {
        string base64Image = ImageToBase64(image);

        var requestBody = new
        {
            model = _model,
            stream = false,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a helpful AI screenshot companion. Keep answers extremely brief, direct, and actionable. Do not use conversational filler like 'Here is the answer' or 'Sure!'. Return exactly what the user needs to know instantly."
                },
                new
                {
                    role = "user",
                    content = userMessage,
                    images = new[] { base64Image }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        string endpoint = _url.EndsWith("/") ? $"{_url}api/chat" : $"{_url}/api/chat";
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = content;

        try 
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Ollama API failed: {response.StatusCode} - {errorBody}\nMake sure Ollama is running and the model '{_model}' is pulled.");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return ExtractTextFromResponse(responseJson);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Could not connect to Ollama at {_url}. Is it running?\nError: {ex.Message}");
        }
    }

    private string ImageToBase64(BitmapSource image)
    {
        using var ms = new MemoryStream();
        var encoder = new JpegBitmapEncoder(); // Jpeg is slightly safer/smaller for Ollama
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private string ExtractTextFromResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var message = root.GetProperty("message");
            return message.GetProperty("content").GetString() ?? "No response received.";
        }
        catch
        {
            return "Failed to parse Ollama response.";
        }
    }
}
