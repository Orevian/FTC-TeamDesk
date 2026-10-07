using System.Reflection;
using System.Runtime.ExceptionServices;

namespace FTC.TeamDesk.Infrastructure;

/// <summary>
/// Decorator that runs every Task-returning service call on the thread pool.
/// Why: Microsoft.Data.Sqlite has no truly asynchronous I/O, so a "await db.SaveChangesAsync()" that is started on the UI thread
/// executes (including the disk flush at commit) on the UI thread and freezes the window. Services are stateless singletons
/// (one DbContext per call), so running them on pool threads is safe; the awaiting view model still resumes on the UI thread.
/// </summary>
public class OffThreadProxy : DispatchProxy
{
    private object _target = null!;

    public static T Wrap<T>(T target) where T : class
    {
        var proxy = Create<T, OffThreadProxy>();
        ((OffThreadProxy)(object)proxy)._target = target;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        var rt = method.ReturnType;
        if (rt == typeof(Task)) return Task.Run(() => (Task)Call(method, args)!);
        if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return typeof(OffThreadProxy).GetMethod(nameof(RunOffThread), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(rt.GetGenericArguments()[0]).Invoke(this, new object?[] { method, args });
        }
        return Call(method, args);   // properties, events and synchronous members pass straight through
    }

    private Task<TResult> RunOffThread<TResult>(MethodInfo method, object?[]? args) => Task.Run(() => (Task<TResult>)Call(method, args)!);

    private object? Call(MethodInfo method, object?[]? args)
    {
        try { return method.Invoke(_target, args); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
