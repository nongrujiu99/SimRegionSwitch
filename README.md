# SIM Region Switch (Android)

一个面向 Android 14–17 的免 Root SIM 国家码覆盖工具原型。

## 第一版功能

- 读取活动 SIM / 双卡
- 预设 US / JP / SG / HK / GB / CA
- 支持自定义 2 位 ISO 国家码
- Shizuku 授权
- 一键应用运营商国家码覆盖
- 一键恢复 Android 原始运营商配置
- 操作后重新读取 `TelephonyManager.getSimCountryIso()` 做结果校验

## 为什么使用 Instrumentation

Android 16/17 对 `CarrierConfigManager.overrideConfig()` 加强了限制：直接由 shell UID 调用可能被拒绝。
本项目让 Shizuku 只负责启动本 APK 自己的 instrumentation；instrumentation 使用
`UiAutomation.adoptShellPermissionIdentity()` 临时获得 `MODIFY_PHONE_STATE`，然后调用
CarrierConfig override。这样 Binder 调用来自应用 UID，而不是 shell UID。

## PC 一键工具（免 Shizuku）

> 原理：电脑 adb 本身就是 shell 身份，可直接启动本 APK 的
> `CarrierOverrideInstrumentation` 完成覆盖，无需安装 Shizuku、无需配对。

1. 手机开启「开发者选项 → USB 调试」，用数据线连接电脑，首次连接在手机弹窗点「允许」。
2. 双击 `一键工具.bat`：
   - 若提示未检测到 adb，选菜单 **8** 自动下载安装（或手动装 platform-tools）。
   - 菜单 **1** 确认设备在线 → 菜单 **2** 安装 APK。
3. 日常使用：菜单 **4** 选地区一键应用，菜单 **6** 验证，菜单 **5** 恢复。
4. 手机重启后重插电脑，再跑一次菜单 **4** 即可。

### PC 一键工具 EXE 的构建

`SimRegionTool.cs` 是 `SimRegion一键工具.exe` 的源码（行为基线以当前 EXE 为准），
仓库提供 `build_exe.bat` 一键重建：

1. 准备构建输入：`adb\adb.exe`、`adb\AdbWinApi.dll`、`adb\AdbWinUsbApi.dll`
   （platform-tools，可用「一键工具.bat」菜单 **8** 自动下载）和 `SimRegionSwitch-debug.apk`。
2. 双击 `build_exe.bat`，仅依赖 Windows 自带的 .NET Framework 4.x 编译器（csc.exe），无需安装 VS。
3. 产物 `SimRegion一键工具.exe` 已内嵌 adb 与 APK，可单独分发；修改源码后重跑该脚本即可保持代码与 EXE 同步。

## 首次使用（手机端 + Shizuku 方式）

1. 手机安装并启动 Shizuku。
2. 开启开发者选项和无线调试，在 Shizuku 内完成一次配对并启动服务。
3. 安装本 App。
4. 授予“读取 SIM”权限。
5. 授予 Shizuku 权限。
6. 选择 SIM 和目标地区，点击“一键应用”。

以后 Shizuku 仍在运行时，通常只需要打开 App → 选地区 → 一键应用。

## 构建

要求：

- Android Studio / JDK 17+
- Android SDK Platform 36
- Android SDK Build Tools 35+
- Gradle 8.11.1
- Android Gradle Plugin 8.10.1

Android Studio 打开项目后 Sync，然后执行：

```bash
./gradlew :app:assembleDebug
```

产物：

```text
app/build/outputs/apk/debug/app-debug.apk
```

> 当前压缩包不内置二进制 `gradle-wrapper.jar`；第一次运行 `gradlew` / `gradlew.bat` 时会从 Gradle 官方 GitHub 自动下载该 jar。Android Studio 也可以直接导入此工程。


## 不想配置本机环境：GitHub 自动编译

工程已包含 `.github/workflows/build-apk.yml`。把整个目录上传到 GitHub 后：

1. 打开仓库的 **Actions**。
2. 运行 **Build Android APK**。
3. 构建完成后下载 `SimRegionSwitch-debug-apk` artifact。
4. 其中就是可安装的 `app-debug.apk`。

## 兼容性说明

- `KEY_SIM_COUNTRY_ISO_OVERRIDE_STRING` 在公开 Android SDK 中从 API 34 起可见。
- Android 16/17 的厂商 ROM 可能对测试 API、instrumentation 或 CarrierConfig 进一步限制。
- 这是非 Root 路线，无法保证所有厂商、所有安全补丁版本都可用。
- Android 16/17 上覆盖可能不是永久持久化；重启、SIM 刷新、系统更新后可能需要再次应用。

## 依赖

- Shizuku API 13.1.5 (MIT)
- AndroidHiddenApiBypass 6.1 (Apache-2.0)

## 安全边界

工具只写 Android CarrierConfig 的 SIM country override，不修改实体 SIM，不写系统分区，不开启 Root。
恢复按钮会对选中 subId 调用 `overrideConfig(subId, null)`，让系统回到生产配置。
