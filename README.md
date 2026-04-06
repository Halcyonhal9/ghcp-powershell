# CopilotPS

PowerShell wrapper for the [GitHub Copilot SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK). Thin binary cmdlets that delegate directly to the .NET SDK — no custom business logic.

## Requirements

- PowerShell 7.6+ (Core edition)
- .NET 9
- A GitHub account with Copilot access

## Build

```bash
dotnet publish src/CopilotPS.csproj -c Release -o out
```

Or use the convenience script:

```bash
pwsh build.ps1
```

## Quickstart

```powershell
Import-Module ./out/CopilotPS.psd1

# Connect to the Copilot CLI
New-CopilotClient

# Verify connectivity
Test-CopilotConnection

# Start a session
New-CopilotSession -SessionId "my-session" -AutoApprove

# Send a message (streams to terminal, returns structured result)
$result = Send-CopilotMessage "Explain what this repository does"
$result.Content   # full response text

# View conversation history
Get-CopilotMessage

# Close session (preserves state for later resume)
Close-CopilotSession

# Resume a previous session
Resume-CopilotSession -SessionId "my-session"

# List all sessions
Get-CopilotSession | Format-Table SessionId, Summary, ModifiedTime

# Delete a session permanently
Remove-CopilotSession -SessionId "my-session"

# Shut down the client
Stop-CopilotClient
```

## Cmdlets

### Client Lifecycle

| Cmdlet | Description |
|--------|-------------|
| `New-CopilotClient` | Creates and starts a Copilot client |
| `Stop-CopilotClient` | Stops and disposes the client |
| `Test-CopilotConnection` | Pings the CLI server |

### Session Lifecycle

| Cmdlet | Description |
|--------|-------------|
| `New-CopilotSession` | Creates a new session |
| `Resume-CopilotSession` | Resumes a closed session by ID |
| `Get-CopilotSession` | Lists all known sessions |
| `Remove-CopilotSession` | Permanently deletes a session |
| `Close-CopilotSession` | Closes without deleting (resumable) |

### Messaging

| Cmdlet | Description |
|--------|-------------|
| `Send-CopilotMessage` | Sends a prompt, streams output, returns result |
| `Get-CopilotMessage` | Retrieves conversation history |

## Key Parameters

- **`-AutoApprove`** on `New-CopilotSession` / `Resume-CopilotSession`: Auto-approve all tool permission requests (skip interactive prompts).
- **`-Client`** / **`-Session`**: Override the module-scoped default on any cmdlet.
- **`-Force`** on `Stop-CopilotClient`: Force-stop instead of graceful shutdown.
- **`-WhatIf`** / **`-Confirm`** on `Remove-CopilotSession` and `Stop-CopilotClient`.

## Testing

```bash
# Unit tests (no network required)
dotnet test tests/CopilotPS.Tests.csproj --filter "Category=Unit"

# End-to-end tests (requires GITHUB_TOKEN and CLI on PATH)
dotnet test tests/CopilotPS.Tests.csproj --filter "Category=EndToEnd"

# All tests
dotnet test tests/CopilotPS.Tests.csproj
```

## License

See [LICENSE](LICENSE) for details.
