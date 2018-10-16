﻿# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe "TestImplicitRemotingBatching hook should correctly batch simple remote command pipelines" -Tags 'Feature','RequireAdminOnWindows' {

    BeforeAll {

        if ($isWindows)
        {
            [powershell] $powerShell = [powershell]::Create([System.Management.Automation.RunspaceMode]::NewRunspace)

            # Create remote session in new PowerShell session
            $powerShell.AddScript('Import-Module -Name HelpersRemoting; $remoteSession = New-RemoteSession').Invoke()
            if ($powerShell.Streams.Error.Count -gt 0) { throw "Unable to create remote session for test" }

            # Import implicit commands from remote session
            $powerShell.Commands.Clear()
            $powerShell.AddScript('Import-PSSession -Session $remoteSession -CommandName "Get-Process","Write-Output" -AllowClobber').Invoke()
            if ($powerShell.Streams.Error.Count -gt 0) { throw "Unable to import pssession for test" }

            # Define $filter variable in local session
            $powerShell.Commands.Clear()
            $powerShell.AddScript('$filter = "pwsh","powershell"').Invoke()
            $localRunspace = $powerShell.Runspace

            [powershell] $psInvoke = [powershell]::Create([System.Management.Automation.RunspaceMode]::NewRunspace)

            $testCases = @(
                @{
                    Name = 'Two implicit commands should be successfully batched'
                    CommandLine = 'Get-Process -Name "pwsh" | Write-Output'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Two implicit commands with Where-Object should be successfully batched'
                    CommandLine = 'Get-Process | Write-Output | Where-Object { $_.Name -like "*pwsh*" }'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Two implicit commands with Sort-Object should be successfully batched'
                    CommandLine = 'Get-Process -Name "pwsh" | Sort-Object -Property Name | Write-Output'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Two implicit commands with ForEach-Object should be successfully batched'
                    CommandLine = 'Get-Process -Name "pwsh" | Write-Output | ForEach-Object { $_ }'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Two implicit commands with Measure-Command should be successfully batched'
                    CommandLine = 'Measure-Command { Get-Process | Write-Output }'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Two implicit commands with Measure-Object should be successfully batched'
                    CommandLine = 'Get-Process | Write-Output | Measure-Object'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Implicit commands with variable arguments should be successfully batched'
                    CommandLine = 'Get-Process -Name $filter | Write-Output'
                    ExpectedOutput = $true
                },
                @{
                    Name = 'Pipeline with non-implicit command should not be batched'
                    CommandLine = 'Get-Process | Write-Output | Select-Object -Property Name'
                    ExpectedOutput = $false
                },
                @{
                    Name = 'Non-simple pipeline should not be batched'
                    CommandLine = '1..2 | % { Get-Process pwsh | Write-Output }'
                    ExpectedOutput = $false
                }
                @{
                    Name = 'Pipeline with single command should not be batched'
                    CommandLine = 'Get-Process pwsh'
                    ExpectedOutput = $false
                },
                @{
                    Name = 'Pipeline without any implicit commands should not be batched'
                    CommandLine = 'Get-PSSession | Out-Default'
                    ExpectedOutput = $false
                }
            )
        }
    }

    AfterAll {

        if ($isWindows)
        {
            if ($remoteSession -ne $null) { Remove-PSSession $remoteSession -ErrorAction Ignore }
            if ($powershell -ne $null) { $powershell.Dispose() }
            if ($psInvoke -ne $null) { $psInvoke.Dispose() }
        }
    }

    It "<Name>" -TestCases $testCases -Skip:(! $IsWindows) {
        param ($CommandLine, $ExpectedOutput)

        $psInvoke.Commands.Clear()
        $psInvoke.Commands.AddScript('param ($cmdLine, $runspace) [System.Management.Automation.Internal.InternalTestHooks]::TestImplicitRemotingBatching($cmdLine, $runspace)').AddArgument($CommandLine).AddArgument($localRunspace)

        $result = $psInvoke.Invoke()
        $result | Should Be $ExpectedOutput
    }
}
