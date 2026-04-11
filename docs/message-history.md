# Message History — Design Doc

## 1. Question

> Is it possible for us to implement a `Get-CopilotMessage` or
> `Get-CopilotSessionHistory` and view prior messages (with all the associated
> tool calls etc)?

**Short answer:** Yes — and the cmdlet already exists. `Get-CopilotMessage`
ships today (see `src/MessageCmdlets.cs:147`) and is a thin pass-through to
`CopilotSession.GetMessagesAsync()` on the SDK. It returns
`IReadOnlyList<SessionEvent>`, which is the same event type family surfaced by
the live session handler — so tool-call events, assistant deltas, user
messages, idle/error markers, etc., are all representable.

What remains is mostly **validation, test coverage, UX polish, and docs** —
not new cmdlet logic. Per `CLAUDE.md` this module is a thin SDK wrapper, so
we deliberately do **not** rebuild the SDK's history model on top of it.

## 2. Current State

### 2.1 Cmdlet

`Get-CopilotMessage` (from `src/MessageCmdlets.cs:147`):

```csharp
[Cmdlet(VerbsCommon.Get, "CopilotMessage")]
[OutputType(typeof(SessionEvent))]
public sealed class GetCopilotMessageCmdlet : PSCmdlet
{
    [Parameter] public CopilotSession? Session { get; set; }

    protected override void EndProcessing()
    {
        // ... resolves Session (explicit or module default) ...
        var messages = target.GetMessagesAsync(CancellationToken.None)
            .GetAwaiter().GetResult();
        WriteObject(messages, enumerateCollection: true);
    }
}
```

- Pass-through to `session.GetMessagesAsync()`.
- Uses `ModuleState.TryRequireSession` so `-Session` is optional; the
  module-default session is used otherwise.
- Output type declared as `SessionEvent` — the same base type the streaming
  handler in `Send-CopilotMessage` dispatches on.
- Exported from `CopilotCmdlets.psd1:19`.

### 2.2 Events the SDK already produces

From the `Send-CopilotMessage` handler in `src/MessageCmdlets.cs:62-97`, we
know the live stream emits at least:

| Event type | Carries |
|---|---|
| `AssistantMessageDeltaEvent` | Incremental `DeltaContent` chunks |
| `AssistantMessageEvent` | Finalized assistant message (`MessageId`, `Content`) |
| `ToolExecutionStartEvent` | `ToolName`, `ToolCallId` |
| `ToolExecutionCompleteEvent` | `ToolCallId`, `Success` flag |
| `SessionIdleEvent` | End-of-turn marker |
| `SessionErrorEvent` | `ErrorType`, `Message` |

User messages and attachments are modeled SDK-side via
`UserMessageDataAttachmentsItem` / `UserMessageDataAttachmentsItemFile`
(referenced in `src/MessageCmdlets.cs:106`), so historical user turns are
already represented in the SDK surface.

### 2.3 Tests that cover it today

- **Unit:** `tests/Unit/MessageCmdletTests.cs:48-64` verifies cmdlet attribute
  and declared `OutputType(typeof(SessionEvent))`.
- **End-to-end:** `tests/EndToEnd/ConversationTests.cs:103-116` sends a
  message, calls `Get-CopilotMessage`, and asserts the history is non-empty.

## 3. What We Don't Yet Know (SDK Verification)

Before committing to any additional work, we need to empirically confirm the
SDK's behavior — nothing below should be inferred from docs alone.

1. **Does `GetMessagesAsync` return historical `ToolExecution*` events?**
   The live handler sees them, but history may be compacted to message-only.
   If tool events are dropped, that is an **SDK gap**, not something we patch
   around locally — we file an SDK feature request per `CLAUDE.md`.
2. **Does `GetMessagesAsync` work after `Resume-CopilotSession`** on a
   session we didn't originate in this process? A resumed session is the
   primary use case for "view prior messages".
3. **Does history include the user turn(s)** (prompts + attachments), not just
   assistant responses?
4. **Is ordering stable** across a transcript (delta → final → tool-start →
   tool-complete → idle)?
5. **Are tool arguments and tool results retained**, or only
   `ToolName`/`ToolCallId`/`Success`? Users asking for "all the associated
   tool calls" probably expect arguments and outputs.
6. **Is there a paging / filter API** on `GetMessagesAsync` (e.g.
   since-messageId, max count)? The current signature in the code only takes
   a `CancellationToken`.

These questions are answered by a short empirical spike against the real CLI
(see §5, Task A).

## 4. Goals (and Non-Goals)

### Goals

- Users can retrieve a full transcript of a session, including tool calls,
  via a single cmdlet.
- The command works on (a) the live session and (b) sessions recovered via
  `Resume-CopilotSession`.
- Output is scriptable: filterable by type, pipeable to `Format-Table` or
  `Where-Object`, and the same `SessionEvent` subclasses as the live stream
  (so user scripts that work on `Send-CopilotMessage`'s `Events` collection
  also work on history).
- Test coverage at unit and end-to-end levels, matching the
  `CLAUDE.md` "always test" rule.

### Non-Goals (thin-wrapper guardrails)

- **No custom conversation reconstruction.** We do not invent a
  `ConversationTurn` / `MessageWithToolCalls` POCO to merge deltas, pair
  tool-start with tool-complete, or reattach tool outputs to tool calls.
  Those are SDK responsibilities. If the SDK doesn't expose them, we file a
  feature request.
- **No local caching of history.** The SDK already persists to
  `~/.copilot/session-state/{sessionId}/` (see `docs/initial-design.md:56`).
  We never duplicate that.
- **No paging implementation on our side.** If the SDK adds paging
  parameters, we add matching `-Since` / `-Limit` parameters as pass-throughs.
  Otherwise, we return the full list.
- **No formatting engine.** We may add a `.ps1xml` file for prettier default
  display (polish, already noted as deferred in `docs/initial-design.md:369`),
  but the object graph itself stays as SDK types.

## 5. Proposed Work

All tasks are optional — the cmdlet is already shipping. Pick and implement
based on what the SDK verification in §3 reveals.

### Task A — SDK Capability Spike (required before anything else)

Write an e2e-category test that:

1. Creates a session with `-AutoApprove`.
2. Sends a prompt that **forces a tool invocation** (e.g. ask Copilot to read
   a known file — we already use the Copilot CLI in e2e tests so tools are
   wired up).
3. Sends a follow-up plain prompt to ensure multiple turns.
4. Calls `Get-CopilotMessage` and records:
   - Count and types of returned events.
   - Whether both `ToolExecutionStartEvent` **and**
     `ToolExecutionCompleteEvent` appear.
   - Whether user messages are present.
   - Whether tool arguments / tool output payloads are visible on the event
     `.Data` objects.
5. Closes and resumes the session via `Resume-CopilotSession`, then calls
   `Get-CopilotMessage` again and asserts the transcript is still there.

Output of this spike: a markdown note appended to this doc (§7, "SDK
Findings") capturing what the SDK actually returns. That note drives all
downstream decisions.

### Task B — Naming / Discoverability

The user's question suggested `Get-CopilotSessionHistory` as an alternative
name. Two options:

1. **Keep `Get-CopilotMessage` as-is.** It's already exported, already
   tested, matches the `*-CopilotMessage` noun family with
   `Send-CopilotMessage`. Recommended.
2. **Add an alias `Get-CopilotSessionHistory`.** Zero-cost discoverability
   win. Declared in `CopilotCmdlets.psd1` under `AliasesToExport`. No new
   C# code.

Recommendation: Option 1, but add a short note in the `README.md` "Messaging"
table clarifying that `Get-CopilotMessage` retrieves the *full transcript*
(not a single message by ID), since the singular noun can be misleading.

### Task C — Parameter Additions (only if SDK supports them)

If the SDK `GetMessagesAsync` overloads expose filtering (e.g. since,
max-count, types), add matching pass-through parameters:

| Parameter | Type | Maps to |
|---|---|---|
| `-Since` | `string` (messageId) | `GetMessagesAsync(since: …)` |
| `-Limit` | `int` | `GetMessagesAsync(limit: …)` |

**Rule:** one parameter per SDK capability, nothing more. If the SDK does
not provide it, we do not implement it client-side.

### Task D — `.ps1xml` Display Formatting (polish)

Add a `CopilotCmdlets.format.ps1xml` (referenced via the manifest's
`FormatsToProcess`) with table views for the common `SessionEvent`
subclasses:

- `AssistantMessageEvent`: `Timestamp`, `MessageId`, `Content` (truncated).
- `ToolExecutionStartEvent` / `ToolExecutionCompleteEvent`: `ToolName`,
  `ToolCallId`, `Success`.
- `UserMessageEvent` (if surfaced by SDK): `Timestamp`, `Content`.

This makes `Get-CopilotMessage | Format-Table` useful out of the box without
adding any new types. Explicitly flagged as deferred polish in
`docs/initial-design.md:369` — Task D is where we'd cash that in.

### Task E — Tests

Unit-test additions (no CLI required):

- `Get-CopilotMessage` throws a clean `ErrorRecord` (not a raw exception)
  when no session is set — the existing pattern uses
  `ModuleState.TryRequireSession`; add a regression test.
- `Get-CopilotMessage` declares `OutputType(typeof(SessionEvent))` — already
  covered at `tests/Unit/MessageCmdletTests.cs:56`.

End-to-end additions (extend `tests/EndToEnd/ConversationTests.cs`):

- **History after resume:** send message → close → resume → call
  `Get-CopilotMessage` → assert assistant content is still present.
- **Tool calls in history:** send a tool-using prompt → call
  `Get-CopilotMessage` → assert a `ToolExecutionStartEvent` appears (only if
  Task A confirms the SDK exposes tool events in history; otherwise omit and
  file the SDK feature request).
- **Ordering:** assert `SessionIdleEvent` is the last event for a completed
  turn.

### Task F — README Update

Add a short "History" section under "Messaging" in `README.md` showing:

```powershell
# Full transcript
Get-CopilotMessage

# Assistant turns only
Get-CopilotMessage |
    Where-Object { $_ -is [GitHub.Copilot.SDK.AssistantMessageEvent] } |
    ForEach-Object { $_.Data.Content }

# Every tool Copilot invoked this session
Get-CopilotMessage |
    Where-Object { $_ -is [GitHub.Copilot.SDK.ToolExecutionStartEvent] } |
    Select-Object @{n='Tool';e={$_.Data.ToolName}}, @{n='Id';e={$_.Data.ToolCallId}}
```

The `initial-design.md:344` example already hints at this pattern — Task F
just elevates it into the user-facing README.

## 6. Risk & Open Questions

| Risk | Mitigation |
|---|---|
| SDK drops tool events from history | File SDK feature request. Do not reconstruct locally. |
| SDK history excludes user turns | File SDK feature request. Do not synthesize from our own prompt cache. |
| Tool arguments / results not retained | File SDK feature request. Users fall back to structured logging from live `Send-CopilotMessage` `.Events`. |
| Resumed-session history is empty | File SDK bug. Confirm persistence path exists at `~/.copilot/session-state/{sessionId}/`. |
| Large transcripts blow out pipeline memory | Accept. If SDK adds streaming/paging later we pass it through (Task C). |

## 7. SDK Findings

*(To be filled in by Task A. Leave empty until the spike runs.)*

## 8. Summary

- `Get-CopilotMessage` already delivers the requested capability at the API
  level — `session.GetMessagesAsync()` returns `SessionEvent` history, and
  the cmdlet is a ~25-line pass-through.
- The real work is (a) verifying exactly what the SDK includes (tool args?
  user turns? ordering? resume support?) and (b) either documenting it or
  filing SDK feature requests for any gaps.
- Nothing in this plan adds business logic; every proposed change is either
  an SDK pass-through, a test, a display format, or a doc update — fully
  consistent with the "thin wrapper, no custom features" rule in `CLAUDE.md`.
- **Recommended next step:** run Task A (SDK capability spike) and record
  findings in §7. Everything else is contingent on what that spike reveals.
