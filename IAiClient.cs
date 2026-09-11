using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public class AiResponse 
{
    public string Text { get; set; } = "";
    public int TokensUsed { get; set; } = 0;
}

public interface IAiClient
{
    Task<AiResponse> ChatWithImageAsync(BitmapSource image, string userMessage);
}

public class MissingKeyException : Exception
{
    public MissingKeyException(string providerName) 
        : base($"{providerName}")
    {
    }
}
