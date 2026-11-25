using Newtonsoft.Json.Linq;
using System;

namespace MixItUp.Base.Model.Joystick
{
    public sealed class JoystickGatewayEvent
    {
        public string Type { get; private set; }
        public string ID { get; private set; }
        public string ChannelID { get; private set; }
        public JObject Data { get; private set; }
        public DateTimeOffset? OccurredAt { get; private set; }

        public string MessageID => ReadString(this.Data["message_id"]) ?? ReadString(this.Data["id"]) ?? this.ID;

        private static string ReadString(JToken value)
        {
            return value?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value) ? (string)value : null;
        }

        public static JoystickGatewayEvent FromFrame(JObject frame, string channelID)
        {
            if (string.IsNullOrWhiteSpace(channelID) || !(frame?["message"] is JObject message)
                || !string.Equals(ReadString(message["channel_id"]) ?? ReadString(message["channelId"]), channelID, StringComparison.Ordinal)
                || !(message["data"] is JObject data))
            {
                return null;
            }

            string type = ReadString(message["type"]);
            string id = ReadString(message["id"]);
            if (string.IsNullOrWhiteSpace(type))
            {
                return null;
            }

            var timestamp = message["occurred_at"];
            DateTimeOffset? occurredAt = timestamp?.Type == JTokenType.Date ? timestamp.ToObject<DateTimeOffset>() :
                DateTimeOffset.TryParse(ReadString(timestamp), out var parsed) ? parsed : null;
            return new JoystickGatewayEvent { Type = type, ID = id, ChannelID = channelID, Data = data, OccurredAt = occurredAt };
        }

    }
}
