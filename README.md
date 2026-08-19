# HidaChat

[![English](https://img.shields.io/badge/Language-English-blue.svg)](README.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](README.it.md)

**Portable Windows Desktop Client (.NET 9 / WPF)** featuring **Multi-Account & Multi-Platform** tab management (**WhatsApp Web** & **Telegram Web**), **Instant Background Preloading**, **Built-In Message Translation**, **Native Toast Notifications & Popups**, and **Zero Installation**.

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE.txt)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Download Release](https://img.shields.io/github/v/release/hidaba/HidaChat?color=green&label=Download%20Windows)](https://github.com/hidaba/HidaChat/releases/latest)
[![Total Downloads](https://img.shields.io/github/downloads/hidaba/HidaChat/total)](https://github.com/hidaba/HidaChat/releases)
[![Last Commit](https://img.shields.io/github/last-commit/hidaba/HidaChat)](https://github.com/hidaba/HidaChat/commits/master)
[![Build Status](https://img.shields.io/github/actions/workflow/status/hidaba/HidaChat/build.yml?branch=master)](https://github.com/hidaba/HidaChat/actions)

---

## 📸 Screenshots & Preview

| **Multi-Account Interface (Dark Mode)** | **Instant Message Translation** |
|:---:|:---:|
| ![Multi-Account Interface](images/screenshot_main.png) | ![Message Translation](images/screenshot_translation.png) |
| **Native Windows Toast Notifications** | **Dark & Light Themes** |
| ![Windows Toast Notifications](images/screenshot_toast.png) | ![Dark and Light Themes](images/screenshot_themes.png) |

---

## 📦 Quick Download & Installation for Windows

### Option 1: Install via Windows Package Manager (`winget`)
```powershell
winget install hidaba.HidaChat
```

### Option 2: Portable ZIP (No Installation)
Get the latest ready-to-use portable release for Windows (ZIP archive):
- ⬇️ **[Download Latest Portable Release (GitHub Releases)](https://github.com/hidaba/HidaChat/releases/latest)**
- 📂 Extract the ZIP file anywhere (Local Drive or USB Flash Drive) and launch `HidaChat.exe`.

> ⚠️ **Important Portability Note**: Do not run HidaChat simultaneously from multiple computers accessing the same shared network folder. HidaChat is designed to be used by one PC at a time to prevent WebView2 profile lock conflicts.

---

## 🌟 Why HidaChat? (Comparison)

| Feature | HidaChat | Official WhatsApp Desktop | Official Telegram Desktop | Altus (`amanharwara/altus`) |
|---|:---:|:---:|:---:|:---:|
| **Installation Required** | ❌ **No (100% Portable)** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Moveable Data / Portable** | ✅ **Yes (ZIP / USB)** | ❌ No | ❌ No | ❌ No |
| **Multi-Platform in One App** | ✅ **Yes (WhatsApp & Telegram)** | ❌ WhatsApp only | ❌ Telegram only | ❌ WhatsApp only |
| **Multi-Account Tabs** | ✅ **Yes (Isolated Profiles)** | ❌ No | ⚠️ Switcher only | ✅ Yes |
| **Instant Tab Preloading** | ✅ **Yes (Zero Reload Lag)** | ❌ No | ❌ No | ❌ No |
| **Integrated Translation** | ✅ **Yes (Hover + Page)** | ❌ No | ⚠️ Basic | ❌ No |
| **Native Windows Toast & Popup** | ✅ **Yes** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Engine Footprint** | **WebView2 (Native Win)** | Electron | Native C++ / Qt | Electron |
| **Open Source** | ✅ **Yes (Apache-2.0)** | ❌ No | ⚠️ GPL | ✅ Yes |

---

## 🚀 Key Features

### 👥 Multi-Account & Multi-Platform (WhatsApp & Telegram)
- **Simultaneous Accounts**: Manage up to 3 separate accounts (**WhatsApp Web** and **Telegram Web**) in distinct horizontal tabs.
- **Isolated WebView2 Profiles**: Each tab maintains its own independent cache, cookies, local storage, and login session under `data/webview/`.
- **Instant Preloading**: Accounts are preloaded in the background on startup (prioritizing the active account), allowing zero-delay, instant switching between tabs without page reloads.
- **Quick Platform Selector**: Click the `+` button in the tab bar or in Settings to instantly add a WhatsApp or Telegram tab with dedicated brand icons (WhatsApp green, Telegram cyan).
- **Background Notifications**: Even while chatting on Telegram, WhatsApp keeps receiving real-time WebSocket messages and triggers native Windows toasts and overlay popups, and vice versa.

### 🌐 Integrated Translation Engine
- **Hover Button**: Hover over any incoming or outgoing message to display an instant translation button.
- **Full-Page Translation**: Translate entire chat threads with a single click.
- **Multi-Platform Support**: Fully compatible with both WhatsApp Web and Telegram Web K message DOM layouts.

### 🎨 Themes & Custom Window Management
- **Automatic Dark/Light Mode**: Seamless synchronization with Windows system theme or manual override (with custom CSS and dark mode injection for both WhatsApp and Telegram).
- **Taskbar-Aware Maximize**: Custom borderless window respecting the Windows taskbar across all monitors, with fluid drag-to-restore and interactive corner resize.

### 🔔 Notifications & System Tray
- **Windows Toast & Overlay Popups**: Interactive toast and corner popups routing clicks straight to the originating account tab.
- **System Tray**: Minimize to notification tray with unread message badge count.
- **Automated OTA Updates**: Background check and direct updates via GitHub Releases.

---

## 💻 System Requirements

- **OS**: Windows 10 (version 20H1 / build 19041 or higher) or Windows 11
- **Runtime**: [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (pre-installed on Windows 11)
- **Framework**: .NET 9.0 Runtime

---

## 🛠️ Building from Source

To compile and run the project locally:

```bash
# Clone repository
git clone https://github.com/hidaba/HidaChat.git
cd HidaChat

# Restore dependencies and build
dotnet restore
dotnet build -c Release
```

Alternatively, open `HidaChat.sln` in **Visual Studio 2022** (.NET 9 SDK installed) and press `F5`.

---

## 📖 Quick Start Guide

1. **Add Accounts**: Launch `HidaChat.exe`. Click the `+` button on the top tab bar to choose between **WhatsApp** or **Telegram**.
2. **Log In**:
   - **WhatsApp**: Scan the displayed QR code using the WhatsApp mobile app (*Linked Devices*).
   - **Telegram**: Scan the QR code with your Telegram mobile app or log in with your phone number / SMS code.
3. **Rename Tabs**: Right-click on any tab header and select **Rename** to customize the label.
4. **Translate Messages**: Hover over any chat bubble to show the translation button 🌐.

### 🕹️ Title Bar Controls

| Icon | Function |
| :---: | :--- |
| ⚙️ | Open Settings (Theme, Language, Account Management, Beta channel) |
| 🔄 | Reload active account webview |
| ⓘ | About HidaChat (Version, license, portable path) |
| ✕ / — | Minimize to system tray or taskbar |

---

## 🗺️ Roadmap & Changelog

Check out [CHANGELOG.md](CHANGELOG.md) to see release history and recent updates.

---

## 🤝 Contributing & Security

- **Contributing**: Please review [CONTRIBUTING.md](CONTRIBUTING.md) before submitting pull requests.
- **Security**: For security concerns or vulnerability reporting, see [SECURITY.md](SECURITY.md).
- **Code of Conduct**: This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).

---

## 📄 License

Distributed under the **Apache 2.0 License**. See [LICENSE.txt](LICENSE.txt) for details.
