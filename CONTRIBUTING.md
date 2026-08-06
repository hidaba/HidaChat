# Contributing to WhatsAppH

First off, thank you for considering contributing to **WhatsAppH**! It's open-source projects like this that make the developer community such an amazing place to learn, inspire, and create.

---

## 🚀 How Can I Contribute?

### Reporting Bugs
Before creating a bug report, please check the existing issues to see if the problem has already been reported. When creating a bug report, please include as many details as possible:
- Use the **Bug Report Template** (`.github/ISSUE_TEMPLATE/bug_report.md`).
- Describe your OS version (Windows 10 / 11) and WebView2 runtime version.
- Describe the clear steps to reproduce the issue.

### Suggesting Enhancements
Feature requests are always welcome!
- Use the **Feature Request Template** (`.github/ISSUE_TEMPLATE/feature_request.md`).
- Explain why this feature would be useful to WhatsAppH users.

### Pull Requests
1. **Fork the repository** and create your branch from `main`.
2. **Make sure the project builds**:
   ```bash
   dotnet restore
   dotnet build -c Release
   ```
3. Follow the project's code style conventions:
   - Use clear, descriptive variable names in Visual Basic .NET.
   - Include XML documentation tags (`''' <summary>`) for new classes or methods.
   - Keep UI localizations updated in `Localization.vb`.
4. Submit your Pull Request targeting the `main` branch with a concise explanation of your changes.

---

## 🎨 Design & Architectural Guidelines

- **Portability First**: WhatsAppH stores user profiles and settings inside `data/webview` within the application folder. Ensure new features do not depend on system-wide registry modifications or installation paths.
- **WPF & WebView2 Interop**: Keep JavaScript bridge calls in `JsScripts.vb` robust and asynchronous.
- **Security**: Never commit internal endpoints, API tokens, or hardcoded IP addresses.

Thank you for helping make WhatsAppH better!
