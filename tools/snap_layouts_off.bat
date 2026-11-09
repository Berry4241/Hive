@echo off
rem Turns OFF Windows' Snap layouts on maximize-button hover, so Windows' own split menu doesn't pop up over Hive. Undo: snap_layouts_on.bat
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0snap_layouts.ps1" -State off
pause
