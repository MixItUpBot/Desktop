using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Util
{
    public interface IDialogShower
    {
        Task ShowMessage(string message);

        Task<bool> ShowConfirmation(string message);

        Task<bool> ShowTwitchAuthenticationMigration(TwitchAuthenticationMigrationDialogModel model);

        Task<string> ShowTextEntry(string message, string defaultValue = null, string description = null);

        Task<string> ShowDropDown(IEnumerable<string> options, string description = null);

        /// <summary>Shows a Browse-button image file picker with client-side extension + size validation.
        /// Returns the selected file path, or null if dismissed via the cancel/skip button.</summary>
        Task<string> ShowImageFileBrowser(string description, IEnumerable<string> validExtensions, long maxFileSizeBytes, string cancelText = null);

        Task<object> ShowCustom(object dialog);

        Task<object> ShowCustomTimed(object dialog, int timeout);

        Task<CommandParametersModel> ShowEditTestCommandParametersDialog(CommandParametersModel parameters);

        void CloseCurrent();
    }

    public static class DialogHelper
    {
        public static IDialogShower dialogShower;

        public static void Initialize(IDialogShower dialogShower)
        {
            DialogHelper.dialogShower = dialogShower;
        }

        public static async Task ShowMessage(string message) { await DialogHelper.dialogShower.ShowMessage(message); }

        public static async Task<bool> ShowConfirmation(string message) { return await DialogHelper.dialogShower.ShowConfirmation(message); }

        public static Task<bool> ShowTwitchAuthenticationMigration(TwitchAuthenticationMigrationDialogModel model) { return DialogHelper.dialogShower.ShowTwitchAuthenticationMigration(model); }

        public static async Task<string> ShowTextEntry(string message, string defaultValue = null, string description = null) { return await DialogHelper.dialogShower.ShowTextEntry(message, defaultValue, description); }

        public static async Task<string> ShowDropDown(IEnumerable<string> options, string description = null) { return await DialogHelper.dialogShower.ShowDropDown(options, description); }

        public static async Task<string> ShowImageFileBrowser(string description, IEnumerable<string> validExtensions, long maxFileSizeBytes, string cancelText = null) { return await DialogHelper.dialogShower.ShowImageFileBrowser(description, validExtensions, maxFileSizeBytes, cancelText); }

        public static async Task<object> ShowEnumDropDown<T>(IEnumerable<T> options, string description = null)
        {
            Dictionary<string, T> mapping = new Dictionary<string, T>();
            foreach (T option in options)
            {
                mapping[EnumLocalizationHelper.GetLocalizedName(option)] = option;
            }

            string result = await DialogHelper.dialogShower.ShowDropDown(mapping.Keys.OrderBy(s => s), description);
            if (!string.IsNullOrWhiteSpace(result) && mapping.ContainsKey(result))
            {
                return mapping[result];
            }
            return null;
        }

        public static async Task<object> ShowCustom(object dialog) { return await DialogHelper.dialogShower.ShowCustom(dialog); }

        public static async Task<object> ShowCustomTimed(object dialog, int timeout) { return await DialogHelper.dialogShower.ShowCustomTimed(dialog, timeout); }

        public static async Task<CommandParametersModel> ShowEditTestCommandParametersDialog(CommandParametersModel parameters)
        {
            CommandParametersModel result = await DialogHelper.dialogShower.ShowEditTestCommandParametersDialog(parameters);
            if (result != null)
            {
                var emptyKeys = result.SpecialIdentifiers
                    .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToArray();

                foreach (var key in emptyKeys)
                {
                    result.SpecialIdentifiers.Remove(key);
                }
            }
            return result;
        }

        public static async Task ShowFailedResult(Result result) { await DialogHelper.ShowFailedResults(new List<Result>() { result }); }

        public static async Task ShowFailedResults(IEnumerable<Result> results)
        {
            if (results.Any(r => !r.Success))
            {
                StringBuilder error = new StringBuilder();
                error.AppendLine(MixItUp.Base.Resources.TheFollowingErrorsMustBeFixed);
                error.AppendLine();
                foreach (Result result in results.Where(r => !r.Success))
                {
                    error.AppendLine(" - " + result.Message);
                }
                await DialogHelper.ShowMessage(error.ToString());
            }
        }

        public static void CloseCurrent() { DialogHelper.dialogShower.CloseCurrent(); }
    }
}
