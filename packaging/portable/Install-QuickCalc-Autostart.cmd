@echo off
setlocal

set "QUICKCALC_DIR=%~dp0"
set "QUICKCALC_LAUNCHER=%~dp0Start-QuickCalc.cmd"
set "QUICKCALC_EXE=%~dp0QuickCalc.App.exe"

if not exist "%QUICKCALC_LAUNCHER%" (
    echo Nie znaleziono pliku Start-QuickCalc.cmd.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$startup=[Environment]::GetFolderPath('Startup'); $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $startup 'QuickCalc.lnk')); $shortcut.TargetPath=$env:QUICKCALC_LAUNCHER; $shortcut.WorkingDirectory=$env:QUICKCALC_DIR; if (Test-Path -LiteralPath $env:QUICKCALC_EXE) { $shortcut.IconLocation=$env:QUICKCALC_EXE }; $shortcut.Save()"

if errorlevel 1 (
    echo Nie udalo sie dodac QuickCalc do autostartu.
    pause
    exit /b 1
)

echo QuickCalc zostal dodany do autostartu biezacego uzytkownika.
echo Nie przenos folderu bez ponownego uruchomienia tego instalatora.
pause
exit /b 0
