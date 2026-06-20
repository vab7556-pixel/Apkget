using System;

namespace TcpServerApp.Services
{
    public class LogService
    {
        public event Action<string>? OnLog;

        public void Append(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            OnLog?.Invoke($"[{ts}] {message}");
        }
    }
}
