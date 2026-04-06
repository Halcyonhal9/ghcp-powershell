#!/usr/bin/env pwsh
dotnet publish src/CopilotPS.csproj -c Release -o out
Write-Host "Import with: Import-Module ./out/CopilotPS.psd1"
