@echo off
rem ============================================================
rem  SimRegion 一键工具 EXE build script
rem  Input : SimRegionTool.cs + adb\adb.exe + adb\AdbWinApi.dll
rem          + adb\AdbWinUsbApi.dll + SimRegionSwitch-debug.apk
rem  Output: SimRegion一键工具.exe (32-bit .NET Framework 4.x GUI)
rem  Dependency: .NET Framework 4.x csc.exe (bundled with Windows)
rem ============================================================
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found: %CSC%
    echo         Please install .NET Framework 4.x and retry.
    pause
    exit /b 1
)
if not exist "SimRegionTool.cs" (
    echo [ERROR] missing source file: SimRegionTool.cs
    pause
    exit /b 1
)
if not exist "adb\adb.exe" (
    echo [ERROR] missing embedded resource: adb\adb.exe
    echo         Download platform-tools and extract to .\adb
    pause
    exit /b 1
)
if not exist "SimRegionSwitch-debug.apk" (
    echo [ERROR] missing embedded resource: SimRegionSwitch-debug.apk
    pause
    exit /b 1
)

"%CSC%" /nologo /optimize+ /target:winexe /codepage:65001 ^
    /out:"SimRegion一键工具.exe" ^
    /resource:"adb\adb.exe",adb.exe ^
    /resource:"adb\AdbWinApi.dll",AdbWinApi.dll ^
    /resource:"adb\AdbWinUsbApi.dll",AdbWinUsbApi.dll ^
    /resource:"SimRegionSwitch-debug.apk",SimRegionSwitch-debug.apk ^
    "SimRegionTool.cs"

if errorlevel 1 (
    echo [ERROR] compile failed, see messages above.
    pause
    exit /b 1
)
echo [OK] generated SimRegion一键工具.exe
pause
