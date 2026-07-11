# CopilotCmdlets

PowerShell cmdlets for the [GitHub Copilot SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK). The module is a thin binary wrapper around the SDK and delegates directly to SDK methods.

## Requirements

- PowerShell 7.6+ (Core edition)
- .NET 10
- A GitHub account with Copilot access
- Windows x64 or macOS arm64 for the bundled Copilot CLI included in the gallery package

## Installation

CopilotCmdlets is published to the PowerShell Gallery.

### Windows

Install PowerShell 7.6+, then run PowerShell (`pwsh`) and install the module:

```powershell
Install-PSResource CopilotCmdlets
```

### macOS

Install PowerShell 7.6+ for macOS, then run `pwsh` and install the module:

```powershell
Install-PSResource CopilotCmdlets
```

If you do not already have a trusted PowerShell Gallery repository configured, PowerShell may prompt you to trust the repository during installation.

After installation, restart `pwsh` to load the module automatically, or run `Import-Module CopilotCmdlets` to use it immediately in the current session.

### Other platforms or custom CLI builds

The published module includes native Copilot CLI payloads for Windows x64 and macOS arm64. On another runtime, either build from source (the SDK build automatically bundles the Copilot CLI for the build host's platform) or pass `-CliPath` to `Connect-Copilot` and `New-CopilotClient` to use a custom CLI binary.

### Build from source

```bash
dotnet publish src/CopilotCmdlets.csproj -c Release -o out
pwsh -NoLogo -Command "Import-Module ./out/CopilotCmdlets.psd1"
```

Or use the convenience script, which builds the supported runtime packages:

```bash
pwsh build.ps1
```

## First-time authentication

Use `Connect-Copilot` to launch the bundled GitHub Copilot CLI interactively. At the CLI prompt, run `/login` to authenticate, then `/exit` to return to PowerShell.

```powershell
Connect-Copilot
```

`Connect-Copilot` accepts `-CliPath` when you need to launch a specific Copilot CLI binary and `-ArgumentList` to pass arguments through to that CLI. If a SDK client is already running, stop it first with `Stop-CopilotClient`, or use `-Force` to continue after the warning.

## Quickstart

```powershell
# Authenticate first if needed.
Connect-Copilot

# Start the SDK client and verify connectivity.
$client = New-CopilotClient
Test-CopilotConnection

# Start a session.
$session = New-CopilotSession -SessionId "my-session" -AutoApprove

# Send a message. Output streams to the terminal and the structured result is returned.
$result = Send-CopilotMessage "Explain what this repository does"
$result.Content
$result.TotalInputTokens
$result.TotalOutputTokens

# View conversation events.
Get-CopilotMessage

# Close the session without deleting it.
Close-CopilotSession

# Resume a previous session.
Resume-CopilotSession -SessionId "my-session"

# Or send directly to a session id; the module resumes it through the current client.
Send-CopilotMessage -Session "my-session" -Prompt "Pick up where we left off"

# List, inspect, and delete sessions.
Get-CopilotSession | Format-Table SessionId, Summary, ModifiedTime
Get-CopilotSession -SessionId "my-session"
Remove-CopilotSession -SessionId "my-session"

# Shut down the client.
Stop-CopilotClient
```

## Cmdlets

### Client and authentication

| Cmdlet | Purpose | Common parameters |
| --- | --- | --- |
| `Connect-Copilot` | Launches the Copilot CLI for interactive commands such as `/login`. | `-CliPath`, `-ArgumentList`, `-Force` |
| `New-CopilotClient` | Starts a Copilot SDK client and stores it as the module default. | `-GitHubToken`, `-CliPath`, `-CliUrl`, `-LogLevel`, `-OtlpEndpoint`, `-TelemetrySourceName`, `-WorkingDirectory`, `-Environment`, `-UseLoggedInUser` |
| `Test-CopilotConnection` | Pings the Copilot CLI server through the current or supplied client. | `-Client`, `-Message` |
| `Get-CopilotStatus` | Returns the Copilot CLI version and protocol version. | `-Client` |
| `Get-CopilotAuthStatus` | Returns authentication state, auth type, and login. | `-Client` |
| `Stop-CopilotClient` | Stops and disposes the current or supplied client. | `-Client`, `-Force`, `-WhatIf`, `-Confirm` |

### Sessions

| Cmdlet | Purpose | Common parameters |
| --- | --- | --- |
| `New-CopilotSession` | Creates a new Copilot session and stores it as the module default. | `-Client`, `-SessionId`, `-Model`, `-SystemMessage`, `-SystemMessageMode`, `-SystemMessageSections`, `-ReasoningEffort`, `-AutoApprove`, `-InfiniteSessions`, `-WorkingDirectory`, `-AvailableTools`, `-ExcludedTools`, `-EnableConfigDiscovery`, `-Agent`, `-SkillDirectories`, `-DisabledSkills`, `-EnableCitations`, `-ExcludedBuiltInAgents`, `-MaxAiCredits`, `-McpServers`, `-Tool` |
| `Resume-CopilotSession` | Resumes an existing session by ID and stores it as the module default. | `-SessionId`, `-ContinuePendingWork`, plus the same configuration parameters as `New-CopilotSession` |
| `Get-CopilotSession` | Lists sessions or returns metadata for one session. | `-SessionId`, `-Client` |
| `Close-CopilotSession` | Closes a session without deleting its saved state. | `-Session` |
| `Remove-CopilotSession` | Permanently deletes a saved session. | `-SessionId`, `-Client`, `-WhatIf`, `-Confirm` |

### Messaging

| Cmdlet | Purpose | Common parameters |
| --- | --- | --- |
| `Send-CopilotMessage` | Sends a prompt, streams assistant output, and returns a `CopilotMessageResult`. | `-Prompt`, `-Session`, `-Attachment`, `-BlobData`, `-BlobMimeType`, `-Mode`, `-DisplayPrompt`, `-Timeout` |
| `Get-CopilotMessage` | Retrieves conversation events from the current or supplied session. | `-Session` |
| `Send-CopilotMessageAsync` | Sends a prompt and immediately returns a `CopilotAsyncResult` handle. | `-Prompt`, `-Session`, `-Tag`, `-Attachment`, `-BlobData`, `-BlobMimeType`, `-Mode`, `-DisplayPrompt` |
| `Receive-CopilotAsyncResult` | Waits for an async message handle and returns a `CopilotMessageResult`. | `-Result`, `-Timeout`, `-DisposeSession` |
| `Stop-CopilotMessage` | Aborts the session's in-flight processing. | `-Session` |

Synchronous and async message sends support file attachments through `-Attachment`. Inline binary attachments can be supplied with base64 `-BlobData` and an optional `-BlobMimeType`.

```powershell
$result = Send-CopilotMessage -Prompt "Summarize this file" -Attachment ./README.md

$job = Send-CopilotMessageAsync -Prompt "Generate a short checklist" -Tag checklist
$job | Receive-CopilotAsyncResult -Timeout (New-TimeSpan -Minutes 10)
```

Keep at most one in-flight async message per session: each handle completes when its session next goes idle, so two concurrent sends to the same session finish together at the first idle. For parallel work, create one session per concurrent message. When sending asynchronously, prefer `-AutoApprove` sessions — interactive permission prompts fire on a background thread and contend with the console prompt.

### Models

| Cmdlet | Purpose | Common parameters |
| --- | --- | --- |
| `Get-CopilotModel` | Lists available Copilot models for the current or supplied client. | `-Client` |
| `Set-CopilotModel` | Changes the model for the current or supplied session. | `-Model`, `-Session`, `-ReasoningEffort`, `-Vision` |

```powershell
Get-CopilotModel | Format-Table Id, Name
Set-CopilotModel -Model "<model-id>" -ReasoningEffort low
```

### Custom tools

`New-CopilotTool` wraps a PowerShell ScriptBlock as a custom tool the model can call during a session. The tool's JSON schema is derived from the ScriptBlock's `param()` block: parameter types map to JSON types, `[Parameter(Mandatory)]` marks required parameters, and `HelpMessage` becomes the parameter description. Each invocation runs in a fresh runspace and the pipeline output (formatted as with `Out-String`) is returned to the model.

| Cmdlet | Purpose | Common parameters |
| --- | --- | --- |
| `New-CopilotTool` | Creates a ScriptBlock-backed custom tool for `New-CopilotSession -Tool`. | `-Name`, `-Description`, `-ScriptBlock`, `-SkipPermission` |

```powershell
$weather = New-CopilotTool -Name "get_weather" -Description "Gets the weather for a city" -ScriptBlock {
    param(
        [Parameter(Mandatory, HelpMessage = "City name")] [string] $City,
        [int] $Days = 1
    )
    "Sunny in $City for the next $Days day(s)"
} -SkipPermission

New-CopilotSession -AutoApprove -Tool $weather
Send-CopilotMessage "What's the weather in Oslo?"
```

### MCP servers

Sessions can attach Model Context Protocol servers with `-McpServers`. Each key is a server name; each value is a hashtable with either `Command` (stdio server: optional `Args`, `Env`, `WorkingDirectory`) or `Url` (HTTP server: optional `Headers`), plus optional `Tools` and `Timeout`.

```powershell
New-CopilotSession -AutoApprove -McpServers @{
    everything = @{
        Command = "npx"
        Args    = @("-y", "@modelcontextprotocol/server-everything")
        Tools   = @("*")
    }
}
```

## Default client and session behavior

`New-CopilotClient` stores the created client in module state. Cmdlets that accept `-Client` use that default when `-Client` is omitted.

`New-CopilotSession` and `Resume-CopilotSession` store the current session in module state. Cmdlets that accept `-Session` use that default when `-Session` is omitted.

Use explicit `-Client` and `-Session` parameters when you want to manage multiple clients or sessions in the same PowerShell process.

## Testing

```bash
# Unit tests (no network required)
dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"

# End-to-end tests (requires GITHUB_TOKEN and a published module: dotnet publish src/CopilotCmdlets.csproj -c Release -o out)
dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=EndToEnd"

# All tests
dotnet test tests/CopilotCmdlets.Tests.csproj
```

## License

See [LICENSE](LICENSE) for details.
