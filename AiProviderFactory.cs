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
            _ => new GeminiClient() // Default to Gemini
        };
    }
}
