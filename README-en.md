# Release Communications

[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows11&logoColor=white)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-5C2D91)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-1.1.1-0078D4)](src/ReleaseCommunications.App/Package.appxmanifest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[日本語](README.md)

A WinUI 3 desktop application for retrieving, filtering, reading, and summarizing Microsoft 365 roadmap items and Azure updates in newest-publication-first order. The interface switches at runtime between Japanese (default) and English, while summary language remains an independent setting.

> [!NOTE]
> The app and notification-area icons are original SVG artwork for this project, using an abstract cloud, update, and release-flow motif. They do not use Microsoft logos, product marks, or trademarks.

## Features

- Microsoft 365, Azure, or combined release feeds
- Published-period, product, status, and tag/platform filtering
- Stable descending publication-date ordering, with unknown dates last
- Responsive master-detail UI with an intentionally blank detail pane until selection
- Full-content summarization grounded in MRC detail plus trusted linked Microsoft article text
- Independently persisted Japanese/English UI and summary-language settings
- Selected-item and bounded-concurrency batch summaries, cancellation, and persistent cache
- Explicit loading, empty, partial-failure, error, and cancelled states
- WinUI controls designed for keyboard use, screen readers, high contrast, and light/dark themes
- Windows notification-area icon with show-window and exit actions

## User interface

<p align="center">
  <img src="src/ReleaseCommunications.App/Assets/AppIcon.svg" width="120" alt="Release Communications original app icon">
</p>

The left pane keeps the newest release items available for scanning and selection. The right pane stays visually blank until an item is selected, then presents a graphical header, publication metadata, full-content summary actions, AI summary sections, and the authoritative source link.

## Architecture

| Project | Responsibility |
|---|---|
| `ReleaseCommunications.Core` | Domain models, contracts, sorting/filtering, validation, prompt construction, response validation |
| `ReleaseCommunications.Infrastructure` | MRC MCP client, article retrieval, HTML extraction, Foundry integration, JSON settings/cache |
| `ReleaseCommunications.App` | WinUI 3 shell, MVVM, localization, master-detail presentation, notification-area integration |
| `tests` | Sorting, summaries, persistence, validation, and XAML accessibility checks |

Before each summary, the app retrieves the item's MRC detail record. It downloads linked content only when a trusted HTTPS Microsoft URL is available. Otherwise, it summarizes the authoritative MRC detail and displays the reason for partial content.

## Prerequisites

- Windows 10 version 2004 or later, x64 or ARM64
- .NET 10 SDK and Windows App SDK development prerequisites
- WinApp CLI 0.6 or later
- Azure CLI and a text-capable Microsoft Foundry model deployment only when summaries are required

## Setup

1. Authenticate with Azure CLI. Replace identifiers with values from your environment.

   ```powershell
   az login --tenant <tenant-id>
   az account set --subscription <subscription-id>
   ```

2. Build and launch:

   ```powershell
   .\BuildAndRun.ps1 .\src\ReleaseCommunications.App\ReleaseCommunications.App.csproj -c Debug --arch x64
   ```

3. In **Settings**, enter:
   - Foundry OpenAI v1 endpoint: `https://<resource-name>.services.ai.azure.com/openai/v1`
   - Text-capable deployment name: `<deployment-name>`

4. Select **Check connections**, **Save**, return to **Updates**, and select **Refresh**.

Settings and generated summaries are stored under `%LOCALAPPDATA%\ReleaseCommunications`.

## Security and privacy

- Authentication uses `AzureCliCredential` exclusively; API keys, access tokens, and CLI credentials are not persisted.
- Stored settings contain only endpoint/deployment names, language choices, limits, and cache policy.
- Linked-page retrieval is restricted to HTTPS under `microsoft.com` or `aka.ms`, and every redirect target is revalidated.
- Automatic redirects are disabled; SSRF, unexpected hosts, and oversized responses are constrained.
- Downloaded HTML and extracted full text are not logged or persisted.
- Normal tests use synthetic data, do not call external services, and incur no model charges.
- Do not include usernames, local paths, real endpoint names, tenant/subscription IDs, or credentials in issues or logs.

Before public release, scan tracked files for secrets, local paths, and personal resource names. All examples in this repository use placeholders.

## Build and test

```powershell
dotnet restore .\ReleaseCommunications.slnx
dotnet test .\ReleaseCommunications.slnx -c Release --no-restore
dotnet restore .\src\ReleaseCommunications.App\ReleaseCommunications.App.csproj -r win-x64
dotnet build .\src\ReleaseCommunications.App\ReleaseCommunications.App.csproj -c Release -p:Platform=x64 --no-restore
```

Use `BuildAndRun.ps1` for analyzer-backed launch diagnostics. Do not run a packaged executable directly.

## License

[MIT License](LICENSE) — Copyright (c) 2026 tokawa-ms
