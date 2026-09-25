$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\QuickCalc.App\QuickCalc.App.csproj'
$output = Join-Path $root 'artifacts\publish'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output
if ($LASTEXITCODE -ne 0) { throw "Publikacja QuickCalc nie powiodła się (kod $LASTEXITCODE)." }

$automationRoot = Join-Path $env:SystemRoot 'Microsoft.NET\assembly\GAC_MSIL'
$automationFiles = @(
    (Join-Path $automationRoot 'UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll'),
    (Join-Path $automationRoot 'UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll')
)
foreach ($automationFile in $automationFiles) {
    if (-not (Test-Path -LiteralPath $automationFile)) { throw "Brak wymaganej biblioteki: $automationFile" }
    Copy-Item -LiteralPath $automationFile -Destination $output -Force
}
$publishedExe = Join-Path $output 'QuickCalc.App.exe'
$selfTest = Start-Process -FilePath $publishedExe -ArgumentList '--self-test' -WorkingDirectory $output -WindowStyle Hidden -Wait -PassThru
if ($selfTest.ExitCode -ne 0) { throw "Test opublikowanej aplikacji nie powiódł się (kod $($selfTest.ExitCode))." }
Write-Host "Gotowe: $output"
