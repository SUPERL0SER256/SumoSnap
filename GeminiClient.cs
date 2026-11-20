using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public class GeminiClient : IAiClient
{
    private static readonly HttpClient _httpClient = new HttpClient(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        EnableMultipleHttp2Connections = true,
        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
    })
    {
        DefaultRequestVersion = System.Net.HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly ConditionalWeakTable<BitmapSource, string> _encodedImageCache = new();

    // Sticky models: once a model succeeds, the app immediately reuses it for 0-latency future queries
    private static string? _stickyWorkingFlashModel;
    private static string? _stickyWorkingProModel;
    private static List<string>? _emergencyDiscoveredModels;

    private readonly string _apiKey;
    private readonly bool _isPro;

    public GeminiClient(bool isPro = false)
    {
        var settings = SettingsManager.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.GeminiApiKey))
        {
            throw new MissingKeyException("Gemini");
        }
        _apiKey = settings.GeminiApiKey;
        _isPro = isPro;
    }

    public async Task<AiResponse> ChatAsync(string userMessage, string systemPrompt)
    {
        var sysInstruction = new
        {
            parts = new[] { new { text = systemPrompt } }
        };

        var contentsArr = new[]
        {
            new { parts = new object[] { new { text = userMessage } } }
        };

        var fastBody = new
        {
            system_instruction = sysInstruction,
            contents = contentsArr,
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1024,
                thinkingConfig = new { thinkingBudget = 0 }
            }
        };

        var standardBody = new
        {
            system_instruction = sysInstruction,
            contents = contentsArr,
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1024
            }
        };

        string fastJson = JsonSerializer.Serialize(fastBody);
        string standardJson = JsonSerializer.Serialize(standardBody);

        return await SendWithRetryAndFallbackAsync(
            _isPro ? standardJson : fastJson,
            _isPro ? null : standardJson);
    }

    public async Task<AiResponse> ChatWithImageAsync(BitmapSource image, string userMessage)
    {
        string base64Image = GetOrEncodeFastJpegBase64(image);

        var sysInstruction = new
        {
            parts = new[]
            {
                new { text = "You are a helpful AI screenshot companion. Keep answers extremely brief, direct, and actionable. Do not use conversational filler. IMPORTANT: Do NOT use LaTeX, Markdown math ($ or $$), or complex formatting. Write all math, symbols, and equations in plain, simple text (e.g., use the word 'alpha' instead of Greek symbols, use standard text formatting for fractions). Return exactly what the user needs to know instantly." }
            }
        };

        var contentsArr = new[]
        {
            new
            {
                parts = new object[]
                {
                    new { text = userMessage },
                    new { inline_data = new { mime_type = "image/jpeg", data = base64Image } }
                }
            }
        };

        var fastBody = new
        {
            system_instruction = sysInstruction,
            contents = contentsArr,
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1024,
                thinkingConfig = new { thinkingBudget = 0 }
            }
        };

        var standardBody = new
        {
            system_instruction = sysInstruction,
            contents = contentsArr,
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1024
            }
        };

        string fastJson = JsonSerializer.Serialize(fastBody);
        string standardJson = JsonSerializer.Serialize(standardBody);

        return await SendWithRetryAndFallbackAsync(
            _isPro ? standardJson : fastJson,
            _isPro ? null : standardJson);
    }

    private List<string> GetPrimaryCandidateModels()
    {
        var list = new List<string>(6);
        void AddUnique(string? m)
        {
            if (!string.IsNullOrWhiteSpace(m) && !list.Contains(m))
                list.Add(m);
        }

        if (_isPro)
        {
            AddUnique(_stickyWorkingProModel);
            AddUnique("gemini-2.5-pro");
            AddUnique("gemini-2.5-flash");
            AddUnique("gemini-2.0-flash");
        }
        else
        {
            AddUnique(_stickyWorkingFlashModel);
            AddUnique("gemini-2.5-flash");
            AddUnique("gemini-2.5-flash-lite");
            AddUnique("gemini-2.0-flash");
            AddUnique("gemini-2.0-flash-lite");
            AddUnique("gemini-2.5-pro");
        }

        return list;
    }

    private async Task<List<string>> DiscoverEmergencyModelsAsync()
    {
        if (_emergencyDiscoveredModels != null && _emergencyDiscoveredModels.Count > 0)
            return _emergencyDiscoveredModels;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models");
            req.Headers.Add("x-goog-api-key", _apiKey);

            using var resp = await _httpClient.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return new List<string>();

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElement)) return new List<string>();

            var validModels = new List<string>();
            foreach (var m in modelsElement.EnumerateArray())
            {
                if (!m.TryGetProperty("name", out var nameProp)) continue;
                string rawName = nameProp.GetString() ?? "";
                string id = rawName.StartsWith("models/") ? rawName.Substring("models/".Length) : rawName;

                if (string.IsNullOrWhiteSpace(id)) continue;

                bool supportsGenerate = false;
                if (m.TryGetProperty("supportedGenerationMethods", out var methods) && methods.ValueKind == JsonValueKind.Array)
                {
                    foreach (var method in methods.EnumerateArray())
                    {
                        if (method.GetString() == "generateContent")
                        {
                            supportsGenerate = true;
                            break;
                        }
                    }
                }
                if (!supportsGenerate) continue;

                string lower = id.ToLowerInvariant();
                if (!lower.StartsWith("gemini-")) continue;
                if (lower.Contains("embedding") || lower.Contains("tts") || lower.Contains("image") ||
                    lower.Contains("audio") || lower.Contains("live") || lower.Contains("robotics") ||
                    lower.Contains("computer-use") || lower.Contains("aqa") || lower.Contains("veo") ||
                    lower.Contains("imagen") || lower.Contains("thinking"))
                {
                    continue;
                }

                validModels.Add(id);
            }

            int ScoreModel(string id)
            {
                if (id == "gemini-2.5-flash") return 100;
                if (id == "gemini-2.5-flash-lite") return 95;
                if (id == "gemini-2.0-flash") return 90;
                if (id.Contains("flash")) return 80;
                if (id == "gemini-2.5-pro") return 70;
                if (id.Contains("pro")) return 60;
                return 10;
            }

            _emergencyDiscoveredModels = validModels.OrderByDescending(ScoreModel).Take(5).ToList();
            return _emergencyDiscoveredModels;
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task<AiResponse> SendWithRetryAndFallbackAsync(string jsonPayload, string? fallbackJsonWithoutThinking)
    {
        var candidateModels = GetPrimaryCandidateModels();

        string lastError = "";
        bool allPrimaryNotFound = true;

        for (int attempt = 0; attempt < candidateModels.Count; attempt++)
        {
            string model = candidateModels[attempt];
            // Only send thinkingConfig to gemini-2.5-flash models; use standard payload for all other models
            string payloadForModel = (fallbackJsonWithoutThinking != null && !model.StartsWith("gemini-2.5-flash"))
                ? fallbackJsonWithoutThinking
                : jsonPayload;

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_isPro ? 30 : 12));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payloadForModel, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("x-goog-api-key", _apiKey);

                using var response = await _httpClient.SendAsync(request, cts.Token);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (_isPro) _stickyWorkingProModel = model;
                    else _stickyWorkingFlashModel = model;

                    return ExtractTextFromResponse(responseJson);
                }

                int code = (int)response.StatusCode;
                if (code != 404) allPrimaryNotFound = false;

                // If thinkingConfig caused a 400 Bad Request, retry immediately with standard payload
                if (code == 400 && fallbackJsonWithoutThinking != null && payloadForModel != fallbackJsonWithoutThinking)
                {
                    using var retryReq = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(fallbackJsonWithoutThinking, Encoding.UTF8, "application/json")
                    };
                    retryReq.Headers.Add("x-goog-api-key", _apiKey);

                    using var retryResp = await _httpClient.SendAsync(retryReq, cts.Token);
                    var retryJson = await retryResp.Content.ReadAsStringAsync();
                    if (retryResp.IsSuccessStatusCode)
                    {
                        if (_isPro) _stickyWorkingProModel = model;
                        else _stickyWorkingFlashModel = model;
                        return ExtractTextFromResponse(retryJson);
                    }
                    code = (int)retryResp.StatusCode;
                    responseJson = retryJson;
                }

                if (string.IsNullOrEmpty(lastError) || code != 404)
                {
                    lastError = $"{code} {response.StatusCode} ({model}) - {responseJson}";
                }

                bool canFailover = code == 503 || code == 502 || code == 500 || code == 429 || code == 404;
                if (!canFailover)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                allPrimaryNotFound = false;
                if (string.IsNullOrEmpty(lastError))
                    lastError = $"Model '{model}' timed out.";
            }
        }

        // Emergency self-healing: if all primary models failed with 404/not found, discover account-enabled models
        if (allPrimaryNotFound)
        {
            var emergencyModels = await DiscoverEmergencyModelsAsync();
            foreach (var model in emergencyModels)
            {
                if (candidateModels.Contains(model)) continue;

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
                string payload = fallbackJsonWithoutThinking ?? jsonPayload;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("x-goog-api-key", _apiKey);

                    using var response = await _httpClient.SendAsync(request, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (_isPro) _stickyWorkingProModel = model;
                        else _stickyWorkingFlashModel = model;
                        return ExtractTextFromResponse(json);
                    }
                }
                catch
                {
                    // Continue to next emergency model
                }
            }
        }

        throw new Exception($"Gemini API failed: {lastError}");
    }

    public static string GetOrEncodeFastJpegBase64(BitmapSource image)
    {
        if (_encodedImageCache.TryGetValue(image, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        BitmapSource sourceToEncode = image;
        const int maxDimension = 1536;
        int w = image.PixelWidth;
        int h = image.PixelHeight;

        if (w > maxDimension || h > maxDimension)
        {
            double scale = (double)maxDimension / Math.Max(w, h);
            var transformed = new TransformedBitmap(image, new ScaleTransform(scale, scale));
            transformed.Freeze();
            sourceToEncode = transformed;
        }

        using var ms = new MemoryStream();
        var encoder = new JpegBitmapEncoder { QualityLevel = 84 };
        encoder.Frames.Add(BitmapFrame.Create(sourceToEncode));
        encoder.Save(ms);
        string encoded = Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);

        _encodedImageCache.AddOrUpdate(image, encoded);
        return encoded;
    }

    private AiResponse ExtractTextFromResponse(string responseJson)
    {
        var response = new AiResponse();
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var candidates = root.GetProperty("candidates");
            var firstCandidate = candidates[0];
            var contentObj = firstCandidate.GetProperty("content");
            var parts = contentObj.GetProperty("parts");

            string? extractedText = null;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thoughtProp) && thoughtProp.ValueKind == JsonValueKind.True)
                    continue;
                if (part.TryGetProperty("text", out var textProp))
                    extractedText = textProp.GetString();
            }

            if (extractedText == null && parts.GetArrayLength() > 0 && parts[0].TryGetProperty("text", out var fallbackText))
            {
                extractedText = fallbackText.GetString();
            }

            response.Text = extractedText ?? "No response received.";

            if (root.TryGetProperty("usageMetadata", out var usageObj) && usageObj.TryGetProperty("totalTokenCount", out var tokenProp))
            {
                response.TokensUsed = tokenProp.GetInt32();
            }
        }
        catch
        {
            response.Text = "Failed to parse Gemini response.";
        }
        return response;
    }
}
