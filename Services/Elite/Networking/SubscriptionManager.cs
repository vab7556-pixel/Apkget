using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TcpServerApp.Services.Elite.Networking
{
    /// <summary>
    /// مدير الاشتراكات البحثية: يهدف للتحكم في تدفقات البيانات (Telemetry Streams) من العقد البحثية.
    /// متوافق مع معايير Android 16 ODP و Federated Compute.
    /// </summary>
    public class SubscriptionManager
    {
        private readonly ConcurrentDictionary<string, HashSet<ElitePacketType>> _activeSubscriptions = new();
        private readonly EliteNetworkListener _networkListener;

        public SubscriptionManager(EliteNetworkListener networkListener)
        {
            _networkListener = networkListener;
            _networkListener.OnPacketReceived += HandleTelemetryPacket;
        }

        /// <summary>
        /// الاشتراك في تدفق بيانات محدد لعقدة بحثية.
        /// </summary>
        public void Subscribe(string clientId, ElitePacketType type)
        {
            var subs = _activeSubscriptions.GetOrAdd(clientId, _ => new HashSet<ElitePacketType>());
            lock (subs)
            {
                subs.Add(type);
            }
            
            // إخطار العميل ببدء البث (Hacking PoC: محاكاة تفعيل مجسات البحث)
            _ = _networkListener.SendPacketAsync(clientId, new Packet(type, System.Text.Encoding.UTF8.GetBytes("ENABLE_STREAM")));
        }

        /// <summary>
        /// إلغاء الاشتراك في تدفق بيانات محدد.
        /// </summary>
        public void Unsubscribe(string clientId, ElitePacketType type)
        {
            if (_activeSubscriptions.TryGetValue(clientId, out var subs))
            {
                lock (subs)
                {
                    subs.Remove(type);
                }
                
                // إخطار العميل بوقف البث لتوفير الطاقة/النطاق الترددي
                _ = _networkListener.SendPacketAsync(clientId, new Packet(type, System.Text.Encoding.UTF8.GetBytes("DISABLE_STREAM")));
            }
        }

        private void HandleTelemetryPacket(string clientId, Packet packet)
        {
            // التحقق مما إذا كان هذا النوع من البيانات مشتركاً به حالياً
            if (_activeSubscriptions.TryGetValue(clientId, out var subs))
            {
                lock (subs)
                {
                    if (!subs.Contains(packet.Type))
                    {
                        // بيانات غير مشتركة (يمكن تجاهلها أو تسجيلها كبحث خامل)
                        return;
                    }
                }
            }
            
            // معالجة البيانات عالية الدقة (High-Fidelity)
            // سيتم ربط هذا الجزء بالواجهات الرسومية (LiveCharts) لاحقاً
        }

        /// <summary>
        /// الحصول على قائمة المشتركين النشطين لنوع بيانات معين.
        /// </summary>
        public List<string> GetSubscribersFor(ElitePacketType type)
        {
            return _activeSubscriptions
                .Where(kvp => kvp.Value.Contains(type))
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }
}
