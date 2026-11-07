#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MixItUp.WPF.Services.Diagnostics;

public sealed record FileHashResult(string status, string? sha256, string? reason);
public static class FileHashInventory
{
    public static async Task<IReadOnlyList<FileHashResult>> Collect(IReadOnlyList<string> files, CancellationToken cancellation,
        Func<string, Stream>? open = null, TimeSpan? fileTimeout = null, TimeSpan? totalTimeout = null, long maxBytes = 512L * 1024 * 1024)
    {
        open ??= path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var reports = files.Select(_ => new FileHashResult("unavailable", null, "not-started")).ToArray();
        var queue = new System.Collections.Concurrent.ConcurrentQueue<int>(Enumerable.Range(0, files.Count));
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        overall.CancelAfter(totalTimeout ?? TimeSpan.FromMinutes(2));
        async Task Worker()
        {
            while (!overall.IsCancellationRequested && queue.TryDequeue(out var index))
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(overall.Token);
                deadline.CancelAfter(fileTimeout ?? TimeSpan.FromSeconds(5));
                var fileToken = deadline.Token;
                var operation = Task.Run(async () => {
                    using var stream = open(files[index]);
                    var length = stream.Length;
                    if (length > maxBytes) throw new IOException("file-size-limit");
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, fileToken)).ToLowerInvariant();
                    if (stream.Length != length || stream.Position != length) throw new IOException("file-changed-during-hash");
                    return hash;
                }, fileToken);
                try { reports[index] = new("complete", await operation.WaitAsync(fileToken), null); }
                catch (OperationCanceledException)
                {
                    reports[index] = new("unavailable", null, cancellation.IsCancellationRequested ? "canceled" : overall.IsCancellationRequested ? "inventory-deadline" : "file-deadline");
                    // A filesystem driver may ignore cancellation. Never add another read behind a stuck worker.
                    _ = operation.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    break;
                }
                catch (Exception error) { reports[index] = new("unavailable", null, error.GetType().Name + ": " + error.Message); }
            }
        }
        await Task.WhenAll(Enumerable.Range(0, Math.Min(4, files.Count)).Select(_ => Worker()));
        while (queue.TryDequeue(out var index)) reports[index] = new("unavailable", null, overall.IsCancellationRequested ? "inventory-deadline" : "hash-workers-unavailable");
        cancellation.ThrowIfCancellationRequested();
        return reports;
    }
}
