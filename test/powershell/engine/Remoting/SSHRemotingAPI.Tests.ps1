# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
Describe "SSH Remoting API Tests" -Tags "Feature" {

    Context "SSHConnectionInfo Class Tests" {

        BeforeAll {
            ## Skip the test if ssh is not present.
            $skipTest = (Get-Command 'ssh' -CommandType Application -ErrorAction SilentlyContinue) -eq $null
        }

        AfterEach {
            if ($null -ne $rs) {
                $rs.Dispose()
            }
        }

        It "SSHConnectionInfo constructor should throw null argument exception for null HostName parameter" {

            { [System.Management.Automation.Runspaces.SSHConnectionInfo]::new(
                "UserName",
                [System.Management.Automation.Internal.AutomationNull]::Value,
                [System.Management.Automation.Internal.AutomationNull]::Value,
                0) } | Should -Throw -ErrorId "PSArgumentNullException"
        }

        It "SSHConnectionInfo should throw file not found exception for invalid key file path" -Skip:$skipTest {

            $e = {
                $sshConnectionInfo = [System.Management.Automation.Runspaces.SSHConnectionInfo]::new(
                    "UserName",
                    "localhost",
                    "NoValidKeyFilePath",
                    22)

                $rs = [runspacefactory]::CreateRunspace($sshConnectionInfo)
                $rs.Open()
            } | Should -Throw -PassThru
            $e.Exception.InnerException.InnerException | Should -BeOfType "System.IO.FileNotFoundException"
        }

        It "SSHConnectionInfo should throw argument exception for invalid port (non 16bit uint)" {
            $e = {
                $sshConnectionInfo = [System.Management.Automation.Runspaces.SSHConnectionInfo]::new(
                    "UserName",
                    "localhost",
                    "ValidKeyFilePath",
                    99999)

                $rs = [runspacefactory]::CreateRunspace($sshConnectionInfo)
                $rs.Open()
            } | Should -Throw -PassThru
            $e.Exception.InnerException | Should -BeOfType "System.ArgumentException"
        }
    }
}
