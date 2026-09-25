$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$published = Join-Path $root 'artifacts\publish\QuickCalc.App.exe'
if (Test-Path -LiteralPath $published) {
    Start-Process -FilePath $published
    exit
}
dotnet run --project (Join-Path $root 'src\QuickCalc.App\QuickCalc.App.csproj') --configuration Release
