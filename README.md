# SumoSnap

SumoSnap is a lightning-fast, distraction-free AI screenshot companion for Windows. Built for power users who want instant answers about what's on their screen without juggling multiple browser tabs or bloated apps.

## Features

- **Instant Capture:** Runs silently in your system tray and binds to your screenshot hotkey.
- **Multi-Model AI Chat:** Seamlessly switch between the best vision models in the world:
  - Google Gemini (3.6 Flash)
  - OpenAI (GPT-4o)
  - Anthropic (Claude 3.5 Sonnet)
- **Zero Distractions:** An ultra-minimal UI focused entirely on your screenshot and your conversation.
- **Brief & Direct:** The AI is strictly prompted to avoid conversational filler and give you the exact information you need instantly.
- **Local Keys:** Your API keys are saved locally on your machine and requests are sent directly to the AI providers. No subscriptions, no middlemen.

## Installation

Download the latest standalone `.exe` from the [Releases](#) page.

Alternatively, to run the source directly:
```powershell
.\run.ps1
```

## How to Use

1. Launch `SumoSnap.exe`. It will minimize to your system tray.
2. Hit `PrintScreen` (or your configured hotkey) to capture your screen.
3. The SumoSnap editor will instantly pop up. 
4. Type your question in the bottom chat bar and hit Enter. The AI will analyze your screenshot and respond immediately!

## Configuration

Click the ⚙ (Settings) icon in the top right corner of the editor to enter your preferred API keys and toggle between Gemini, OpenAI, and Anthropic.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
