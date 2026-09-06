using System;

namespace SumoSnap;

public static class AiProviderFactory
{
    public static IAiClient CreateClient()
    {
        var settings = SettingsManager.LoadSettings();
        
        return settings.ActiveProvider switch
        {
            "OpenAI" => new OpenAiClient(),
            "Anthropic" => new AnthropicClient(),
            "GeminiPro" => new GeminiClient(isPro: true),
            _ => new GeminiClient(isPro: false) // Default to Gemini Flash
        };
    }
}
