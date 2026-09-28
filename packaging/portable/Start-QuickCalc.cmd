@echo off
setlocal

set "QUICKCALC_DIR=%~dp0"
set "QUICKCALC_EXE=%QUICKCALC_DIR%QuickCalc.App.exe"

if not exist "%QUICKCALC_EXE%" (
    echo Nie znaleziono pliku QuickCalc.App.exe.
    echo Skopiuj lub rozpakuj caly folder QuickCalc, a nie tylko ten skrypt.
    pause
    exit /b 1
)

start "" /D "%QUICKCALC_DIR%" "%QUICKCALC_EXE%"
exit /b 0
