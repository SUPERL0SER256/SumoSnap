using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public interface IAiClient
{
    Task<string> ChatWithImageAsync(BitmapSource image, string userMessage);
}

public class MissingKeyException : Exception
{
    public MissingKeyException(string providerName) 
        : base($"{providerName}")
    {
    }
}
