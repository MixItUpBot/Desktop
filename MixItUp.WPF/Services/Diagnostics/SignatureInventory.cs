#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MixItUp.WPF.Services.Diagnostics;

public static class SignatureInventory
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".dll", ".sys", ".ocx", ".cpl", ".scr", ".mui", ".msi", ".msp", ".cat", ".ps1", ".psm1", ".psd1", ".ps1xml" };
    public static bool IsApplicable(string path) => Extensions.Contains(Path.GetExtension(path));
    public static JsonObject Unavailable(string reason) => new() { ["status"] = "unavailable", ["signaturePresent"] = null, ["verification"] = "not-completed", ["reason"] = reason };
    public static JsonObject NotApplicable() => new() { ["status"] = "not-applicable", ["signaturePresent"] = null, ["verification"] = "not-applicable", ["reason"] = "file-type" };
    public static JsonObject Interpret(JsonObject input)
    {
        string status = input["windowsStatus"]?.GetValue<string>() ?? "UnknownError";
        input["status"] = status switch { "Valid" => "valid", "NotSigned" => "unsigned", "HashMismatch" => "invalid", "NotTrusted" => "untrusted", "NotSupportedFileFormat" or "Incompatible" => "unsupported", _ => "unavailable" };
        input["signaturePresent"] = input["signer"] != null || input["timestampCertificate"] != null ? true : status == "NotSigned" ? false : null;
        input["verification"] = status == "Valid" ? "passed" : "not-valid";
        input["timestampPresent"] = input["timestampCertificate"] != null;
        input["timestampUtc"] = null;
        return input;
    }

    public static Task<IReadOnlyDictionary<int, JsonObject>> Collect(IReadOnlyList<string> files, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult<IReadOnlyDictionary<int, JsonObject>>(files.Select((f,i)=>(f,i)).ToDictionary(x=>x.i,x=>IsApplicable(x.f)?Unavailable("windows-required"):NotApplicable()));
        const string script = """
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$certificateCache = @{}
function CertificateInfo($certificate) {
 if ($null -eq $certificate) { return $null }
 $key = $certificate.Thumbprint
 if (!$certificateCache.ContainsKey($key)) {
  $certificateCache[$key] = @{ subject=$certificate.Subject; issuer=$certificate.Issuer; thumbprint=$key; serialNumber=$certificate.SerialNumber; validFromUtc=$certificate.NotBefore.ToUniversalTime().ToString('o'); validToUtc=$certificate.NotAfter.ToUniversalTime().ToString('o'); signatureAlgorithm=$certificate.SignatureAlgorithm.Value }
 }
 return $certificateCache[$key]
}
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::Out.WriteLine('{"ready":true}')
[Console]::Out.Flush()
while ($null -ne ($line = [Console]::In.ReadLine())) {
 $file = $line | ConvertFrom-Json
 try {
  $signature = Get-AuthenticodeSignature -LiteralPath $file.path -ErrorAction Stop
  $row = @{ index=$file.index; windowsStatus=[string]$signature.Status; statusMessage=$signature.StatusMessage; signatureType=[string]$signature.SignatureType; isOSBinary=$signature.IsOSBinary; signer=(CertificateInfo $signature.SignerCertificate); timestampCertificate=(CertificateInfo $signature.TimeStamperCertificate); verifier='Windows PowerShell Get-AuthenticodeSignature'; verifierVersion=[string]$PSVersionTable.PSVersion }
 } catch { $row = @{ index=$file.index; windowsStatus='UnknownError'; statusMessage=$_.Exception.Message; errorHResult=$_.Exception.HResult; errorType=$_.Exception.GetType().Name } }
 [Console]::Out.WriteLine(($row | ConvertTo-Json -Depth 6 -Compress))
 [Console]::Out.Flush()
}
""";

        return CollectWithWorkers(files, () => {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"));
            foreach(var argument in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(script))}) start.ArgumentList.Add(argument);
            return start;
        }, cancellation);
    }
    public static async Task<IReadOnlyDictionary<int, JsonObject>> CollectWithWorkers(IReadOnlyList<string> files, Func<ProcessStartInfo> startFactory, CancellationToken cancellation, TimeSpan? fileTimeout = null, TimeSpan? totalTimeout = null)
    {
        var reports=files.Select((f,i)=>(f,i)).ToDictionary(x=>x.i,x=>IsApplicable(x.f)?Unavailable("not-started"):NotApplicable());
        var queue=new System.Collections.Concurrent.ConcurrentQueue<int>(Enumerable.Range(0,files.Count).Where(i=>IsApplicable(files[i])));
        using var overall=CancellationTokenSource.CreateLinkedTokenSource(cancellation);overall.CancelAfter(totalTimeout??TimeSpan.FromMinutes(10));
        async Task Worker()
        {
            SignatureProcess? process=null;int failures=0;
            try
            {
                while(!overall.IsCancellationRequested && queue.TryDequeue(out var index))
                {
                    try
                    {
                        if(process==null) { process=new SignatureProcess(startFactory()); await process.Start(overall.Token); }
                        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(overall.Token);deadline.CancelAfter(fileTimeout??TimeSpan.FromSeconds(20));
                        var row=await process.Request(index,files[index],deadline.Token);
                        row.Remove("index");lock(reports)reports[index]=Interpret(row);failures=0;
                    }
                    catch(Exception error) when(error is OperationCanceledException or IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException)
                    {
                        lock(reports)reports[index]=Unavailable(cancellation.IsCancellationRequested?"canceled":overall.IsCancellationRequested?"inventory-deadline":error is OperationCanceledException?"file-deadline":error is InvalidDataException?"output-limit-or-protocol":"worker-failed");
                        if(process!=null){await process.DisposeAsync();process=null;}
                        if(error is not OperationCanceledException && ++failures>=3)break;
                    }
                }
            }
            finally {if(process!=null)await process.DisposeAsync();}
        }
        await Task.WhenAll(Enumerable.Range(0,Math.Min(4,queue.Count)).Select(_=>Worker()));
        while(queue.TryDequeue(out var index))reports[index]=Unavailable(overall.IsCancellationRequested?"inventory-deadline":"workers-unavailable");
        cancellation.ThrowIfCancellationRequested();return reports;
    }
}

public sealed class SignatureProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly CancellationTokenSource lifetime=new();
    private Task? errors;
    public SignatureProcess(ProcessStartInfo start)
    {
        start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardInput=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
        start.StandardInputEncoding=new UTF8Encoding(false);start.StandardOutputEncoding=Encoding.UTF8;
        process=new Process{StartInfo=start};
    }
    public async Task Start(CancellationToken cancellation)
    {
        process.Start();errors=DrainErrors();
        using var startup=CancellationTokenSource.CreateLinkedTokenSource(cancellation);startup.CancelAfter(TimeSpan.FromSeconds(45));
        var ready=JsonNode.Parse(await ReadLine(startup.Token));
        if(ready?["ready"]?.GetValue<bool>()!=true)throw new InvalidDataException("signature-worker-handshake");
    }
    public async Task<JsonObject> Request(int index,string path,CancellationToken cancellation)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{index,path}).AsMemory(),cancellation);
        await process.StandardInput.FlushAsync(cancellation);
        var row=JsonNode.Parse(await ReadLine(cancellation))!.AsObject();
        if(row["index"]?.GetValue<int>()!=index)throw new InvalidDataException("signature-worker-index");
        return row;
    }
    private async Task<string> ReadLine(CancellationToken cancellation)
    {
        var text=new StringBuilder();var character=new char[1];
        while(await process.StandardOutput.ReadAsync(character.AsMemory(),cancellation)>0)
        {
            if(character[0]=='\n')return text.ToString().TrimEnd('\r');
            text.Append(character[0]);if(text.Length>64*1024)throw new InvalidDataException("signature-result-output-limit");
        }
        throw new IOException("signature-worker-exited");
    }
    private async Task DrainErrors()
    {
        var buffer=new char[4096];
        try{while(await process.StandardError.ReadAsync(buffer.AsMemory(),lifetime.Token)>0){}}
        catch(Exception e) when(e is OperationCanceledException or IOException or ObjectDisposedException){}
    }
    public async ValueTask DisposeAsync()
    {
        try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}
        lifetime.Cancel();
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception e) when(e is InvalidOperationException or TimeoutException){}
        if(errors!=null)await errors;
        process.Dispose();lifetime.Dispose();
    }
}
