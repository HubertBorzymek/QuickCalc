@echo off
setlocal

set "QUICKCALC_ROOT=%~dp0"
set "QUICKCALC_EXE=%QUICKCALC_ROOT%artifacts\publish\QuickCalc.App.exe"

if not exist "%QUICKCALC_EXE%" (
    echo Nie znaleziono opublikowanej aplikacji QuickCalc:
    echo %QUICKCALC_EXE%
    echo.
    echo Najpierw uruchom Publish-QuickCalc.ps1.
    pause
    exit /b 1
)

start "" /D "%QUICKCALC_ROOT%artifacts\publish" "%QUICKCALC_EXE%"
exit /b 0
