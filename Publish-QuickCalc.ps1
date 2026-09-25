$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\QuickCalc.App\QuickCalc.App.csproj'
$output = Join-Path $root 'artifacts\publish'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output
Write-Host "Gotowe: $output"
