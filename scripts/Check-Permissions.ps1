<#
.SYNOPSIS
  Release check from the spec (section 10): confirm a built APK requests no unnecessary permissions.

.EXAMPLE
  .\scripts\Check-Permissions.ps1 -Apk .\src\PopBlocks\bin\Release\net10.0-android\publish\com.popblocks.game-Signed.apk
#>
param(
    [Parameter(Mandatory = $true)][string]$Apk
)

if (-not (Test-Path $Apk)) { throw "APK not found: $Apk" }

# Locate aapt2 from the Android SDK build-tools.
$roots = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, "$env:LOCALAPPDATA\Android\Sdk", "${env:ProgramFiles(x86)}\Android\android-sdk") |
    Where-Object { $_ -and (Test-Path $_) }
$aapt2 = $roots | ForEach-Object { Get-ChildItem -Path "$_\build-tools" -Filter aapt2.exe -Recurse -ErrorAction SilentlyContinue } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $aapt2) { throw "aapt2.exe not found. Install Android SDK build-tools or set ANDROID_HOME." }

Write-Host "Using $($aapt2.FullName)`n"
$output = & $aapt2.FullName dump permissions $Apk
$output | ForEach-Object { Write-Host $_ }

$requested = $output | Where-Object { $_ -match "uses-permission" }
Write-Host ""
if ($requested | Where-Object { $_ -match "INTERNET" }) {
    Write-Host "FAIL: the package requests android.permission.INTERNET." -ForegroundColor Red
    exit 1
}
if ($requested) {
    Write-Host "Note: the package requests permissions listed above. Review each one." -ForegroundColor Yellow
} else {
    Write-Host "PASS: no permissions requested." -ForegroundColor Green
}

Write-Host "`nNow review dependencies (should show only Microsoft.* / Microsoft.Maui.* packages):"
Write-Host "  dotnet list src\PopBlocks\PopBlocks.csproj package --include-transitive"
