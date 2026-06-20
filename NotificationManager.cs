using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TcpServerApp
{
    public class NotificationManager
    {
        private static NotificationManager _instance;
        private StackPanel _notificationPanel;
        private readonly List<NotificationControl> _activeNotifications = new List<NotificationControl>();
        private const int MaxNotifications = 5;

        public static NotificationManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new NotificationManager();
                return _instance;
            }
        }

        public void Initialize(StackPanel panel)
        {
            _notificationPanel = panel;
        }

        public void ShowNotification(string title, string message, NotificationType type, double duration = 5)
        {
            if (_notificationPanel == null)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Remove oldest notification if limit reached
                if (_activeNotifications.Count >= MaxNotifications)
                {
                    var oldest = _activeNotifications.First();
                    RemoveNotification(oldest);
                }

                var notification = new NotificationControl();
                notification.Closed += (s, e) => RemoveNotification(notification);
                
                _notificationPanel.Children.Add(notification);
                _activeNotifications.Add(notification);
                
                notification.Show(title, message, type, duration);
            });
        }

        private void RemoveNotification(NotificationControl notification)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_notificationPanel.Children.Contains(notification))
                {
                    _notificationPanel.Children.Remove(notification);
                }
                _activeNotifications.Remove(notification);
            });
        }

        public void ClearAll()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var notifications = _activeNotifications.ToList();
                foreach (var notification in notifications)
                {
                    RemoveNotification(notification);
                }
            });
        }

        // Convenience methods
        public void Success(string message, string title = "نجح") => 
            ShowNotification(title, message, NotificationType.Success);

        public void Error(string message, string title = "خطأ") => 
            ShowNotification(title, message, NotificationType.Error);

        public void Warning(string message, string title = "تحذير") => 
            ShowNotification(title, message, NotificationType.Warning);

        public void Info(string message, string title = "معلومات") => 
            ShowNotification(title, message, NotificationType.Info);

        public void Connection(string message, string title = "اتصال") => 
            ShowNotification(title, message, NotificationType.Connection);

        public void Data(string message, string title = "بيانات") => 
            ShowNotification(title, message, NotificationType.Data);
    }
}
