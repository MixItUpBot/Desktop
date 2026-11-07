using MixItUp.Base;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModels;
using MixItUp.WPF.Controls;
using MixItUp.WPF.Util;
using MixItUp.Base.Util;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Windows
{
    public class LoadingWindowBase : Window
    {
        public UIViewModelBase ViewModel { get; protected set; }

        private int asyncOperationCount = 0;

        private LoadingStatusBar statusBar;

        public LoadingWindowBase(UIViewModelBase viewModel)
            : this()
        {
            this.DataContext = this.ViewModel = viewModel;
        }

        public LoadingWindowBase()
        {
            this.Loaded += LoadingWindowBase_Loaded;
            this.Closing += LoadingWindowBase_Closing;
            this.Activated += LoadingWindowBase_Activated;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public void Initialize(LoadingStatusBar statusBar)
        {
            this.statusBar = statusBar;
        }

        public async Task RunAsyncOperation(Func<Task> action)
        {
            try
            {
                this.StartLoadingOperation();

                await action();
            }
            finally
            {
                this.EndLoadingOperation();
            }
        }

        public async Task<T> RunAsyncOperation<T>(Func<Task<T>> action)
        {
            try
            {
                this.StartLoadingOperation();

                T result = await action();

                return result;
            }
            finally
            {
                this.EndLoadingOperation();
            }
        }

        public void StartLoadingOperation()
        {
            try
            {
                this.asyncOperationCount++;
                this.IsEnabled = false;
                this.statusBar.ShowProgressBar();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public void EndLoadingOperation()
        {
            try
            {
                this.asyncOperationCount--;
                if (this.asyncOperationCount <= 0)
                {
                    this.asyncOperationCount = 0;
                    this.statusBar.HideProgressBar();
                    this.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        protected void ShowMainWindow(Window window)
        {
            // Every post-login path funnels through here, so this is where the user's own font and
            // scale take effect. The login window itself deliberately stays on the packaged defaults
            // (see App.OnStartup), which also means an unreadable font or an extreme scale can never
            // leave someone unable to read the login screen well enough to fix it.
            try
            {
                ServiceManager.Get<IThemeService>().ApplyFont(ChannelSession.AppSettings.UIFontFamily);
                ServiceManager.Get<IThemeService>().ApplyFontScale(ChannelSession.AppSettings.UIScale / 100.0);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            Application.Current.MainWindow = window;
            window.Show();
        }

        protected virtual Task OnLoaded() { return Task.CompletedTask; }

        protected virtual Task OnClosing() { return Task.CompletedTask; }
        protected virtual bool CanClose() { return true; }
        protected virtual Task OnCloseBlocked() { return Task.CompletedTask; }

        private async void LoadingWindowBase_Loaded(object sender, RoutedEventArgs e)
        {
            await this.RunAsyncOperation(async () =>
            {
                await this.OnLoaded();
            });
        }

        private async void LoadingWindowBase_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!this.CanClose())
            {
                e.Cancel = true;
                await this.OnCloseBlocked();
                return;
            }
            await this.RunAsyncOperation(async () =>
            {
                await this.OnClosing();
            });
        }

        private void LoadingWindowBase_Activated(object sender, EventArgs e)
        {
            WPFDialogShower.SetLastActiveWindow(this);
        }
    }
}
