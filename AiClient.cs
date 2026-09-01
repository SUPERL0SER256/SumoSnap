using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AIScreenshotUtility;

public class AiClient
{
    private static readonly HttpClient _httpClient = new HttpClient();
    
    private readonly AppSettings _settings;

    public AiClient()
    {
        _settings = SettingsManager.LoadSettings();
    }

    public class MissingKeyException : Exception
    {
        public MissingKeyException(string message) : base(message) { }
    }

    public async Task<BitmapSource> RemoveBackgroundAsync(BitmapSource sourceImage)
    {
        if (string.IsNullOrWhiteSpace(_settings.RemoveBgApiKey))
        {
            throw new MissingKeyException("Remove.bg");
        }

        // 1. Convert WPF BitmapSource to a byte array (PNG)
        byte[] imageBytes = GetImageBytes(sourceImage);

        // 2. Prepare the multipart form data request for remove.bg
        using var requestContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        
        requestContent.Add(imageContent, "image_file", "screenshot.png");
        requestContent.Add(new StringContent("auto"), "size");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _settings.RemoveBgApiKey);

        // 3. Send the request
        var response = await _httpClient.PostAsync("https://api.remove.bg/v1.0/removebg", requestContent);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Remove.bg API failed: {response.StatusCode} - {errorBody}");
        }

        // 4. Convert the response bytes back into a WPF BitmapSource
        byte[] resultBytes = await response.Content.ReadAsByteArrayAsync();
        return LoadImage(resultBytes);
    }

    public async Task<BitmapSource> EnhanceAsync(BitmapSource sourceImage)
    {
        if (string.IsNullOrWhiteSpace(_settings.StabilityApiKey))
        {
            throw new MissingKeyException("Stability AI");
        }

        byte[] imageBytes = GetImageBytes(sourceImage);

        using var requestContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(imageContent, "image", "screenshot.png");
        requestContent.Add(new StringContent("A high quality, high resolution, sharp version of this image"), "prompt");
        requestContent.Add(new StringContent("png"), "output_format");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stability.ai/v2beta/stable-image/upscale/conservative");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.StabilityApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        request.Content = requestContent;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Stability AI Enhance failed: {response.StatusCode} - {errorBody}");
        }

        byte[] resultBytes = await response.Content.ReadAsByteArrayAsync();
        return LoadImage(resultBytes);
    }

    public async Task<BitmapSource> ReframeAsync(BitmapSource sourceImage)
    {
        if (string.IsNullOrWhiteSpace(_settings.StabilityApiKey))
        {
            throw new MissingKeyException("Stability AI");
        }

        byte[] imageBytes = GetImageBytes(sourceImage);

        using var requestContent = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(imageContent, "image", "screenshot.png");
        
        // Expand the image by 200 pixels in every direction
        requestContent.Add(new StringContent("200"), "left");
        requestContent.Add(new StringContent("200"), "right");
        requestContent.Add(new StringContent("200"), "up");
        requestContent.Add(new StringContent("200"), "down");
        requestContent.Add(new StringContent("png"), "output_format");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stability.ai/v2beta/stable-image/edit/outpaint");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.StabilityApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        request.Content = requestContent;

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Stability AI Reframe failed: {response.StatusCode} - {errorBody}");
        }

        byte[] resultBytes = await response.Content.ReadAsByteArrayAsync();
        return LoadImage(resultBytes);
    }

    private byte[] GetImageBytes(BitmapSource sourceImage)
    {
        using var memoryStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(sourceImage));
        encoder.Save(memoryStream);
        return memoryStream.ToArray();
    }

    private BitmapSource LoadImage(byte[] imageData)
    {
        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(imageData))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze(); // Required for cross-thread operations
        }
        return bitmap;
    }
}
