# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
Describe "Join-Path cmdlet tests" -Tags "CI" {
    BeforeAll {
        $SepChar = [IO.path]::DirectorySeparatorChar
        $StartingLocation = Get-Location
    }

    AfterEach {
        Set-Location $StartingLocation
    }

    It "should output multiple paths when called with multiple -Path targets" {
        Setup -Dir SubDir1
        (Join-Path -Path TestDrive:, $TestDrive -ChildPath "SubDir1" -resolve).Length | Should -Be 2
    }

    It "should throw 'DriveNotFound' when called with -Resolve and drive does not exist" {
        { Join-Path bogusdrive:\\somedir otherdir -resolve -ErrorAction Stop; Throw "Previous statement unexpectedly succeeded..." } |
            Should -Throw -ErrorId "DriveNotFound,Microsoft.PowerShell.Commands.JoinPathCommand"
    }

    It "should throw 'PathNotFound' when called with -Resolve and item does not exist" {
        { Join-Path "Bogus" "Path" -resolve -ErrorAction Stop; Throw "Previous statement unexpectedly succeeded..." } |
            Should -Throw -ErrorId "PathNotFound,Microsoft.PowerShell.Commands.JoinPathCommand"
    }

    #[BugId(BugDatabase.WindowsOutOfBandReleases, 905237)] Note: Result should be the same on non-Windows platforms too
    It "should return one object when called with a Windows FileSystem::Redirector" {
        set-location ("env:" + $SepChar)
        $result = join-path FileSystem::windir system32
        $result.Count | Should -Be 1
        $result       | Should -BeExactly ("FileSystem::windir" + $SepChar + "system32")
    }

    #[BugId(BugDatabase.WindowsOutOfBandReleases, 913084)]
    It "should be able to join-path special string 'Variable:' with 'foo'" {
        $result = Join-Path "Variable:" "foo"
        $result.Count | Should -Be 1
        $result       | Should -BeExactly ("Variable:" + $SepChar + "foo")
    }

    #[BugId(BugDatabase.WindowsOutOfBandReleases, 913084)]
    It "should be able to join-path special string 'Alias:' with 'foo'" {
        $result = Join-Path "Alias:" "foo"
        $result.Count | Should -Be 1
        $result       | Should -BeExactly ("Alias:" + $SepChar + "foo")
    }

    #[BugId(BugDatabase.WindowsOutOfBandReleases, 913084)]
    It "should be able to join-path special string 'Env:' with 'foo'" {
        $result = Join-Path "Env:" "foo"
        $result.Count | Should -Be 1
        $result       | Should -BeExactly ("Env:" + $SepChar + "foo")
    }

    It "should be able to join multiple child paths passed by position with remaining arguments" {
        $result = Join-Path one two three four five
        $result.Count | Should -Be 1
        $result       | Should -BeExactly "one${sepChar}two${sepChar}three${sepChar}four${sepChar}five"
    }

    It "should be able to resolve nonexistent paths with -Resolve -SkipValidation" {
        Setup -Dir Subdir1
        $Result = Join-Path -Path $TestDrive -ChildPath 'Subdir1' -AdditionalChildPath 'Nope' -Resolve -SkipValidation
        $Result.Count | Should -Be 1
        $Result | Should -BeExactly "$TestDrive${sepChar}Subdir1${sepChar}Nope"
    }
}
