using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SumoSnap;

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

    private StringContent CreateStringContent(string name, string value)
    {
        var content = new StringContent(value);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = $"\"{name}\""
        };
        return content;
    }

    private ByteArrayContent CreateImageContent(byte[] imageBytes)
    {
        var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"image\"",
            FileName = "\"screenshot.png\""
        };
        return content;
    }

    public async Task<BitmapSource> EnhanceAsync(BitmapSource sourceImage)
    {
        if (string.IsNullOrWhiteSpace(_settings.StabilityApiKey))
        {
            throw new MissingKeyException("Stability AI");
        }

        byte[] imageBytes = GetImageBytes(sourceImage);

        using var requestContent = new MultipartFormDataContent();
        requestContent.Add(CreateImageContent(imageBytes));
        requestContent.Add(CreateStringContent("prompt", "A high quality, high resolution, sharp version of this image"));
        requestContent.Add(CreateStringContent("output_format", "png"));

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
        requestContent.Add(CreateImageContent(imageBytes));
        
        // Expand the image by 200 pixels in every direction
        requestContent.Add(CreateStringContent("left", "200"));
        requestContent.Add(CreateStringContent("right", "200"));
        requestContent.Add(CreateStringContent("up", "200"));
        requestContent.Add(CreateStringContent("down", "200"));
        requestContent.Add(CreateStringContent("output_format", "png"));

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
        var paddedImage = PadForStability(sourceImage);
        using var memoryStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(paddedImage));
        encoder.Save(memoryStream);
        return memoryStream.ToArray();
    }

    private BitmapSource PadForStability(BitmapSource source)
    {
        double width = source.PixelWidth;
        double height = source.PixelHeight;

        // Stability API requires aspect ratio between 1:2.5 and 2.5:1
        double ratio = width / height;
        double targetWidth = width;
        double targetHeight = height;

        if (ratio > 2.5)
        {
            // Too wide, pad height
            targetHeight = width / 2.5;
        }
        else if (ratio < (1.0 / 2.5))
        {
            // Too tall, pad width
            targetWidth = height / 2.5;
        }
        
        // Also ensure minimum 64x64 as per Stability API docs
        if (targetWidth < 64) targetWidth = 64;
        if (targetHeight < 64) targetHeight = 64;

        if (targetWidth == source.PixelWidth && targetHeight == source.PixelHeight)
        {
            return source;
        }

        var visual = new System.Windows.Media.DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            // Center the original image in the new padded transparent canvas
            double x = (targetWidth - width) / 2;
            double y = (targetHeight - height) / 2;
            ctx.DrawImage(source, new System.Windows.Rect(x, y, width, height));
        }

        var padded = new RenderTargetBitmap((int)Math.Ceiling(targetWidth), (int)Math.Ceiling(targetHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        padded.Render(visual);
        return padded;
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
