# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Class WebListener
{
    [int]$HttpPort
    [int]$HttpsPort
    [int]$Tls11Port
    [int]$TlsPort
    [System.Diagnostics.Process]$Process

    WebListener () { }

    [String] GetStatus()
    {
        if ($This.Process.HasExited) {
            return "Exited"
        }
        else {
            return "Running"
        }
    }
}

[WebListener]$WebListener

function New-ClientCertificate
{
    param([string]$CertificatePath, [string]$Password)

    if ($Password)
    {
        $Passphrase = ConvertTo-SecureString -Force -AsPlainText $Password
    }

    $distinguishedName = @{
        CN = 'adatum.com'
        C = 'US'
        S = 'Washington'
        L = 'Redmond'
        O = 'A. Datum Corporation'
        OU = 'R&D'
        E = 'randd@adatum.com'
    }

    $certificateParameters = @{
        OutCertPath = $CertificatePath
        StartDate = [datetime]::Now.Subtract([timespan]::FromDays(30))
        Duration = [timespan]::FromDays(365)
        Passphrase = $Passphrase
        CertificateFormat = 'Pfx'
        KeyLength = 4096
        ForCertificateAuthority = $true
        Force = $true
    } + $distinguishedName

    SelfSignedCertificate\New-SelfSignedCertificate @certificateParameters
}

function New-ServerCertificate
{
    param([string]$CertificatePath, [string]$Password)

    if ($Password)
    {
        $Passphrase = ConvertTo-SecureString -Force -AsPlainText $Password
    }

    $distinguishedName = @{
        CN = 'localhost'
    }

    $certificateParameters = @{
        OutCertPath = $CertificatePath
        StartDate = [datetime]::Now.Subtract([timespan]::FromDays(30))
        Duration = [timespan]::FromDays(1000)
        Passphrase = $Passphrase
        KeyUsage = 'DigitalSignature','KeyEncipherment'
        EnhancedKeyUsage = 'ServerAuthentication','ClientAuthentication'
        CertificateFormat = 'Pfx'
        KeyLength = 2048
        Force = $true
    } + $distinguishedName

    SelfSignedCertificate\New-SelfSignedCertificate @certificateParameters
}

function Get-WebListener
{
    [CmdletBinding(ConfirmImpact = 'Low')]
    [OutputType([WebListener])]
    param()

    process
    {
        return [WebListener]$Script:WebListener
    }
}

function Start-WebListener
{
    [CmdletBinding(ConfirmImpact = 'Low')]
    [OutputType([WebListener])]
    param
    (
        [ValidateRange(1,65535)]
        [int]$HttpPort = 8083,

        [ValidateRange(1,65535)]
        [int]$HttpsPort = 8084,

        [ValidateRange(1,65535)]
        [int]$Tls11Port = 8085,

        [ValidateRange(1,65535)]
        [int]$TlsPort = 8086
    )

    process
    {
        $runningListener = Get-WebListener
        if ($null -ne $runningListener -and $runningListener.GetStatus() -eq 'Running')
        {
            return $runningListener
        }

        $initTimeoutSeconds  = 15
        $appExe              = 'WebListener'
        $serverPfx           = 'ServerCert.pfx'
        $serverPfxPassword   = New-RandomHexString
        $clientPfx           = 'ClientCert.pfx'
        $sleepMilliseconds   = 100

        $serverPfxPath = Join-Path ([System.IO.Path]::GetTempPath()) $serverPfx
        $Script:ClientPfxPath = Join-Path ([System.IO.Path]::GetTempPath()) $clientPfx
        $Script:ClientPfxPassword = New-RandomHexString
        New-ServerCertificate -CertificatePath $serverPfxPath -Password $serverPfxPassword
        New-ClientCertificate -CertificatePath $Script:ClientPfxPath -Password $Script:ClientPfxPassword

        $oldASPEnvPreference = $env:ASPNETCORE_ENVIRONMENT

        try {
            $env:ASPNETCORE_ENVIRONMENT = 'Development'

            $params = @{
                FilePath = $appExe
                PassThru = $true
                UseNewEnvironment = $IsLinux # start a non-keyboard input blocking process on linux.
            }

            $webListenerProcess = Start-Process @params -ArgumentList @(
                $serverPfxPath
                $serverPfxPassword
                $HttpPort
                $HttpsPort
                $Tls11Port
                $TlsPort
            )
        } catch {
            # rethrow any exception
            throw $_
        } finally {
            $env:ASPNETCORE_ENVIRONMENT = $oldASPEnvPreference
        }

        $Script:WebListener = [WebListener]@{
            HttpPort  = $HttpPort
            HttpsPort = $HttpsPort
            Tls11Port = $Tls11Port
            TlsPort   = $TlsPort
            Process   = $webListenerProcess
        }

        # Count iterations of $sleepMilliseconds instead of using system time to work around possible CI VM sleep/delays
        $sleepCountRemaining = $initTimeoutSeconds * 1000 / $sleepMilliseconds
        do
        {
            Start-Sleep -Milliseconds $sleepMilliseconds
            $isRunning = (Get-WebListener).Process -ne $null
            $sleepCountRemaining--
        }
        while (-not $isRunning -and $sleepCountRemaining -gt 0)

        if (-not $isRunning)
        {
            throw 'WebListener did not start before the timeout was reached.'
        }
        return $Script:WebListener
    }
}

function Stop-WebListener
{
    [CmdletBinding(ConfirmImpact = 'Low')]
    [OutputType([Void])]
    param()

    process
    {
        $Script:WebListener.Process | Stop-Process
        $Script:WebListener = $null
    }
}

function Get-WebListenerClientCertificate {
    [CmdletBinding(ConfirmImpact = 'Low')]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param()
    process {
        [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($Script:ClientPfxPath, $Script:ClientPfxPassword)
    }
}

function Get-WebListenerUrl {
    [CmdletBinding()]
    [OutputType([Uri])]
    param (
        [switch]$Https,

        [ValidateSet('Default', 'Tls12', 'Tls11', 'Tls')]
        [string]$SslProtocol = 'Default',

        [ValidateSet(
            'Auth',
            'Cert',
            'Compression',
            'Delay',
            'Delete',
            'Encoding',
            'Get',
            'Home',
            'Link',
            'Multipart',
            'Patch',
            'Post',
            'Put',
            'Redirect',
            'Response',
            'ResponseHeaders',
            'Resume',
            'Retry',
            '/'
        )]
        [String]$Test,

        [String]$TestValue,

        [System.Collections.IDictionary]$Query
    )
    process {
        $runningListener = Get-WebListener
        if ($null -eq $runningListener -or $runningListener.GetStatus() -ne 'Running')
        {
            return $null
        }
        $Uri = [System.UriBuilder]::new()
        # Use 127.0.0.1 and not localhost due to https://github.com/dotnet/corefx/issues/24104
        $Uri.Host = '127.0.0.1'
        $Uri.Port = $runningListener.HttpPort
        $Uri.Scheme = 'Http'

        if ($Https.IsPresent)
        {
            switch ($SslProtocol)
            {
                'Tls11' { $Uri.Port = $runningListener.Tls11Port }
                'Tls'   { $Uri.Port = $runningListener.TlsPort }
                # The base HTTPs port is configured for Tls12 only
                default { $Uri.Port = $runningListener.HttpsPort }
            }
            $Uri.Scheme = 'Https'
        }

        if ($TestValue)
        {
            $Uri.Path = '{0}/{1}' -f $Test, $TestValue
        }
        else
        {
            $Uri.Path = $Test
        }
        $StringBuilder = [System.Text.StringBuilder]::new()
        foreach ($key in $Query.Keys)
        {
            $null = $StringBuilder.Append([System.Net.WebUtility]::UrlEncode($key))
            $null = $StringBuilder.Append('=')
            $null = $StringBuilder.Append([System.Net.WebUtility]::UrlEncode($Query[$key].ToString()))
            $null = $StringBuilder.Append('&')
        }
        $Uri.Query = $StringBuilder.ToString()

        return [Uri]$Uri.ToString()
    }
}
