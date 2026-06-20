using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TcpServerApp.Services.Elite.Security
{
    /// <summary>
    /// مخزن مفاتيح مشفر لحفظ مفاتيح AES بأسماء مستعارة (Alias).
    /// يحفظ المفاتيح في ملف JSON مشفر بـ AES-256-GCM مشتق من كلمة مرور رئيسية (PBKDF2).
    ///
    /// بنية الملف على القرص:
    ///   Salt[32] | Nonce[12] | Tag[16] | EncryptedJSON
    ///
    /// بنية JSON الداخلية (مشفرة):
    ///   { "entries": [ { "alias": "...", "key": "Base64", "created": "..." } ] }
    /// </summary>
    public class EliteKeyVault
    {
        // ── PBKDF2 Parameters ─────────────────────────────────────────────────
        private const int SaltSize       = 32;
        private const int Iterations     = 200_000;
        private const int KeySize        = 32;   // AES-256
        private const int NonceSize      = 12;
        private const int TagSize        = 16;

        // ── Vault File ────────────────────────────────────────────────────────
        private readonly string _vaultPath;

        /// <summary>
        /// يُنشئ (أو يفتح) مخزن مفاتيح في <paramref name="vaultPath"/>.
        /// </summary>
        public EliteKeyVault(string vaultPath = null)
        {
            _vaultPath = vaultPath
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Ekhtibar", "elite_vault.ekv");

            // تأكد من وجود المجلد الأب
            Directory.CreateDirectory(Path.GetDirectoryName(_vaultPath)!);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>يحفظ (أو يحدّث) مفتاح بالاسم المستعار <paramref name="alias"/>.</summary>
        public void SaveKey(string alias, string base64Key, string masterPassword)
        {
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("يجب أن يكون الاسم المستعار غير فارغ", nameof(alias));

            var entries = LoadEntries(masterPassword);

            // تحديث أو إضافة
            var existing = entries.FirstOrDefault(e =>
                string.Equals(e.Alias, alias, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Key     = base64Key;
                existing.Created = DateTime.UtcNow.ToString("O");
            }
            else
            {
                entries.Add(new VaultEntry
                {
                    Alias   = alias,
                    Key     = base64Key,
                    Created = DateTime.UtcNow.ToString("O")
                });
            }

            SaveEntries(entries, masterPassword);
        }

        /// <summary>يعيد المفتاح المخزن تحت <paramref name="alias"/>.</summary>
        public string LoadKey(string alias, string masterPassword)
        {
            var entries = LoadEntries(masterPassword);
            var entry   = entries.FirstOrDefault(e =>
                string.Equals(e.Alias, alias, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new KeyNotFoundException($"لا يوجد مفتاح باسم: {alias}");

            return entry.Key;
        }

        /// <summary>يعيد قائمة بجميع الأسماء المستعارة المخزنة.</summary>
        public List<VaultEntry> ListEntries(string masterPassword)
        {
            return LoadEntries(masterPassword);
        }

        /// <summary>يحذف مفتاحاً بالاسم المستعار.</summary>
        public bool DeleteKey(string alias, string masterPassword)
        {
            var entries = LoadEntries(masterPassword);
            int removed = entries.RemoveAll(e =>
                string.Equals(e.Alias, alias, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
                SaveEntries(entries, masterPassword);

            return removed > 0;
        }

        /// <summary>يتحقق من صحة كلمة المرور (يحاول فك التشفير).</summary>
        public bool VerifyPassword(string masterPassword)
        {
            if (!File.Exists(_vaultPath)) return true; // مخزن جديد — أي كلمة مرور مقبولة
            try { LoadEntries(masterPassword); return true; }
            catch { return false; }
        }

        /// <summary>مسار ملف المخزن على القرص.</summary>
        public string VaultPath => _vaultPath;

        /// <summary>هل المخزن موجود بالفعل؟</summary>
        public bool Exists => File.Exists(_vaultPath);

        // ── Internal Helpers ──────────────────────────────────────────────────

        private (byte[] key, byte[] salt) DeriveKey(string password, byte[] salt = null)
        {
            salt ??= RandomBytes(SaltSize);
            using var kdf = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(password), salt,
                Iterations, HashAlgorithmName.SHA256);
            return (kdf.GetBytes(KeySize), salt);
        }

        private List<VaultEntry> LoadEntries(string masterPassword)
        {
            if (!File.Exists(_vaultPath))
                return new List<VaultEntry>();

            byte[] raw = File.ReadAllBytes(_vaultPath);

            // بنية: Salt[32] | Nonce[12] | Tag[16] | CipherData
            if (raw.Length < SaltSize + NonceSize + TagSize + 2)
                throw new CryptographicException("ملف المخزن تالف أو غير مكتمل");

            byte[] salt     = raw[..SaltSize];
            byte[] nonce    = raw[SaltSize..(SaltSize + NonceSize)];
            byte[] tag      = raw[(SaltSize + NonceSize)..(SaltSize + NonceSize + TagSize)];
            byte[] cipher   = raw[(SaltSize + NonceSize + TagSize)..];
            byte[] plain    = new byte[cipher.Length];

            var (key, _) = DeriveKey(masterPassword, salt);

            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
            }
            catch (CryptographicException)
            {
                throw new CryptographicException("كلمة المرور خاطئة أو البيانات تالفة");
            }

            string json = Encoding.UTF8.GetString(plain);
            var doc     = JsonSerializer.Deserialize<VaultDocument>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return doc?.Entries ?? new List<VaultEntry>();
        }

        private void SaveEntries(List<VaultEntry> entries, string masterPassword)
        {
            var doc  = new VaultDocument { Entries = entries };
            string json = JsonSerializer.Serialize(doc,
                new JsonSerializerOptions { WriteIndented = false });
            byte[] plain  = Encoding.UTF8.GetBytes(json);
            byte[] cipher = new byte[plain.Length];
            byte[] tag    = new byte[TagSize];

            var (key, salt) = DeriveKey(masterPassword);
            byte[] nonce    = RandomBytes(NonceSize);

            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, plain, cipher, tag);

            // بنية: Salt[32] | Nonce[12] | Tag[16] | CipherData
            using var ms = new MemoryStream();
            ms.Write(salt,   0, salt.Length);
            ms.Write(nonce,  0, nonce.Length);
            ms.Write(tag,    0, tag.Length);
            ms.Write(cipher, 0, cipher.Length);

            File.WriteAllBytes(_vaultPath, ms.ToArray());
        }

        private static byte[] RandomBytes(int count)
        {
            byte[] b = new byte[count];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(b);
            return b;
        }

        // ── Model ─────────────────────────────────────────────────────────────

        private class VaultDocument
        {
            public List<VaultEntry> Entries { get; set; } = new();
        }

        /// <summary>سجل مفتاح واحد في المخزن.</summary>
        public class VaultEntry
        {
            public string Alias   { get; set; }
            public string Key     { get; set; }
            public string Created { get; set; }
        }
    }
}
