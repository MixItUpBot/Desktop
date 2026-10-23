using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services
{
    public class WindowsTelemetryService : ITelemetryService
    {
        private const int MaxTelemetryEventsPerSession = 1000;
        private const string ServiceName = "mixitup-desktop";

        private static readonly ActivitySource ActivitySource = new ActivitySource(ServiceName);

        private static readonly Meter Meter = new Meter(ServiceName);

        private readonly Counter<long> loginCounter;
        private readonly Counter<long> commandCounter;
        private readonly Counter<long> actionCounter;
        private readonly Counter<long> serviceCounter;
        private readonly Counter<long> exceptionCounter;
        private readonly Counter<long> featureUsageCounter;

        private TracerProvider tracerProvider;
        private MeterProvider meterProvider;

        private string userId;
        private string sessionId;
        private int totalEventsSent = 0;

        public WindowsTelemetryService()
        {
            this.sessionId = Guid.NewGuid().ToString();

            this.loginCounter = Meter.CreateCounter<long>("mixitup.logins", "count", "Number of user logins");
            this.commandCounter = Meter.CreateCounter<long>("mixitup.commands", "count", "Number of commands executed");
            this.actionCounter = Meter.CreateCounter<long>("mixitup.actions", "count", "Number of actions executed");
            this.serviceCounter = Meter.CreateCounter<long>("mixitup.services", "count", "Number of service connections");
            this.exceptionCounter = Meter.CreateCounter<long>("mixitup.exceptions", "count", "Number of exceptions");
            this.featureUsageCounter = Meter.CreateCounter<long>("mixitup.feature_usage", "count", "Feature usage");
        }

        public string Name { get { return "Telemetry"; } }

        public bool IsConnected { get; private set; }

        public Task<Result> Connect()
        {
            try
            {
                string endpoint = ServiceManager.Get<SecretsService>().GetSecret("OtelEndpoint");
                string secret = ServiceManager.Get<SecretsService>().GetSecret("OtelSecret");

                string headers = null;
                if (!string.IsNullOrEmpty(secret))
                {
                    headers = $"Authorization=Bearer {secret}";
                }

                var appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";

                var resourceBuilder = ResourceBuilder.CreateDefault()
                    .AddService(
                        serviceName: ServiceName,
                        serviceVersion: appVersion,
                        serviceInstanceId: this.sessionId)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = ChannelSession.IsDebug() ? "development" : "production",
                        ["app.release_channel"] = BuildChannelHelper.GetUpdateChannel(ChannelSession.AppSettings.UpdateChannel, ChannelSession.AppSettings.PreviewProgram, false),
                        ["app.language"] = Languages.GetLangauge().ToString(),
                        ["os.version"] = Environment.OSVersion.Version.ToString(),
                        ["os.locale"] = CultureInfo.CurrentUICulture.Name
                    });

                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(ServiceName)
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(endpoint + "v1/traces");
                        o.Protocol = OtlpExportProtocol.HttpProtobuf;
                        if (!string.IsNullOrEmpty(headers))
                        {
                            o.Headers = headers;
                        }
                    })
                    //.AddConsoleExporter()
                    .Build();

                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(ServiceName)
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(endpoint + "v1/metrics");
                        o.Protocol = OtlpExportProtocol.HttpProtobuf;
                        if (!string.IsNullOrEmpty(headers))
                        {
                            o.Headers = headers;
                        }
                    })
                    //.AddConsoleExporter()
                    .Build();

                this.IsConnected = true;
                return Task.FromResult(new Result());
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                this.IsConnected = false;
                return Task.FromResult(new Result());
            }
        }

        public async Task Disconnect()
        {
            try
            {
                this.tracerProvider?.ForceFlush();
                this.meterProvider?.ForceFlush();

                await Task.Delay(1000);

                this.tracerProvider?.Dispose();
                this.meterProvider?.Dispose();

                this.tracerProvider = null;
                this.meterProvider = null;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            this.IsConnected = false;
        }

        public void TrackException(Exception ex)
        {
            this.TrySendEvent(() =>
            {
                using var activity = ActivitySource.StartActivity("Exception", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity.SetTag("exception.type", ex.GetType().FullName);
                    activity.SetTag("exception.message", ex.Message);
                    activity.SetTag("exception.stacktrace", ex.StackTrace);
                    activity.SetTag("user.id", this.userId);
                    activity.SetTag("session.id", this.sessionId);

                    activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message }
                    }));
                }

                this.exceptionCounter.Add(1, new KeyValuePair<string, object>("exception.type", ex.GetType().Name));
            });
        }

        public void TrackLogin(string userID, IEnumerable<StreamingPlatformTypeEnum> platforms)
        {
            this.TrySendEvent(() =>
            {
                var platformsString = string.Join(", ", platforms.Select(p => p.ToString()));
                var appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";
                var appLanguage = Languages.GetLangauge().ToString();
                var osVersion = Environment.OSVersion.Version.ToString();
                var releaseChannel = BuildChannelHelper.GetUpdateChannel(ChannelSession.AppSettings.UpdateChannel, ChannelSession.AppSettings.PreviewProgram, false);

                using var activity = ActivitySource.StartActivity("Login", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag("user.id", userID);
                    activity.SetTag("platforms", platformsString);
                    activity.SetTag("session.id", this.sessionId);
                }

                this.loginCounter.Add(1,
                    new KeyValuePair<string, object>("platforms", platformsString),
                    new KeyValuePair<string, object>("user.id", userID),
                    new KeyValuePair<string, object>("app.version", appVersion),
                    new KeyValuePair<string, object>("app.language", appLanguage),
                    new KeyValuePair<string, object>("os.version", osVersion),
                    new KeyValuePair<string, object>("app.release.channel", releaseChannel));
            });
        }

        public void TrackCommand(CommandTypeEnum type, string details = null)
        {
            this.TrySendEvent(() =>
            {
                var typeName = EnumHelper.GetEnumName(type);
                details ??= "None";

                using var activity = ActivitySource.StartActivity("Command", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag("command.type", typeName);
                    activity.SetTag("command.details", details);
                    activity.SetTag("user.id", this.userId);
                    activity.SetTag("session.id", this.sessionId);
                }

                this.commandCounter.Add(1,
                    new KeyValuePair<string, object>("command.type", typeName),
                    new KeyValuePair<string, object>("command.details", details));
            });
        }

        public void TrackAction(ActionTypeEnum type)
        {
            this.TrySendEvent(() =>
            {
                var typeName = EnumHelper.GetEnumName(type);

                using var activity = ActivitySource.StartActivity("Action", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag("action.type", typeName);
                    activity.SetTag("user.id", this.userId);
                    activity.SetTag("session.id", this.sessionId);
                }

                this.actionCounter.Add(1,
                    new KeyValuePair<string, object>("action.type", typeName));
            });
        }

        public void TrackService(string type)
        {
            this.TrySendEvent(() =>
            {
                using var activity = ActivitySource.StartActivity("ServiceConnection", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag("service.type", type);
                    activity.SetTag("user.id", this.userId);
                    activity.SetTag("session.id", this.sessionId);
                }

                this.serviceCounter.Add(1,
                    new KeyValuePair<string, object>("service.type", type));
            });
        }

        public void SetUserID(string id)
        {
            this.userId = id;
        }

        public void TrackFeature(string featureName, Dictionary<string, object> properties = null)
        {
            this.TrySendEvent(() =>
            {
                using var activity = ActivitySource.StartActivity("FeatureUsage", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag("feature.name", featureName);
                    activity.SetTag("user.id", this.userId);
                    activity.SetTag("session.id", this.sessionId);

                    if (properties != null)
                    {
                        foreach (var kvp in properties)
                        {
                            activity.SetTag(kvp.Key, kvp.Value);
                        }
                    }
                }

                this.featureUsageCounter.Add(1,
                    new KeyValuePair<string, object>("feature.name", featureName));
            });
        }

        private void TrySendEvent(Action eventAction)
        {
            if (ChannelSession.Settings != null && ChannelSession.Settings.OptOutTracking)
            {
                return;
            }

            if (!this.IsConnected)
            {
                return;
            }

            if (this.totalEventsSent < MaxTelemetryEventsPerSession)
            {
                try
                {
                    eventAction();
                    this.totalEventsSent++;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }
    }
}