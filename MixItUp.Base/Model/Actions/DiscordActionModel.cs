using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum DiscordActionTypeEnum
    {
        SendMessage,
        MuteSelf,
        DeafenSelf,
        SendEmbed,
        ChangeChannelName,
    }

    [DataContract]
    public class DiscordActionModel : ActionModelBase
    {
        public static DiscordActionModel CreateForChatMessage(DiscordChannel channel, string message, string filePath) { return new DiscordActionModel(DiscordActionTypeEnum.SendMessage) { ChannelID = channel.ID, MessageText = message, FilePath = filePath }; }

        public static DiscordActionModel CreateForChannelName(DiscordChannel channel, string name) { return new DiscordActionModel(DiscordActionTypeEnum.ChangeChannelName) { ChannelID = channel.ID, MessageText = name }; }

        public static DiscordActionModel CreateForMuteSelf(bool mute) { return new DiscordActionModel(DiscordActionTypeEnum.MuteSelf) { ShouldMuteDeafen = mute }; }

        public static DiscordActionModel CreateForDeafenSelf(bool deafen) { return new DiscordActionModel(DiscordActionTypeEnum.DeafenSelf) { ShouldMuteDeafen = deafen }; }

        public static DiscordActionModel CreateForEmbed(DiscordChannel channel, string messageText, string filePath,
            string embedTitle, string embedDescription, string embedColor, string embedURL,
            string embedThumbnailURL, string embedImageURL, string embedAuthorName, string embedAuthorURL, string embedAuthorIconURL,
            string embedFooterText, string embedFooterIconURL, bool embedIncludeTimestamp)
        {
            return new DiscordActionModel(DiscordActionTypeEnum.SendEmbed)
            {
                ChannelID = channel.ID,
                MessageText = messageText,
                FilePath = filePath,
                EmbedTitle = embedTitle,
                EmbedDescription = embedDescription,
                EmbedColor = embedColor,
                EmbedURL = embedURL,
                EmbedThumbnailURL = embedThumbnailURL,
                EmbedImageURL = embedImageURL,
                EmbedAuthorName = embedAuthorName,
                EmbedAuthorURL = embedAuthorURL,
                EmbedAuthorIconURL = embedAuthorIconURL,
                EmbedFooterText = embedFooterText,
                EmbedFooterIconURL = embedFooterIconURL,
                EmbedIncludeTimestamp = embedIncludeTimestamp
            };
        }

        [DataMember]
        public DiscordActionTypeEnum ActionType { get; set; }

        [DataMember]
        public string ChannelID { get; set; }

        [DataMember]
        public string MessageText { get; set; }
        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public bool ShouldMuteDeafen { get; set; }

        [DataMember]
        public string EmbedTitle { get; set; }
        [DataMember]
        public string EmbedDescription { get; set; }
        [DataMember]
        public string EmbedColor { get; set; }
        [DataMember]
        public string EmbedURL { get; set; }
        [DataMember]
        public string EmbedThumbnailURL { get; set; }
        [DataMember]
        public string EmbedImageURL { get; set; }
        [DataMember]
        public string EmbedAuthorName { get; set; }
        [DataMember]
        public string EmbedAuthorURL { get; set; }
        [DataMember]
        public string EmbedAuthorIconURL { get; set; }
        [DataMember]
        public string EmbedFooterText { get; set; }
        [DataMember]
        public string EmbedFooterIconURL { get; set; }
        [DataMember]
        public bool EmbedIncludeTimestamp { get; set; }

        private DiscordChannel channel;

        public DiscordActionModel(DiscordActionTypeEnum actionType)
            : base(ActionTypeEnum.Discord)
        {
            this.ActionType = actionType;
        }

        [Obsolete]
        public DiscordActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (this.ActionType == DiscordActionTypeEnum.ChangeChannelName)
            {
                string name = await ReplaceStringWithSpecialModifiers(this.MessageText, parameters);
                Result result = await ServiceManager.Get<DiscordService>().ChangeChannelName(this.ChannelID, name);
                if (!result.Success) { throw new InvalidOperationException(result.Message); }
            }
            else if (this.ActionType == DiscordActionTypeEnum.SendMessage)
            {
                if (this.channel == null)
                {
                    this.channel = await ServiceManager.Get<DiscordService>().GetChannel(this.ChannelID);
                }

                if (this.channel != null)
                {
                    string message = await ReplaceStringWithSpecialModifiers(this.MessageText, parameters);
                    string filePath = await ReplaceStringWithSpecialModifiers(this.FilePath, parameters);

                    this.LogMissingFiles(parameters.InitialCommandID, filePath);

                    await ServiceManager.Get<DiscordService>().CreateMessage(this.channel, message, filePath);
                }
            }
            else if (this.ActionType == DiscordActionTypeEnum.MuteSelf)
            {
                await ServiceManager.Get<DiscordService>().MuteServerMember(ServiceManager.Get<DiscordService>().Server, ServiceManager.Get<DiscordService>().User, this.ShouldMuteDeafen);
            }
            else if (this.ActionType == DiscordActionTypeEnum.DeafenSelf)
            {
                await ServiceManager.Get<DiscordService>().DeafenServerMember(ServiceManager.Get<DiscordService>().Server, ServiceManager.Get<DiscordService>().User, this.ShouldMuteDeafen);
            }
            else if (this.ActionType == DiscordActionTypeEnum.SendEmbed)
            {
                if (this.channel == null)
                {
                    this.channel = await ServiceManager.Get<DiscordService>().GetChannel(this.ChannelID);
                }

                if (this.channel != null)
                {
                    string message = await ReplaceStringWithSpecialModifiers(this.MessageText, parameters);
                    string filePath = await ReplaceStringWithSpecialModifiers(this.FilePath, parameters);

                    this.LogMissingFiles(parameters.InitialCommandID, filePath);

                    string embedTitle = await ReplaceStringWithSpecialModifiers(this.EmbedTitle, parameters);
                    string embedDescription = await ReplaceStringWithSpecialModifiers(this.EmbedDescription, parameters);
                    string embedColor = await ReplaceStringWithSpecialModifiers(this.EmbedColor, parameters);
                    string embedURL = await ReplaceStringWithSpecialModifiers(this.EmbedURL, parameters);
                    string embedThumbnailURL = await ReplaceStringWithSpecialModifiers(this.EmbedThumbnailURL, parameters);
                    string embedImageURL = await ReplaceStringWithSpecialModifiers(this.EmbedImageURL, parameters);
                    string embedAuthorName = await ReplaceStringWithSpecialModifiers(this.EmbedAuthorName, parameters);
                    string embedAuthorURL = await ReplaceStringWithSpecialModifiers(this.EmbedAuthorURL, parameters);
                    string embedAuthorIconURL = await ReplaceStringWithSpecialModifiers(this.EmbedAuthorIconURL, parameters);
                    string embedFooterText = await ReplaceStringWithSpecialModifiers(this.EmbedFooterText, parameters);
                    string embedFooterIconURL = await ReplaceStringWithSpecialModifiers(this.EmbedFooterIconURL, parameters);

                    DiscordEmbed embed = new DiscordEmbed()
                    {
                        Title = embedTitle,
                        Description = embedDescription,
                        Color = embedColor,
                        URL = embedURL,
                        ThumbnailURL = embedThumbnailURL,
                        ImageURL = embedImageURL,
                        AuthorName = embedAuthorName,
                        AuthorURL = embedAuthorURL,
                        AuthorIconURL = embedAuthorIconURL,
                        FooterText = embedFooterText,
                        FooterIconURL = embedFooterIconURL,
                        IncludeTimestamp = this.EmbedIncludeTimestamp
                    };

                    await ServiceManager.Get<DiscordService>().CreateMessageWithEmbed(this.channel, message, embed, filePath);
                }
            }
        }

        private void LogMissingFiles(Guid commandID, string filePath)
        {
            foreach (string path in this.GetFilePaths(filePath))
            {
                if (!ServiceManager.Get<IFileService>().IsURLPath(path) && !ServiceManager.Get<IFileService>().FileExists(path))
                {
                    Logger.Log(LogLevel.Error, $"Command: {commandID} - Discord Action - File does not exist: {path}");
                }
            }
        }

        private IEnumerable<string> GetFilePaths(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Enumerable.Empty<string>();
            }
            return filePath.Split('|').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p));
        }
    }
}
