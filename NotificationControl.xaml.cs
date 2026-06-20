using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TcpServerApp
{
    public partial class NotificationControl : UserControl
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _progressTimer;
        private double _duration;
        private DateTime _startTime;
        public event EventHandler Closed;

        public NotificationControl()
        {
            InitializeComponent();
        }

        public void Show(string title, string message, NotificationType type, double durationSeconds = 5)
        {
            TitleText.Text = title;
            MessageText.Text = message;
            TimeText.Text = "الآن";
            _duration = durationSeconds;
            _startTime = DateTime.Now;

            // Set colors based on type
            switch (type)
            {
                case NotificationType.Success:
                    NotificationBorder.Background = new LinearGradientBrush(
                        Color.FromRgb(16, 185, 129),
                        Color.FromRgb(5, 150, 105),
                        new Point(0, 0), new Point(1, 1));
                    IconText.Text = "✅";
                    TimeoutProgress.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    break;

                case NotificationType.Error:
                    NotificationBorder.Background = new LinearGradientBrush(
                        Color.FromRgb(232, 17, 35),
                        Color.FromRgb(197, 15, 31),
                        new Point(0, 0), new Point(1, 1));
                    IconText.Text = "❌";
                    TimeoutProgress.Foreground = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    break;

                case NotificationType.Warning:
                    NotificationBorder.Background = new LinearGradientBrush(
                        Color.FromRgb(245, 158, 11),
                        Color.FromRgb(217, 119, 6),
                        new Point(0, 0), new Point(1, 1));
                    IconText.Text = "⚠️";
                    TimeoutProgress.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    break;

                case NotificationType.Info:
                    NotificationBorder.Background = new LinearGradientBrush(
                        Color.FromRgb(0, 120, 212),
                        Color.FromRgb(0, 90, 158),
                        new Point(0, 0), new Point(1, 1));
                    IconText.Text = "ℹ️";
                    TimeoutProgress.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                    break;

                case NotificationType.Connection:
                    NotificationBorder.Background = new LinearGradientBrush(
                        Color.FromRgb(139, 92, 246),
                        Color.FromRgb(124, 58, 237),
                        new Point(0, 0), new Point(1, 1));
                    IconText.Text = "🔗";
                    TimeoutProgress.Foreground = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                    break;

                case NotificationType.Data:
                    NotificationBorder.Background = new LinearGradientBrush(
                        Color.FromRgb(6, 182, 212),
                        Color.FromRgb(8, 145, 178),
                        new Point(0, 0), new Point(1, 1));
                    IconText.Text = "📊";
                    TimeoutProgress.Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212));
                    break;
            }

            // Start fade in animation
            var fadeIn = (Storyboard)Resources["FadeInAnimation"];
            fadeIn.Begin(NotificationBorder);

            // Start auto-close timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Start progress timer
            _progressTimer = new DispatcherTimer();
            _progressTimer.Interval = TimeSpan.FromMilliseconds(50);
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - _startTime).TotalSeconds;
            var remaining = Math.Max(0, _duration - elapsed);
            TimeoutProgress.Value = (remaining / _duration) * 100;

            // Update time text
            if (elapsed < 60)
            {
                TimeText.Text = "الآن";
            }
            else if (elapsed < 3600)
            {
                TimeText.Text = $"منذ {(int)(elapsed / 60)} دقيقة";
            }
            else
            {
                TimeText.Text = $"منذ {(int)(elapsed / 3600)} ساعة";
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timer.Stop();
            _progressTimer?.Stop();
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            _progressTimer?.Stop();
            Close();
        }

        private void Notification_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Optional: Handle notification click
        }

        private void Close()
        {
            var fadeOut = (Storyboard)Resources["FadeOutAnimation"];
            fadeOut.Begin(NotificationBorder);
        }

        private void FadeOut_Completed(object sender, EventArgs e)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info,
        Connection,
        Data
    }
}
