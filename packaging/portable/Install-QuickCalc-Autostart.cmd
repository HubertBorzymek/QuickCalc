@echo off
setlocal

set "QUICKCALC_DIR=%~dp0"
set "QUICKCALC_EXE=%~dp0QuickCalc.App.exe"

if not exist "%QUICKCALC_EXE%" (
    echo Nie znaleziono pliku QuickCalc.App.exe.
    pause
    exit /b 1
)

rem Skrot wskazuje bezposrednio na EXE, wiec przy logowaniu nie miga okno konsoli.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$startup=[Environment]::GetFolderPath('Startup'); $shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $startup 'QuickCalc.lnk')); $shortcut.TargetPath=$env:QUICKCALC_EXE; $shortcut.WorkingDirectory=$env:QUICKCALC_DIR; $shortcut.IconLocation=$env:QUICKCALC_EXE; $shortcut.Save()"

if errorlevel 1 (
    echo Nie udalo sie dodac QuickCalc do autostartu.
    pause
    exit /b 1
)

echo QuickCalc zostal dodany do autostartu biezacego uzytkownika.
echo Nie przenos folderu bez ponownego uruchomienia tego instalatora.
pause
exit /b 0
