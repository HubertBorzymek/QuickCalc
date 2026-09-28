$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\QuickCalc.App\QuickCalc.App.csproj'
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $artifacts 'release'))
$packageName = 'QuickCalc-portable-win-x64'
$packageDirectory = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $packageName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot ($packageName + '.zip')))
$templateDirectory = Join-Path $root 'packaging\portable'

if (-not $releaseRoot.StartsWith($artifacts + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Katalog wydania musi znajdować się wewnątrz katalogu artifacts.'
}
if (-not $packageDirectory.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Nieprawidłowy katalog pakietu portable.'
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false `
    -o $packageDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Publikacja QuickCalc nie powiodła się (kod $LASTEXITCODE)."
}

Copy-Item -LiteralPath (Join-Path $templateDirectory 'Start-QuickCalc.cmd') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $templateDirectory 'Install-QuickCalc-Autostart.cmd') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $templateDirectory 'Remove-QuickCalc-Autostart.cmd') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $templateDirectory 'README.txt') -Destination $packageDirectory

$publishedExe = Join-Path $packageDirectory 'QuickCalc.App.exe'
$selfTest = Start-Process -FilePath $publishedExe -ArgumentList '--self-test' `
    -WorkingDirectory $packageDirectory -WindowStyle Hidden -Wait -PassThru
if ($selfTest.ExitCode -ne 0) {
    throw "Test gotowej aplikacji nie powiódł się (kod $($selfTest.ExitCode))."
}

Compress-Archive -LiteralPath $packageDirectory -DestinationPath $zipPath -CompressionLevel Optimal

$folderSize = (Get-ChildItem -LiteralPath $packageDirectory -File -Recurse |
    Measure-Object -Property Length -Sum).Sum
$zipSize = (Get-Item -LiteralPath $zipPath).Length
Write-Host "Gotowy folder: $packageDirectory"
Write-Host "Gotowy ZIP:    $zipPath"
Write-Host ('Rozmiar folderu: {0:N1} MB; ZIP: {1:N1} MB' -f ($folderSize / 1MB), ($zipSize / 1MB))
