# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe "Handle ByRef-like types gracefully" -Tags "CI" {

    BeforeAll {
        $code = @'
using System;
namespace DotNetInterop
{
    public class Test
    {
        public static Span<int> Space
        {
            get { return default(Span<int>); }
            set { DoNothing(value); }
        }

        public Span<int> Room
        {
            get { return default(Span<int>); }
            set { DoNothing(value); }
        }

        private static void DoNothing(Span<int> param)
        {
        }

        public string PrintMySpan(string str, Span<int> mySpan = default)
        {
            return str;
        }
    
        public Span<int> GetSpan(int[] array)
        {
            return array.AsSpan();
        }
    }

    public ref struct MyByRefLikeType
    {
        public static int Index;
    }
}
'@
        if (-not ("DotNetInterop.Test" -as [type]))
        {
            Add-Type -TypeDefinition $code -IgnoreWarnings
        }

        $testObj = [DotNetInterop.Test]::new()
    }

    It "New-Object should fail gracefully when used for a ByRef-like type" {
        { New-Object -TypeName 'System.Span[string]' } | Should -Throw -ErrorId "CannotInstantiateBoxedByRefLikeType,Microsoft.PowerShell.Commands.NewObjectCommand"
        { New-Object -TypeName 'DotNetInterop.MyByRefLikeType' } | Should -Throw -ErrorId "CannotInstantiateBoxedByRefLikeType,Microsoft.PowerShell.Commands.NewObjectCommand"
    }

    It "The 'new' method call should fail gracefully when used on a ByRef-like type" {
        { [System.Span[string]]::new() } | Should -Throw -ErrorId "CannotInstantiateBoxedByRefLikeType"
        { [DotNetInterop.MyByRefLikeType]::new() } | Should -Throw -ErrorId "CannotInstantiateBoxedByRefLikeType"
    }

    It "Cast to a ByRef-like type should fail gracefully" {
        { [System.Span[int]] ([int[]]1,2,3) } | Should -Throw -ErrorId "InvalidCastToByRefLikeType"
        { [DotNetInterop.MyByRefLikeType] "text" } | Should -Throw -ErrorId "InvalidCastToByRefLikeType"
    }

    It "LanguagePrimitives.ConvertTo should fail gracefully for a ByRef-like type '<Name>'" -TestCases @(
        @{ Name = "Span";            Type = [System.Span[int]] }
        @{ Name = "MyByRefLikeType"; Type = [DotNetInterop.MyByRefLikeType] }
    ) {
        param($Type)
        $expectedError = $null
        try {
            [System.Management.Automation.LanguagePrimitives]::ConvertTo(([int[]]1,2,3), $Type)
        } catch {
            $expectedError = $_
        }

        $expectedError | Should -Not -BeNullOrEmpty
        $expectedError.Exception.InnerException.ErrorRecord.FullyQualifiedErrorId | Should -BeExactly "InvalidCastToByRefLikeType"
    }

    It "Getting value of a ByRef-like type instance property should not throw and should return null, even in strict mode - <Mechanism>" -TestCases @(
        @{ Mechanism = "Compiler/Binder"; Script = { [System.Text.Encoding]::ASCII.Preamble } }
        @{ Mechanism = "Dotnet-Adapter";  Script = { [System.Text.Encoding]::ASCII.PSObject.Properties["Preamble"].Value } }
    ) {
        param($Script)

        try {
            Set-StrictMode -Version latest
            & $Script | Should -Be $null
        } finally {
            Set-StrictMode -Off
        }
    }

    It "Setting value of a ByRef-like type instance property should fail gracefully - <Mechanism>" -TestCases @(
        @{ Mechanism = "Compiler/Binder"; Script = { $testObj.Room = [int[]](1,2,3) } }
        @{ Mechanism = "Dotnet-Adapter";  Script = { $testObj.PSObject.Properties["Room"].Value = [int[]](1,2,3) } }
    ) {
        param($Script)
        $Script | Should -Throw -ErrorId "CannotAccessByRefLikePropertyOrField"
    }

    It "<Action> value of a ByRef-like type static property should fail gracefully" -TestCases @(
        @{ Action = "Getting"; Script = { [DotNetInterop.Test]::Space } }
        @{ Action = "Setting"; Script = { [DotNetInterop.Test]::Space = "blah" } }
    ) {
        param($Script)
        $Script | Should -Throw -ErrorId "CannotAccessByRefLikePropertyOrField"
    }

    It "Invoke a method with optional ByRef-like parameter could work" {
        $testObj.PrintMySpan("Hello") | Should -BeExactly "Hello"
    }

    It "Invoke a method with ByRef-like parameter should fail gracefully - <Mechanism>" -TestCases @(
        @{ Mechanism = "Compiler/Binder"; Script = { $testObj.PrintMySpan("Hello", 1) } }
        @{ Mechanism = "Dotnet-Adapter";  Script = { $testObj.psobject.Methods["PrintMySpan"].Invoke("Hello", 1) } }
    ) {
        param($Script)
        $Script | Should -Throw -ErrorId "MethodArgumentConversionInvalidCastArgument"
    }

    It "Invoke a method with ByRef-like return type should fail gracefully - Compiler/Binder" {
        { $testObj.GetSpan([int[]]@(1,2,3)) } | Should -Throw -ErrorId "CannotCallMethodWithByRefLikeReturnType"
    }

    It "Invoke a method with ByRef-like return type should fail gracefully - Dotnet-Adapter" {
        $expectedError = $null
        try {
            $testObj.psobject.Methods["GetSpan"].Invoke([int[]]@(1,2,3))
        } catch {
            $expectedError = $_
        }
        $expectedError | Should -Not -BeNullOrEmpty
        $expectedError.Exception.InnerException.ErrorRecord.FullyQualifiedErrorId | Should -BeExactly "CannotCallMethodWithByRefLikeReturnType"
    }

    It "Access static property of a ByRef-like type" {
        [DotNetInterop.MyByRefLikeType]::Index = 10
        [DotNetInterop.MyByRefLikeType]::Index | Should -Be 10
    }
}
