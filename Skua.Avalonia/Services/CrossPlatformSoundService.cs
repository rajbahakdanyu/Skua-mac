using Skua.Core.Interfaces;

namespace Skua.Avalonia.Services;

public class CrossPlatformSoundService : ISoundService
{
    public void Beep()
    {
        // Console.Beep() is Windows-only with frequency; use simple write for cross-platform
        Console.Write('\a');
    }

    public void Beep(int frequency, int duration)
    {
        // Cross-platform: no native beep with frequency on macOS
        // Fall back to terminal bell
        Console.Write('\a');
    }
}
