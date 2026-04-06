# CopilotPS

A PowerShell module that wraps the [GitHub Copilot SDK](https://www.nuget.org/packages/GitHub.Copilot.SDK) as native cmdlets. Build AI-powered workflows, code-review pipelines, and interactive assistants directly from your PowerShell terminal.

## Requirements

- [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) SDK or runtime
- [PowerShell 7.4+](https://github.com/PowerShell/PowerShell) (Core edition)
- A valid `GITHUB_TOKEN` with Copilot access (for authentication)
- The `github-copilot` CLI on your PATH (unless connecting via URL with `-CliUrl`)

## Installation

### Build from source

```powershell
git clone https://github.com/halcyonhal9/ghcp-powershell.git
cd ghcp-powershell

# Option 1 — use the convenience script
pwsh build.ps1

# Option 2 — publish directly
dotnet publish src/CopilotPS.csproj -c Release -o out
```

### Import the module

```powershell
Import-Module ./out/CopilotPS.psd1
```

To make it available in every session, add the `Import-Module` line to your [PowerShell profile](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_profiles) (`$PROFILE`).

## Quick start

```powershell
Import-Module ./out/CopilotPS.psd1

# 1. Start a client (connects to the Copilot CLI server)
New-CopilotClient

# 2. Verify the connection
Test-CopilotConnection

# 3. Open a session
New-CopilotSession

# 4. Send a message (streams to the terminal in real time)
$result = Send-CopilotMessage "Explain how PowerShell pipelines work"

# 5. Inspect the full response
$result.Content

# 6. Clean up
Close-CopilotSession
Stop-CopilotClient
```

## Cmdlet reference

CopilotPS exports 10 cmdlets organized into three groups: **client lifecycle**, **session lifecycle**, and **messaging**.

---

### Client lifecycle

#### `New-CopilotClient`

Creates and starts a Copilot client. The client is stored as the module default so subsequent cmdlets use it automatically.

```powershell
New-CopilotClient                                        # default — uses CLI on PATH
New-CopilotClient -GitHubToken $env:GITHUB_TOKEN         # explicit token
New-CopilotClient -CliUrl "http://localhost:8080"        # connect to a remote CLI server
New-CopilotClient -CliPath "/usr/local/bin/github-copilot" -LogLevel "debug"
```

| Parameter      | Type   | Required | Default  | Description                                 |
| -------------- | ------ | -------- | -------- | ------------------------------------------- |
| `-GitHubToken` | string | No       | —        | GitHub token for authentication              |
| `-CliPath`     | string | No       | —        | Path to the `github-copilot` CLI binary      |
| `-CliUrl`      | string | No       | —        | URL of a running CLI server (disables stdio) |
| `-LogLevel`    | string | No       | `"info"` | SDK log level (`debug`, `info`, `warn`, etc.)|

---

#### `Stop-CopilotClient`

Stops and disposes the client. Clears the module default client and session.

```powershell
Stop-CopilotClient                  # graceful shutdown
Stop-CopilotClient -Force           # force-stop the CLI process
Stop-CopilotClient -Client $other   # stop a specific client instance
```

| Parameter | Type           | Required | Default              | Description                    |
| --------- | -------------- | -------- | -------------------- | ------------------------------ |
| `-Client` | CopilotClient  | No       | Module default       | Client to stop                 |
| `-Force`  | Switch         | No       | —                    | Force-stop instead of graceful |

Supports `-WhatIf` and `-Confirm`.

---

#### `Test-CopilotConnection`

Pings the Copilot CLI server and returns a `PingResponse` with the server timestamp and protocol version.

```powershell
Test-CopilotConnection
Test-CopilotConnection -Message "hello"
```

| Parameter  | Type   | Required | Default        | Description                   |
| ---------- | ------ | -------- | -------------- | ----------------------------- |
| `-Client`  | CopilotClient | No | Module default | Client to ping through        |
| `-Message` | string | No       | —              | Optional message to send      |

---

### Session lifecycle

#### `New-CopilotSession`

Creates a new conversation session. Streaming is always enabled.

```powershell
New-CopilotSession
New-CopilotSession -Model "gpt-5" -SessionId "code-review-42"
New-CopilotSession -SystemMessage "You are a security auditor." -AutoApprove
New-CopilotSession -WorkingDirectory "/repos/my-project" -ReasoningEffort "high"
New-CopilotSession -AvailableTools "read_file","write_file" -ExcludedTools "run_command"
```

| Parameter            | Type     | Required | Default        | Description                                          |
| -------------------- | -------- | -------- | -------------- | ---------------------------------------------------- |
| `-Client`            | CopilotClient | No | Module default | Client to create the session on                      |
| `-SessionId`         | string   | No       | SDK-generated  | Custom session identifier                            |
| `-Model`             | string   | No       | —              | Model to use (e.g. `"gpt-5"`)                        |
| `-SystemMessage`     | string   | No       | —              | System prompt that guides assistant behavior          |
| `-ReasoningEffort`   | string   | No       | —              | Reasoning effort level                               |
| `-AutoApprove`       | Switch   | No       | —              | Auto-approve all tool permission requests             |
| `-InfiniteSessions`  | Switch   | No       | —              | Enable infinite session support                      |
| `-WorkingDirectory`  | string   | No       | —              | Working directory for file operations                |
| `-AvailableTools`    | string[] | No       | —              | Allowlist of tools the agent can use                 |
| `-ExcludedTools`     | string[] | No       | —              | Blocklist of tools the agent cannot use              |

When `-AutoApprove` is omitted, tool-use requests are shown interactively in the terminal and require manual approval.

---

#### `Resume-CopilotSession`

Resumes a previously closed session by ID, preserving conversation history.

```powershell
Resume-CopilotSession "code-review-42"
Resume-CopilotSession -SessionId "code-review-42" -Model "gpt-5" -AutoApprove
```

| Parameter          | Type     | Required | Default        | Description                                |
| ------------------ | -------- | -------- | -------------- | ------------------------------------------ |
| `-SessionId`       | string   | **Yes**  | —              | ID of the session to resume (positional)   |
| `-Client`          | CopilotClient | No | Module default | Client to use                              |
| `-Model`           | string   | No       | —              | Override the model for the resumed session |
| `-SystemMessage`   | string   | No       | —              | Override the system prompt                 |
| `-ReasoningEffort` | string   | No       | —              | Reasoning effort level                     |
| `-AutoApprove`     | Switch   | No       | —              | Auto-approve tool permission requests      |
| `-WorkingDirectory`| string   | No       | —              | Override the working directory             |

---

#### `Get-CopilotSession`

Lists all sessions known to the Copilot CLI server.

```powershell
Get-CopilotSession
Get-CopilotSession | Format-Table SessionId, Summary, ModifiedTime
Get-CopilotSession | Where-Object { $_.IsRemote -eq $false }
```

| Parameter | Type          | Required | Default        | Description      |
| --------- | ------------- | -------- | -------------- | ---------------- |
| `-Client` | CopilotClient | No       | Module default | Client to query  |

Each returned `SessionMetadata` object contains: `SessionId`, `StartTime`, `ModifiedTime`, `Summary`, `IsRemote`, and `Context`.

---

#### `Close-CopilotSession`

Disposes the session connection but **preserves persisted state** so you can resume it later with `Resume-CopilotSession`.

```powershell
Close-CopilotSession
Close-CopilotSession -Session $mySession
```

| Parameter  | Type           | Required | Default         | Description          |
| ---------- | -------------- | -------- | --------------- | -------------------- |
| `-Session` | CopilotSession | No       | Module default  | Session to close     |

---

#### `Remove-CopilotSession`

Permanently deletes a session and all its persisted state. This cannot be undone.

```powershell
Remove-CopilotSession "code-review-42"
Remove-CopilotSession -SessionId "old-session" -Confirm
```

| Parameter    | Type          | Required | Default        | Description                               |
| ------------ | ------------- | -------- | -------------- | ----------------------------------------- |
| `-SessionId` | string        | **Yes**  | —              | ID of the session to delete (positional)  |
| `-Client`    | CopilotClient | No       | Module default | Client to use                             |

Supports `-WhatIf` and `-Confirm`.

---

### Messaging

#### `Send-CopilotMessage`

Sends a prompt to the active session. The response streams to the terminal in real time. The cmdlet blocks until the session becomes idle (or the timeout is reached) and then outputs a structured result object.

```powershell
# Basic usage
$result = Send-CopilotMessage "Summarize this repository"
$result.Content      # full response text
$result.MessageId    # SDK-assigned message ID
$result.Events       # all session events during this turn

# With file attachments
Send-CopilotMessage "Review this file" -Attachment "./src/Program.cs"

# With a custom timeout
Send-CopilotMessage "Run a full analysis" -Timeout ([TimeSpan]::FromMinutes(10))

# Piping the content
$result = Send-CopilotMessage "What patterns do you see?"
$result.Content | Set-Clipboard
```

| Parameter     | Type           | Required | Default        | Description                                    |
| ------------- | -------------- | -------- | -------------- | ---------------------------------------------- |
| `-Prompt`     | string         | **Yes**  | —              | The message to send (positional)               |
| `-Session`    | CopilotSession | No       | Module default | Session to send to                             |
| `-Attachment` | string[]       | No       | —              | File paths to attach to the message            |
| `-Timeout`    | TimeSpan       | No       | 5 minutes      | Maximum time to wait for a response            |

**Return type: `CopilotMessageResult`**

| Property    | Type                | Description                          |
| ----------- | ------------------- | ------------------------------------ |
| `MessageId` | string              | SDK-assigned message identifier      |
| `Content`   | string              | Full accumulated response text       |
| `SessionId` | string              | Session that produced this response  |
| `Events`    | List\<SessionEvent> | All events emitted during this turn  |

While streaming, tool executions are logged to stderr:
```
[Tool] read_file (id: call_abc123)
[Tool] completed (id: call_abc123)
```

---

#### `Get-CopilotMessage`

Retrieves the full conversation history for a session.

```powershell
Get-CopilotMessage
Get-CopilotMessage -Session $mySession
Get-CopilotMessage | Select-Object -Last 5
```

| Parameter  | Type           | Required | Default        | Description           |
| ---------- | -------------- | -------- | -------------- | --------------------- |
| `-Session` | CopilotSession | No       | Module default | Session to query      |

Returns a collection of `SessionEvent` objects representing the full message history.

---

## Common patterns

### Scripted code review

```powershell
New-CopilotClient
New-CopilotSession -SystemMessage "You are a senior code reviewer. Be concise." -AutoApprove

$files = Get-ChildItem src/*.cs
foreach ($file in $files) {
    $result = Send-CopilotMessage "Review this file for bugs and style issues" -Attachment $file.FullName
    [PSCustomObject]@{
        File    = $file.Name
        Review  = $result.Content
    }
}

Stop-CopilotClient
```

### Multi-turn conversation

```powershell
New-CopilotClient
New-CopilotSession -SessionId "architecture-chat"

Send-CopilotMessage "Describe the CQRS pattern"
Send-CopilotMessage "How would you apply it to a PowerShell module?"
Send-CopilotMessage "Show me a concrete example"

# Review the full conversation
Get-CopilotMessage

Close-CopilotSession
Stop-CopilotClient
```

### Resume a session later

```powershell
New-CopilotClient

# Pick up where you left off
Resume-CopilotSession "architecture-chat"
Send-CopilotMessage "What did we discuss last time?"

Stop-CopilotClient
```

### Session management

```powershell
New-CopilotClient

# List all sessions
Get-CopilotSession | Format-Table SessionId, Summary, ModifiedTime

# Clean up old sessions
Get-CopilotSession |
    Where-Object { $_.ModifiedTime -lt (Get-Date).AddDays(-30) } |
    ForEach-Object { Remove-CopilotSession $_.SessionId }

Stop-CopilotClient
```

## Module defaults and explicit parameters

CopilotPS stores the most recently created client and session as module-level defaults. Every cmdlet that needs a client or session will use these defaults automatically.

You can override the defaults at any time by passing `-Client` or `-Session` explicitly:

```powershell
$clientA = New-CopilotClient -CliPath "/opt/copilot-v1/github-copilot"
$clientB = New-CopilotClient -CliPath "/opt/copilot-v2/github-copilot"  # now the default

# Explicitly use clientA even though clientB is the default
Test-CopilotConnection -Client $clientA
```

## Building and testing

```powershell
# Build
dotnet publish src/CopilotPS.csproj -c Release -o out

# Run unit tests (no network required)
dotnet test tests/CopilotPS.Tests.csproj --filter "Category=Unit"

# Run end-to-end tests (requires GITHUB_TOKEN and github-copilot CLI)
$env:GITHUB_TOKEN = "ghp_..."
dotnet test tests/CopilotPS.Tests.csproj --filter "Category=EndToEnd"

# Run all tests
dotnet test tests/CopilotPS.Tests.csproj
```

## License

See [LICENSE](LICENSE) for details.
