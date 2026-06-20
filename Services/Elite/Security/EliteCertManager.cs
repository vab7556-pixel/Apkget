using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TcpServerApp.Services.Elite.Security
{
    /// <summary>
    /// إدارة الشهادات الرقمية X.509 لبيئة البحث الأكاديمي.
    /// تتيح إنشاء Root CA وشهادات توقيع APK وتصديرها بصيغة PEM/PFX.
    /// جامعة حلب | Android 36 Research Platform
    /// </summary>
    public static class EliteCertManager
    {
        #region Root CA

        /// <summary>
        /// إنشاء Root CA موقّعة ذاتياً — مناسبة لبيئة البحث المعزولة.
        /// تستخدم RSA-4096 + SHA-256.
        /// </summary>
        /// <param name="subject">اسم الكيان (مثل: CN=Elite Research CA, O=University of Aleppo)</param>
        /// <param name="validDays">مدة الصلاحية بالأيام (افتراضي 365)</param>
        public static X509Certificate2 CreateResearchCa(string subject = null, int validDays = 365)
        {
            subject ??= "CN=Elite Research CA, O=University of Aleppo, C=SY";

            using var rsa = RSA.Create(4096);
            var req = new CertificateRequest(
                new X500DistinguishedName(subject),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // إضافة BasicConstraints (CA=true)
            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: true,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));

            // KeyUsage: CertSign + CRLSign
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    critical: true));

            // SubjectKeyIdentifier
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, critical: false));

            var now  = DateTimeOffset.UtcNow;
            var cert = req.CreateSelfSigned(
                notBefore: now.AddMinutes(-5),
                notAfter:  now.AddDays(validDays));

            return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        #endregion

        #region APK Signing Certificate

        /// <summary>
        /// إنشاء شهادة توقيع APK موقّعة بالـ CA المعطاة.
        /// تستخدم RSA-2048 + SHA-256 (متطلبات Android).
        /// </summary>
        /// <param name="caCert">شهادة الـ Root CA (يجب أن تحتوي على المفتاح الخاص)</param>
        /// <param name="apkName">اسم التطبيق (يُدرج في الـ CN)</param>
        /// <param name="validDays">مدة الصلاحية</param>
        public static X509Certificate2 CreateApkSigningCert(
            X509Certificate2 caCert, string apkName = "ResearchApp", int validDays = 365)
        {
            using var rsaApp = RSA.Create(2048);
            var subject = $"CN={apkName}, O=Ekhtibar Research, C=SY";

            var req = new CertificateRequest(
                new X500DistinguishedName(subject),
                rsaApp,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // BasicConstraints: ليس CA
            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            // KeyUsage: DigitalSignature
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

            // ExtendedKeyUsage: Code Signing
            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.3") }, // Code Signing OID
                    critical: false));

            // SubjectKeyIdentifier
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, critical: false));

            // Serial Number عشوائي
            byte[] serial = new byte[16];
            RandomNumberGenerator.Fill(serial);
            serial[0] &= 0x7F; // تأكد أنه موجب

            var now  = DateTimeOffset.UtcNow;
            var cert = req.Create(
                caCert,
                notBefore: now.AddMinutes(-5),
                notAfter:  now.AddDays(validDays),
                serialNumber: serial);

            // دمج المفتاح الخاص مع الشهادة
            return cert.CopyWithPrivateKey(rsaApp);
        }

        #endregion

        #region Export Methods

        /// <summary>
        /// تصدير الشهادة بصيغة PEM (Base64).
        /// إذا كانت includePrivateKey=true، يُصدر المفتاح الخاص أيضاً (PKCS#8).
        /// </summary>
        public static string ExportToPem(X509Certificate2 cert, bool includePrivateKey = false)
        {
            var sb = new StringBuilder();

            // تصدير الشهادة العامة
            byte[] certBytes = cert.Export(X509ContentType.Cert);
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(certBytes, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");

            // تصدير المفتاح الخاص إن طُلب
            if (includePrivateKey && cert.HasPrivateKey)
            {
                using var rsa = cert.GetRSAPrivateKey();
                if (rsa != null)
                {
                    byte[] privKey = rsa.ExportPkcs8PrivateKey();
                    sb.AppendLine("-----BEGIN PRIVATE KEY-----");
                    sb.AppendLine(Convert.ToBase64String(privKey, Base64FormattingOptions.InsertLineBreaks));
                    sb.AppendLine("-----END PRIVATE KEY-----");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// تصدير الشهادة بصيغة PFX (PKCS#12) إلى ملف.
        /// </summary>
        /// <param name="cert">الشهادة (مع المفتاح الخاص)</param>
        /// <param name="path">مسار الملف</param>
        /// <param name="password">كلمة مرور PFX</param>
        public static void ExportToPfx(X509Certificate2 cert, string path, string password = "android")
        {
            byte[] pfxData = cert.Export(X509ContentType.Pfx, password);
            File.WriteAllBytes(path, pfxData);
        }

        /// <summary>
        /// استيراد شهادة من ملف PFX.
        /// </summary>
        public static X509Certificate2 ImportFromPfx(string path, string password = "android")
        {
            return new X509Certificate2(path, password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        /// <summary>
        /// استيراد شهادة من محتوى PEM نصي.
        /// </summary>
        public static X509Certificate2 ImportFromPem(string pemContent)
        {
            return X509Certificate2.CreateFromPem(pemContent);
        }

        #endregion

        #region Certificate Info

        /// <summary>
        /// طباعة معلومات الشهادة بصيغة مقروءة.
        /// </summary>
        public static string GetCertInfo(X509Certificate2 cert)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Subject:     {cert.Subject}");
            sb.AppendLine($"Issuer:      {cert.Issuer}");
            sb.AppendLine($"Serial:      {cert.SerialNumber}");
            sb.AppendLine($"Thumbprint:  {cert.Thumbprint}");
            sb.AppendLine($"Valid From:  {cert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Valid To:    {cert.NotAfter:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Has PK:      {cert.HasPrivateKey}");
            sb.AppendLine($"Algorithm:   {cert.SignatureAlgorithm.FriendlyName}");
            return sb.ToString();
        }

        #endregion
    }
}
