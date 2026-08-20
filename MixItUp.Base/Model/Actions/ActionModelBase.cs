using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum ActionTypeEnum
    {
        Custom = 0,
        [Name("ChatMessage")]
        Chat,
        [Name("ConsumablesCurrencyRankEtc")]
        Consumables,
        ExternalProgram,
        [Name("InputKeyboardAndMouse")]
        Input,
        [Name("OverlayImagesAndVideos")]
        Overlay,
        Sound,
        Wait,
        [Name("CounterCreateAndUpdate")]
        Counter,
        GameQueue,
        TextToSpeech,
        WebRequest,
        SpecialIdentifier,
        [Name("FileReadAndWrite")]
        File,
        Discord,
        [Obsolete]
        Translation,
        [Obsolete]
        Twitter,
        Conditional,
        StreamingSoftware,
        Streamlabs,
        Command,
        Serial,
        Moderation,
        [Obsolete]
        OvrStream,
        IFTTT,
        Twitch,
        PixelChat,
        VTubeStudio,
        Voicemod,
        YouTube,
        [Obsolete]
        Trovo,
        PolyPop,
        SAMMI,
        [Obsolete]
        InfiniteAlbum,
        TITS,
        MusicPlayer,
        LumiaStream,
        Random,
        Script,
        Group,
        Repeat,
        VTSPog,
        MtionStudio = 42,
        MeldStudio = 43,
        Kick = 44,
        Velora = 45,
        Veadotube = 46,
        PlatformMessage = 47,
        UserLookup = 48,
        VPZone = 49,
        VConnect = 50,
        RahiTuber = 51,
        // Values are persisted numerically in saved command data. Always append new
        // actions with the next value, never insert into the order above.
    }

    [DataContract]
    public abstract class ActionModelBase
    {
        [DataMember]
        public Guid ID { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public ActionTypeEnum Type { get; set; }

        [DataMember]
        public bool Enabled { get; set; } = true;

        public ActionModelBase(ActionTypeEnum type)
        {
            this.ID = Guid.NewGuid();
            this.Type = type;
            this.Name = EnumLocalizationHelper.GetLocalizedName(this.Type);
            this.Enabled = true;
        }

        [Obsolete]
        public ActionModelBase() { }

        private static readonly ConcurrentDictionary<System.Type, ActionTypeEnum?> classActionTypes = new ConcurrentDictionary<System.Type, ActionTypeEnum?>();

        // Data saved by builds where the ActionTypeEnum values shifted can carry the wrong Type.
        // The concrete class is authoritative, so repair Type from it on load.
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            ActionTypeEnum? actual = classActionTypes.GetOrAdd(this.GetType(), type =>
            {
                const string suffix = "ActionModel";
                if (type.Name.EndsWith(suffix) && Enum.TryParse(type.Name.Substring(0, type.Name.Length - suffix.Length), out ActionTypeEnum result))
                {
                    return result;
                }
                return null;
            });

            if (actual.HasValue && this.Type != actual.Value)
            {
                this.Type = actual.Value;
            }
        }

        public virtual async Task TestPerform(Dictionary<string, string> specialIdentifiers)
        {
            await this.Perform(new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.All, new List<string>() { "@" + ChannelSession.User.Username }, specialIdentifiers) { TargetUser = ChannelSession.User });
        }

        public async Task Perform(CommandParametersModel parameters)
        {
            if (this.Enabled)
            {
                Logger.Log(LogLevel.Debug, $"Starting action performing: {this}");

                ServiceManager.Get<ITelemetryService>().TrackAction(this.Type);

                await this.PerformInternal(parameters);
            }
        }

        protected abstract Task PerformInternal(CommandParametersModel parameters);

        protected static async Task<string> ReplaceStringWithSpecialModifiers(string str, CommandParametersModel parameters, bool encode = false)
        {
            return await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(str, parameters, encode);
        }

        public override string ToString() { return string.Format("{0} - {1}", this.ID, this.Name); }

        public int CompareTo(object obj)
        {
            if (obj is ActionModelBase)
            {
                return this.CompareTo((ActionModelBase)obj);
            }
            return 0;
        }

        public int CompareTo(ActionModelBase other) { return this.Name.CompareTo(other.Name); }

        public override bool Equals(object obj)
        {
            if (obj is ActionModelBase)
            {
                return this.Equals((ActionModelBase)obj);
            }
            return false;
        }

        public bool Equals(ActionModelBase other) { return this.ID.Equals(other.ID); }

        public override int GetHashCode() { return this.ID.GetHashCode(); }
    }
}
