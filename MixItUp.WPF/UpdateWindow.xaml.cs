using MixItUp.Base.Model.API;
using MixItUp.Base.Model.API.Files.V2;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using MixItUp.WPF.Util;
using MixItUp.WPF.Windows;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : LoadingWindowBase
    {
        private UpdateVersionManifestModel manifest;
        private string channel;
        private string minimumVersion;
        private readonly bool isMandatory;
        private bool _shuttingDown = false;

        public UpdateWindow(UpdateVersionManifestModel manifest, string channel, bool isMandatory = false, string minimumVersion = null)
        {
            this.manifest = manifest;
            this.channel = channel;
            this.minimumVersion = minimumVersion;

            if (!BuildChannelHelper.BYPASS_UPDATE_CHECK)
            {
                this.isMandatory = isMandatory;
            }

            InitializeComponent();

            this.Initialize(this.StatusBar);
            this.AttachHyperlinkHandler();
            this.Closing += UpdateWindow_Closing;
        }

        protected override async Task OnLoaded()
        {
            _ = this.TryLoadPatreonMemberShoutout();

            this.NewVersionTextBlock.Text = this.manifest.version;
            this.CurrentVersionTextBlock.Text = VersionHelper.GetFullVersionString();

            if (string.Equals(this.channel, "preview", StringComparison.OrdinalIgnoreCase))
            {
                this.PreviewUpdateGrid.Visibility = Visibility.Visible;
            }

            this.SkipUpdateButton.Visibility = this.isMandatory ? Visibility.Collapsed : Visibility.Visible;
            this.SkipUpdateButton.IsEnabled = !this.isMandatory;

            if (this.isMandatory)
            {
                this.MandatoryBanner.Visibility = Visibility.Visible;
            }

            try
            {
                using (HttpClient client = HttpConnectionFactory.CreateClient())
                {
                    string changelogUrl = manifest.GetChangelogUrl(BuildChannelHelper.API_FILES_UPDATE_ROOT, this.channel);
                    HttpResponseMessage response = await client.GetAsync(changelogUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string markdown = await response.Content.ReadAsStringAsync();
                        this.UpdateChangelogViewer.Markdown = markdown;
                    }
                    else
                    {
                        Logger.Log(LogLevel.Warning, $"Failed to retrieve changelog from {changelogUrl}: {(int)response.StatusCode} {response.ReasonPhrase}");
                        this.UpdateChangelogViewer.Markdown = "Unable to load changelog.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                this.UpdateChangelogViewer.Markdown = "Unable to load changelog.";
            }
            TaskbarFlashHelper.Flash(this);

            await base.OnLoaded();
        }

        private async void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await this.RunAsyncOperation(async () =>
            {
                string targetVersion = this.isMandatory ? this.minimumVersion : null;
                await DownloadAndInstallUpdate(this.manifest, targetVersion, this.channel);
            });
        }

        internal static async Task<bool> DownloadAndInstallUpdate(UpdateVersionManifestModel manifest, string targetVersion = null, string targetChannel = null)
        {
            if (manifest == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(manifest.installer))
            {
                await DialogHelper.ShowMessage("Installer URL missing from the update manifest.");
                return false;
            }

            string setupFilePath = Path.Combine(Path.GetTempPath(), $"MixItUp-Setup-{Guid.NewGuid():N}.exe");

            try
            {
                using (HttpClient client = HttpConnectionFactory.CreateClient())
                using (HttpResponseMessage response = await client.GetAsync(manifest.installer, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Log(LogLevel.Warning, $"Failed to download installer from {manifest.installer}: {(int)response.StatusCode} {response.ReasonPhrase}");
                        await DialogHelper.ShowMessage("Unable to download the installer. Please try again later.");
                        return false;
                    }

                    using (Stream sourceStream = await response.Content.ReadAsStreamAsync())
                    using (FileStream destinationStream = new FileStream(setupFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await DialogHelper.ShowMessage("Unable to download the installer. Please try again later.");
                return false;
            }

            if (!File.Exists(setupFilePath))
            {
                await DialogHelper.ShowMessage("Unable to download the installer. Please try again later.");
                return false;
            }

            string installDirectory = Path.GetFullPath(AppContext.BaseDirectory);
            string launchArgs = QuoteArgument(installDirectory);
            if (!string.IsNullOrEmpty(targetVersion))
            {
                launchArgs += $" --target-version={targetVersion}";
            }
            if (!string.IsNullOrEmpty(targetChannel))
            {
                launchArgs += $" --target-channel={targetChannel}";
            }
            ServiceManager.Get<IProcessService>().LaunchProgram(setupFilePath, launchArgs);
            Application.Current.Shutdown();
            return true;
        }

        private void SkipUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PatreonButton_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.Get<IProcessService>().LaunchLink("https://www.patreon.com/mixitupbot");
        }

        private void PatreonSocialButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_patreonSocialLink))
            {
                ServiceManager.Get<IProcessService>().LaunchLink(_patreonSocialLink);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.isMandatory)
            {
                Application.Current.Shutdown();
            }
            else
            {
                this.Close();
            }
        }

        private void UpdateWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TaskbarFlashHelper.Flash(this, stop: true);
            if (this.isMandatory && !_shuttingDown)
            {
                e.Cancel = true;
                _shuttingDown = true;
                Application.Current.Shutdown();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }

        private void AttachHyperlinkHandler()
        {
            this.UpdateChangelogViewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(this.OnMarkdownLinkClicked));
        }

        private void OnMarkdownLinkClicked(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                string target = e.Uri != null ? e.Uri.AbsoluteUri : e.OriginalSource?.ToString();
                if (!string.IsNullOrWhiteSpace(target))
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    e.Handled = true;
                }
            }
            catch
            {
                // Ignore navigation failures.
            }
        }

        private string _patreonSocialLink;

        private async Task TryLoadPatreonMemberShoutout()
        {
            string[] supporterLeadInMessages = new string[]
            {
                "This update was made possible by supporters like:",
                "Special thanks to the Patreon supporters helping power this update:",
                "This release was supported by amazing community members like:",
                "With support from Patreon members like:",
                "Mix It Up development is fueled by supporters like:",
            };

            string[] patreonCallToActionMessages = new string[]
            {
                "Want to help shape the future of Mix It Up? Join us on Patreon!",
                "If you enjoy Mix It Up, consider supporting development on Patreon.",
                "Become part of the community that helps make Mix It Up possible.",
                "Support future updates and features by joining the Mix It Up Patreon community.",
                "Help keep Mix It Up growing by becoming a Patreon supporter.",
            };

            string[] communitySupportMessages = new string[]
            {
                "Mix It Up is a free, open-source project powered by the incredible support of our Patreon community.",
                "Mix It Up stays free and growing thanks to the generosity of Patreon supporters like you.",
                "Built by the community, supported by the community. Patreon supporters help keep Mix It Up moving forward.",
                "Every Patreon supporter helps fund development, improvements, and new features for Mix It Up.",
                "Mix It Up is made possible through the continued support of our amazing Patreon community.",
            };

            this.PatreonLeadInTextBlock.Text = supporterLeadInMessages[RandomHelper.GenerateRandomNumber(supporterLeadInMessages.Length)];
            this.PatreonCommunityTextBlock.Text = communitySupportMessages[RandomHelper.GenerateRandomNumber(communitySupportMessages.Length)];
            this.PatreonCTATextBlock.Text = patreonCallToActionMessages[RandomHelper.GenerateRandomNumber(patreonCallToActionMessages.Length)];

            try
            {
                PatreonMemberShoutoutModel member = await ServiceManager.Get<MixItUpService>().GetRandomPatreonMemberShoutout();
                if (member == null || string.IsNullOrWhiteSpace(member.DisplayName))
                {
                    return;
                }

                this.PatreonMemberNameTextBlock.Text = member.DisplayName;
                this.PatreonShoutoutBorder.Visibility = Visibility.Visible;

                if (!string.IsNullOrWhiteSpace(member.AvatarUrl))
                {
                    ImageHelper.SetImageSource(this.PatreonMemberAvatarImage, member.AvatarUrl, 56, 56, member.DisplayName);
                }

                if (!string.IsNullOrWhiteSpace(member.SocialMediaLink) && !string.IsNullOrWhiteSpace(member.Platform) && !string.IsNullOrWhiteSpace(member.PlatformUsername))
                {
                    _patreonSocialLink = member.SocialMediaLink;

                    string platformImage = member.Platform switch
                    {
                        "twitch" => "/Assets/Images/twitch-color_sm.png",
                        "kick" => "/Assets/Images/kick-color_sm.png",
                        "velora" => "/Assets/Images/velora-color_sm.png",
                        "youtube" => "/Assets/Images/youtube-color_sm.png",
                        _ => null,
                    };

                    if (platformImage != null)
                    {
                        this.PatreonSocialPlatformImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(platformImage, UriKind.Relative));
                        this.PatreonSocialUsernameTextBlock.Text = member.PlatformUsername;
                        this.PatreonSocialButton.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        internal static string QuoteArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "\"\"";
            }

            value = value.Trim();
            value = value.Replace("\"", "\\\"");

            int trailingSlashes = 0;
            for (int i = value.Length - 1; i >= 0 && value[i] == '\\'; i--)
            {
                trailingSlashes++;
            }

            if (trailingSlashes > 0)
            {
                value = value + new string('\\', trailingSlashes);
            }

            return $"\"{value}\"";
        }
    }
}
