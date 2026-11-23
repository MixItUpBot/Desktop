using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Dialogs
{
    /// <summary>
    /// Interaction logic for EditTestCommandParametersDialogControl.xaml
    /// </summary>
    public partial class EditTestCommandParametersDialogControl : UserControl
    {
        private readonly CommandParametersModel parameters;

        private readonly ObservableCollection<DataWrapper> values = new ObservableCollection<DataWrapper>();

        public EditTestCommandParametersDialogControl(CommandParametersModel parameters)
        {
            InitializeComponent();

            this.parameters = parameters;

            this.PlatformComboBox.ItemsSource = StreamingPlatforms.SelectablePlatforms;
            this.PlatformComboBox.SelectedItem = parameters.Platform;

            this.UserTextBox.Text = parameters.User.Username;

            this.ArgumentsTextBox.Text = string.Join(" ", this.parameters.Arguments);
            this.PlatformComboBox.SelectionChanged += this.PlatformComboBox_SelectionChanged;

            foreach (var kvp in this.parameters.SpecialIdentifiers)
            {
                if (kvp.Key != SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier)
                {
                    this.values.Add(new DataWrapper { SpecialIdentifier = kvp.Key, Value = kvp.Value });
                }
            }

            this.SpecialIdentifiersList.ItemsSource = values;

            if (ChannelSession.Settings.AlwaysUseCommandLocksWhenTestingCommands)
            {
                this.UseCommandLocks.IsChecked = true;
            }
        }

        private void PlatformComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.PlatformComboBox.SelectedItem is not StreamingPlatformTypeEnum platform) { return; }

            var previousUser = this.parameters.User;
            this.parameters.Platform = platform;
            this.parameters.User = CommandParametersModel.GetDefaultTestUser(platform);
            if (string.Equals(this.UserTextBox.Text, previousUser.Username, StringComparison.OrdinalIgnoreCase))
            {
                this.UserTextBox.Text = this.parameters.User.Username;
            }
            if (this.ArgumentsTextBox.Text == "@" + previousUser.Username)
            {
                this.ArgumentsTextBox.Text = "@" + this.parameters.User.Username;
            }
        }

        public async Task<CommandParametersModel> GetCommandParameters()
        {
            await this.parameters.SetTestUser(this.UserTextBox.Text);
            this.parameters.SetArguments(CommandParametersModel.GenerateArguments(this.ArgumentsTextBox.Text));

            this.parameters.SpecialIdentifiers.Clear();
            this.parameters.SpecialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = this.parameters.Platform.ToString();
            foreach (var value in this.values)
            {
                this.parameters.SpecialIdentifiers.Add(value.SpecialIdentifier, value.Value);
            }

            this.parameters.UseCommandLocks = this.UseCommandLocks.IsChecked.GetValueOrDefault();

            return this.parameters;
        }

        private class DataWrapper
        {
            public string SpecialIdentifier { get; set; }
            public string Value { get; set; }
        }
    }
}
