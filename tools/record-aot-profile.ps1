<#
.SYNOPSIS
    Records src/Clients/MAUI/tour.aprof on an Android device without driving the app by hand.

.DESCRIPTION
    Builds and installs the profiling variant (-p:K7AotProfiling=true: JIT on, AOT profiler
    embedded, AotProfileTour enabled), starts the app with the Mono AOT profiler listening on
    port 9999, waits until the in-app tour has visited every page (K7-Startup marker
    "AotProfileTour-done"), saves the profile with aprofutil and clears the profiler property.

    The tour covers pages and D-pad focus only. custom.aprof (manual session with playback and
    menus) stays next to it: the Release build passes every *.aprof and the AOT compiler takes
    the union.

    The tour starts once a profile is signed in: if the profile picker shows, pick a profile
    with the remote, then leave the device alone until the script ends.

    Without the four AndroidSigning* parameters, Install uses the debug keystore. Android then
    refuses the update over a release-signed com.k7.maui and the install wipes app data.
    Pass the same keystore, store password, alias and key password as the usual release APK.

    Compatible with Windows PowerShell 5.1.

.EXAMPLE
    .\tools\record-aot-profile.ps1
    .\tools\record-aot-profile.ps1 -SkipBuild -Device 192.168.1.50:5555
    .\tools\record-aot-profile.ps1 -AndroidSigningKeyStore C:\keys\k7.keystore -AndroidSigningStorePass $store -AndroidSigningKeyAlias k7 -AndroidSigningKeyPass $key
#>
param(
    [switch]$SkipBuild,
    [string]$Device = '',
    [int]$TimeoutMinutes = 20,
    [string]$Activity = 'com.k7.maui/crc648f5a7c81d1a4b3dd.MainActivity',
    [string]$Output = '',
    [string]$AndroidSigningKeyStore = '',
    [string]$AndroidSigningStorePass = '',
    [string]$AndroidSigningKeyAlias = '',
    [string]$AndroidSigningKeyPass = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Clients\MAUI\K7.Clients.MAUI.csproj'
if ([string]::IsNullOrEmpty($Output)) {
    $Output = Join-Path $repoRoot 'src\Clients\MAUI\tour.aprof'
}

$aprofutil = Join-Path $env:USERPROFILE '.nuget\packages\mono.aotprofiler.android\10.0.0-preview1\tools\aprofutil.exe'

function Invoke-Adb {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$AdbArgs)
    if ([string]::IsNullOrEmpty($Device)) {
        & adb @AdbArgs
    } else {
        & adb -s $Device @AdbArgs
    }
}

if (-not $SkipBuild) {
    $signing = @($AndroidSigningKeyStore, $AndroidSigningStorePass, $AndroidSigningKeyAlias, $AndroidSigningKeyPass)
    $signingSet = @($signing | Where-Object { -not [string]::IsNullOrEmpty($_) }).Count
    if ($signingSet -gt 0 -and $signingSet -lt 4) {
        throw "Pass AndroidSigningKeyStore, AndroidSigningStorePass, AndroidSigningKeyAlias and AndroidSigningKeyPass together."
    }

    $buildArgs = @(
        $project,
        '-f', 'net10.0-android',
        '-c', 'Release',
        '-p:K7AotProfiling=true',
        '-t:Install'
    )
    if ($signingSet -eq 4) {
        if (-not (Test-Path -LiteralPath $AndroidSigningKeyStore)) {
            throw "Keystore not found: $AndroidSigningKeyStore"
        }
        $buildArgs += @(
            '-p:AndroidKeyStore=true',
            "-p:AndroidSigningKeyStore=$AndroidSigningKeyStore",
            "-p:AndroidSigningStorePass=$AndroidSigningStorePass",
            "-p:AndroidSigningKeyAlias=$AndroidSigningKeyAlias",
            "-p:AndroidSigningKeyPass=$AndroidSigningKeyPass"
        )
    }

    Write-Host 'Building and installing the profiling variant...'
    & dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build or install failed (exit code $LASTEXITCODE)."
    }
}

if (-not (Test-Path $aprofutil)) {
    throw "aprofutil not found at $aprofutil. The profiling build restores Mono.AotProfiler.Android; run it once without -SkipBuild."
}

try {
    Write-Host 'Waking the device and starting the app with the AOT profiler...'
    Invoke-Adb shell input keyevent KEYCODE_WAKEUP | Out-Null
    Invoke-Adb shell setprop debug.mono.profile 'aot:port=9999' | Out-Null
    Invoke-Adb forward tcp:9999 tcp:9999 | Out-Null
    Invoke-Adb logcat -c | Out-Null
    Invoke-Adb shell am start -S -n $Activity | Out-Null

    Write-Host 'Waiting for the in-app tour (pick a profile with the remote if the picker shows)...'
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $done = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        $log = Invoke-Adb logcat -d -s K7-Startup:I AndroidRuntime:E
        if ($log -match 'FATAL EXCEPTION') {
            throw "The app crashed before the tour finished. adb logcat -s AndroidRuntime:E shows the exception."
        }
        if ($log -match 'AotProfileTour-done') {
            $done = $true
            break
        }
        if ($log -match 'AotProfileTour-start') {
            Write-Host '  tour running...'
        }
    }

    if (-not $done) {
        throw "The tour did not finish within $TimeoutMinutes minutes. Check that a profile is signed in."
    }

    Write-Host "Saving the profile to $Output ..."
    & $aprofutil -s -v -p 9999 -o $Output
    if ($LASTEXITCODE -ne 0) {
        throw "aprofutil failed (exit code $LASTEXITCODE)."
    }

    Write-Host 'Profile saved. Build the regular Release APK to use it, and commit tour.aprof.'
}
finally {
    Invoke-Adb shell setprop debug.mono.profile "''" | Out-Null
    Invoke-Adb forward --remove tcp:9999 | Out-Null
}
