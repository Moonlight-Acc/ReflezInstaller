using System.Diagnostics;

namespace Installer.Helpers;

public class BashHelper
{
    public static async Task<string> ExecuteCommand(string command, bool ignoreErrors = false, bool captureOutput = true, string? workingDir = default)
    {
        var process = await ExecuteCommandRaw(command, workingDir, captureOutput);

        var output = captureOutput ? await process.StandardOutput.ReadToEndAsync() : "";
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            if(!ignoreErrors)
                throw new Exception(output);
        }

        return output.Trim();
    }
    
    public static async Task<int> ExecuteCommandForExitCode(string command, string? workingDir = default)
    {
        var process = await ExecuteCommandRaw(command, workingDir);
        await process.WaitForExitAsync();

        return process.ExitCode;
    }

    public static async Task ExecuteWithOutputHandler(string command, Func<string, bool, Task> handle, string? workingDir = default, bool ignoreErrors = false)
    {
        var process = await ExecuteCommandRaw(command, workingDir);

        while (!process.StandardOutput.EndOfStream)
        {
            var line = await process.StandardOutput.ReadLineAsync() ?? "";
            await handle.Invoke(line, false);
        }
        
        while (!process.StandardError.EndOfStream)
        {
            var line = await process.StandardError.ReadLineAsync() ?? "";
            await handle.Invoke(line, true);
        }
        
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            if(!ignoreErrors)
                throw new Exception("Exit code is not 0");
        }
    }
    
    public static Task<Process> ExecuteCommandRaw(string command, string? workingDir = default, bool captureOutput = true)
    {
        Process process = new Process();
        
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = captureOutput;
        process.StartInfo.RedirectStandardError = captureOutput;

        if (workingDir != null)
            process.StartInfo.WorkingDirectory = workingDir;

        process.Start();

        return Task.FromResult(process);
    }
}