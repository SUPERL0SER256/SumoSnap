<div align="center">
  
# SumoSnap
**The heavy-hitting, lightweight screenshot utility that brings real AI to your desktop.**

![Windows 11](https://img.shields.io/badge/Windows-11%20%7C%2010-0078D6?style=for-the-badge&logo=windows)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

<!-- TODO: USER, take a screenshot of the SumoSnap editor and drag-and-drop it into the GitHub web editor right here to replace this placeholder text! -->
<br>
<i>[ PLACEHOLDER: Insert your screenshot here! ]</i>
<br><br>
</div>

SumoSnap is a completely free, background screenshot utility built to replace the default Windows Snipping Tool. It sits silently in your system tray taking up virtually zero memory until you summon it. When you do, it hits your screenshots with state-of-the-art AI.

There are no bulky windows or loading screens. Just press the hotkey, drag a box, and your screenshot pops up with a few extra superpowers. 

## Features

- **Instant Capture:** Press `Ctrl + Shift + Q` (or `Print Screen`) to instantly freeze your screen and drag a region.
- **AI Background Removal:** Powered by the Remove.bg API. Turn any screenshot into a perfect transparent PNG instantly.
- **AI Enhance:** Screenshot too tiny or blurry? SumoSnap uses Stability AI to generate crisp, high-resolution pixels out of thin air.
- **AI Reframe:** Grabbed a screenshot but missed the edges? The Reframe button outpaints the image, magically generating 200 pixels of extra context around your edges based on what it thinks should be there.
- **Bring Your Own Key (BYOK):** No expensive monthly SaaS subscriptions. You plug in your own API keys, they get saved securely to your local Windows profile, and you only pay for exactly what you use.

## How to Install & Run

You don't need to touch any code to get SumoSnap running.

1. Go to the [Releases page](../../releases) and download the latest `SumoSnap-v1.0.zip`.
2. Extract the folder anywhere on your computer.
3. Double-click `SumoSnap.exe`. 
4. A balloon will pop up in your system tray letting you know it is running in the background! 

## Getting Your API Keys

When you first try to use an AI feature, SumoSnap will gently ask for your API keys. It is totally free to get started:

1. Grab a free key for background removal at [remove.bg/api](https://www.remove.bg/api)
2. Grab a key for upscaling and outpainting at [platform.stability.ai](https://platform.stability.ai/account/keys)

Just right-click the SumoSnap icon in your system tray, click **Settings**, paste them in, and you are officially unstoppable.

---
*Built for Windows. Powered by C# and WPF.*
