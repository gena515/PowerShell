# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
Describe "Add-Type" -Tags "CI" {
    BeforeAll {
        $guid = [Guid]::NewGuid().ToString().Replace("-","")

        $CSharpCode1 = @"
        namespace Test.AddType
        {
            public class CSharpTest1$guid
            {
                public static int Add1(int a, int b)
                {
                    return (a + b);
                }
            }
        }
"@
        $CSharpCode2 = @"
        namespace Test.AddType
        {
            public class CSharpTest2$guid
            {
                public static int Add2(int a, int b)
                {
                    return (a + b);
                }
            }
        }
"@
        $CSharpFile1 = Join-Path -Path $TestDrive -ChildPath "CSharpFile1.cs"
        $CSharpFile2 = Join-Path -Path $TestDrive -ChildPath "CSharpFile2.cs"

        Set-Content -Path $CSharpFile1 -Value $CSharpCode1 -Force
        Set-Content -Path $CSharpFile2 -Value $CSharpCode2 -Force

        $VBCode1 = @"
        Namespace Test.AddType
            Public Class VBTest1$guid
                Public Shared Function Add1(a As Integer, b As Integer) As String
                    return (a + b)
                End Function
            End Class
        End Namespace
"@
        $VBCode2 = @"
        Namespace Test.AddType
            Public Class VBTest2$guid
                Public Shared Function Add2(a As Integer, b As Integer) As String
                    return (a + b)
                End Function
            End Class
        End Namespace
"@
        $VBFile1 = Join-Path -Path $TestDrive -ChildPath "VBFile1.cs"
        $VBFile2 = Join-Path -Path $TestDrive -ChildPath "VBFile2.cs"

        Set-Content -Path $VBFile1 -Value $VBCode1 -Force
        Set-Content -Path $VBFile2 -Value $VBCode2 -Force
    }

    It "Public 'Language' enumeration contains all members" {
        [Enum]::GetNames("Microsoft.PowerShell.Commands.Language") -join "," | Should -BeExactly "CSharp,VisualBasic"
    }

    It "Should not throw given a simple C# class definition" {
        # Also we check that '-Language CSharp' is by default.
        # In subsequent launches from the same session
        # the test will be passed without real compile - it will return an assembly previously compiled.
        { Add-Type -TypeDefinition "public static class CSharpfooType { }" } | Should Not Throw
        [CSharpfooType].Name | Should BeExactly "CSharpfooType"
    }

    It "Should not throw given a simple VisualBasic class definition" {
        # In subsequent launches from the same session
        # the test will be passed without real compile - it will return an assembly previously compiled.
        { Add-Type -TypeDefinition "Public Class VBfooType `n End Class" -Language VisualBasic } | Should Not Throw
        [VBfooType].Name | Should BeExactly "VBfooType"
    }

    It "Can use System.Management.Automation.CmdletAttribute" {
        $code = @"
using System.Management.Automation;
[System.Management.Automation.Cmdlet("Get", "Thing$guid", ConfirmImpact = System.Management.Automation.ConfirmImpact.High, SupportsPaging = true)]
public class AttributeTest$guid : PSCmdlet
{
    protected override void EndProcessing()

    {
        WriteObject("$guid");
    }
}
"@
        $cls = Add-Type -TypeDefinition $code -PassThru | Select-Object -First 1
        $testModule = Import-Module $cls.Assembly -PassThru

        Invoke-Expression -Command "Get-Thing$guid" | Should BeExactly $guid

        Remove-Module $testModule -ErrorAction SilentlyContinue -Force
    }

    It "Can load TPA assembly System.Runtime.Serialization.Primitives.dll" {
        $returnedTypes = Add-Type -AssemblyName 'System.Runtime.Serialization.Primitives' -PassThru
        $returnedTypes.Count | Should BeGreaterThan 0
        ($returnedTypes[0].Assembly.FullName -Split ",")[0]  | Should BeExactly 'System.Runtime.Serialization.Primitives'
    }

    It "Can compile <sourceLanguage> files" -TestCases @(
        @{
            type1 = "[Test.AddType.CSharpTest1$guid]"
            type2 = "[Test.AddType.CSharpTest2$guid]"
            file1 = $CSharpFile1
            file2 = $CSharpFile2
            sourceLanguage = "CSharp"
        }
        @{
            type1 = "[Test.AddType.VBTest1$guid]"
            type2 = "[Test.AddType.VBTest2$guid]"
            file1 = $VBFile1
            file2 = $VBFile2
            sourceLanguage = "VisualBasic"
        }
    ) {
        param($type1, $type2, $file1, $file2, $sourceLanguage)

        { $type1 = Invoke-Expression -Command $type1 } | Should Throw
        { $type2 = Invoke-Expression -Command $type2 } | Should Throw

        $returnedTypes = Add-Type -Path $file1,$file2 -Language $sourceLanguage -PassThru

        $type1 = Invoke-Expression -Command $type1
        $type2 = Invoke-Expression -Command $type2

        # We can compile, load and use new code.
        $type1::Add1(1, 2) | Should Be 3
        $type2::Add2(3, 4) | Should Be 7

        # Return the same assembly if source code has not been changed.
        # Also check that '-LiteralPath' works.
        $returnedTypes2 = Add-Type -LiteralPath $file1,$file2 -PassThru
        $returnedTypes[0].Assembly.FullName | Should BeExactly $returnedTypes2[0].Assembly.FullName
    }

    It "Can compile <sourceLanguage> with MemberDefinition" -TestCases @(
        @{
            sourceCode = "public static string TestString() { return UTF8Encoding.UTF8.ToString();}"
            sourceType = "TestCSharpType1"
            sourceNS = "TestCSharpNS"
            sourceUsingNS = "System.Text"
            sourceRunType = "TestCSharpNS.TestCSharpType1"
            sourceDefaultNSRunType = "Microsoft.PowerShell.Commands.AddType.AutoGeneratedTypes.TestCSharpType1"
            expectedResult = "System.Text.UTF8Encoding+UTF8EncodingSealed"
            sourceLanguage = "CSharp"
        }
        @{
            sourceCode = "Public Shared Function TestString() As String `n Return UTF8Encoding.UTF8.ToString() `n End Function"
            sourceType = "TestVisualBasicType1"
            sourceNS = "TestVisualBasicNS"
            sourceUsingNS = "System.Text"
            sourceRunType = "TestVisualBasicNS.TestVisualBasicType1"
            sourceDefaultNSRunType = "Microsoft.PowerShell.Commands.AddType.AutoGeneratedTypes.TestVisualBasicType1"
            expectedResult = "System.Text.UTF8Encoding+UTF8EncodingSealed"
            sourceLanguage = "VisualBasic"
        }
    ) {
        param($sourceCode, $sourceType, $sourceNS, $sourceUsingNS, $sourceRunType, $sourceDefaultNSRunType, $expectedResult, $sourceLanguage)

        # Add-Type show parse and compile errors and then finish with an terminationg error.
        # Catch non-termination information error.
        { Add-Type -MemberDefinition $sourceCode -Name $sourceType -Namespace $sourceNS -Language $sourceLanguage -ErrorAction Stop } | ShouldBeErrorId "SOURCE_CODE_ERROR,Microsoft.PowerShell.Commands.AddTypeCommand"
        # Catch final terminationg error.
        { Add-Type -MemberDefinition $sourceCode -Name $sourceType -Namespace $sourceNS -Language $sourceLanguage -ErrorAction SilentlyContinue } | ShouldBeErrorId "COMPILER_ERRORS,Microsoft.PowerShell.Commands.AddTypeCommand"

        $returnedTypes = Add-Type -MemberDefinition $sourceCode -Name $sourceType -UsingNamespace $sourceUsingNS -Namespace $sourceNS -Language $sourceLanguage -PassThru
        ([type]$sourceRunType)::TestString() | Should BeExactly $expectedResult

        # Return the same assembly if source code has not been changed.
        $returnedTypes2 = Add-Type -MemberDefinition $sourceCode -Name $sourceType -UsingNamespace $sourceUsingNS -Namespace $sourceNS -Language $sourceLanguage -PassThru
        $returnedTypes[0].Assembly.FullName | Should BeExactly $returnedTypes2[0].Assembly.FullName

        # With default namespace.
        Add-Type -MemberDefinition $sourceCode -Name $sourceType -UsingNamespace $sourceUsingNS -Language $sourceLanguage
        ([type]$sourceDefaultNSRunType)::TestString() | Should BeExactly $expectedResult
    }

    It "Can compile without loading" {

        ## The assembly files cannot be removed once they are loaded, unless the current PowerShell session exits.
        ## If we use $TestDrive here, then Pester will try to remove them afterward and result in errors.
        $TempPath = [System.IO.Path]::GetTempFileName()
        if (Test-Path $TempPath) { Remove-Item -Path $TempPath -Force -Recurse }
        New-Item -Path $TempPath -ItemType Directory -Force > $null

        { [Test.AddType.BasicTest1]::Add1(1, 2) } | Should -Throw -ErrorId "TypeNotFound"
        { [Test.AddType.BasicTest2]::Add2(3, 4) } | Should -Throw -ErrorId "TypeNotFound"

        $code = @"
using System.Management.Automation;
[System.Management.Automation.Cmdlet("Get", "CompileThing$guid", ConfirmImpact = System.Management.Automation.ConfirmImpact.High, SupportsPaging = true)]
public class AttributeTest$guid : PSCmdlet
{
    protected override void EndProcessing()

    {
        WriteObject("$guid");
    }
}
"@

        $cmdlet = "Get-CompileThing$guid"

        Add-Type -TypeDefinition $code -CompileOnly -OutputAssembly $outFile -PassThru | Should BeNullOrEmpty
        { Invoke-Expression -Command $cmdlet } | Should Throw

        $testModule = Import-Module -Name $outFile -PassThru
        Invoke-Expression -Command $cmdlet | Should BeExactly $guid

        Remove-Module $testModule -Force
    }

    It "Can report C# parse and compile errors" {
        # Add-Type show parse and compile errors and then finish with an terminationg error.
        # We test only for '-MemberDefinition' because '-Path' uses the same code path.
        # In the tests the error is that 'using System.Text;' is missing.
        #
        # Catch non-termination information error.
        { Add-Type -MemberDefinition "public static string TestString() { return UTF8Encoding.UTF8.ToString();}" -Name "TestType1" -Namespace "TestNS" -ErrorAction Stop } | ShouldBeErrorId "SOURCE_CODE_ERROR,Microsoft.PowerShell.Commands.AddTypeCommand"
        # Catch final terminationg error.
        { Add-Type -MemberDefinition "public static string TestString() { return UTF8Encoding.UTF8.ToString();}" -Name "TestType1" -Namespace "TestNS" -ErrorAction SilentlyContinue } | ShouldBeErrorId "COMPILER_ERRORS,Microsoft.PowerShell.Commands.AddTypeCommand"

        # Catch non-termination information error for ExtendedOptions.
        { Add-Type -ExtendedOptions "/platform:anycpuERROR" -Language CSharp -MemberDefinition "public static string TestString() { return ""}" -Name "TestType1" -Namespace "TestNS" -ErrorAction Stop } | ShouldBeErrorId "SOURCE_CODE_ERROR,Microsoft.PowerShell.Commands.AddTypeCommand"
        { Add-Type -ExtendedOptions "/platform:anycpuERROR" -Language VisualBasic -MemberDefinition "Public Shared Function TestString() As String `n Return `"`" `n End Function" -Name "TestType1" -Namespace "TestNS" -ErrorAction Stop } | ShouldBeErrorId "SOURCE_CODE_ERROR,Microsoft.PowerShell.Commands.AddTypeCommand"
    }

        { [Test.AddType.BasicTest1]::Add1(1, 2) } | Should -Not -Throw
        { [Test.AddType.BasicTest2]::Add2(3, 4) } | Should -Not -Throw
    }
}
