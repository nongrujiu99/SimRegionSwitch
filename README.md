# SIM Region Switch

免 Root 的 SIM 国家码覆盖工具，包含两部分：

- **Android App**（`app/`）：手机端，Shizuku 授权方式，可手动选择 SIM 与国家码。
- **PC 一键工具**（`SimRegionTool.cs` → `SimRegion一键工具.exe`）：免 Shizuku，通过电脑 adb 直接驱动 App 内的 instrumentation 完成覆盖 / 恢复，适合日常快速操作。

## 快速开始（PC 一键工具，推荐）

从 [Releases](https://github.com/nongrujiu99/SimRegionSwitch/releases) 下载 `SimRegionTool.exe`（单文件，已内嵌 adb 与 APK，无需安装）：

1. 手机开启「开发者选项 → USB 调试」，用数据线连接电脑，首次连接在手机弹窗点「允许」。
2. 双击 `SimRegionTool.exe`：
   - 顶部实时显示设备连接状态与当前 SIM 国家码；
   - 选择目标地区（美国 / 日本 / 新加坡 / 香港 / 英国 / 加拿大，或自定义 2 位 ISO 国家码）；
   - 点「一键应用」：自动检查 / 安装 APK → 读取 subId → 执行国家码覆盖，完成后回读校验；
   - 点「恢复原始配置」：还原运营商原始配置。
3. 手机重启后重插电脑，再应用一次即可。

> 备用方案：`一键工具.bat` 是命令行菜单版（功能相同，含 adb 自动下载、SIM 列表查看、全流程一键执行等）；若提示未检测到 adb，选菜单 **8** 自动下载安装 platform-tools。

### PC 一键工具 EXE 的构建

`SimRegionTool.cs` 是 `SimRegion一键工具.exe` 的源码（行为基线以当前 EXE 为准），仓库提供 `build_exe.bat` 一键重建：

1. 准备构建输入：`adb\adb.exe`、`adb\AdbWinApi.dll`、`adb\AdbWinUsbApi.dll`
   （platform-tools，可用「一键工具.bat」菜单 **8** 自动下载）和 `SimRegionSwitch-debug.apk`。
2. 双击 `build_exe.bat`，仅依赖 Windows 自带的 .NET Framework 4.x 编译器（csc.exe），无需安装 VS。
3. 产物 `SimRegion一键工具.exe` 已内嵌 adb 与 APK，可单独分发；修改源码后重跑该脚本即可保持代码与 EXE 同步。

## Android App 功能

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

PC 一键工具则更进一步：电脑 adb 本身就是 shell 身份，可直接启动本 APK 的
`CarrierOverrideInstrumentation` 完成覆盖，无需安装 Shizuku、无需配对。

## 手机端使用（Shizuku 方式）

1. 手机安装并启动 Shizuku。
2. 开启开发者选项和无线调试，在 Shizuku 内完成一次配对并启动服务。
3. 安装本 App。
4. 授予“读取 SIM”权限。
5. 授予 Shizuku 权限。
6. 选择 SIM 和目标地区，点击“一键应用”。

以后 Shizuku 仍在运行时，通常只需要打开 App → 选地区 → 一键应用。

## Android App 构建

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

> 发布 / 分发给 EXE 或「一键工具.bat」使用时，请将该 APK 重命名为 `SimRegionSwitch-debug.apk`
> （与 `build_exe.bat` 内嵌、`一键工具.bat` 引用的文件名保持一致）。
> 仓库未提交二进制 `gradle-wrapper.jar`；第一次运行 `gradlew` / `gradlew.bat` 时会自动下载该 jar。Android Studio 也可以直接导入此工程。

## GitHub Actions 自动构建

仓库已包含 `.github/workflows/build-apk.yml`。推送代码到 GitHub 后：

1. 打开仓库的 **Actions** 页面。
2. 运行 **Build Android APK**（push 自动触发，也可手动 Run workflow）。
3. 构建完成后下载 `SimRegionSwitch-debug-apk` artifact。
4. 其中就是可安装的 `SimRegionSwitch-debug.apk`。

## 兼容性说明

- 支持 Android 11 及以上（minSdk 30），重点适配 Android 14–17。
- `KEY_SIM_COUNTRY_ISO_OVERRIDE_STRING` 在公开 Android SDK 中从 API 34 起可见。
- Android 16/17 的厂商 ROM 可能对测试 API、instrumentation 或 CarrierConfig 进一步限制。
- 这是非 Root 路线，无法保证所有厂商、所有安全补丁版本都可用。
- Android 16/17 上覆盖可能不是永久持久化；重启、SIM 刷新、系统更新后可能需要再次应用。

## 依赖

- Shizuku API 13.1.5 (MIT)
- AndroidHiddenApiBypass 6.1 (Apache-2.0)
- androidx.annotation 1.9.1 (Apache-2.0)

## 安全边界

工具只写 Android CarrierConfig 的 SIM country override，不修改实体 SIM，不写系统分区，不开启 Root。
恢复按钮会对选中 subId 调用 `overrideConfig(subId, null)`，让系统回到生产配置。
