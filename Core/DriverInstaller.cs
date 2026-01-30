using System.Diagnostics;

namespace NvwUpd.Core;

/// <summary>
/// Installs NVIDIA drivers with optional silent mode.
/// </summary>
public class DriverInstaller : IDriverInstaller
{
    public async Task InstallDriverAsync(string installerPath, bool silent = true)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Driver installer not found.", installerPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            Verb = "runas" // Request admin elevation
        };

        if (silent)
        {
            // NVIDIA silent install arguments
            // -s: Silent install
            // -noreboot: Don't reboot automatically
            // -noeula: Don't show EULA
            // -clean: Clean install (optional, removes old settings)
            startInfo.Arguments = "-s -noreboot -noeula";
        }

        try
        {
            using var process = Process.Start(startInfo);
            
            if (process != null)
            {
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Driver installation failed with exit code: {process.ExitCode}");
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC prompt
            throw new OperationCanceledException("User cancelled the administrator permission request.", ex);
        }
    }

    /// <summary>
    /// Extracts the driver package without installing.
    /// Useful for extracting only specific components.
    /// </summary>
    public async Task ExtractDriverAsync(string installerPath, string extractPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Driver installer not found.", installerPath);
        }

        Directory.CreateDirectory(extractPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = $"-x \"{extractPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }
}
