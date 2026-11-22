using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Pomodoro.Page
{
    /// <summary>
    /// Interaction logic for timeUpPage.xaml
    /// </summary>
    public partial class timeUpPage : System.Windows.Controls.Page
    {
        private readonly Action whenDone;
        private bool completed;
        private bool transitionsSuspended;
        private bool transitionPending;
        private bool alarmStarted;
        public timeUpPage(Action whenDone)
        {
            InitializeComponent();
            this.whenDone = whenDone;
            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (completed || alarmStarted) return;
            alarmStarted = true;
            Alarm._player.MediaEnded += _player_MediaEnded;
            Alarm._player.MediaFailed += Player_MediaFailed;
            if (!Alarm.playAlarm()) Complete();
        }

        public void SuspendTransitions()
        {
            transitionsSuspended = true;
        }

        public void ResumeTransitions()
        {
            transitionsSuspended = false;
            if (transitionPending) Complete();
        }

        private void Complete()
        {
            if (completed) return;
            if (transitionsSuspended)
            {
                transitionPending = true;
                return;
            }

            completed = true;
            DetachAlarm();
            whenDone();
        }

        private void DetachAlarm()
        {
            Alarm._player.MediaEnded -= _player_MediaEnded;
            Alarm._player.MediaFailed -= Player_MediaFailed;
            Alarm._player.Stop();
        }

        private void Player_MediaFailed(object? sender, ExceptionEventArgs e)
        {
            Complete();
        }

        private void _player_MediaEnded(object? sender, EventArgs e)
        {
            Complete();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        public void Cancel()
        {
            completed = true;
            transitionPending = false;
            DetachAlarm();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Complete();
        }
    }
}
