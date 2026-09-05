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

    public async Task<string> ChatWithImageAsync(BitmapSource image, string userMessage)
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
                    content = "You are a helpful AI screenshot companion. Keep answers extremely brief, direct, and actionable. Do not use conversational filler like 'Here is the answer' or 'Sure!'. Return exactly what the user needs to know instantly."
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
            var choices = root.GetProperty("choices");
            var firstChoice = choices[0];
            var message = firstChoice.GetProperty("message");
            return message.GetProperty("content").GetString() ?? "No response received.";
        }
        catch
        {
            return "Failed to parse OpenAI response.";
        }
    }
}
