using MixItUp.Base.Services;
using MixItUp.Base.Util;
using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public class LocalOAuthKestrelServer : KestrelServerBase
    {
        public const int REDIRECT_PORT = 8919;

        public const string REDIRECT_URL = "http://localhost:8919/";

        /// <summary>How long to wait for the platform to redirect back with an authorization code before
        /// giving up. Without a bound, a login that never returns leaves the UI spinning forever. Generous
        /// enough that a first-time user creating a platform account mid-flow is not cut off.</summary>
        private const int AuthorizationTimeoutMinutes = 10;

        public const string AUTHORIZATION_CODE_URL_PARAMETER = "code";

        public const string LOGIN_REDIRECT_HTML = @"<!DOCTYPE html>
                <html>
                <head>
                <meta charset=""utf-8"">
                <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
                <title>Mix It Up - Logged In</title>
                <link rel=""shortcut icon"" href=""https://files.mixitup.bot/static/branding/mixitup.ico"">
                <style>
                *{margin:0;padding:0;box-sizing:border-box}
                body{font-family:system-ui,-apple-system,sans-serif;background:#12053a;height:100vh;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;overflow:hidden;position:relative}
                .bg{position:absolute;inset:0;background-size:cover;background-position:center;background-repeat:no-repeat;opacity:0;transition:opacity .6s ease}
                .bg.loaded{opacity:1}
                .loader{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;z-index:5;transition:opacity .4s ease}
                .loader.hidden{opacity:0;pointer-events:none}
                .spinner{width:36px;height:36px;border:3px solid rgba(255,255,255,.15);border-top-color:rgba(255,255,255,.7);border-radius:50%;animation:spin .8s linear infinite}
                @keyframes spin{to{transform:rotate(360deg)}}
                .content{position:relative;z-index:2;display:flex;flex-direction:column;align-items:center;margin-bottom:3rem;opacity:0;transform:translateY(12px);transition:opacity .6s ease .15s,transform .6s ease .15s}
                .content.visible{opacity:1;transform:translateY(0)}
                .card{background:rgba(0,0,0,.45);backdrop-filter:blur(24px);-webkit-backdrop-filter:blur(24px);border:1px solid rgba(255,255,255,.12);border-radius:14px;padding:1.75rem 2.25rem;text-align:center;box-shadow:0 8px 32px rgba(0,0,0,.4);max-width:420px;width:90%}
                .check{width:44px;height:44px;margin:0 auto 1rem;border-radius:50%;background:rgba(74,222,128,.15);display:flex;align-items:center;justify-content:center}
                .check svg{width:24px;height:24px}
                .subtitle{color:#fff;font-size:1.35rem;font-weight:600;margin-bottom:.4rem}
                .message{color:rgba(255,255,255,.7);font-size:.9rem;line-height:1.5}
                @media(max-width:640px){.card{padding:1.5rem 1.25rem}.subtitle{font-size:1.15rem}.content{margin-bottom:2rem}}
                </style>
                </head>
                <body>
                <div class=""bg"" id=""bg""></div>
                <div class=""loader"" id=""loader""><div class=""spinner""></div></div>
                <div class=""content"" id=""content"">
                <div class=""card"">
                <div class=""check""><svg viewBox=""0 0 24 24"" fill=""none"" stroke=""#4ade80"" stroke-width=""2.5"" stroke-linecap=""round"" stroke-linejoin=""round""><polyline points=""20 6 9 17 4 12""/></svg></div>
                <h2 class=""subtitle"">Logged In Successfully</h2>
                <p class=""message"">You may now close this page and return to Mix It Up.</p>
                </div>
                </div>
                <script>
                (function(){var done=false;function show(){if(done)return;done=true;document.getElementById('loader').classList.add('hidden');document.getElementById('content').classList.add('visible')}var img=new Image();img.onload=function(){document.getElementById('bg').style.backgroundImage='url('+img.src+')';document.getElementById('bg').classList.add('loaded');show()};img.onerror=function(){show()};setTimeout(show,5000);img.src='https://files.mixitup.bot/static/branding/mixitup_wallpaper-color_1080.png'})();
                </script>
                </body>
                </html>";

        private string authorizationCode;
        private string expectedState;

        public LocalOAuthKestrelServer() { }

        public async Task<string> GetAuthorizationCode(string authorizationURL, int secondsToWait)
        {
            try
            {
                await this.Start(REDIRECT_URL);

                ServiceManager.Get<IProcessService>().LaunchLink(authorizationURL);

                for (int i = 0; i < secondsToWait; i++)
                {
                    if (!string.IsNullOrEmpty(this.authorizationCode))
                    {
                        break;
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            await this.Stop();

            return this.authorizationCode;
        }

        public async Task<Result<string>> GetAuthorizationCode(string authorizationURL, CancellationToken cancellationToken)
        {
            this.expectedState = System.Web.HttpUtility.ParseQueryString(new Uri(authorizationURL).Query)["state"];
            Result portAvailability = CheckRedirectPortAvailability();
            if (!portAvailability.Success)
            {
                return new Result<string>(success: false, message: portAvailability.Message);
            }

            try
            {
                await this.Start(REDIRECT_URL);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await this.Stop();
                return new Result<string>(success: false, message: Resources.UnableToStartAuthenticationSession + Environment.NewLine + Environment.NewLine + ex.Message);
            }

            bool timedOut = false;
            bool manualPromptShown = false;
            try
            {
                if (!ServiceManager.Get<IProcessService>().TryLaunchLink(authorizationURL))
                {
                    // The listener is up and can still take the redirect, so keep waiting and give the user
                    // the address to open themselves rather than failing the whole attempt here.
                    Logger.Log(LogLevel.Error, $"Failed to launch browser for authorization URL: {authorizationURL}");
                    manualPromptShown = this.ShowManualAuthorizationPrompt(authorizationURL);
                }

                DateTimeOffset timeout = DateTimeOffset.Now.AddMinutes(AuthorizationTimeoutMinutes);
                while (!cancellationToken.IsCancellationRequested && string.IsNullOrWhiteSpace(this.authorizationCode))
                {
                    if (DateTimeOffset.Now >= timeout)
                    {
                        timedOut = true;
                        break;
                    }
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            await this.Stop();

            if (manualPromptShown)
            {
                this.CloseManualAuthorizationPrompt();
            }

            if (!string.IsNullOrWhiteSpace(this.authorizationCode))
            {
                return new Result<string>(success: true, message: null) { Value = this.authorizationCode };
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // The user cancelled on purpose, so there is nothing to report back to them.
                return new Result<string>(success: false, message: null);
            }

            return new Result<string>(success: false, message: timedOut ? Resources.AuthenticationTimedOut : Resources.AuthenticationFailedGeneric);
        }

        /// <summary>Test-binds the redirect port so a listener that cannot start reports why instead of
        /// failing silently. The platforms only accept this exact redirect URI, so there is no alternate
        /// port to fall back to.</summary>
        private static Result CheckRedirectPortAvailability()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, REDIRECT_PORT);
                listener.Start();
                return new Result();
            }
            catch (SocketException ex)
            {
                Logger.Log(ex);
                if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    return new Result(string.Format(Resources.AuthenticationPortInUse, REDIRECT_PORT));
                }
                if (ex.SocketErrorCode == SocketError.AccessDenied)
                {
                    return new Result(string.Format(Resources.AuthenticationPortBlocked, REDIRECT_PORT));
                }
                return new Result(Resources.UnableToStartAuthenticationSession + Environment.NewLine + Environment.NewLine + ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(Resources.UnableToStartAuthenticationSession + Environment.NewLine + Environment.NewLine + ex.Message);
            }
            finally
            {
                try { listener?.Stop(); }
                catch (Exception ex) { Logger.Log(ex); }
            }
        }

        private bool ShowManualAuthorizationPrompt(string authorizationURL)
        {
            try
            {
                // Deliberately not awaited: the dialog stays up while the wait loop keeps polling for the
                // redirect, and is closed from CloseManualAuthorizationPrompt once the login resolves.
                _ = DispatcherHelper.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await DialogHelper.ShowTextEntry(Resources.AuthenticationBrowserLaunchFailed, authorizationURL);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return false;
        }

        private void CloseManualAuthorizationPrompt()
        {
            try
            {
                DispatcherHelper.Dispatcher.Invoke(() => DialogHelper.CloseCurrent());
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        protected virtual void ProcessRequestParameters(HttpContext context) { }

        protected override async Task ProcessConnection(HttpContext context)
        {
            if (!string.IsNullOrEmpty(this.expectedState) && this.GetRequestParameter(context, "state") != this.expectedState)
            {
                await this.CloseConnection(context, StatusCodes.Status400BadRequest, string.Empty);
                return;
            }
            int statusCode = StatusCodes.Status500InternalServerError;
            string result = string.Empty;

            this.ProcessRequestParameters(context);

            string token = this.GetRequestParameter(context, AUTHORIZATION_CODE_URL_PARAMETER);
            if (!string.IsNullOrEmpty(token))
            {
                statusCode = StatusCodes.Status200OK;
                result = LOGIN_REDIRECT_HTML;

                this.authorizationCode = token;
            }

            await this.CloseConnection(context, statusCode, result);
        }

        protected string GetRequestParameter(HttpContext context, string parameter)
        {
            if (context.Request.Query.TryGetValue(parameter, out var value))
            {
                return value.ToString();
            }
            return null;
        }

        protected async Task CloseConnection(HttpContext context, int statusCode, string content)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.StatusCode = statusCode;

            byte[] buffer = Encoding.UTF8.GetBytes(content ?? string.Empty);
            context.Response.ContentLength = buffer.Length;
            await context.Response.Body.WriteAsync(buffer, 0, buffer.Length);
            await context.Response.Body.FlushAsync();
            await context.Response.CompleteAsync();
        }
    }
}
