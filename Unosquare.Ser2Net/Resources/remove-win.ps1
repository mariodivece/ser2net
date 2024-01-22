# powershell -executionpolicy bypass -file .\script.ps1 arg0 arg1

$serviceKey         = $(if ($args.Length -gt 0) { $args[0] } else { throw "Missing argument 0 corresponding to service key";          exit 1; })
$servicePath        = $(if ($args.Length -gt 1) { $args[1] } else { throw "Missing argument 1 corresponding to service path";         exit 1; })
$serviceDisplay     = $(if ($args.Length -gt 2) { $args[2] } else { throw "Missing argument 2 corresponding to service display name"; exit 1; })
$serviceDescription = $(if ($args.Length -gt 3) { $args[3] } else { throw "Missing argument 3 corresponding to service description";  exit 1; })

# Setup some state variables
$performInstall = $false
$err = @()

Write-Host $("Service Key: '" + $serviceKey + "'")
$currentService = Get-Service -Name $serviceKey -ErrorAction SilentlyContinue -ErrorVariable err

if ($currentService -eq $null)
{
    Write-Host $("Service '" + $serviceKey + "' does not exist. Will install.")
    $performInstall = $true
}
else
{
    # 1. Stop the existing service if running
    if ($currentService.Status -eq 'Running')
    {
        Stop-Service -Name $currentService.Name -Force -ErrorAction SilentlyContinue -ErrorVariable err
        if ($err.Count -ne 0)
        {
            throw $("Could not stop service. " + $err)
            exit 2
        }
    }

    # 2. Uninstall Service
    $serviceFilter = $("Name='" + $currentService.Name + "'")
    $serviceObject = Get-CimInstance -ClassName Win32_Service -Filter $serviceFilter
    Remove-CimInstance -InputObject $serviceObject -ErrorAction SilentlyContinue -ErrorVariable err
    if ($err.Count -ne 0)
    {
        throw $("Could not remove service. " + $err)
        exit 3
    }

    $performInstall = $true
}

if ($performInstall -ne $true)
{
    throw $("Could not perform all necessary steps.")
    exit 4
}

# 3. Install the service
New-Service `
 -Name $serviceKey `
 -BinaryPathName $servicePath `
 -DisplayName $serviceDisplay `
 -Description $serviceDescription `
 -StartupType Automatic `
 -ErrorAction SilentlyContinue `
 -ErrorVariable err

if ($err.Count -ne 0)
{
    throw $("Could not install service. " + $err)
    exit 5
}

Start-Service -Name $serviceKey -ErrorAction SilentlyContinue -ErrorVariable err
if ($err.Count -ne 0)
{
    throw $("Could not start service. " + $err)
    exit 6
}

exit 0
