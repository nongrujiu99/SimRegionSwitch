@echo off
set JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot
set ANDROID_HOME=C:\Android\sdk
set PATH=%JAVA_HOME%\bin;%PATH%
cd /d C:\Users\11596\Desktop\Codex_test\SimRegion
call gradlew.bat :app:assembleDebug --no-daemon
