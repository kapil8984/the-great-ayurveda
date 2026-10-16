using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ayurveda.Web.Services;

/// <summary>
/// Background service for Windows shared hosting (e.g. MonsterASP 200MB tier).
/// Periodically monitors the process working set and trims unused memory pages
/// when working set exceeds 150 MB, ensuring peak memory stays strictly under 200 MB.
/// </summary>
public class WorkingSetTrimmerService : BackgroundService
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows()) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                var proc = Process.GetCurrentProcess();
                if (proc.WorkingSet64 > 150L * 1024 * 1024) // 150 MB
                {
                    EmptyWorkingSet(proc.Handle);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Non-critical background trim failure
            }
        }
    }
}
