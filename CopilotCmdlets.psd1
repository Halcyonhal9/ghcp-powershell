@{
    RootModule           = 'CopilotCmdlets.dll'
    ModuleVersion     = '0.6.0'
    GUID                 = 'a7f3d8e1-4b2c-4f9a-8e6d-1c3b5a7f9e2d'
    Author               = 'Ben Appleby'
    Description          = 'PowerShell Cmdlets for the GitHub Copilot SDK'
    PowerShellVersion    = '7.6'
    CompatiblePSEditions = @('Core')
    FormatsToProcess     = @('CopilotCmdlets.format.ps1xml')
    CmdletsToExport      = @(
        'New-CopilotClient'
        'Stop-CopilotClient'
        'Test-CopilotConnection'
        'Get-CopilotStatus'
        'Get-CopilotAuthStatus'
        'Connect-Copilot'
        'Register-CopilotSessionLifecycleEvent'
        'Get-CopilotLastSessionId'
        'Get-CopilotForegroundSessionId'
        'Set-CopilotForegroundSessionId'
        'New-CopilotSession'
        'Resume-CopilotSession'
        'Get-CopilotSession'
        'Remove-CopilotSession'
        'Close-CopilotSession'
        'New-CopilotSessionHooks'
        'New-CopilotCommand'
        'New-CopilotProvider'
        'New-CopilotNamedProvider'
        'New-CopilotSectionOverride'
        'New-CopilotToolSet'
        'Confirm-CopilotElicitation'
        'Select-CopilotElicitation'
        'Read-CopilotElicitationInput'
        'Request-CopilotElicitation'
        'Send-CopilotMessage'
        'Send-CopilotMessageAsync'
        'Receive-CopilotAsyncResult'
        'Stop-CopilotMessage'
        'Get-CopilotMessage'
        'Get-CopilotModel'
        'Set-CopilotModel'
        'New-CopilotTool'
    )
    FunctionsToExport    = @()
    AliasesToExport      = @()
    VariablesToExport    = @()
    PrivateData          = @{
        PSData = @{
            Tags         = @('Copilot', 'Github', 'SDK')
            LicenseUri   = 'https://github.com/Halcyonhal9/ghcp-powershell/blob/main/LICENSE'
            ProjectUri   = 'https://github.com/Halcyonhal9/ghcp-powershell'
            ReleaseNotes = @'
0.6.0 - Expose the remaining GitHub.Copilot.SDK 1.0.6 surface tracked by #28.
New session configuration: hooks, MCP OAuth, custom/default agents, BYOK
providers and models, elicitation and mode handlers, commands, ToolSet,
infinite-session tuning, large-output and memory configuration, and
remote/cloud options. New client/session lifecycle and foreground/last-session
cmdlets. New Session.Ui elicitation cmdlets and provider/callback builders.
Message sends now support -AgentMode. ScriptBlock callbacks preserve the
originating PowerShell language mode and raw SDK delegates remain available.

0.5.0 - Upgrade to GitHub.Copilot.SDK 1.0.6.
BREAKING: SDK types moved from the GitHub.Copilot.SDK namespace to
GitHub.Copilot / GitHub.Copilot.Rpc. Scripts that match pipeline output by
type name (e.g. [GitHub.Copilot.SDK.AssistantMessageEvent]) must update to
the new names. Get-CopilotMessage now returns the session event log via the
SDK's GetEventsAsync.
New cmdlets: Get-CopilotStatus, Get-CopilotAuthStatus, Stop-CopilotMessage,
New-CopilotTool. New session options: -McpServers, -Tool, -EnableCitations,
-MaxAiCredits, -ExcludedBuiltInAgents, -DisabledSkills, -ContinuePendingWork.
New client options: -WorkingDirectory, -Environment, -UseLoggedInUser.
New message options: -Mode, -DisplayPrompt.
'@
        }
    }
}
