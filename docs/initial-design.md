# CopilotCmdlets - PowerShell Wrapper for GitHub Copilot SDK

## 1. Goals

- Expose the full surface of the .NET `GitHub.Copilot.SDK` library as PowerShell cmdlets.
- Write as little code as possible: thin pass-through to SDK, zero custom business logic.
- Run on Windows, macOS, and Ubuntu via PowerShell 7.6+ (.NET 10).
- Default to streaming output and interactive permission prompts.
- Design for a future `-NoWait` async pattern without requiring a rewrite.

## 2. Technology Choices

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Module type | **C# binary cmdlet** (.dll) | Type-safe, direct SDK references, no reflection hacks. Fully cross-platform on .NET 10 — no platform-specific code needed. |
| Target framework | `net10.0` | PowerShell 7.6 runs on .NET 10. |
| SDK reference | `GitHub.Copilot.SDK` NuGet 0.1.x | The official .NET SDK. |
| PS SDK reference | `System.Management.Automation` 7.5.x | `PrivateAssets="all"` — not shipped with the module. |
| Naming convention | `Verb-CopilotNoun` | Standard PowerShell verb-noun, `Copilot` prefix on all nouns. |

### Cross-Platform Notes

No platform-specific code is required. The Copilot SDK manages CLI process lifecycle cross-platform internally. `Console.ReadLine()` (used for interactive prompts from background threads) works identically on all three platforms in pwsh 7+.

## 3. Project Layout

```
src/
├── CopilotCmdlets.csproj   # Project file
├── ModuleState.cs          # Singleton state + permission/input handlers + module cleanup
├── ClientCmdlets.cs        # New-CopilotClient, Stop-CopilotClient, Test-CopilotConnection
├── SessionCmdlets.cs       # New/Resume/Get/Remove/Close-CopilotSession
└── MessageCmdlets.cs       # Send-CopilotMessage, Get-CopilotMessage
CopilotCmdlets.psd1         # Module manifest (copied to output on build)
build.ps1                   # Convenience: dotnet publish + tells you the import path
```

**Five C# files, one manifest, one build script.** That's the entire module.

## 4. Module State (Singleton Pattern)

```
ModuleState  (internal static class)
├── client          : CopilotClient?      ← the default client
├── currentSession  : CopilotSession?     ← the default session
├── RequireClient(explicit?)              ← returns explicit ?? client ?? throw
├── RequireSession(explicit?)             ← returns explicit ?? currentSession ?? throw
└── CleanupAsync()                        ← disposes session + stops client
```

Every cmdlet that needs a client or session accepts an optional `-Client` / `-Session` parameter. If omitted, the module-scoped singleton is used. This gives a simple `Connect-AzAccount`-style UX for interactive use while still supporting multi-client scripts.

An `IModuleAssemblyCleanup` implementation calls `ModuleState.CleanupAsync()` when the module is removed, preventing orphaned CLI processes.

### Session Persistence

The SDK persists session state to `~/.copilot/session-state/{sessionId}/` automatically. **We store no additional local state.** Users list sessions via `Get-CopilotSession` (which calls `ListSessionsAsync`) and resume by session ID. If users want friendly names, they can provide their own `-SessionId` at creation time (the SDK supports this).

## 5. Cmdlet Inventory

### 5.1 Client Lifecycle

#### `New-CopilotClient`

Creates a `CopilotClient`, calls `StartAsync()`, stores it as the module default, and writes it to the pipeline.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-GitHubToken` | string | — | `CopilotClientOptions.GithubToken` |
| `-CliPath` | string | — | `CopilotClientOptions.CliPath` |
| `-CliUrl` | string | — | `CopilotClientOptions.CliUrl` (also sets `UseStdio = false`) |
| `-LogLevel` | string | `"info"` | `CopilotClientOptions.LogLevel` |

**Output:** `CopilotClient`

#### `Stop-CopilotClient`

Stops and disposes the client. Clears module state if it was the default.

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `-Client` | CopilotClient | module default | Explicit override |
| `-Force` | switch | — | Calls `ForceStopAsync` instead of `StopAsync` |

**Output:** none

#### `Test-CopilotConnection`

Pings the CLI server.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-Client` | CopilotClient | module default | |
| `-Message` | string | — | `PingAsync(message)` |

**Output:** `PingResponse` (Message, Timestamp, ProtocolVersion)

### 5.2 Session Lifecycle

#### `New-CopilotSession`

Creates a session, registers permission/input handlers, stores it as the module default.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-Client` | CopilotClient | module default | |
| `-SessionId` | string | (SDK generates) | `SessionConfig.SessionId` |
| `-Model` | string | — | `SessionConfig.Model` |
| `-SystemMessage` | string | — | `SessionConfig.SystemMessage` |
| `-ReasoningEffort` | string | — | `SessionConfig.ReasoningEffort` |
| `-AutoApprove` | switch | — | Uses auto-approve handler instead of interactive |
| `-InfiniteSessions` | switch | — | `SessionConfig.InfiniteSessions` |
| `-WorkingDirectory` | string | — | `SessionConfig.WorkingDirectory` |
| `-AvailableTools` | string[] | — | `SessionConfig.AvailableTools` |
| `-ExcludedTools` | string[] | — | `SessionConfig.ExcludedTools` |

Streaming is always enabled (`SessionConfig.Streaming = true`).

Permission handler: interactive `Console.ReadLine` prompt by default; `-AutoApprove` overrides to approve-all.

User input handler: always registered, uses interactive `Console.ReadLine`.

**Output:** `CopilotSession`

#### `Resume-CopilotSession`

Resumes an existing session by ID. Stores it as the module default.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-SessionId` | string | **required** | `ResumeSessionAsync(sessionId, ...)` |
| `-Client` | CopilotClient | module default | |
| `-Model` | string | — | `ResumeSessionConfig.Model` |
| `-AutoApprove` | switch | — | Permission handler override |
| `-SystemMessage` | string | — | `ResumeSessionConfig.SystemMessage` |
| `-ReasoningEffort` | string | — | `ResumeSessionConfig.ReasoningEffort` |
| `-WorkingDirectory` | string | — | `ResumeSessionConfig.WorkingDirectory` |

**Output:** `CopilotSession`

#### `Get-CopilotSession`

Lists all sessions known to the CLI server.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-Client` | CopilotClient | module default | `ListSessionsAsync()` |

**Output:** `List<SessionMetadata>` (SessionId, StartTime, ModifiedTime, Summary, IsRemote, Context)

#### `Remove-CopilotSession`

Permanently deletes a session and its persisted state.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-SessionId` | string | **required** | `DeleteSessionAsync(sessionId)` |
| `-Client` | CopilotClient | module default | |

Supports `ShouldProcess` for `-WhatIf` / `-Confirm`.

**Output:** none

#### `Close-CopilotSession`

Disposes the session object (closes the connection) but preserves on-disk state so it can be resumed later.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-Session` | CopilotSession | module default | `DisposeAsync()` |

Clears `ModuleState.currentSession` if it was the default.

**Output:** none

### 5.3 Messaging

#### `Send-CopilotMessage`

Sends a prompt and blocks until the session goes idle. Streams assistant output to the host in real time. Returns a result object to the pipeline.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-Prompt` | string | **required**, position 0 | `MessageOptions.Prompt` |
| `-Session` | CopilotSession | module default | |
| `-Attachment` | string[] | — | File paths → `UserMessageDataAttachmentsItemFile` |
| `-Timeout` | TimeSpan | 5 minutes | Passed to internal wait logic |

**Streaming behavior:**
1. Before calling `SendAsync`, subscribe to `session.On(handler)`.
2. In the handler (runs on the JSON-RPC thread):
   - `AssistantMessageDeltaEvent` → `Console.Write(delta.Data.DeltaContent)` (real-time streaming to terminal).
   - `AssistantMessageEvent` → accumulate final content.
   - `ToolExecutionStartEvent` → `Console.Error.WriteLine` (shows as verbose-like info; avoids polluting pipeline).
   - `ToolExecutionCompleteEvent` → `Console.Error.WriteLine` with success/failure.
   - `SessionIdleEvent` → signal the blocking wait to complete.
   - `SessionErrorEvent` → signal the blocking wait with an error.
3. After idle, write a result object to the pipeline:

```
CopilotMessageResult
├── MessageId   : string
├── Content     : string     ← full accumulated assistant response
├── SessionId   : string
└── Events      : SessionEvent[]  ← all events received during this send
```

`CopilotMessageResult` is a small POCO defined in `MessageCmdlets.cs`. It wraps the SDK types without adding logic.

**Why `Console.Write` for streaming instead of `WriteObject`/`WriteVerbose`:** PowerShell cmdlet Write* methods can only be called from the pipeline thread. The SDK event handler runs on the JSON-RPC background thread. `Console.Write` is thread-safe and works cross-platform. The final structured result still goes through `WriteObject` on the pipeline thread after the wait completes.

#### `Get-CopilotMessage`

Retrieves conversation history for a session.

| Parameter | Type | Default | Maps to |
|-----------|------|---------|---------|
| `-Session` | CopilotSession | module default | `GetMessagesAsync()` |

**Output:** `IReadOnlyList<SessionEvent>`

## 6. Permission Handling

Two modes, selected per-session at creation/resume time:

### Interactive (default)
A `PermissionHandler` delegate that:
1. Prints `[Permission] {request.Kind}` in yellow via `Console.ForegroundColor`.
2. Dumps any `ExtensionData` key-value pairs for context.
3. Prompts `Allow? (y/n):` via `Console.ReadLine()`.
4. Returns `PermissionRequestResult { Kind = "approve" }` or `Kind = "deny"`.

### Auto-Approve (`-AutoApprove` switch)
Returns `Kind = "approve"` unconditionally. Suitable for unattended scripts where the user accepts all tool executions.

### User Input (agent asks a question)
Always registered. Uses `Console.ReadLine()` with choice display when `request.Choices` is provided.

## 7. Future: Async / Non-Blocking Pattern

The current design blocks the pipeline thread during `Send-CopilotMessage`. The future async design adds:

```powershell
# Future: returns immediately with just a MessageId
$msg = Send-CopilotMessage "Do something complex" -NoWait

# Future: poll or wait for results
$result = Wait-CopilotMessage -MessageId $msg.MessageId
# or
$result = Get-CopilotMessageResult -MessageId $msg.MessageId
```

### What's needed to support this:

1. **`-NoWait` switch on `Send-CopilotMessage`** — calls `SendAsync` (not `SendAndWaitAsync`), immediately returns a `CopilotMessageResult` with only `MessageId` populated.
2. **`Wait-CopilotMessage` cmdlet** — subscribes to events and blocks until idle, returning the full result. Essentially the second half of the current synchronous flow, factored out.
3. **Background event accumulator** — `ModuleState` gains a `ConcurrentDictionary<string, CopilotMessageResult>` keyed by message ID. The event handler populates results in the background. `Get-CopilotMessageResult` reads from this dictionary.

This is why `CopilotMessageResult` includes `MessageId` from day one — it's the correlation key for the async pattern. The current synchronous path is just the special case where we wait immediately.

**No changes to the current cmdlet signatures are needed** — `-NoWait` is additive.

## 8. Development Environment (VS Code)

### Workspace

Open `CopilotCmdlets.code-workspace` in VS Code. It configures:
- Recommended extensions: C# Dev Kit, PowerShell
- `bin`/`obj`/`out` excluded from file tree and search
- Format-on-save enabled

### Debug Configurations (`.vscode/launch.json`)

| Configuration | What it does |
|---------------|-------------|
| **Launch pwsh with Module** | Builds the module, then opens a `pwsh` session with it pre-imported. Set breakpoints in C# and step through cmdlet execution. |
| **Attach to pwsh** | Attach the debugger to an already-running `pwsh` process. |
| **Run Unit Tests** | Builds and runs unit tests with the debugger attached. |
| **Run End-to-End Tests** | Builds and runs e2e tests (requires `GITHUB_TOKEN`). |

### Build Tasks (`.vscode/tasks.json`)

- **build** (default, Ctrl+Shift+B): `dotnet publish` to `out/`
- **build-tests**: `dotnet build` the test project

## 9. Build & Usage

### build.ps1

```powershell
#!/usr/bin/env pwsh
dotnet publish src/CopilotCmdlets.csproj -c Release -o out
Write-Host "Import with: Import-Module ./out/CopilotCmdlets.psd1"
```

### CopilotCmdlets.psd1 (module manifest)

```powershell
@{
    RootModule        = 'CopilotCmdlets.dll'
    ModuleVersion     = '0.1.0'
    GUID              = '<generated>'
    Author            = 'GitHub'
    Description       = 'PowerShell wrapper for the GitHub Copilot SDK'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')
    CmdletsToExport   = @(
        'New-CopilotClient'
        'Stop-CopilotClient'
        'Test-CopilotConnection'
        'New-CopilotSession'
        'Resume-CopilotSession'
        'Get-CopilotSession'
        'Remove-CopilotSession'
        'Close-CopilotSession'
        'Send-CopilotMessage'
        'Get-CopilotMessage'
    )
    FunctionsToExport = @()
    AliasesToExport   = @()
    VariablesToExport = @()
}
```

### Example Usage

```powershell
Import-Module ./out/CopilotCmdlets.psd1

# Connect
New-CopilotClient

# Verify
Test-CopilotConnection

# Start a session with a memorable ID
New-CopilotSession -SessionId "my-review" -Model "gpt-5"

# Chat (streams to terminal, returns structured result)
$result = Send-CopilotMessage "Analyze the code in src/ for security issues"
$result.Content  # full response text

# History
Get-CopilotMessage | Where-Object { $_ -is [GitHub.Copilot.SDK.AssistantMessageEvent] }

# Later: resume
Close-CopilotSession
Resume-CopilotSession -SessionId "my-review"
Send-CopilotMessage "What did we discuss earlier?"

# List all sessions
Get-CopilotSession | Format-Table SessionId, Summary, ModifiedTime

# Cleanup
Remove-CopilotSession -SessionId "my-review"
Stop-CopilotClient
```

## 10. What's NOT in Scope (v1)

| Feature | Why deferred |
|---------|-------------|
| BYOK `-Provider` parameter | "For now, we'll use GitHub hosted models." Plumbing the `Provider` object into `SessionConfig` is straightforward when needed. |
| Custom tools | Requires defining `AIFunction` objects — complex UX for PowerShell. Defer until there's a clear need. |
| Session hooks | The SDK's `SessionHooks` type requires delegates. Could be exposed later as script blocks. |
| MCP servers / custom agents | Pass-through config objects. Easy to add as `-McpServers` / `-CustomAgents` hashtable params. |
| `-NoWait` async pattern | Designed for (see section 7) but not implemented in v1. |
| Tab completion | Argument completers for `-Model`, `-SessionId` etc. Nice-to-have, add later. |
| Format/type data (.ps1xml) | Custom formatting for `SessionMetadata`, `CopilotMessageResult`. Polish item. |

## 11. Implementation Phases

### Phase 1 — Project Skeleton & Client Lifecycle

**Goal:** Build the project, connect to the Copilot CLI, and verify the round-trip works.

**Deliverables:**
- `CopilotCmdlets.csproj` with SDK and PowerShell references
- `CopilotCmdlets.psd1` module manifest (export only Phase 1 cmdlets initially)
- `ModuleState.cs` — static singleton with `client` field, `RequireClient()`, and `IModuleAssemblyCleanup`
- `ClientCmdlets.cs` — `New-CopilotClient`, `Stop-CopilotClient`, `Test-CopilotConnection`
- `build.ps1`

**Exit criteria:** `New-CopilotClient | Test-CopilotConnection` succeeds against a running CLI; `Stop-CopilotClient` shuts it down cleanly; unit tests for Phase 1 pass.

---

### Phase 2 — Session Lifecycle

**Goal:** Create, list, resume, close, and delete sessions.

**Deliverables:**
- `SessionCmdlets.cs` — `New-CopilotSession`, `Resume-CopilotSession`, `Get-CopilotSession`, `Remove-CopilotSession`, `Close-CopilotSession`
- Extend `ModuleState` with `currentSession`, `RequireSession()`, permission handlers, and user-input handlers
- Update manifest to export session cmdlets

**Exit criteria:** Full session round-trip (create → list → close → resume → delete) works; interactive and auto-approve permission modes work; unit tests for Phase 2 pass.

---

### Phase 3 — Messaging & Streaming

**Goal:** Send messages, stream output, and retrieve history.

**Deliverables:**
- `MessageCmdlets.cs` — `Send-CopilotMessage`, `Get-CopilotMessage`, `CopilotMessageResult` POCO
- Event handler wiring (deltas → `Console.Write`, tool events → `Console.Error`, idle/error → signal)
- Update manifest to export all cmdlets

**Exit criteria:** `Send-CopilotMessage` streams to terminal and returns a structured `CopilotMessageResult`; `Get-CopilotMessage` returns history; end-to-end test of a full conversation passes.

---

### Phase 4 — Polish & Hardening

**Goal:** Production-ready quality, CI green, documentation.

**Deliverables:**
- Timeout handling and cancellation support in `Send-CopilotMessage`
- Error paths: missing client, missing session, SDK exceptions surfaced as `ErrorRecord`
- `-WhatIf`/`-Confirm` support on `Remove-CopilotSession` and `Stop-CopilotClient`
- README with quickstart
- All unit and end-to-end tests passing in CI

**Exit criteria:** No known defects; CI pipeline runs unit tests and (optionally) end-to-end tests; module installs cleanly from build output.

---

## 12. Testing Strategy

### Project Layout

```
tests/
├── CopilotCmdlets.Tests.csproj  # xUnit test project, references src/CopilotCmdlets.csproj
├── Unit/
│   ├── ModuleStateTests.cs          # RequireClient/RequireSession null-handling, cleanup
│   ├── ClientCmdletTests.cs         # Parameter binding, state side-effects
│   ├── SessionCmdletTests.cs        # Parameter binding, handler selection, state side-effects
│   └── MessageCmdletTests.cs        # Result construction, event accumulation
└── EndToEnd/
    ├── ClientLifecycleTests.cs      # Start → ping → stop against real CLI
    ├── SessionLifecycleTests.cs     # Create → list → close → resume → delete
    └── ConversationTests.cs         # Send message → stream → get history
```

### Unit Tests

Unit tests validate cmdlet logic **without** a running Copilot CLI process.

**Approach — Interface-based mocking:**
- The Copilot SDK types (`CopilotClient`, `CopilotSession`) are injected into cmdlets via the `-Client` / `-Session` parameters or via `ModuleState`.
- Where SDK types are concrete classes without interfaces, use a thin internal adapter or wrap behind a minimal interface (e.g., `ICopilotClientWrapper`) that can be substituted in tests. Keep adapters trivial — no logic, just delegation.
- Use **xUnit** as the test framework and **NSubstitute** (or Moq) for mocking.

**What unit tests cover:**
| Area | Examples |
|------|----------|
| `ModuleState` | `RequireClient()` throws when no client set; cleanup disposes both session and client; setting a new client replaces the old one |
| Parameter defaults | `-LogLevel` defaults to `"info"`; `-Timeout` defaults to 5 minutes |
| Permission handler selection | `-AutoApprove` produces approve-all handler; default produces interactive handler |
| `CopilotMessageResult` construction | Events are accumulated correctly; `Content` is assembled from delta events |
| Error handling | Missing client/session produces the correct `ErrorRecord`; SDK exceptions are wrapped properly |
| `ShouldProcess` | `Remove-CopilotSession` respects `-WhatIf` (no delete call made) |

**Running unit tests:**
```bash
dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"
```

### End-to-End Tests

End-to-end tests exercise the **real SDK against a running Copilot CLI** process.

**Prerequisites:**
- A valid GitHub token with Copilot access (set via `GITHUB_TOKEN` env var)
- The `github-copilot` CLI binary available on `PATH` (or specify via `COPILOT_CLI_PATH`)
- Network access to GitHub

**Approach:**
- Tests use `PowerShell.Create()` from `System.Management.Automation` to invoke cmdlets in-process, simulating real PowerShell pipeline execution.
- Each test class manages its own client lifecycle (`New-CopilotClient` in setup, `Stop-CopilotClient` in teardown).
- Session tests create disposable sessions with unique IDs and clean up via `Remove-CopilotSession` in teardown.
- Tests are marked with `[Trait("Category", "EndToEnd")]` so they can be skipped in environments without credentials.

**What end-to-end tests cover:**
| Area | Examples |
|------|----------|
| Client lifecycle | Start client → ping → stop; force-stop |
| Session lifecycle | Create session → list (verify present) → close → resume → delete → list (verify absent) |
| Messaging | Send a simple prompt → verify `CopilotMessageResult.Content` is non-empty; verify `Events` contains expected event types |
| Streaming | Send a prompt → verify delta events were emitted (captured via test event handler) |
| Permission flow | Create session with `-AutoApprove` → send a tool-using prompt → verify no interactive prompt blocks |
| Error cases | Send to a disposed session → verify error; resume a non-existent session → verify error |

**Running end-to-end tests:**
```bash
# Requires GITHUB_TOKEN and CLI on PATH
dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=EndToEnd"
```

### CI Integration

```yaml
# Suggested GitHub Actions job structure
test-unit:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with: { dotnet-version: '9.0.x' }
    - run: dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"

test-e2e:
  runs-on: ubuntu-latest
  if: github.event_name == 'push' && github.ref == 'refs/heads/main'
  environment: copilot-testing
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with: { dotnet-version: '9.0.x' }
    - run: dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=EndToEnd"
      env:
        GITHUB_TOKEN: ${{ secrets.COPILOT_GITHUB_TOKEN }}
```

Unit tests run on every PR. End-to-end tests run on pushes to `main` (or manually) since they require secrets and network access.

## 13. File Inventory (5 C# files)

| File | Contents | Approximate lines |
|------|----------|-------------------|
| `ModuleState.cs` | `ModuleState` static class, `PermissionHandlers`, `UserInputHandlers`, `ModuleCleanup` | ~80 |
| `ClientCmdlets.cs` | `NewCopilotClientCmdlet`, `StopCopilotClientCmdlet`, `TestCopilotConnectionCmdlet` | ~70 |
| `SessionCmdlets.cs` | `NewCopilotSessionCmdlet`, `ResumeCopilotSessionCmdlet`, `GetCopilotSessionCmdlet`, `RemoveCopilotSessionCmdlet`, `CloseCopilotSessionCmdlet` | ~120 |
| `MessageCmdlets.cs` | `SendCopilotMessageCmdlet`, `GetCopilotMessageCmdlet`, `CopilotMessageResult` | ~100 |
| `CopilotCmdlets.csproj` | Project file | ~20 |
| **Total** | | **~390** |
