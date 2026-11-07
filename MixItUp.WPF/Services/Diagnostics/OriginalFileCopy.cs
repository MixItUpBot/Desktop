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

public sealed class OriginalFileCopyException(string message, Exception? inner = null) : IOException(message, inner);
public sealed record OriginalFileReceipt(string SourceName, string StagedName, long Bytes, string SourceSha256, string StagedSha256);

public static class OriginalFileCopy
{
    public static FileStream OpenStable(string source, long maximum)
    {
        try
        {
            if (Collector.HasLink(source)) throw new IOException("Linked files cannot be collected.");
            var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > maximum) { file.Dispose(); throw new IOException("The original file exceeds the collection size limit. It will not be truncated."); }
            return file;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new OriginalFileCopyException($"Cannot safely copy {Path.GetFileName(source)}. Close Mix It Up and retry. {error.Message}", error);
        }
    }

    public static OriginalFileReceipt Copy(FileStream source, string destination, CancellationToken cancellation)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var length = source.Length;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            source.Position = 0;
            using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[65536]; long copied = 0;
                for (;;) { cancellation.ThrowIfCancellationRequested(); int read = source.Read(buffer); if (read == 0) break; target.Write(buffer, 0, read); hash.AppendData(buffer, 0, read); copied += read; }
                if (copied != length || source.Length != length) throw new IOException("The source changed while copying.");
                target.Flush(true);
            }
            var sourceHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            source.Position = 0;
            using (var verify = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[65536];
                for (;;) { cancellation.ThrowIfCancellationRequested(); int read = source.Read(buffer); if (read == 0) break; verify.AppendData(buffer, 0, read); }
                if (source.Length != length || Convert.ToHexString(verify.GetHashAndReset()).ToLowerInvariant() != sourceHash) throw new IOException("The source changed after copying.");
            }
            using var staged = File.OpenRead(destination);
            var stagedHash = Convert.ToHexString(SHA256.HashData(staged)).ToLowerInvariant();
            if (staged.Length != length || sourceHash != stagedHash) throw new IOException("The copied bytes did not match the original.");
            return new(Path.GetFileName(source.Name), Path.GetFileName(destination), length, sourceHash, stagedHash);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(destination); } catch { }
            throw new OriginalFileCopyException($"Could not preserve the original bytes of {Path.GetFileName(source.Name)}. No upload will start. Close Mix It Up and retry.", error);
        }
    }
}
