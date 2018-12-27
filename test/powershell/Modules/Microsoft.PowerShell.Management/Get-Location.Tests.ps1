# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
[Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSAvoidUsingCmdletAliases", "")]
param()
Describe "Get-Location" -Tags "CI" {
    $currentDirectory=[System.IO.Directory]::GetCurrentDirectory()
    BeforeEach {
	Push-Location $currentDirectory
    }

    AfterEach {
	Pop-location
    }

    It "Should list the output of the current working directory" {

	(Get-Location).Path | Should -BeExactly $currentDirectory
    }

    # PSAvoidUsingCmdletAliases should be suppressed here
    It "Should do exactly the same thing as its alias" {
	(pwd).Path | Should -BeExactly (Get-Location).Path
    }
}
