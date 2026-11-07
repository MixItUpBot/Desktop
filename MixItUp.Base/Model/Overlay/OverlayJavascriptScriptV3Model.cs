using MixItUp.Base.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    public class OverlayJavascriptScriptV3Model : OverlayItemV3ModelBase
    {
        public const string ScriptCompletePacketType = "ScriptComplete";

        private readonly TaskCompletionSource<string> completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

#pragma warning disable CS0612 // Type or member is obsolete
        public OverlayJavascriptScriptV3Model(string javascript) : base(OverlayItemV3Type.JavascriptScript)
#pragma warning restore CS0612 // Type or member is obsolete
        {
            // Install handlers in a separate script so syntax errors in user code are caught too.
            this.HTML = @"<script>
                function reportScriptError(error) {
                    window.top.postMessage(JSON.stringify({ Type: 'ScriptComplete', ID: '{ID}', Error: String(error) }), '*');
                }
                window.addEventListener('error', function (event) { reportScriptError(event.message); });
                window.addEventListener('unhandledrejection', function (event) { reportScriptError(event.reason); });
                </script>";
            this.Javascript = javascript;
        }

        public override async Task ProcessPacket(OverlayV3Packet packet)
        {
            await base.ProcessPacket(packet);

            if (string.Equals(packet.Type, OverlayJavascriptScriptV3Model.ScriptCompletePacketType))
            {
                if (packet.Data.TryGetValue("Error", out JToken error) && error != null)
                {
                    this.completion.TrySetException(new InvalidOperationException(error.ToString()));
                }
                else
                {
                    this.completion.TrySetResult(packet.Data.TryGetValue("Result", out JToken value) ? value?.ToString() : null);
                }
            }
        }

        public Task<string> WaitForResult()
        {
            return this.completion.Task;
        }
    }
}
