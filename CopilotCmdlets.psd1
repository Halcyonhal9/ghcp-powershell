@{
    RootModule           = 'CopilotCmdlets.dll'
    ModuleVersion     = '0.4.12'
    GUID                 = 'a7f3d8e1-4b2c-4f9a-8e6d-1c3b5a7f9e2d'
    Author               = 'Ben Appleby'
    Description          = 'PowerShell Cmdlets for the GitHub Copilot SDK'
    PowerShellVersion    = '7.4'
    CompatiblePSEditions = @('Core')
    FormatsToProcess     = @('CopilotCmdlets.format.ps1xml')
    CmdletsToExport      = @(
        'New-CopilotClient'
        'Stop-CopilotClient'
        'Test-CopilotConnection'
        'Connect-Copilot'
        'New-CopilotSession'
        'Resume-CopilotSession'
        'Get-CopilotSession'
        'Remove-CopilotSession'
        'Close-CopilotSession'
        'Send-CopilotMessage'
        'Send-CopilotMessageAsync'
        'Receive-CopilotAsyncResult'
        'Get-CopilotMessage'
        'Get-CopilotModel'
        'Set-CopilotModel'
    )
    FunctionsToExport    = @()
    AliasesToExport      = @()
    VariablesToExport    = @()
    PrivateData          = @{
        PSData = @{
            Tags       = @('Copilot', 'Github', 'SDK')
            LicenseUri = 'https://github.com/Halcyonhal9/ghcp-powershell/blob/main/LICENSE'
            ProjectUri = 'https://github.com/Halcyonhal9/ghcp-powershell'
        }
    }
}
