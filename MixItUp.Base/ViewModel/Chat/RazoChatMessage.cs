using MixItUp.Base.Model;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;

namespace MixItUp.Base.ViewModel.Chat
{
    public class RazoChatMessage : UserChatMessageViewModel
    {
        public string ProviderID { get; }
        public RazoChatMessage(string channel, JObject data, UserV2ViewModel user)
            : base(LocalID(channel, data.Value<string>("id")), StreamingPlatformTypeEnum.Razo, user)
        {
            this.ProviderID = data.Value<string>("id");
            if (data["sent_at"]?.ToObject<DateTimeOffset?>() is DateTimeOffset when) this.Timestamp = when;
            foreach (string part in (data.Value<string>("message") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)) this.AddStringMessagePart(part);
        }

        public static string LocalID(string channel, string id) => "razo:" + channel.ToLowerInvariant() + ":" + id;
    }
}
