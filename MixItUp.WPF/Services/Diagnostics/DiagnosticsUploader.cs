#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MixItUp.WPF.Services.Diagnostics;

public sealed record PreparedFile(int Index, string Name, string Path, long Size, string Sha256)
{
    public bool Acknowledged { get; set; }
}

public sealed class DiagnosticsUploader(HttpClient http, TimeSpan? retryDelay = null)
{
    public async Task Upload(string code, IReadOnlyList<PreparedFile> files, IProgress<string> progress, CancellationToken cancellation)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9]{6}$")) throw new IOException("Invalid access code format.");
        var root = $"https://support.mixitup.bot/api/v1/diagnostics/{code}";
        var manifest = JsonSerializer.Serialize(new { files = files.Select(f => new { index = f.Index, name = f.Name, size = f.Size, sha256 = f.Sha256 }) });
        var manifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
        progress.Report("Registering this diagnostics submission...");
        using (var response = await Send(() => new HttpRequestMessage(HttpMethod.Post, root + "/submission") { Content = new StringContent(manifest, Encoding.UTF8, "application/json") }, progress, cancellation))
        {
            if (!response.IsSuccessStatusCode) throw Failure(response, "Submission was not confirmed");
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            if (!result.RootElement.GetProperty("configured").GetBoolean() || result.RootElement.GetProperty("fileCount").GetInt32() != files.Count || result.RootElement.GetProperty("manifestSha256").GetString() != manifestHash)
                throw new IOException("The submission receipt differs from the original files. Nothing further was uploaded.");
        }
        foreach (var file in files)
        {
            if (file.Acknowledged) continue;
            progress.Report($"Uploading {file.Name} ({files.Count(f => f.Acknowledged) + 1} of {files.Count})...");
            using var response = await Send(() =>
            {
                var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    if (stream.Length != file.Size || Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() != file.Sha256) throw new IOException("A prepared file changed. Close the utility and start a new submission.");
                    stream.Position = 0;
                    var request = new HttpRequestMessage(HttpMethod.Put, root + $"/files/{file.Index}");
                    request.Headers.Add("x-file-name", file.Name);
                    request.Content = new StreamContent(stream); request.Content.Headers.ContentLength = file.Size;
                    request.Content.Headers.ContentType = new(file.Name.EndsWith(".zip") ? "application/zip" : file.Name.EndsWith(".json") ? "application/json" : "text/plain");
                    return request;
                }
                catch { stream.Dispose(); throw; }
            }, progress, cancellation);
            if (!response.IsSuccessStatusCode) throw Failure(response, $"{files.Count(f => f.Acknowledged)} of {files.Count} files acknowledged. {file.Name} was not confirmed");
            using var receipt = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            if (!receipt.RootElement.GetProperty("stored").GetBoolean() || receipt.RootElement.GetProperty("sha256").GetString() != file.Sha256 || receipt.RootElement.GetProperty("byteSize").GetInt64() != file.Size)
                throw new IOException($"Integrity could not be confirmed for {file.Name}. Retry the same submission.");
            file.Acknowledged = true;
        }
        progress.Report("Confirming that every file is attached to the support request...");
        using var completed = await Send(() => new HttpRequestMessage(HttpMethod.Post, root + "/complete"), progress, cancellation);
        if (!completed.IsSuccessStatusCode) throw Failure(completed, "Completion was not confirmed");
        using var completion = JsonDocument.Parse(await completed.Content.ReadAsStringAsync(cancellation));
        var final = completion.RootElement;
        if (!final.GetProperty("complete").GetBoolean() || final.GetProperty("fileCount").GetInt32() != files.Count || final.GetProperty("receivedFiles").GetInt32() != files.Count || final.GetProperty("byteSize").GetInt64() != files.Sum(f => f.Size) || final.GetProperty("manifestSha256").GetString() != manifestHash)
            throw new IOException("The server has not confirmed every file. Retry keeps the original files and checks completion again.");
    }

    private static IOException Failure(HttpResponseMessage response, string prefix) => new($"{prefix} (HTTP {(int)response.StatusCode}). Retry keeps the same original files and skips acknowledged uploads. An expired code requires a new support request.");

    private async Task<HttpResponseMessage> Send(Func<HttpRequestMessage> requestFactory, IProgress<string> progress, CancellationToken cancellation)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            TimeSpan delay = retryDelay ?? TimeSpan.FromSeconds(attempt + 1);
            try
            {
                using var request = requestFactory();
                var response = await http.SendAsync(request, cancellation);
                var retryable = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
                if (!retryable || attempt >= 2) return response;
                if (response.Headers.RetryAfter?.Delta is { } backoff) delay = TimeSpan.FromSeconds(Math.Clamp(backoff.TotalSeconds, 0, 15));
                response.Dispose();
            }
            catch (Exception error) when (attempt < 2 && !cancellation.IsCancellationRequested && error is HttpRequestException or TaskCanceledException) { }
            progress.Report("The service could not confirm the request. Retrying the same bytes...");
            await Task.Delay(delay, cancellation);
        }
    }
}
