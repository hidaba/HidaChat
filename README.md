# WhatsAppH

[![English](https://img.shields.io/badge/Language-English-blue.svg)](README.md)
[![Italiano](https://img.shields.io/badge/Lingua-Italiano-green.svg)](README.it.md)

**Portable Windows Desktop Client for WhatsApp Web (.NET 9 / WPF)** featuring **Multi-Account** tab management, **Built-In Message Translation**, **Native Toast Notifications**, and **Zero Installation**.

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE.txt)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Download Release](https://img.shields.io/github/v/release/hidaba/WhatsAppH?color=green&label=Download%20Windows)](https://github.com/hidaba/WhatsAppH/releases/latest)
[![Total Downloads](https://img.shields.io/github/downloads/hidaba/WhatsAppH/total)](https://github.com/hidaba/WhatsAppH/releases)
[![Last Commit](https://img.shields.io/github/last-commit/hidaba/WhatsAppH)](https://github.com/hidaba/WhatsAppH/commits/master)
[![Build Status](https://img.shields.io/github/actions/workflow/status/hidaba/WhatsAppH/build.yml?branch=master)](https://github.com/hidaba/WhatsAppH/actions)

---

## 📸 Screenshots & Preview

| **Multi-Account Interface (Dark Mode)** | **Instant Message Translation** |
|:---:|:---:|
| ![Multi-Account Interface](images/screenshot_main.png) | ![Message Translation](images/screenshot_translation.png) |
| **Native Windows Toast Notifications** | **Dark & Light Themes** |
| ![Windows Toast Notifications](images/screenshot_toast.png) | ![Dark and Light Themes](images/screenshot_themes.png) |

---

## 📦 Quick Download for Windows

Get the latest ready-to-use portable release for Windows (ZIP archive, no installation required):

- ⬇️ **[Download Latest Portable Release (GitHub Releases)](https://github.com/hidaba/WhatsAppH/releases/latest)**
- 📂 Extract the ZIP file anywhere (Local Drive or USB Flash Drive) and launch `WhatsappH.exe`.

> ⚠️ **Important Portability Note**: Do not run WhatsAppH simultaneously from multiple computers accessing the same shared network folder. WhatsAppH is designed to be used by one PC at a time to prevent WebView2 profile lock conflicts.

---

## 🌟 Why WhatsAppH? (Comparison)

| Feature | WhatsAppH | Official WhatsApp Desktop | Altus (`amanharwara/altus`) | whatRust (`karem505/whatRust`) |
|---|:---:|:---:|:---:|:---:|
| **Installation Required** | ❌ **No (100% Portable)** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Moveable Data / Portable** | ✅ **Yes (ZIP / USB)** | ❌ No | ❌ No | ❌ No |
| **Multi-Account Support** | ✅ **Yes (Isolated Tabs)** | ❌ No | ✅ Yes | ✅ Yes |
| **Integrated Translation** | ✅ **Yes (Hover + Page)** | ❌ No | ❌ No | ❌ No |
| **Native Windows Toast** | ✅ **Yes** | ✅ Yes | ✅ Yes | ✅ Yes |
| **Engine Footprint** | **WebView2 (Native Win)** | Electron | Electron | WebView (Tauri/Rust) |
| **Open Source** | ✅ **Yes (Apache-2.0)** | ❌ No | ✅ Yes | ✅ Yes |

---

## 🚀 Key Features

- 👥 **Multi-Account Management**: Run multiple WhatsApp Web accounts concurrently in dedicated tabs with isolated WebView2 user profiles.
- 🎨 **Modern Themes**: Seamless Dark/Light mode switcher with automatic Windows system theme sync.
- 🌐 **Built-In Translation**:
  - **Hover Button**: Translate individual chat messages on hover.
  - **Full-Page Batch**: Translate entire chat conversations instantly.
  - **Notification Translation**: Automatically translate incoming message previews.
- 🔔 **Native Windows Toast Notifications**: Custom click routing directly opens the active account and target conversation.
- 📌 **System Tray Integration**: Minimize to tray with unread notification badge counts.
- 🚀 **Automated OTA Updates**: Background version check and seamless update download via GitHub Releases.

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
git clone https://github.com/hidaba/WhatsAppH.git
cd WhatsAppH

# Restore dependencies and build
dotnet restore
dotnet build -c Release
```

Alternatively, open `WhatsappH.sln` in **Visual Studio 2022** (.NET 9 SDK installed) and press `F5`.

---

## 📖 Quick Start Guide

1. **Add Accounts**: Launch `WhatsappH.exe` and add or rename account tabs via the top header bar or **Settings** (⚙️).
2. **Scan QR Code**: Scan the QR code with WhatsApp on your phone to link your account.
3. **Translate Messages**: Hover over any chat message to reveal the instant translation button 🌐.

### 🕹️ Title Bar Controls

| Icon | Function |
| :---: | :--- |
| ⚙️ | Open Settings (Theme, Language, Account Management) |
| 🔄 | Reload active account webview |
| 🌐 | Trigger full chat page translation |
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
