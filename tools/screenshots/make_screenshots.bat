@echo off
rem Draws the screenshots in docs\screenshots with demo sessions (no real windows or data are used) and Hive's own drawing code.
rem Uses the C# compiler that ships with Windows (.NET Framework 4.8). Nothing is installed.
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF
cd /d "%~dp0..\.."
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /main:Hive.DemoProgram /out:"tools\screenshots\HiveDemo.exe" /win32manifest:src\app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:Microsoft.VisualBasic.dll /r:"%WPF%\UIAutomationClient.dll" /r:"%WPF%\UIAutomationTypes.dll" /r:"%WPF%\WindowsBase.dll" src\Hive.cs tools\screenshots\HiveDemo.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
"tools\screenshots\HiveDemo.exe" "docs\screenshots"
if errorlevel 1 (echo SCREENSHOTS FAILED & exit /b 1)
echo SCREENSHOTS OK: docs\screenshots
