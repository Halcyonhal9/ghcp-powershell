@{
    RootModule        = 'CopilotCmdlets.dll'
    ModuleVersion     = '0.1.0'
    GUID              = 'a7f3d8e1-4b2c-4f9a-8e6d-1c3b5a7f9e2d'
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
        'Get-CopilotModel'
    )
    FunctionsToExport = @()
    AliasesToExport   = @()
    VariablesToExport = @()
}
