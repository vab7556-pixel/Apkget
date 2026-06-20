using System;
using System.Collections.Generic;
using System.Linq;

namespace TcpServerApp.Services.Elite.Networking
{
    /// <summary>
    /// المجمع الآمن (Secure Aggregator): يحاكي بروتوكول Secure Multi-Party Computation (SMPC) المعمول به في Android 16.
    /// يهدف إلى تجميع تحديثات النماذج (Model Updates) مع الحفاظ على خصوصية البيانات الفردية لكل جهاز.
    /// </summary>
    public class SecureAggregator
    {
        private readonly List<float[]> _gradientBuffer = new();
        private readonly int _minNodesRequired;

        public event Action<float[]>? OnAggregationComplete;

        public SecureAggregator(int minNodesRequired = 3)
        {
            _minNodesRequired = minNodesRequired;
        }

        /// <summary>
        /// إضافة تحديث نموذج "مُعمّى" (Masked) من عقدة معينة.
        /// </summary>
        public void AddMaskedUpdate(byte[] payload)
        {
            // في الواقع، يتم هنا فك التعمية باستخدام مفاتيح موزعة
            // سنقوم هنا بتحويل البيانات الثنائية إلى مصفوفة أرقام عشرية (أوزان النموذج)
            float[] updates = ConvertToFloatArray(payload);
            
            lock (_gradientBuffer)
            {
                _gradientBuffer.Add(updates);
                
                if (_gradientBuffer.Count >= _minNodesRequired)
                {
                    AggregateAndDispatch();
                }
            }
        }

        private void AggregateAndDispatch()
        {
            if (_gradientBuffer.Count == 0) return;

            int length = _gradientBuffer[0].Length;
            float[] averagedGradients = new float[length];

            for (int i = 0; i < length; i++)
            {
                float sum = 0;
                foreach (var update in _gradientBuffer)
                {
                    sum += update[i];
                }
                averagedGradients[i] = sum / _gradientBuffer.Count;
            }

            _gradientBuffer.Clear();
            OnAggregationComplete?.Invoke(averagedGradients);
        }

        private float[] ConvertToFloatArray(byte[] bytes)
        {
            int floatCount = bytes.Length / 4;
            float[] result = new float[floatCount];
            for (int i = 0; i < floatCount; i++)
            {
                result[i] = BitConverter.ToSingle(bytes, i * 4);
            }
            return result;
        }

        /// <summary>
        /// تحويل مصفوفة الأوزان المجمعة إلى بيانات ثنائية للإرسال.
        /// </summary>
        public byte[] GetAggregatedPayload(float[] weights)
        {
            byte[] result = new byte[weights.Length * 4];
            for (int i = 0; i < weights.Length; i++)
            {
                byte[] temp = BitConverter.GetBytes(weights[i]);
                Buffer.BlockCopy(temp, 0, result, i * 4, 4);
            }
            return result;
        }
    }
}
