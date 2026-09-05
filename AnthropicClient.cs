using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public class AnthropicClient : IAiClient
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly string _apiKey;

    public AnthropicClient()
    {
        var settings = SettingsManager.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.AnthropicApiKey))
        {
            throw new MissingKeyException("Anthropic");
        }
        _apiKey = settings.AnthropicApiKey;
    }

    public async Task<string> ChatWithImageAsync(BitmapSource image, string userMessage)
    {
        string base64Image = ImageToBase64(image);

        var requestBody = new
        {
            model = "claude-3-5-sonnet-20240620",
            max_tokens = 500,
            system = "You are a helpful AI screenshot companion. Keep answers extremely brief, direct, and actionable. Do not use conversational filler like 'Here is the answer' or 'Sure!'. Return exactly what the user needs to know instantly.",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image", source = new { type = "base64", media_type = "image/png", data = base64Image } },
                        new { type = "text", text = userMessage }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Anthropic API failed: {response.StatusCode} - {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        return ExtractTextFromResponse(responseJson);
    }

    private string ImageToBase64(BitmapSource image)
    {
        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
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
            var content = root.GetProperty("content");
            var firstContent = content[0];
            return firstContent.GetProperty("text").GetString() ?? "No response received.";
        }
        catch
        {
            return "Failed to parse Anthropic response.";
        }
    }
}
