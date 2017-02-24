
Describe "Clear-Content cmdlet tests" -Tags "CI" {
  $file1 = "file1.txt"
  Setup -File "$file1"

  Context "Clear-Content should actually clear content" {
    It "should clear-Content of testdrive:\$file1" {
      set-content -path testdrive:\$file1 -value "ExpectedContent" -passthru | Should be "ExpectedContent"
      clear-content -Path testdrive:\$file1
    }
    It "shouldn't get any content from testdrive:\$file1" {
      $result = get-content -path testdrive:\$file1
      $result | Should BeExactly $null
    }
    It "should throw `"Cannot bind argument to parameter 'Path'`" when -Path is `$null" {
      { clear-content -path $null -ErrorAction Stop } | ShouldBeErrorId "ParameterArgumentValidationErrorNullNotAllowed,Microsoft.PowerShell.Commands.ClearContentCommand"
    }
    #[BugId(BugDatabase.WindowsOutOfBandReleases, 903880)]
    It "should throw `"Cannot bind argument to parameter 'Path'`" when -Path is `$()" {
      { clear-content -path $() -ErrorAction Stop } | ShouldBeErrorId "ParameterArgumentValidationErrorNullNotAllowed,Microsoft.PowerShell.Commands.ClearContentCommand"
    }
    #[DRT][BugId(BugDatabase.WindowsOutOfBandReleases, 906022)]
    It "should throw 'PSNotSupportedException' when you set-content to an unsupported provider" -Skip:($IsLinux -Or $IsOSX) {
      { clear-content -path HKLM:\\software\\microsoft -ErrorAction Stop } | ShouldBeErrorId "NotSupported,Microsoft.PowerShell.Commands.ClearContentCommand"
    }
  }
}
