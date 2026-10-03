@echo off
chcp 936 >nul
setlocal enabledelayedexpansion
title SIM Region Switch - PC 一键工具

rem ========== 配置区 ==========
rem adb 已加入 PATH 则无需修改；否则请填 adb 完整路径，例如：
rem set "ADB=C:\platform-tools\adb.exe"
set "ADB=adb"
where adb >nul 2>nul
if errorlevel 1 (
    if exist "%~dp0adb\adb.exe" set "ADB=%~dp0adb\adb.exe"
)

set "PKG=com.example.simregionswitch"
set "INSTR=com.example.simregionswitch/.CarrierOverrideInstrumentation"
set "APK=%~dp0SimRegionSwitch-debug.apk"
set "TMPFILE=%TEMP%\srs_siminfo.txt"

rem ---------- adb 可用性检测 ----------
%ADB% version >nul 2>nul
if errorlevel 1 (
    echo [警告] 未检测到 adb 命令
    echo        方案1: 在菜单选 8 自动下载安装 platform-tools
    echo        方案2: 手动下载 https://developer.android.com/tools/releases/platform-tools
    echo               解压后修改本文件顶部: set "ADB=adb完整路径"
    echo.
    pause
)

:menu
cls
echo.
echo  ==============================================
echo     SIM Region Switch 电脑一键工具 (免 Shizuku)
echo  ==============================================
echo     前提: 手机开启"USB 调试"并连接电脑
echo     首次连接请在手机弹窗点击"允许 USB 调试"
echo  ----------------------------------------------
echo     1. 检测设备连接
echo     2. 安装/更新本 APK
echo     3. 查看 SIM 列表
echo     4. 一键应用覆盖 (选地区)
echo     5. 恢复原始配置
echo     6. 查看当前 SIM 国家码
echo     7. 授予读取SIM权限 (可选, 手机端查看用)
echo     8. 安装 adb (自动下载 platform-tools)
echo     9. ★ 一键全流程: 检测+安装+应用+验证
echo     0. 退出
echo  ----------------------------------------------
set /p choice="请选择: "

if "%choice%"=="1" goto check
if "%choice%"=="2" goto install
if "%choice%"=="3" goto simlist
if "%choice%"=="4" goto apply
if "%choice%"=="5" goto restore
if "%choice%"=="6" goto verify
if "%choice%"=="7" goto grant
if "%choice%"=="8" goto setupadb
if "%choice%"=="9" goto allinone
if "%choice%"=="0" exit /b
echo 无效选择
pause
goto menu

:check
%ADB% version >nul 2>nul
if errorlevel 1 (
    echo [错误] 未找到 adb, 请先运行菜单 8 安装
    pause
    goto menu
)
%ADB% devices
echo.
echo 判断方法:
echo   - 列表为空      = 线材或驱动问题, 换数据线 / 装 Android 驱动
echo   - unauthorized  = 手机弹窗没点允许, 解锁手机点"允许 USB 调试"
echo   - offline       = 换 USB 口或重插
echo   - device        = 连接正常, 可以继续
pause
goto menu

:install
if not exist "%APK%" (
    echo [错误] 找不到 APK: %APK%
    pause
    goto menu
)
%ADB% install -r "%APK%"
echo.
echo 安装流程结束。返回菜单继续。
pause
goto menu

:simlist
echo === 运营商 / 卡信息 ===
%ADB% shell dumpsys isub
echo.
echo === SIM 信息表 (每行开头的 _id 即 subId) ===
%ADB% shell content query --uri content://telephony/siminfo
pause
goto menu

:apply
echo 正在读取 SIM 列表...
%ADB% shell dumpsys isub > "%TMPFILE%" 2>nul
set "SUBID="
for /f "tokens=2" %%a in ('findstr /i /c:"subId:" "%TMPFILE%"') do if not defined SUBID set "SUBID=%%a"
if not defined SUBID (
    for /f "tokens=2 delims==" %%a in ('findstr /i /c:"subId=" "%TMPFILE%"') do if not defined SUBID set "SUBID=%%a"
)
if not defined SUBID (
    echo --- 尝试备用方式读取 ---
    %ADB% shell content query --uri content://telephony/siminfo 2>&1
)
if not defined SUBID (
    echo [提示] 未能自动识别 subId, 请用菜单3查看后手动输入
    set /p SUBID="subId: "
) else (
    echo 检测到 subId = %SUBID%
    set /p CONFIRM="直接使用请回车; 要修改请直接输入新 subId: "
    if not "!CONFIRM!"=="" set "SUBID=!CONFIRM!"
)
echo.
echo 选择目标地区:
echo   [1] US 美国    [2] JP 日本    [3] SG 新加坡
echo   [4] HK 香港    [5] GB 英国    [6] CA 加拿大
echo   [7] 自定义 (输入任意 2 位字母国家码)
set /p CN="地区编号: "
set "COUNTRY="
if "%CN%"=="1" set "COUNTRY=us"
if "%CN%"=="2" set "COUNTRY=jp"
if "%CN%"=="3" set "COUNTRY=sg"
if "%CN%"=="4" set "COUNTRY=hk"
if "%CN%"=="5" set "COUNTRY=gb"
if "%CN%"=="6" set "COUNTRY=ca"
if "%CN%"=="7" (
    set /p "COUNTRY=国家码(2位字母): "
)
if not defined COUNTRY (
    echo [错误] 未选择有效地区
    pause
    goto menu
)
echo.
echo 正在执行: 将 subId=%SUBID% 覆盖为 %COUNTRY% ...
%ADB% shell am instrument -w -r -e action apply -e subId %SUBID% -e country %COUNTRY% %INSTR%
echo.
echo 执行完毕。看到 result=applied 即成功; 可回菜单6验证。
pause
goto menu

:restore
echo 正在读取 SIM 列表...
%ADB% shell dumpsys isub > "%TMPFILE%" 2>nul
set "SUBID="
for /f "tokens=2" %%a in ('findstr /i /c:"subId:" "%TMPFILE%"') do if not defined SUBID set "SUBID=%%a"
if not defined SUBID (
    for /f "tokens=2 delims==" %%a in ('findstr /i /c:"subId=" "%TMPFILE%"') do if not defined SUBID set "SUBID=%%a"
)
if not defined SUBID (
    echo --- 尝试备用方式读取 ---
    %ADB% shell content query --uri content://telephony/siminfo 2>&1
)
if not defined SUBID (
    set /p SUBID="subId: "
) else (
    echo 检测到 subId = %SUBID%
    set /p CONFIRM="直接恢复请回车; 要修改请直接输入新 subId: "
    if not "!CONFIRM!"=="" set "SUBID=!CONFIRM!"
)
echo 正在恢复原始运营商配置...
%ADB% shell am instrument -w -r -e action restore -e subId %SUBID% %INSTR%
echo.
echo 执行完毕。可回菜单6验证。
pause
goto menu

:verify
echo 当前系统读取的 SIM 国家码 (含 country 的行):
%ADB% shell "dumpsys telephony.registry | grep -i country"
pause
goto menu

:grant
%ADB% shell pm grant %PKG% android.permission.READ_PHONE_STATE
echo 已授予读取SIM权限 (手机端 App 可显示状态; 一键应用请直接用本工具)
pause
goto menu

:setupadb
echo 正在下载 platform-tools (约 10MB)...
curl -L -o "%TEMP%\platform-tools.zip" https://dl.google.com/android/repository/platform-tools-latest-windows.zip
if errorlevel 1 (
    echo [错误] 下载失败, 请手动下载: https://developer.android.com/tools/releases/platform-tools
    pause
    goto menu
)
if not exist "%~dp0adb" mkdir "%~dp0adb"
tar -xf "%TEMP%\platform-tools.zip" -C "%~dp0adb" --strip-components 1
if errorlevel 1 (
    echo [错误] 解压失败
    pause
    goto menu
)
set "ADB=%~dp0adb\adb.exe"
echo 完成! adb 已安装到: %~dp0adb
echo 建议首次运行: 菜单 1 检测设备 - 菜单 2 安装 APK - 菜单 4 一键应用
pause
goto menu

:allinone
echo.
echo === [1/4] 检测设备 ===
%ADB% version >nul 2>nul
if errorlevel 1 (
    echo [错误] 未找到 adb, 请先运行菜单 8 安装
    pause
    goto menu
)
%ADB% devices
echo.
%ADB% get-state >nul 2>nul
if errorlevel 1 (
    echo [错误] 未检测到设备
    echo 排查: 1 手机USB调试已开启? 2 数据线支持数据传输? 3 手机弹窗点过允许?
    echo adb devices输出即原因: 空列表=线材驱动问题  unauthorized=没点允许  offline=换口重插
    pause
    goto menu
)
echo.
echo === [2/4] 安装 APK ===
if not exist "%APK%" (
    echo [错误] 找不到 APK: %APK%
    pause
    goto menu
)
%ADB% install -r "%APK%"
if errorlevel 1 (
    echo [警告] 安装未能确认成功, 继续尝试应用...
)
echo.
echo === [3/4] 读取 SIM 并选择地区 ===
%ADB% shell dumpsys isub > "%TMPFILE%" 2>nul
set "SUBID="
for /f "tokens=2" %%a in ('findstr /i /c:"subId:" "%TMPFILE%"') do if not defined SUBID set "SUBID=%%a"
if not defined SUBID (
    for /f "tokens=2 delims==" %%a in ('findstr /i /c:"subId=" "%TMPFILE%"') do if not defined SUBID set "SUBID=%%a"
)
if not defined SUBID (
    echo --- 尝试备用方式读取 ---
    %ADB% shell content query --uri content://telephony/siminfo 2>&1
)
if not defined SUBID (
    echo [提示] 未能自动识别 subId, 请用菜单3查看后手动输入
    set /p SUBID="subId: "
) else (
    echo 检测到 subId = %SUBID%
    set /p CONFIRM="直接使用请回车; 要修改请直接输入新 subId: "
    if not "!CONFIRM!"=="" set "SUBID=!CONFIRM!"
)
echo.
echo 选择目标地区:
echo   [1] US 美国    [2] JP 日本    [3] SG 新加坡
echo   [4] HK 香港    [5] GB 英国    [6] CA 加拿大
echo   [7] 自定义 (输入任意 2 位字母国家码)
set /p CN="地区编号: "
set "COUNTRY="
if "%CN%"=="1" set "COUNTRY=us"
if "%CN%"=="2" set "COUNTRY=jp"
if "%CN%"=="3" set "COUNTRY=sg"
if "%CN%"=="4" set "COUNTRY=hk"
if "%CN%"=="5" set "COUNTRY=gb"
if "%CN%"=="6" set "COUNTRY=ca"
if "%CN%"=="7" (
    set /p "COUNTRY=国家码(2位字母): "
)
if not defined COUNTRY (
    echo [错误] 未选择有效地区
    pause
    goto menu
)
echo.
echo === [4/4] 应用覆盖 %COUNTRY% 并验证 ===
%ADB% shell am instrument -w -r -e action apply -e subId %SUBID% -e country %COUNTRY% %INSTR%
echo.
echo 验证 - 当前系统读取的 SIM 国家码:
%ADB% shell "dumpsys telephony.registry | grep -i country"
echo.
echo 全流程结束。看到 result=applied 且 country 为 %COUNTRY% 即成功。
pause
goto menu
