using MixItUp.Base;
using MixItUp.Base.Util;
using MixItUp.WPF.Services.Diagnostics;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace MixItUp.WPF.Windows
{
    public partial class DiagnosticsWindow : Window
    {
        private readonly DesktopDiagnosticsSession session = new DesktopDiagnosticsSession();
        private CancellationTokenSource cancellation;
        private bool closeRequested;

        public DiagnosticsWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
            Closed += (_, _) => session.Dispose();
        }

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            if (Consent.IsChecked != true || !System.Text.RegularExpressions.Regex.IsMatch(AccessCode.Text, "^[A-Z0-9]{6}$"))
            {
                Status.Text = MixItUp.Base.Resources.DiagnosticsAccessCode;
                return;
            }
            RunButton.IsEnabled = AccessCode.IsEnabled = Consent.IsEnabled = false;
            Progress.Visibility = Visibility.Visible;
            Status.Text = MixItUp.Base.Resources.CollectDiagnostics;
            cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            bool completed = false;
            try
            {
                completed = await session.Run(AccessCode.Text, () => System.Threading.Tasks.Task.FromResult(
                    MessageBox.Show(this, MixItUp.Base.Resources.DiagnosticsPartialConsent, MixItUp.Base.Resources.CollectDiagnostics, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes), cancellation.Token);
                Status.Text = completed ? MixItUp.Base.Resources.DiagnosticsSuccess : MixItUp.Base.Resources.DiagnosticsFailure;
            }
            catch (OperationCanceledException) { Status.Text = MixItUp.Base.Resources.DiagnosticsFailure; }
            catch (Exception ex) { Logger.Log(ex); Status.Text = MixItUp.Base.Resources.DiagnosticsFailure; }
            finally
            {
                cancellation.Dispose(); cancellation = null;
                Progress.Visibility = Visibility.Collapsed;
                RunButton.IsEnabled = AccessCode.IsEnabled = Consent.IsEnabled = !completed;
                if (closeRequested) Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { Close(); }
        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (cancellation != null) { closeRequested = true; cancellation.Cancel(); e.Cancel = true; }
        }
    }
}
