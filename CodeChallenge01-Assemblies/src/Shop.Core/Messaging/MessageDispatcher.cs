using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Shop.Core.Messaging;

public sealed class MessageDispatcher(IServiceProvider provider)
{
    private delegate IReadOnlyList<string> Invoker(IServiceProvider provider, object message);

    // One compiled delegate per message type: reflection runs once, later calls are direct.
    private static readonly ConcurrentDictionary<Type, Invoker> Invokers = new();

    private static readonly MethodInfo DispatchTypedMethod =
        typeof(MessageDispatcher).GetMethod(nameof(DispatchTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Finds every registered IHandler&lt;T&gt; for the RUNTIME type of the message and calls them in
    /// registration order.
    /// </summary>
    public IReadOnlyList<string> Dispatch(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // message.GetType() is the runtime type; the compile-time type here is just object.
        var invoker = Invokers.GetOrAdd(message.GetType(), static messageType =>
            // Close DispatchTyped<T> over the message type and turn it into a delegate.
            // A delegate call does NOT wrap exceptions in TargetInvocationException,
            // unlike MethodInfo.Invoke, so a handler's exception keeps its original type.
            DispatchTypedMethod.MakeGenericMethod(messageType).CreateDelegate<Invoker>());

        return invoker(provider, message);
    }

    private static IReadOnlyList<string> DispatchTyped<T>(IServiceProvider provider, object message)
    {
        // GetServices returns an empty sequence when nothing is registered, so no special case is needed.
        return provider.GetServices<IHandler<T>>()
            .Select(handler => handler.Handle((T)message))
            .ToList();
    }
}
