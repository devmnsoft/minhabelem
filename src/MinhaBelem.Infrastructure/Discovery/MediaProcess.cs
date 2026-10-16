using System.Diagnostics;
namespace MinhaBelem.Infrastructure.Discovery;
internal static class MediaProcess
{
    public static async Task<byte[]> RunAsync(string executable, IEnumerable<string> args, byte[] input, int outputLimit, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable)) throw new InvalidOperationException("Processador de mídia não configurado no servidor.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Processador de mídia indisponível.");
        using var registration = ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        var errors = process.StandardError.ReadToEndAsync(ct);
        async Task Write()
        {
            try { await process.StandardInput.BaseStream.WriteAsync(input, ct); }
            finally { process.StandardInput.Close(); }
        }
        async Task<byte[]> Read()
        {
            using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
            while ((read = await process.StandardOutput.BaseStream.ReadAsync(buffer, ct)) != 0)
            {
                if (output.Length + read > outputLimit) { process.Kill(entireProcessTree: true); throw new InvalidOperationException("Saída do processador excede o limite."); }
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            return output.ToArray();
        }
        var written = Write(); var result = Read();
        try { await Task.WhenAll(written, result, process.WaitForExitAsync(ct)); }
        catch (IOException) { throw new InvalidOperationException("Não foi possível processar a mídia recebida."); }
        await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException("O processador recusou a mídia recebida.");
        return await result;
    }
}
