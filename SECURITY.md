# Security Policy

## Supported Versions

The following versions of **WhatsAppH** are currently supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 0.2.x   | :white_check_mark: |
| < 0.2.0 | :x:                |

---

## Reporting a Vulnerability

Security is a top priority for WhatsAppH. If you discover a security vulnerability, please follow these guidelines:

1. **Do NOT open a public GitHub issue** for security vulnerabilities.
2. Report the vulnerability privately by opening a [Private Security Advisory](https://github.com/hidaba/WhatsAppH/security/advisories/new) on GitHub or contacting the maintainer directly via GitHub profile.
3. Include detailed information:
   - Type of issue (e.g. credential leakage, XSS in webview bridge, unauthorized RPC).
   - Step-by-step instructions to reproduce the issue.
   - Proof-of-concept code or screenshot if applicable.

---

## Security Practices in WhatsAppH

- **WebView2 Profile Isolation**: User sessions and cookies are isolated per account in `data/webview/`.
- **No Remote Code Execution**: Script injection in `JsScripts.vb` is strictly scoped to local UI handling and message translation.
- **GitHub Release Verification**: Automatic updates use HTTPS calls directly to `https://api.github.com/repos/hidaba/WhatsAppH/releases/latest`.

Thank you for keeping WhatsAppH and its users safe!
