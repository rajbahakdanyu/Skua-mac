using Avalonia.Threading;
using Skua.Core.Interfaces;

namespace Skua.Avalonia.Services;

public class AvaloniaDispatcherService : IDispatcherService
{
    public void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }
}
