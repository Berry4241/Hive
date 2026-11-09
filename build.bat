@echo off
rem Builds Hive.exe with the C# compiler that ships with Windows (.NET Framework 4.8). Nothing is installed.
rem If Hive is running, close it first (its x), or the old Hive.exe stays locked.
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
rem Windows UI Automation (part of Windows), used to find a session in Claude's sidebar when Hive brings it back
set WPF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF
cd /d "%~dp0"
if not exist "%CSC%" (echo The C# compiler of .NET Framework 4.8 was not found at %CSC% & exit /b 1)
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:"Hive.exe" /win32manifest:src\app.manifest /win32icon:src\hive.ico /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:Microsoft.VisualBasic.dll /r:"%WPF%\UIAutomationClient.dll" /r:"%WPF%\UIAutomationTypes.dll" /r:"%WPF%\WindowsBase.dll" src\Hive.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo BUILD OK: Hive.exe
