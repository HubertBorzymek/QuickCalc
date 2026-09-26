@echo off
setlocal

set "QUICKCALC_ROOT=%~dp0"
set "QUICKCALC_LAUNCHER=%~dp0Start-QuickCalc.cmd"
set "QUICKCALC_EXE=%~dp0artifacts\publish\QuickCalc.App.exe"

if not exist "%QUICKCALC_LAUNCHER%" (
    echo Nie znaleziono pliku Start-QuickCalc.cmd.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$startup=[Environment]::GetFolderPath('Startup'); $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $startup 'QuickCalc.lnk')); $shortcut.TargetPath=$env:QUICKCALC_LAUNCHER; $shortcut.WorkingDirectory=$env:QUICKCALC_ROOT; if (Test-Path -LiteralPath $env:QUICKCALC_EXE) { $shortcut.IconLocation=$env:QUICKCALC_EXE }; $shortcut.Save()"

if errorlevel 1 (
    echo Nie udalo sie dodac QuickCalc do autostartu.
    pause
    exit /b 1
)

echo QuickCalc zostal dodany do autostartu biezacego uzytkownika.
echo Skrot uruchomi aplikacje przy nastepnym logowaniu do Windows.
pause
exit /b 0
