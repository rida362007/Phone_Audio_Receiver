<p align="center"><img src="Assets/icon.png" width="110" alt="logo"></p>

# Phone Audio Receiver

Turn your Windows 10/11 laptop into a Bluetooth speaker for your Android phone (A2DP sink).
Native C# / WPF app using Windows' `AudioPlaybackConnection` API. Arabic & English UI.

## Features
- One-click connect to a paired phone, audio plays through the laptop speakers
- Auto-connect / auto-reconnect, remembers your phone
- Runs in the system tray, optional start with Windows
- Language chosen on first launch and saved

## Install
1. Pair your phone with Windows once (Settings → Bluetooth).
2. Download `PhoneAudioReceiver.exe` from [Releases](../../releases) and run it.
3. Select your phone → **Connect**, then on the phone tap the laptop's name and enable *Media audio*.

> Requires Windows 10 version 2004 (build 19041) or later. Phone call audio (HFP) is not supported by Windows.

## Build
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Pushing a tag like `v1.0.0` builds and publishes a release automatically (GitHub Actions).

## License
MIT
