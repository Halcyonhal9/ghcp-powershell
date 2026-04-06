#!/usr/bin/env pwsh
dotnet publish src/CopilotCmdlets.csproj -c Release -o out
Write-Host "Import with: Import-Module ./out/CopilotCmdlets.psd1"
