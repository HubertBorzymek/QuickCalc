@echo off
rem Aktualizacja nadpisuje ten plik, dlatego wywolanie i wyjscie musza byc w jednej linii.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-QuickCalc.ps1" & exit /b
