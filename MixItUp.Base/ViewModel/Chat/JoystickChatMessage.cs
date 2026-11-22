using MixItUp.Base.Model;
using MixItUp.Base.Model.Joystick;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;

namespace MixItUp.Base.ViewModel.Chat
{
    public class JoystickChatMessage : UserChatMessageViewModel
    {
        public string ProviderID { get; }

        public JoystickChatMessage(JoystickGatewayEvent item, UserV2ViewModel user, string streamerUsername)
            : base(item.MessageID ?? Guid.NewGuid().ToString(), StreamingPlatformTypeEnum.Joystick, user)
        {
            this.ProviderID = item.MessageID;
            JObject data = item.Data;
            string visibility = data.Value<string>("visibility");
            if (visibility == "private" || visibility == "whisper")
            {
                this.TargetUsername = (data["recipient"] as JObject)?.Value<string>("username")
                    ?? (data["recipient"] as JValue)?.Value<string>()
                    ?? (data["streamer"] as JObject)?.Value<string>("username") ?? streamerUsername;
            }
            string text = data.Value<string>("text") ?? string.Empty;
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) this.AddStringMessagePart(word);
            this.PlainTextMessage = text;
        }
    }
}
