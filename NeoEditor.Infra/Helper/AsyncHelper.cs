using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Serilog;

namespace NeoEditor.Helper;

/// <summary>
/// Safe fire-and-forget helpers that log exceptions instead of silently swallowing them.
/// </summary>
public static class AsyncHelper
{
    /// <summary>
    /// Fire a task in the background and log any exceptions.
    /// Use this as a safe replacement for <c>_ = AsyncMethod()</c>.
    /// </summary>
    public static void FireAndForget(Task task, [CallerMemberName] string caller = "")
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
                Log.Logger.Error(t.Exception, "[FireAndForget] {Caller} failed", caller);
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
