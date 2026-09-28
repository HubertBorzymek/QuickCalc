@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$link=Join-Path ([Environment]::GetFolderPath('Startup')) 'QuickCalc.lnk'; if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force }"

if errorlevel 1 (
    echo Nie udalo sie usunac QuickCalc z autostartu.
    pause
    exit /b 1
)

echo QuickCalc zostal usuniety z autostartu biezacego uzytkownika.
pause
exit /b 0
