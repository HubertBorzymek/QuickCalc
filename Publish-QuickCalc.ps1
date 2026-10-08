$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\QuickCalc.App\QuickCalc.App.csproj'
$output = Join-Path $root 'artifacts\publish'
foreach ($automationName in @('UIAutomationClient.dll', 'UIAutomationTypes.dll', 'UIAutomationProvider.dll', 'UIAutomationClientSideProviders.dll')) {
    $staleAutomationFile = Join-Path $output $automationName
    if (Test-Path -LiteralPath $staleAutomationFile) { Remove-Item -LiteralPath $staleAutomationFile -Force }
}
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output
if ($LASTEXITCODE -ne 0) { throw "Publikacja QuickCalc nie powiodła się (kod $LASTEXITCODE)." }

$publishedExe = Join-Path $output 'QuickCalc.App.exe'
$selfTest = Start-Process -FilePath $publishedExe -ArgumentList '--self-test' -WorkingDirectory $output -WindowStyle Hidden -Wait -PassThru
if ($selfTest.ExitCode -ne 0) { throw "Test opublikowanej aplikacji nie powiódł się (kod $($selfTest.ExitCode))." }
Write-Host "Gotowe: $output"
