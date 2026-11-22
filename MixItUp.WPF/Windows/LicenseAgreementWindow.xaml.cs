using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using MixItUp.Updates;

namespace MixItUp.WPF.Windows
{
    /// <summary>
    /// Interaction logic for LicenseAgreementWindow.xaml
    /// </summary>
    public partial class LicenseAgreementWindow : Window
    {
        public bool Accepted = false;

        public LicenseAgreementWindow()
        {
            InitializeComponent();
        }

        public LicenseAgreementWindow(string agreement) : this()
        {
            AgreementContent.Markdown = agreement;
        }

        internal static bool AcceptInstalledAgreement()
        {
            using var stream = typeof(LicenseAgreementWindow).Assembly.GetManifestResourceStream("MixItUp.EULA.md");
            using var reader = new StreamReader(stream);
            string agreement = reader.ReadToEnd();
            Match version = Regex.Match(agreement, @"(?m)^> \*\*Version:\*\*[ \t]*(\d+\.\d+\.\d+)[ \t]*\r?$");
            if (!version.Success) { throw new InvalidDataException("Missing bundled EULA version."); }
            return ConfirmAgreement(agreement, version.Groups[1].Value);
        }

        internal static bool ConfirmAgreement(string agreement, string version)
        {
            string root = AppContext.BaseDirectory;
            if (new PendingUpdate { EulaVersion = version }.LicenseAccepted(root)) { return true; }
            var dialog = new LicenseAgreementWindow(agreement);
            dialog.ShowDialog();
            if (!dialog.Accepted) { return false; }
            string receipt = Path.Combine(root, PendingUpdate.EulaReceipt);
            string temporary = receipt + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporary, new[] { version, DateTime.UtcNow.ToString("o") });
                File.Move(temporary, receipt, true);
            }
            finally { File.Delete(temporary); }
            return true;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            this.Accepted = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
