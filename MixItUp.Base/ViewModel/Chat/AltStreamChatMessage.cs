using MixItUp.Base.Model;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MixItUp.Base.ViewModel.Chat
{
    public class AltStreamChatMessage : UserChatMessageViewModel
    {
        public string ReplyMessageID { get; }
        public bool IsHighlighted { get; }
        public AltStreamChatMessage(string prefix, JObject frame, UserV2ViewModel user, IReadOnlyDictionary<string, ChatEmoteViewModelBase> emotes = null)
            : base(prefix + ((frame["message"] as JObject)?.Value<string>("id") ?? frame.Value<string>("id")), StreamingPlatformTypeEnum.AltStream, user)
        {
            this.IsHighlighted = frame.Value<string>("type") == "highlight";
            if ((frame["reply"] as JObject)?.Value<string>("messageId") is string reply) this.ReplyMessageID = prefix + reply;
            if (frame.Value<long?>("sentAt") is long time && time >= 0 && time <= 253402300799999)
                this.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(time);
            string text = (frame["message"] as JObject)?.Value<string>("text");
            foreach (var word in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (emotes != null && emotes.TryGetValue(word, out var emote)) this.MessageParts.Add(emote);
                else this.MessageParts.Add(word);
            }
            this.PlainTextMessage = text ?? string.Empty;
            if ((frame["message"] as JObject)?["sticker"] is JObject sticker && ReadEmote(sticker) is ChatEmoteViewModelBase image)
                this.MessageParts.Add(image);
        }

        internal static ChatEmoteViewModelBase ReadEmote(JObject data)
        {
            string name = data.Value<string>("name"), url = data.Value<string>("url");
            return !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url) &&
                Uri.TryCreate(new Uri("https://altstream.online"), url, out var image) && image.Scheme == Uri.UriSchemeHttps
                ? new Sticker(name, image.AbsoluteUri) : null;
        }

        private sealed class Sticker : ChatEmoteViewModelBase
        {
            public Sticker(string name, string url) { this.Name = name; this.ImageURL = url; }
        }
    }
}
