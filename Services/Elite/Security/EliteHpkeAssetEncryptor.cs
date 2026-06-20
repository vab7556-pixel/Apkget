using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite.Security
{
    /// <summary>
    /// ✨ ميزة بحثية حصرية: تشفير Assets بـ HPKE (RFC 9180) مع فك التشفير بـ android.crypto.hpke
    ///
    /// الفكرة: كل أصل (asset) يُشفَّر بـ مفتاح جلسة مؤقت (ephemeral) مشتق من ECDH،
    ///         لذلك حتى لو سُرق المفتاح الخاص لاحقاً لا يمكن فك تشفير الأصول السابقة
    ///         → خاصية Forward Secrecy مستحيلة مع AES-CBC/GCM وحده.
    ///
    /// Suite المستخدمة — تطابق android.crypto.hpke في Android 36:
    ///   KEM  : DHKEM(P-256, HKDF-SHA256)   ← كما في KemParameterSpec.DHKEM_P256_HKDF_SHA256
    ///   KDF  : HKDF-SHA256                  ← كما في KdfParameterSpec.HKDF_SHA256
    ///   AEAD : AES-256-GCM                  ← كما في AeadParameterSpec.AES_256_GCM
    ///
    /// صيغة الملف المشفر (.hpke.elc):
    ///   [MAGIC 4B "ELHP"] [encLen 2B] [enc: P-256 uncompressed 65B] [ciphertext+tag]
    ///
    /// جانب .NET (هذا الملف): تشفير الأصول على سطح المكتب.
    /// جانب Android 36 (Smali): فك التشفير داخل APK باستخدام android.crypto.hpke.Hpke.
    /// </summary>
    public static class EliteHpkeAssetEncryptor
    {
        // ── ثوابت الصيغة ──────────────────────────────────────────────────────
        private static readonly byte[] Magic = { 0x45, 0x4C, 0x48, 0x50 }; // "ELHP"
        private const int  EncLen    = 65;  // P-256 uncompressed public key
        private const int  TagSize   = 16;  // AES-GCM tag
        private const int  NonceSize = 12;  // AES-GCM nonce
        private const int  KeySize   = 32;  // AES-256

        // ── ثوابت HKDF (تطابق RFC 9180 مع DHKEM_P256_HKDF_SHA256 + AES_256_GCM) ──
        private static readonly byte[] SuiteId =
            Encoding.ASCII.GetBytes("HPKE\x00\x10\x00\x01\x00\x02"); // DHKEM_P256+HKDF_SHA256+AES_256_GCM
        private static readonly byte[] InfoLabel =
            Encoding.UTF8.GetBytes("EliteHPKE-Android36-Research");   // info للـ KeySchedule

        // ══════════════════════════════════════════════════════════════════════
        // 1. توليد المفتاح الثابت للمستقبل (Recipient Keypair)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// يولّد زوج مفاتيح P-256 دائم للمستقبل (APK).
        /// PublicKeyBase64  → يُضمَّن كـ constant في Smali المحقون.
        /// PrivateKeyBase64 → يُخزَّن بأمان (يلزمه فقط لفك التشفير يدوياً).
        /// </summary>
        public static (string PublicKeyBase64, string PrivateKeyBase64) GenerateRecipientKeyPair()
        {
            using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var pub  = ecdh.ExportSubjectPublicKeyInfo();   // DER SubjectPublicKeyInfo
            var priv = ecdh.ExportPkcs8PrivateKey();        // DER PKCS#8

            return (Convert.ToBase64String(pub), Convert.ToBase64String(priv));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. تشفير بيانات منفردة بـ HPKE BASE mode (RFC 9180 §6.1)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// يشفّر <paramref name="plaintext"/> بـ HPKE BASE mode مع recipient P-256 public key.
        /// الناتج: [MAGIC(4)] [encLen=65 (2B LE)] [enc(65B)] [ciphertext+tag]
        /// </summary>
        public static byte[] SealAsset(byte[] plaintext, byte[] recipientPublicKeyDer)
        {
            // ── استيراد المفتاح العام للمستقبل ───────────────────────────────
            using var recipientEcdh = ECDiffieHellman.Create();
            recipientEcdh.ImportSubjectPublicKeyInfo(recipientPublicKeyDer, out _);
            var recipientPub = recipientEcdh.PublicKey;

            // ── توليد Ephemeral Keypair (مؤقت لكل رسالة) ─────────────────────
            using var ephemeralEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var ephemeralPub = ephemeralEcdh.PublicKey;

            // ── ECDH → Shared Secret ──────────────────────────────────────────
            byte[] sharedSecret = ephemeralEcdh.DeriveRawSecretAgreement(recipientPub);

            // ── HKDF-SHA256 → AES-256-GCM key + Nonce ────────────────────────
            // تطابق RFC 9180: ExtractAndExpand(dh, kem_context, suite_id)
            var (aesKey, nonce) = DeriveHpkeKeyNonce(sharedSecret,
                GetEphemeralPublicKeyBytes(ephemeralEcdh),
                GetRecipientPublicKeyBytes(recipientEcdh));

            // ── AES-256-GCM encryption ────────────────────────────────────────
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag        = new byte[TagSize];
            using (var gcm = new AesGcm(aesKey, TagSize))
                gcm.Encrypt(nonce, plaintext, ciphertext, tag, InfoLabel);

            // ── المفتاح المغلَّف (enc) = P-256 uncompressed public key (65 bytes) ──
            byte[] enc = GetEphemeralPublicKeyBytes(ephemeralEcdh);

            // ── تجميع الناتج: [MAGIC(4)][encLen(2)][enc(65)][ciphertext+tag] ─
            using var ms = new MemoryStream();
            ms.Write(Magic, 0, 4);
            ms.WriteByte((byte)(EncLen & 0xFF));
            ms.WriteByte((byte)((EncLen >> 8) & 0xFF));
            ms.Write(enc, 0, enc.Length);
            ms.Write(ciphertext, 0, ciphertext.Length);
            ms.Write(tag, 0, tag.Length);
            return ms.ToArray();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. فك التشفير (.NET) — للتحقق والاختبار
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>يفك تشفير ملف .hpke.elc — للاختبار على .NET (Android يفعل هذا بـ HPKE API)</summary>
        public static byte[] OpenAsset(byte[] sealed_, byte[] recipientPrivateKeyDer)
        {
            if (sealed_.Length < 4 + 2 + EncLen + TagSize)
                throw new InvalidDataException("بيانات HPKE غير صالحة — صغيرة جداً");

            // التحقق من Magic
            if (sealed_[0] != 0x45 || sealed_[1] != 0x4C || sealed_[2] != 0x48 || sealed_[3] != 0x50)
                throw new InvalidDataException("Magic bytes خاطئة — ليس ملف HPKE Elite");

            int encLen = sealed_[4] | (sealed_[5] << 8);
            if (encLen != EncLen)
                throw new InvalidDataException($"encLen غير متوقع: {encLen} (يُتوقع {EncLen})");

            byte[] enc        = new byte[EncLen];
            byte[] ciphertext = new byte[sealed_.Length - 4 - 2 - EncLen - TagSize];
            byte[] tag        = new byte[TagSize];

            Buffer.BlockCopy(sealed_, 6,                  enc,        0, EncLen);
            Buffer.BlockCopy(sealed_, 6 + EncLen,         ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(sealed_, 6 + EncLen + ciphertext.Length, tag, 0, TagSize);

            // استيراد المفتاح الخاص للمستقبل
            using var recipientEcdh = ECDiffieHellman.Create();
            recipientEcdh.ImportPkcs8PrivateKey(recipientPrivateKeyDer, out _);

            // إعادة بناء المفتاح العام المؤقت من bytes
            using var ephemeralEcdh = ECDiffieHellman.Create();
            var ephParams = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q     = new ECPoint
                {
                    X = enc[1..33],
                    Y = enc[33..65]
                }
            };
            ephemeralEcdh.ImportParameters(ephParams);

            // ECDH من جانب المستقبل
            byte[] sharedSecret = recipientEcdh.DeriveRawSecretAgreement(ephemeralEcdh.PublicKey);
            var (aesKey, nonce) = DeriveHpkeKeyNonce(sharedSecret, enc,
                GetRecipientPublicKeyBytes(recipientEcdh));

            byte[] plain = new byte[ciphertext.Length];
            using (var gcm = new AesGcm(aesKey, TagSize))
                gcm.Decrypt(nonce, ciphertext, tag, plain, InfoLabel);

            return plain;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. تشفير مجلد assets/ كاملاً بـ HPKE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// يشفّر كل ملفات assets/ بـ HPKE ويضيف لاحقة .hpke.elc
        /// يعيد عدد الملفات المشفرة.
        /// </summary>
        public static async Task<int> EncryptAssetsHpkeAsync(
            string decompDir,
            byte[] recipientPublicKeyDer,
            Action<string> log,
            CancellationToken ct)
        {
            string assetsDir = Path.Combine(decompDir, "assets");
            if (!Directory.Exists(assetsDir))
            {
                log("    [HPKE] لا يوجد مجلد assets");
                return 0;
            }

            var files = Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories);
            int count = 0;

            foreach (string file in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    byte[] plain  = await File.ReadAllBytesAsync(file, ct);
                    byte[] sealed_ = SealAsset(plain, recipientPublicKeyDer);
                    string outPath = file + ".hpke.elc";
                    await File.WriteAllBytesAsync(outPath, sealed_, ct);
                    File.Delete(file);
                    log($"    🔒 [HPKE] {Path.GetFileName(file)} → {Path.GetFileName(outPath)}");
                    count++;
                }
                catch (Exception ex)
                {
                    log($"    ⚠ [HPKE] تخطي {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            return count;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. Smali Generator — android.crypto.hpke.Hpke على Android 36
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// يولّد كود Smali لـ EliteHpkeDecryptor يستخدم android.crypto.hpke.Hpke
        /// (API حصري في Android 36 / API Level 36).
        ///
        /// recipientPublicKeyBase64: المفتاح العام للمستقبل (P-256 SubjectPublicKeyInfo DER → Base64)
        ///
        /// الـ Smali يُنفذ:
        ///   1. فك تشفير المفتاح العام من Base64
        ///   2. استيراده كـ ECPublicKey عبر KeyFactory
        ///   3. بناء Hpke.Recipient باستخدام android.crypto.hpke
        ///   4. استدعاء recipient.open(ciphertext, aad) لفك التشفير
        ///   5. دعم كامل لصيغة "ELHP" magic bytes
        /// </summary>
        public static string GenerateHpkeDecryptorSmali(string recipientPublicKeyBase64)
        {
            string q = "\"";
            var sb = new StringBuilder();

            sb.AppendLine(".class public Lcom/elite/crypto/EliteHpkeDecryptor;");
            sb.AppendLine(".super Ljava/lang/Object;");
            sb.AppendLine($".source {q}EliteHpkeDecryptor.java{q}");
            sb.AppendLine();
            sb.AppendLine("# =========================================================");
            sb.AppendLine("#  Elite HPKE Asset Decryptor — Android 36 Exclusive API");
            sb.AppendLine("#  Generated by Ekhtibar Research Platform");
            sb.AppendLine("#  University of Aleppo | Android 36 Research");
            sb.AppendLine("#");
            sb.AppendLine("#  Suite: DHKEM(P-256, HKDF-SHA256) + HKDF-SHA256 + AES-256-GCM");
            sb.AppendLine($"#  KEM   : KemParameterSpec.DHKEM_P256_HKDF_SHA256");
            sb.AppendLine($"#  KDF   : KdfParameterSpec.HKDF_SHA256");
            sb.AppendLine($"#  AEAD  : AeadParameterSpec.AES_256_GCM");
            sb.AppendLine("#  Mode  : BASE (no sender auth) — RFC 9180 §5.1");
            sb.AppendLine("#  API   : android.crypto.hpke.Hpke (Android 16 / API 36)");
            sb.AppendLine("#");
            sb.AppendLine("#  Format: [MAGIC(4)=ELHP][encLen(2)][enc(65)][ciphertext+tag]");
            sb.AppendLine("# =========================================================");
            sb.AppendLine();

            // ── المفتاح العام الثابت (Recipient Public Key) ──────────────────
            sb.AppendLine("# Recipient P-256 Public Key (SubjectPublicKeyInfo DER → Base64)");
            sb.AppendLine($".field public static final RECIPIENT_PUBLIC_KEY:Ljava/lang/String; = {q}{recipientPublicKeyBase64}{q}");
            sb.AppendLine();

            // ── ثابت suite name ───────────────────────────────────────────────
            sb.AppendLine($".field public static final HPKE_SUITE:Ljava/lang/String; = {q}DHKEM_P256_HKDF_SHA256/HKDF_SHA256/AES_256_GCM{q}");
            sb.AppendLine();
            sb.AppendLine($"# AAD (Additional Authenticated Data) = label تطبيق Elite");
            sb.AppendLine($".field public static final HPKE_AAD:Ljava/lang/String; = {q}EliteHPKE-Android36-Research{q}");
            sb.AppendLine();

            // ════════════════════════════════════════════════════════════
            // loadRecipientPublicKey() → ECPublicKey
            // ════════════════════════════════════════════════════════════
            sb.AppendLine("# loadRecipientPublicKey() -> PublicKey");
            sb.AppendLine("# يُحمّل مفتاح P-256 العام من Base64 DER");
            sb.AppendLine(".method public static loadRecipientPublicKey()Ljava/security/PublicKey;");
            sb.AppendLine("    .registers 8");
            sb.AppendLine();
            sb.AppendLine($"    const-string v0, {q}RECIPIENT_PUBLIC_KEY{q}");
            sb.AppendLine("    const-class v1, Lcom/elite/crypto/EliteHpkeDecryptor;");
            sb.AppendLine("    invoke-virtual {v1, v0}, Ljava/lang/Class;->getField(Ljava/lang/String;)Ljava/lang/reflect/Field;");
            sb.AppendLine("    move-result-object v2");
            sb.AppendLine("    const/4 v3, 0x0");
            sb.AppendLine("    invoke-virtual {v2, v3}, Ljava/lang/reflect/Field;->get(Ljava/lang/Object;)Ljava/lang/Object;");
            sb.AppendLine("    move-result-object v4");
            sb.AppendLine("    check-cast v4, Ljava/lang/String;");
            sb.AppendLine("    const/4 v5, 0x0");
            sb.AppendLine("    invoke-static {v4, v5}, Landroid/util/Base64;->decode(Ljava/lang/String;I)[B");
            sb.AppendLine("    move-result-object v6");
            sb.AppendLine("    # KeyFactory.getInstance(\"EC\")");
            sb.AppendLine($"    const-string v0, {q}EC{q}");
            sb.AppendLine("    invoke-static {v0}, Ljava/security/KeyFactory;->getInstance(Ljava/lang/String;)Ljava/security/KeyFactory;");
            sb.AppendLine("    move-result-object v0");
            sb.AppendLine("    # X509EncodedKeySpec(derBytes)");
            sb.AppendLine("    new-instance v1, Ljava/security/spec/X509EncodedKeySpec;");
            sb.AppendLine("    invoke-direct {v1, v6}, Ljava/security/spec/X509EncodedKeySpec;-><init>([B)V");
            sb.AppendLine("    invoke-virtual {v0, v1}, Ljava/security/KeyFactory;->generatePublic(Ljava/security/spec/KeySpec;)Ljava/security/PublicKey;");
            sb.AppendLine("    move-result-object v0");
            sb.AppendLine("    return-object v0");
            sb.AppendLine(".end method");
            sb.AppendLine();

            // ════════════════════════════════════════════════════════════
            // openAsset(byte[] sealedData, PrivateKey recipientPrivKey) → byte[]
            // الدالة الرئيسية: تستخدم android.crypto.hpke.Hpke مباشرة
            // ════════════════════════════════════════════════════════════
            sb.AppendLine("# openAsset(byte[] sealedData, PrivateKey recipientPrivKey) -> byte[]");
            sb.AppendLine("# يفك تشفير asset مشفر بـ HPKE BASE mode");
            sb.AppendLine("# يتحقق من Magic bytes ثم يستدعي android.crypto.hpke.Hpke.open()");
            sb.AppendLine(".method public static openAsset([BLjava/security/PrivateKey;)[B");
            sb.AppendLine("    .registers 20");
            sb.AppendLine("    # p0 = sealedData, p1 = recipientPrivKey");
            sb.AppendLine();
            sb.AppendLine("    # ── التحقق من Magic bytes 'ELHP' ──");
            sb.AppendLine("    const/4 v0, 0x0");
            sb.AppendLine("    aget-byte v1, p0, v0");
            sb.AppendLine("    const/16 v2, 0x45");  // 'E'
            sb.AppendLine("    if-ne v1, v2, :bad_magic");
            sb.AppendLine("    const/4 v0, 0x1");
            sb.AppendLine("    aget-byte v1, p0, v0");
            sb.AppendLine("    const/16 v2, 0x4C");  // 'L'
            sb.AppendLine("    if-ne v1, v2, :bad_magic");
            sb.AppendLine("    const/4 v0, 0x2");
            sb.AppendLine("    aget-byte v1, p0, v0");
            sb.AppendLine("    const/16 v2, 0x48");  // 'H'
            sb.AppendLine("    if-ne v1, v2, :bad_magic");
            sb.AppendLine("    const/4 v0, 0x3");
            sb.AppendLine("    aget-byte v1, p0, v0");
            sb.AppendLine("    const/16 v2, 0x50");  // 'P'
            sb.AppendLine("    if-ne v1, v2, :bad_magic");
            sb.AppendLine("    goto :magic_ok");
            sb.AppendLine("    :bad_magic");
            sb.AppendLine("    new-instance v0, Ljava/lang/SecurityException;");
            sb.AppendLine($"    const-string v1, {q}EliteHPKE: Invalid magic bytes — not an HPKE asset{q}");
            sb.AppendLine("    invoke-direct {v0, v1}, Ljava/lang/SecurityException;-><init>(Ljava/lang/String;)V");
            sb.AppendLine("    throw v0");
            sb.AppendLine("    :magic_ok");
            sb.AppendLine();
            sb.AppendLine("    # ── قراءة encLen (2 bytes LE) ──");
            sb.AppendLine("    const/4 v3, 0x4");
            sb.AppendLine("    aget-byte v4, p0, v3");
            sb.AppendLine("    const/4 v5, 0x5");
            sb.AppendLine("    aget-byte v6, p0, v5");
            sb.AppendLine("    int-to-byte v4, v4");
            sb.AppendLine("    shl-int/lit8 v6, v6, 0x8");
            sb.AppendLine("    or-int/2addr v4, v6");
            sb.AppendLine("    move v7, v4        # v7 = encLen (65)");
            sb.AppendLine();
            sb.AppendLine("    # ── استخراج enc (ephemeral public key bytes) ──");
            sb.AppendLine("    const/4 v8, 0x6    # offset = 6 (بعد magic+encLen)");
            sb.AppendLine("    invoke-static {p0, v8, v7}, Ljava/util/Arrays;->copyOfRange([BII)[B");
            sb.AppendLine("    # تصحيح: endOffset = 6 + encLen");
            sb.AppendLine("    add-int v9, v8, v7");
            sb.AppendLine("    invoke-static {p0, v8, v9}, Ljava/util/Arrays;->copyOfRange([BII)[B");
            sb.AppendLine("    move-result-object v10   # v10 = enc");
            sb.AppendLine();
            sb.AppendLine("    # ── استخراج ciphertext (ما تبقى بعد enc) ──");
            sb.AppendLine("    array-length v11, p0");
            sb.AppendLine("    invoke-static {p0, v9, v11}, Ljava/util/Arrays;->copyOfRange([BII)[B");
            sb.AppendLine("    move-result-object v12   # v12 = ciphertext+tag");
            sb.AppendLine();
            sb.AppendLine("    # ════ استدعاء android.crypto.hpke.Hpke.open() ════");
            sb.AppendLine($"    const-string v13, {q}DHKEM_P256_HKDF_SHA256/HKDF_SHA256/AES_256_GCM{q}");
            sb.AppendLine("    invoke-static {v13}, Landroid/crypto/hpke/Hpke;->getInstance(Ljava/lang/String;)Landroid/crypto/hpke/Hpke;");
            sb.AppendLine("    move-result-object v14   # v14 = hpke instance");
            sb.AppendLine();
            sb.AppendLine("    # ── بناء android.crypto.hpke.Message(enc, ciphertext) ──");
            sb.AppendLine("    new-instance v15, Landroid/crypto/hpke/Message;");
            sb.AppendLine("    invoke-direct {v15, v10, v12}, Landroid/crypto/hpke/Message;-><init>([B[B)V");
            sb.AppendLine();
            sb.AppendLine("    # ── AAD = InfoLabel ──");
            sb.AppendLine($"    const-string v16, {q}EliteHPKE-Android36-Research{q}");
            sb.AppendLine($"    const-string v17, {q}UTF-8{q}");
            sb.AppendLine("    invoke-virtual {v16, v17}, Ljava/lang/String;->getBytes(Ljava/lang/String;)[B");
            sb.AppendLine("    move-result-object v16   # v16 = aad bytes");
            sb.AppendLine();
            sb.AppendLine("    # ── info = null (BASE mode) ──");
            sb.AppendLine("    const/4 v17, 0x0");
            sb.AppendLine();
            sb.AppendLine("    # ── Hpke.open(recipientPrivKey, info=null, message, aad) ──");
            sb.AppendLine("    invoke-virtual {v14, p1, v17, v15, v16}, Landroid/crypto/hpke/Hpke;->open(Ljava/security/PrivateKey;[BLandroid/crypto/hpke/Message;[B)[B");
            sb.AppendLine("    move-result-object v18   # v18 = plaintext");
            sb.AppendLine("    return-object v18");
            sb.AppendLine(".end method");
            sb.AppendLine();

            // ════════════════════════════════════════════════════════════
            // decryptAssetFile(String assetPath, PrivateKey key) → byte[]
            // ════════════════════════════════════════════════════════════
            sb.AppendLine("# decryptAssetFile(String assetPath, PrivateKey recipientKey) -> byte[]");
            sb.AppendLine("# يقرأ ملف .hpke.elc ويفك تشفيره");
            sb.AppendLine(".method public static decryptAssetFile(Ljava/lang/String;Ljava/security/PrivateKey;)[B");
            sb.AppendLine("    .registers 8");
            sb.AppendLine($"    const-string v0, {q}android.os.Build{q}");
            sb.AppendLine("    # قراءة الملف كـ byte[]");
            sb.AppendLine("    new-instance v1, Ljava/io/FileInputStream;");
            sb.AppendLine("    invoke-direct {v1, p0}, Ljava/io/FileInputStream;-><init>(Ljava/lang/String;)V");
            sb.AppendLine("    invoke-virtual {v1}, Ljava/io/FileInputStream;->readAllBytes()[B");
            sb.AppendLine("    move-result-object v2");
            sb.AppendLine("    invoke-virtual {v1}, Ljava/io/FileInputStream;->close()V");
            sb.AppendLine("    # فك التشفير");
            sb.AppendLine("    invoke-static {v2, p1}, Lcom/elite/crypto/EliteHpkeDecryptor;->openAsset([BLjava/security/PrivateKey;)[B");
            sb.AppendLine("    move-result-object v3");
            sb.AppendLine("    return-object v3");
            sb.AppendLine(".end method");
            sb.AppendLine();
            sb.AppendLine("# ملاحظة: هذا الكود يتطلب Android 16 (API 36) لوجود android.crypto.hpke");
            sb.AppendLine("# android.os.Build.VERSION.SDK_INT >= 36 يجب التحقق منه قبل الاستدعاء");

            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Private Helpers
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>HKDF-SHA256 لاشتقاق AES key + Nonce من ECDH shared secret (تطابق RFC 9180)</summary>
        private static (byte[] aesKey, byte[] nonce) DeriveHpkeKeyNonce(
            byte[] sharedSecret, byte[] ephemeralPubBytes, byte[] recipientPubBytes)
        {
            // kem_context = enc || pkR
            byte[] kemContext = new byte[ephemeralPubBytes.Length + recipientPubBytes.Length];
            Buffer.BlockCopy(ephemeralPubBytes, 0, kemContext, 0,                      ephemeralPubBytes.Length);
            Buffer.BlockCopy(recipientPubBytes, 0, kemContext, ephemeralPubBytes.Length, recipientPubBytes.Length);

            // Extract: PRK = HKDF-Extract(suite_id, shared_secret)
            byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, sharedSecret, SuiteId);

            // Expand key: 32 bytes với label "key"
            byte[] keyInfo   = BuildHkdfInfo("key", kemContext);
            byte[] aesKey    = HKDF.DeriveKey(HashAlgorithmName.SHA256, prk, KeySize,   salt: null, info: keyInfo);

            // Expand nonce: 12 bytes với label "base_nonce"
            byte[] nonceInfo = BuildHkdfInfo("base_nonce", kemContext);
            byte[] nonce     = HKDF.DeriveKey(HashAlgorithmName.SHA256, prk, NonceSize, salt: null, info: nonceInfo);

            return (aesKey, nonce);
        }

        private static byte[] BuildHkdfInfo(string label, byte[] context)
        {
            // RFC 9180: LabeledExpand info = "HPKE-v1" || suite_id || label || context
            byte[] prefix    = Encoding.ASCII.GetBytes("HPKE-v1");
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);

            using var ms = new MemoryStream();
            ms.Write(prefix,    0, prefix.Length);
            ms.Write(SuiteId,   0, SuiteId.Length);
            ms.Write(labelBytes, 0, labelBytes.Length);
            if (context != null) ms.Write(context, 0, context.Length);
            return ms.ToArray();
        }

        private static byte[] GetEphemeralPublicKeyBytes(ECDiffieHellman ecdh)
        {
            var p = ecdh.ExportParameters(false).Q;
            // Uncompressed P-256 point: 0x04 || X || Y
            var result = new byte[65];
            result[0] = 0x04;
            Buffer.BlockCopy(p.X, 0, result, 1,  32);
            Buffer.BlockCopy(p.Y, 0, result, 33, 32);
            return result;
        }

        private static byte[] GetRecipientPublicKeyBytes(ECDiffieHellman ecdh)
        {
            var p = ecdh.ExportParameters(false).Q;
            var result = new byte[65];
            result[0] = 0x04;
            Buffer.BlockCopy(p.X, 0, result, 1,  32);
            Buffer.BlockCopy(p.Y, 0, result, 33, 32);
            return result;
        }
    }
}
