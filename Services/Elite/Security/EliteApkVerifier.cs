using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite.Security
{
    /// <summary>
    /// ✨ ميزة بحثية متقدمة: التحقق الشامل من APK المعالج
    ///
    /// تُنفّذ 4 مراحل تحليلية متسلسلة:
    ///
    /// 1. [SIGNATURE]  التحقق من التوقيع الرقمي v1/v2/v3 باستخدام apksigner verify الحقيقي
    /// 2. [DEX]        تحليل DEX bytecode بـ dexdump.exe للتحقق من حقن Smali فعلياً
    /// 3. [ASSETS]     فحص وجود وصحة الأصول المشفرة (.elc / .hpke.elc) داخل APK
    /// 4. [INTEGRITY]  التحقق من تكامل ZIP structure وعدم تلف APK
    ///
    /// الأدوات المستخدمة (من ResearchPayloadTools):
    ///   - apksigner.jar  → التوقيع الرقمي (Android SDK official)
    ///   - dexdump.exe    → تحليل DEX classes (build-tools/36.1.0)
    ///   - ZipArchive     → تحليل ZIP structure (بدون أدوات خارجية)
    ///
    /// الناتج: VerificationReport يحتوي نتائج شاملة قابلة للتصدير للبحث الأكاديمي
    /// </summary>
    public class EliteApkVerifier
    {
        // ── Tool Path Resolution (نفس نمط APKCryptoService) ────────────────────

        private static string ResolveResearchRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // المسار القياسي (bin/Debug/net10.0-windows/res/ResearchPayloadTools)
                Path.Combine(baseDir, "res", "ResearchPayloadTools"),
                // عند التشغيل من IDE — مستويات للأعلى ثم res
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "res", "ResearchPayloadTools")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "res", "ResearchPayloadTools")),
                // fallback: المسار الكامل
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Ekhtibar", "bin", "Debug", "net10.0-windows", "res", "ResearchPayloadTools"),
            };
            return candidates.FirstOrDefault(Directory.Exists)
                ?? throw new DirectoryNotFoundException("[EliteVerifier] ResearchPayloadTools غير موجود");
        }

        private static readonly Lazy<string> _root = new(ResolveResearchRoot);
        private static string Root         => _root.Value;
        // apksigner.jar موجود مباشرة في جذر ResearchPayloadTools
        private static string ApkSignerJar => Path.Combine(Root, "apksigner.jar");
        // dexdump موجود في build-tools/36.1.0
        private static string DexDumpExe   => Path.Combine(Root, "build-tools", "36.1.0", "dexdump.exe");
        // JRE في apktool/jre
        private static string JreExe       => Path.Combine(Root, "apktool", "jre", "bin", "java.exe");
        private string JavaExe             => File.Exists(JreExe) ? JreExe : "java";


        // ── Injected Smali Signatures (ما نبحث عنه في DEX) ─────────────────────
        // هذه الـ strings تُثبت أن Smali المحقون مُدرَج في DEX classes
        private static readonly string[] SmaliSignatures = new[]
        {
            "Lcom/elite/crypto/EliteDecryptor;",    // الفئة الرئيسية المحقونة
            "Lcom/elite/crypto/EliteHpkeDecryptor;", // HPKE decryptor (Android 36)
            "EliteDecryptor",
            "AES/CBC/PKCS5Padding",                  // خوارزمية فك التشفير في الـ Smali
            "ELBC",                                  // Elite Magic bytes داخل DEX strings
        };

        // ── Encrypted Asset Extensions ───────────────────────────────────────────
        private static readonly string[] EncryptedExtensions = { ".elc", ".hpke.elc" };

        // ── Logging ──────────────────────────────────────────────────────────────
        private readonly Action<string> _log;
        private readonly Action<double> _progress;

        public EliteApkVerifier(Action<string> log, Action<double> progress = null)
        {
            _log      = log ?? (_ => { });
            _progress = progress ?? (_ => { });
        }

        // ══════════════════════════════════════════════════════════════════════════
        // نقطة الدخول الرئيسية — يُشغَّل بعد ProcessApkAsync مباشرةً
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// يُنفّذ التحقق الشامل من APK المعالج في 4 مراحل متسلسلة.
        /// </summary>
        /// <param name="apkPath">مسار APK المُعالَج (الناتج من APKCryptoService)</param>
        /// <param name="expectInjection">هل يُتوقَّع وجود Smali محقون</param>
        /// <param name="expectEncryptedAssets">هل يُتوقَّع وجود assets مشفرة</param>
        /// <param name="ct">رمز الإلغاء</param>
        /// <returns>تقرير تحقق شامل VerificationReport</returns>
        public async Task<VerificationReport> VerifyApkAsync(
            string   apkPath,
            bool     expectInjection      = true,
            bool     expectEncryptedAssets = true,
            CancellationToken ct          = default)
        {
            var report = new VerificationReport
            {
                ApkPath       = apkPath,
                ApkName       = Path.GetFileName(apkPath),
                VerifiedAt    = DateTime.Now,
                ApkSizeBytes  = new FileInfo(apkPath).Length,
            };

            // SHA-256 للـ APK
            report.ApkSha256 = await ComputeSha256Async(apkPath, ct);
            _log($"🔍 [Verifier] بدء التحقق من: {report.ApkName}");
            _log($"    الحجم: {FormatSize(report.ApkSizeBytes)} | SHA-256: {report.ApkSha256[..16]}...");
            _progress(5);

            // ─ المرحلة 1: التوقيع الرقمي ─────────────────────────────────────
            _log("─────────────────────────────────────────────");
            _log("📋 [المرحلة 1/4] التحقق من التوقيع الرقمي...");
            report.SignatureResult = await VerifySignatureAsync(apkPath, ct);
            _progress(30);

            // ─ المرحلة 2: تحليل DEX ─────────────────────────────────────────
            _log("─────────────────────────────────────────────");
            _log("🔬 [المرحلة 2/4] تحليل DEX bytecode...");
            report.DexResult = await AnalyzeDexAsync(apkPath, expectInjection, ct);
            _progress(60);

            // ─ المرحلة 3: فحص الأصول المشفرة ───────────────────────────────
            _log("─────────────────────────────────────────────");
            _log("📦 [المرحلة 3/4] فحص الأصول المشفرة...");
            report.AssetsResult = AnalyzeEncryptedAssets(apkPath, expectEncryptedAssets);
            _progress(80);

            // ─ المرحلة 4: تكامل ZIP ─────────────────────────────────────────
            _log("─────────────────────────────────────────────");
            _log("🗜 [المرحلة 4/4] فحص تكامل ZIP structure...");
            report.ZipResult = AnalyzeZipIntegrity(apkPath);
            _progress(95);

            // ─ الحكم النهائي ─────────────────────────────────────────────────
            report.OverallPassed =
                report.SignatureResult.Passed &&
                report.DexResult.Passed       &&
                report.AssetsResult.Passed    &&
                report.ZipResult.Passed;

            _log("─────────────────────────────────────────────");
            _log(report.OverallPassed
                ? $"✅ [Verifier] APK سليم تماماً — اجتاز {report.PassedCount}/4 مراحل"
                : $"⚠️ [Verifier] APK يحتاج مراجعة — اجتاز {report.PassedCount}/4 مراحل");

            _progress(100);
            return report;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // المرحلة 1: التحقق من التوقيع بـ apksigner verify
        // ══════════════════════════════════════════════════════════════════════════

        private async Task<SignatureVerificationResult> VerifySignatureAsync(
            string apkPath, CancellationToken ct)
        {
            var result = new SignatureVerificationResult();

            if (!File.Exists(ApkSignerJar))
            {
                result.Error = $"apksigner.jar غير موجود في: {ApkSignerJar}";
                _log($"    ⚠ {result.Error}");
                result.Passed = false;
                return result;
            }

            try
            {
                // apksigner verify --print-certs
                string output = await RunProcessAsync(
                    JavaExe,
                    $"-jar \"{ApkSignerJar}\" verify --print-certs \"{apkPath}\"",
                    ct);

                result.RawOutput = output;

                // تحليل المخرجات
                result.IsVerified = output.Contains("Verified using v") ||
                                    output.Contains("Verified") &&
                                    !output.Contains("DOES NOT VERIFY");

                // استخراج إصدارات التوقيع (v1/v2/v3)
                if (Regex.IsMatch(output, @"Verified using v1 scheme.*true", RegexOptions.IgnoreCase))
                    result.V1Signed = true;
                if (Regex.IsMatch(output, @"Verified using v2 scheme.*true", RegexOptions.IgnoreCase))
                    result.V2Signed = true;
                if (Regex.IsMatch(output, @"Verified using v3 scheme.*true", RegexOptions.IgnoreCase))
                    result.V3Signed = true;

                // استخراج معلومات الـ certificate
                var sha256Match = Regex.Match(output, @"Certificate SHA-256 digest:\s*([0-9a-f]+)", RegexOptions.IgnoreCase);
                if (sha256Match.Success)
                    result.CertificateSha256 = sha256Match.Groups[1].Value;

                var subjectMatch = Regex.Match(output, @"Subject:\s*(.+)", RegexOptions.IgnoreCase);
                if (subjectMatch.Success)
                    result.CertificateSubject = subjectMatch.Groups[1].Value.Trim();

                result.Passed = result.IsVerified;

                if (result.Passed)
                {
                    _log($"    ✅ التوقيع صحيح — v1:{result.V1Signed} | v2:{result.V2Signed} | v3:{result.V3Signed}");
                    if (!string.IsNullOrEmpty(result.CertificateSubject))
                        _log($"    📜 Certificate: {result.CertificateSubject}");
                    if (!string.IsNullOrEmpty(result.CertificateSha256))
                        _log($"    🔑 Cert-SHA256: {result.CertificateSha256[..32]}...");
                }
                else
                {
                    _log($"    ❌ التوقيع غير صحيح أو مفقود");
                    _log($"    {output.Split('\n').FirstOrDefault(l => l.Contains("ERROR") || l.Contains("error"), "").Trim()}");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Error  = ex.Message;
                result.Passed = false;
                _log($"    ❌ خطأ في التحقق من التوقيع: {ex.Message}");
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // المرحلة 2: تحليل DEX بـ dexdump.exe
        // ══════════════════════════════════════════════════════════════════════════

        private async Task<DexAnalysisResult> AnalyzeDexAsync(
            string apkPath, bool expectInjection, CancellationToken ct)
        {
            var result = new DexAnalysisResult();

            try
            {
                // استخراج classes.dex من APK مؤقتاً
                string tempDir  = Path.Combine(Path.GetTempPath(), $"elite_verify_{Guid.NewGuid():N}");
                string dexPath  = Path.Combine(tempDir, "classes.dex");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // APK هو ZIP — نستخرج classes.dex
                    bool dexExtracted = false;
                    using (var zip = ZipFile.OpenRead(apkPath))
                    {
                        // نجمع كل ملفات DEX (classes.dex, classes2.dex, ...)
                        var dexEntries = zip.Entries
                            .Where(e => Regex.IsMatch(e.Name, @"^classes\d*\.dex$"))
                            .OrderBy(e => e.Name)
                            .ToList();

                        result.DexFilesCount = dexEntries.Count;
                        _log($"    📦 DEX files: {result.DexFilesCount} ملف ({string.Join(", ", dexEntries.Select(e => e.Name))})");

                        // نستخرج classes.dex الرئيسي
                        var mainDex = dexEntries.FirstOrDefault(e => e.Name == "classes.dex");
                        if (mainDex != null)
                        {
                            mainDex.ExtractToFile(dexPath, true);
                            result.MainDexSizeBytes = mainDex.Length;
                            dexExtracted = true;
                        }

                        // نجمع كل DEX strings (من جميع الـ DEX) — للبحث عن Smali المحقون
                        var allDexContent = new StringBuilder();
                        foreach (var dex in dexEntries)
                        {
                            string dexTemp = Path.Combine(tempDir, dex.Name);
                            dex.ExtractToFile(dexTemp, true);

                            if (File.Exists(DexDumpExe))
                            {
                                // تحليل كل DEX file بـ dexdump
                                string dexOut = await RunProcessAsync(
                                    DexDumpExe,
                                    $"-d \"{dexTemp}\"",
                                    ct);
                                allDexContent.Append(dexOut);
                            }
                            else
                            {
                                // Fallback: raw binary scan للـ strings
                                byte[] dexBytes = await File.ReadAllBytesAsync(dexTemp, ct);
                                allDexContent.Append(ExtractReadableStrings(dexBytes));
                            }
                        }

                        result.DexDumpOutput = allDexContent.ToString();
                    }

                    // البحث عن Smali Signatures المحقونة
                    result.FoundSignatures = new List<string>();
                    foreach (var sig in SmaliSignatures)
                    {
                        if (result.DexDumpOutput.Contains(sig, StringComparison.OrdinalIgnoreCase))
                        {
                            result.FoundSignatures.Add(sig);
                            _log($"    ✅ وُجد في DEX: {sig}");
                        }
                    }

                    // هل العثور على Smali مطابق للتوقع؟
                    bool injectionFound = result.FoundSignatures.Count > 0;

                    if (expectInjection)
                    {
                        result.Passed = injectionFound;
                        if (injectionFound)
                            _log($"    ✅ Smali محقون مؤكَّد ({result.FoundSignatures.Count} توقيع)");
                        else
                            _log($"    ⚠️ لم يُعثر على Smali المحقون في DEX — هل تم اختيار Inject Decryptor؟");
                    }
                    else
                    {
                        // لم يُطلَب الحقن — نتحقق فقط من سلامة DEX structure
                        result.Passed = result.DexFilesCount > 0;
                        _log($"    ℹ️ DEX سليم — {result.DexFilesCount} ملف، {FormatSize(result.MainDexSizeBytes)}");
                    }

                    // إحصائيات DEX
                    result.ClassesCount  = Regex.Matches(result.DexDumpOutput, @"Class #").Count;
                    result.MethodsCount  = Regex.Matches(result.DexDumpOutput, @"method_idx").Count;

                    if (result.ClassesCount > 0)
                        _log($"    📊 DEX إحصائيات: {result.ClassesCount:N0} فئة | {result.MethodsCount:N0} دالة | {FormatSize(result.MainDexSizeBytes)}");
                }
                finally
                {
                    // تنظيف الملفات المؤقتة
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Error  = ex.Message;
                result.Passed = false;
                _log($"    ❌ خطأ في تحليل DEX: {ex.Message}");
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // المرحلة 3: فحص الأصول (Assets) المشفرة داخل APK
        // ══════════════════════════════════════════════════════════════════════════

        private AssetsVerificationResult AnalyzeEncryptedAssets(
            string apkPath, bool expectEncrypted)
        {
            var result = new AssetsVerificationResult();

            try
            {
                using var zip = ZipFile.OpenRead(apkPath);
                var allEntries = zip.Entries.ToList();

                // جميع ملفات assets/
                var assetEntries = allEntries
                    .Where(e => e.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                result.TotalAssetsCount = assetEntries.Count;

                // الأصول المشفرة بـ AES-CBC (.elc)
                var elcEntries = assetEntries
                    .Where(e => e.Name.EndsWith(".elc", StringComparison.OrdinalIgnoreCase) &&
                                !e.Name.EndsWith(".hpke.elc", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // الأصول المشفرة بـ HPKE (.hpke.elc) — Android 36
                var hpkeEntries = assetEntries
                    .Where(e => e.Name.EndsWith(".hpke.elc", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                result.ElcAssetsCount  = elcEntries.Count;
                result.HpkeAssetsCount = hpkeEntries.Count;
                result.EncryptedAssetsCount = elcEntries.Count + hpkeEntries.Count;

                // التحقق من Magic Bytes للـ .elc files
                result.ValidElcFiles = 0;
                foreach (var entry in elcEntries.Take(10)) // نفحص أول 10 فقط للسرعة
                {
                    using var stream = entry.Open();
                    byte[] header = new byte[4];
                    int read = stream.Read(header, 0, 4);
                    if (read == 4 && IsValidElcMagic(header))
                    {
                        result.ValidElcFiles++;
                    }
                }

                // التحقق من Magic Bytes للـ .hpke.elc files
                result.ValidHpkeFiles = 0;
                foreach (var entry in hpkeEntries.Take(10))
                {
                    using var stream = entry.Open();
                    byte[] header = new byte[4];
                    int read = stream.Read(header, 0, 4);
                    if (read == 4 && IsValidHpkeMagic(header))
                    {
                        result.ValidHpkeFiles++;
                    }
                }

                // التحقق من وجود EliteDecryptor.smali كـ Asset محقون
                bool hasDecryptorClass = allEntries
                    .Any(e => e.FullName.Contains("EliteDecryptor", StringComparison.OrdinalIgnoreCase));

                // المنطق
                if (expectEncrypted)
                {
                    result.Passed = result.EncryptedAssetsCount > 0;
                    if (result.Passed)
                    {
                        _log($"    ✅ أصول مشفرة: {result.ElcAssetsCount} AES-CBC (.elc) | {result.HpkeAssetsCount} HPKE (.hpke.elc)");
                        if (result.ElcAssetsCount > 0)
                            _log($"       Magic ✅: {result.ValidElcFiles}/{Math.Min(result.ElcAssetsCount, 10)} ملف AES سليم");
                        if (result.HpkeAssetsCount > 0)
                            _log($"       Magic ✅: {result.ValidHpkeFiles}/{Math.Min(result.HpkeAssetsCount, 10)} ملف HPKE سليم");
                    }
                    else
                    {
                        _log($"    ⚠️ لا توجد أصول مشفرة من {result.TotalAssetsCount} أصل — هل تم اختيار Encrypt Assets؟");
                    }
                }
                else
                {
                    result.Passed = true;
                    _log($"    ℹ️ إجمالي الأصول: {result.TotalAssetsCount} ملف");
                }

                // حجم إجمالي للأصول
                result.TotalAssetsSizeBytes = assetEntries.Sum(e => e.Length);
                _log($"    📁 حجم assets/: {FormatSize(result.TotalAssetsSizeBytes)}");
            }
            catch (Exception ex)
            {
                result.Error  = ex.Message;
                result.Passed = false;
                _log($"    ❌ خطأ في فحص الأصول: {ex.Message}");
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // المرحلة 4: فحص تكامل ZIP Structure
        // ══════════════════════════════════════════════════════════════════════════

        private ZipIntegrityResult AnalyzeZipIntegrity(string apkPath)
        {
            var result = new ZipIntegrityResult();

            try
            {
                using var zip = ZipFile.OpenRead(apkPath);
                var entries = zip.Entries.ToList();

                result.TotalEntries = entries.Count;

                // التحقق من وجود AndroidManifest.xml
                result.HasManifest = entries.Any(e =>
                    e.FullName.Equals("AndroidManifest.xml", StringComparison.OrdinalIgnoreCase));

                // التحقق من وجود classes.dex
                result.HasDex = entries.Any(e =>
                    e.Name.Equals("classes.dex", StringComparison.OrdinalIgnoreCase));

                // التحقق من وجود resources.arsc
                result.HasResources = entries.Any(e =>
                    e.Name.Equals("resources.arsc", StringComparison.OrdinalIgnoreCase));

                // التحقق من CRC32 لعينة من الملفات (أول 20)
                result.CrcErrors = 0;
                foreach (var entry in entries.Take(20))
                {
                    try
                    {
                        using var s = entry.Open();
                        // قراءة كاملة تتحقق من CRC32 تلقائياً
                        var buf = new byte[4096];
                        while (s.Read(buf, 0, buf.Length) > 0) { }
                    }
                    catch
                    {
                        result.CrcErrors++;
                    }
                }

                // تقدير ما إذا كان APK مضغوطاً بـ ZipAlign
                // ZipAlign يضع compressed size = 0 للملفات غير المضغوطة ويُحاذيها بـ 4 bytes
                var uncompressedEntries = entries
                    .Where(e => e.CompressedLength == e.Length && e.Length > 0)
                    .ToList();
                result.ZipAligned = uncompressedEntries.All(e => e.Length % 4 == 0);

                result.Passed = result.HasManifest &&
                                result.HasDex      &&
                                result.CrcErrors == 0;

                _log($"    {(result.HasManifest ? "✅" : "❌")} AndroidManifest.xml | {(result.HasDex ? "✅" : "❌")} classes.dex | {(result.HasResources ? "✅" : "ℹ️")} resources.arsc");
                _log($"    📊 {result.TotalEntries:N0} ملف في APK | CRC errors: {result.CrcErrors} | ZipAligned: {(result.ZipAligned ? "✅" : "⚠️")}");

                if (result.CrcErrors > 0)
                    _log($"    ❌ وُجد {result.CrcErrors} خطأ CRC — APK تالف جزئياً");
            }
            catch (Exception ex)
            {
                result.Error  = ex.Message;
                result.Passed = false;
                _log($"    ❌ خطأ في فحص ZIP: {ex.Message}");
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // تصدير التقرير للبحث الأكاديمي
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// يصدّر VerificationReport كملف نصي بتنسيق أكاديمي
        /// </summary>
        public static string ExportReport(VerificationReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("  Elite APK Verification Report — جامعة حلب / Android 36");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine($"APK       : {report.ApkName}");
            sb.AppendLine($"الحجم     : {FormatSize(report.ApkSizeBytes)}");
            sb.AppendLine($"SHA-256   : {report.ApkSha256}");
            sb.AppendLine($"التحقق    : {report.VerifiedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"النتيجة   : {(report.OverallPassed ? "✅ سليم" : "⚠️ يحتاج مراجعة")} ({report.PassedCount}/4)");
            sb.AppendLine();

            sb.AppendLine("── [1] التوقيع الرقمي ─────────────────────────────────");
            var sig = report.SignatureResult;
            sb.AppendLine($"  النتيجة : {(sig.Passed ? "✅ صحيح" : "❌ خطأ")}");
            if (sig.IsVerified)
            {
                sb.AppendLine($"  v1      : {sig.V1Signed} | v2: {sig.V2Signed} | v3: {sig.V3Signed}");
                if (!string.IsNullOrEmpty(sig.CertificateSubject))
                    sb.AppendLine($"  Subject : {sig.CertificateSubject}");
                if (!string.IsNullOrEmpty(sig.CertificateSha256))
                    sb.AppendLine($"  Cert-SHA256: {sig.CertificateSha256}");
            }
            if (!string.IsNullOrEmpty(sig.Error))
                sb.AppendLine($"  الخطأ  : {sig.Error}");
            sb.AppendLine();

            sb.AppendLine("── [2] تحليل DEX ──────────────────────────────────────");
            var dex = report.DexResult;
            sb.AppendLine($"  النتيجة : {(dex.Passed ? "✅ سليم" : "⚠️ يحتاج مراجعة")}");
            sb.AppendLine($"  DEX files: {dex.DexFilesCount} | Classes: {dex.ClassesCount:N0} | Methods: {dex.MethodsCount:N0}");
            sb.AppendLine($"  classes.dex : {FormatSize(dex.MainDexSizeBytes)}");
            if (dex.FoundSignatures?.Count > 0)
            {
                sb.AppendLine($"  Smali محقون ({dex.FoundSignatures.Count} توقيع):");
                foreach (var s in dex.FoundSignatures)
                    sb.AppendLine($"    ✅ {s}");
            }
            if (!string.IsNullOrEmpty(dex.Error))
                sb.AppendLine($"  الخطأ  : {dex.Error}");
            sb.AppendLine();

            sb.AppendLine("── [3] الأصول المشفرة ─────────────────────────────────");
            var assets = report.AssetsResult;
            sb.AppendLine($"  النتيجة : {(assets.Passed ? "✅ سليم" : "⚠️ يحتاج مراجعة")}");
            sb.AppendLine($"  إجمالي assets : {assets.TotalAssetsCount} ({FormatSize(assets.TotalAssetsSizeBytes)})");
            sb.AppendLine($"  AES-CBC (.elc): {assets.ElcAssetsCount} | HPKE (.hpke.elc): {assets.HpkeAssetsCount}");
            sb.AppendLine($"  Magic ✅: {assets.ValidElcFiles} AES | {assets.ValidHpkeFiles} HPKE");
            if (!string.IsNullOrEmpty(assets.Error))
                sb.AppendLine($"  الخطأ  : {assets.Error}");
            sb.AppendLine();

            sb.AppendLine("── [4] تكامل ZIP ──────────────────────────────────────");
            var zip = report.ZipResult;
            sb.AppendLine($"  النتيجة : {(zip.Passed ? "✅ سليم" : "❌ تالف")}");
            sb.AppendLine($"  Entries : {zip.TotalEntries:N0} | Manifest: {zip.HasManifest} | DEX: {zip.HasDex}");
            sb.AppendLine($"  CRC Errors: {zip.CrcErrors} | ZipAligned: {zip.ZipAligned}");
            if (!string.IsNullOrEmpty(zip.Error))
                sb.AppendLine($"  الخطأ  : {zip.Error}");

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("  تم التوليد بواسطة Ekhtibar Research Platform");
            sb.AppendLine($"  Android 36 (SDK {36}) | build.version.security_patch: 2025-04-05");
            sb.AppendLine("═══════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        // ── Private Helpers ───────────────────────────────────────────────────────

        private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
        {
            using var sha = SHA256.Create();
            using var fs  = File.OpenRead(path);
            byte[] hash   = await sha.ComputeHashAsync(fs, ct);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task<string> RunProcessAsync(string exe, string args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName               = exe,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            var sb = new StringBuilder();
            await Task.Run(() =>
            {
                using var p = Process.Start(psi)!;
                sb.Append(p.StandardOutput.ReadToEnd());
                sb.Append(p.StandardError.ReadToEnd());
                p.WaitForExit(60_000); // حد أقصى 60 ثانية
            }, ct);

            return sb.ToString();
        }

        /// <summary>
        /// استخراج نصوص قابلة للقراءة من DEX binary — Fallback إذا لم يكن dexdump موجوداً
        /// يستخرج strings ≥4 أحرف ASCII متتالية (نفس مبدأ Unix `strings` command)
        /// </summary>
        private static string ExtractReadableStrings(byte[] data, int minLength = 4)
        {
            var sb = new StringBuilder();
            int count = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] is >= 0x20 and <= 0x7E)
                {
                    count++;
                }
                else
                {
                    if (count >= minLength)
                    {
                        // استخراج النص
                        sb.Append(Encoding.ASCII.GetString(data, i - count, count));
                        sb.Append('\n');
                    }
                    count = 0;
                }
            }
            return sb.ToString();
        }

        // Magic Bytes validation
        private static bool IsValidElcMagic(byte[] header)
            => (header[0] == 0x45 && header[1] == 0x4C && header[2] == 0x42 && header[3] == 0x43) || // ELBC (AES-CBC)
               (header[0] == 0x45 && header[1] == 0x4C && header[2] == 0x47 && header[3] == 0x43);   // ELGC (AES-GCM)

        private static bool IsValidHpkeMagic(byte[] header)
            => header[0] == 0x45 && header[1] == 0x4C && header[2] == 0x48 && header[3] == 0x50;     // ELHP

        private static string FormatSize(long bytes) => bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024         => $"{bytes / 1_024.0:F1} KB",
            _                => $"{bytes} B"
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Models — نماذج البيانات
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>تقرير التحقق الشامل</summary>
    public class VerificationReport
    {
        public string   ApkPath          { get; set; }
        public string   ApkName          { get; set; }
        public long     ApkSizeBytes     { get; set; }
        public string   ApkSha256        { get; set; }
        public DateTime VerifiedAt       { get; set; }
        public bool     OverallPassed    { get; set; }

        public SignatureVerificationResult SignatureResult { get; set; } = new();
        public DexAnalysisResult           DexResult      { get; set; } = new();
        public AssetsVerificationResult    AssetsResult   { get; set; } = new();
        public ZipIntegrityResult          ZipResult      { get; set; } = new();

        public int PassedCount =>
            (SignatureResult.Passed ? 1 : 0) +
            (DexResult.Passed       ? 1 : 0) +
            (AssetsResult.Passed    ? 1 : 0) +
            (ZipResult.Passed       ? 1 : 0);
    }

    public class SignatureVerificationResult
    {
        public bool   Passed             { get; set; }
        public bool   IsVerified         { get; set; }
        public bool   V1Signed           { get; set; }
        public bool   V2Signed           { get; set; }
        public bool   V3Signed           { get; set; }
        public string CertificateSha256  { get; set; }
        public string CertificateSubject { get; set; }
        public string RawOutput          { get; set; }
        public string Error              { get; set; }
    }

    public class DexAnalysisResult
    {
        public bool          Passed           { get; set; }
        public int           DexFilesCount    { get; set; }
        public long          MainDexSizeBytes { get; set; }
        public int           ClassesCount     { get; set; }
        public int           MethodsCount     { get; set; }
        public List<string>  FoundSignatures  { get; set; } = new();
        public string        DexDumpOutput    { get; set; }
        public string        Error            { get; set; }
    }

    public class AssetsVerificationResult
    {
        public bool   Passed               { get; set; }
        public int    TotalAssetsCount     { get; set; }
        public int    EncryptedAssetsCount { get; set; }
        public int    ElcAssetsCount       { get; set; }
        public int    HpkeAssetsCount      { get; set; }
        public int    ValidElcFiles        { get; set; }
        public int    ValidHpkeFiles       { get; set; }
        public long   TotalAssetsSizeBytes { get; set; }
        public string Error               { get; set; }
    }

    public class ZipIntegrityResult
    {
        public bool   Passed        { get; set; }
        public int    TotalEntries  { get; set; }
        public bool   HasManifest   { get; set; }
        public bool   HasDex        { get; set; }
        public bool   HasResources  { get; set; }
        public int    CrcErrors     { get; set; }
        public bool   ZipAligned    { get; set; }
        public string Error        { get; set; }
    }
}
