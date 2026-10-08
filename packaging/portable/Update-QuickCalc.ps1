param([switch]$NoLaunch, [switch]$NoPause)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repository = 'HubertBorzymek/QuickCalc'
$assetName = 'QuickCalc-portable-win-x64.zip'
$installDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $installDirectory 'QuickCalc.App.exe'
# Pliki uzytkownika, ktorych aktualizacja nie nadpisuje.
$preserved = @('hotkeys.json', 'quickcalc-error.log')
$workDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('QuickCalc-update-' + [guid]::NewGuid())

function Pause-AndExit([int]$code) {
    Write-Host ''
    if (-not $NoPause) { Read-Host 'Nacisnij Enter, aby zamknac' | Out-Null }
    exit $code
}

try {
    Write-Host 'Sprawdzam najnowsza wersje QuickCalc na GitHub...'
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repository/releases/latest" `
        -Headers @{ 'User-Agent' = 'QuickCalc-Updater' }
    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "Wydanie $($release.tag_name) nie zawiera pliku $assetName." }

    $versionFile = Join-Path $installDirectory 'VERSION.txt'
    $currentVersion = if (Test-Path -LiteralPath $versionFile) { (Get-Content -LiteralPath $versionFile -TotalCount 1).Trim() } else { 'nieznana' }
    Write-Host "Zainstalowana wersja: $currentVersion"
    Write-Host "Najnowsza wersja:     $($release.tag_name)"
    if ($currentVersion -eq $release.tag_name) {
        Write-Host 'Masz juz najnowsza wersje.'
        Pause-AndExit 0
    }

    New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
    $zipPath = Join-Path $workDirectory $assetName
    Write-Host ('Pobieram {0} ({1:N1} MB)...' -f $assetName, ($asset.size / 1MB))
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -UseBasicParsing `
        -Headers @{ 'User-Agent' = 'QuickCalc-Updater' }

    $extractDirectory = Join-Path $workDirectory 'extracted'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDirectory -Force
    $newExe = Get-ChildItem -LiteralPath $extractDirectory -Filter 'QuickCalc.App.exe' -Recurse | Select-Object -First 1
    if (-not $newExe) { throw 'Pobrane archiwum nie zawiera QuickCalc.App.exe.' }
    $sourceDirectory = $newExe.DirectoryName

    $running = Get-Process -Name 'QuickCalc.App' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and ($_.Path -ieq $exePath) }
    if ($running) {
        Write-Host 'Zamykam uruchomiony QuickCalc...'
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }

    Write-Host 'Podmieniam pliki...'
    Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($sourceDirectory.Length).TrimStart('\')
        $target = Join-Path $installDirectory $relative
        if (($preserved -contains $relative) -and (Test-Path -LiteralPath $target)) { return }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
    Set-Content -LiteralPath $versionFile -Value $release.tag_name -Encoding ASCII

    # Starsze wersje tworzyly skrot autostartu do Start-QuickCalc.cmd; przepinamy go na EXE.
    $startupLink = Join-Path ([Environment]::GetFolderPath('Startup')) 'QuickCalc.lnk'
    if (Test-Path -LiteralPath $startupLink) {
        $wsh = New-Object -ComObject WScript.Shell
        $shortcut = $wsh.CreateShortcut($startupLink)
        $linkDirectory = if ($shortcut.TargetPath) { Split-Path -Parent $shortcut.TargetPath } else { '' }
        if ($linkDirectory -and ($linkDirectory.TrimEnd('\') -ieq $installDirectory.TrimEnd('\'))) {
            $shortcut.TargetPath = $exePath
            $shortcut.WorkingDirectory = $installDirectory
            $shortcut.IconLocation = $exePath
            $shortcut.Save()
            Write-Host 'Odswiezono skrot autostartu.'
        }
    }

    Write-Host "Zaktualizowano do $($release.tag_name). Uruchamiam QuickCalc..."
    if (-not $NoLaunch) { Start-Process -FilePath $exePath -WorkingDirectory $installDirectory }
    Pause-AndExit 0
}
catch {
    Write-Host ''
    Write-Host ('Aktualizacja nie powiodla sie: ' + $_.Exception.Message) -ForegroundColor Red
    Pause-AndExit 1
}
finally {
    if (Test-Path -LiteralPath $workDirectory) { Remove-Item -LiteralPath $workDirectory -Recurse -Force -ErrorAction SilentlyContinue }
}
