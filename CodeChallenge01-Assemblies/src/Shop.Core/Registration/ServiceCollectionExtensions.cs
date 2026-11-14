using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shop.Core.Registration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// TASK 2: register every concrete class in <paramref name="assembly"/> that implements
    /// IScopedService, ISingletonService or ITransientService.
    /// Rules (see README):
    ///  - lifetime comes from the marker interface
    ///  - register the class against each interface it implements, except the marker interfaces
    ///  - skip interfaces, abstract classes, open generic types and classes marked [SkipRegistration]
    ///  - non-public classes are registered too
    ///  - calling this method twice must not create duplicate registrations
    ///  - BONUS: a class implementing several service interfaces resolves to the same instance for all of them
    /// </summary>
    public static IServiceCollection AddServicesFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var markerInterfaces = new[]
        {
            typeof(IScopedService),
            typeof(ISingletonService),
            typeof(ITransientService)
        };

        var types = assembly.GetTypes();

        foreach (var implementationType in types)
        {
            // Only concrete classes are eligible.
            if (!implementationType.IsClass ||
                implementationType.IsAbstract ||
                implementationType.ContainsGenericParameters)
            {
                continue;
            }

            // Skip classes decorated with SkipRegistration.
            if (implementationType.GetCustomAttribute<SkipRegistrationAttribute>(
                    inherit: true) is not null)
            {
                continue;
            }

            var implementedInterfaces = implementationType.GetInterfaces();

            // Determine lifetime from marker interfaces.
            var lifetimes = new List<ServiceLifetime>();

            if (typeof(IScopedService).IsAssignableFrom(implementationType))
                lifetimes.Add(ServiceLifetime.Scoped);

            if (typeof(ISingletonService).IsAssignableFrom(implementationType))
                lifetimes.Add(ServiceLifetime.Singleton);

            if (typeof(ITransientService).IsAssignableFrom(implementationType))
                lifetimes.Add(ServiceLifetime.Transient);

            // Classes without a lifetime marker are ignored.
            if (lifetimes.Count == 0)
                continue;

            // A class must have exactly one lifetime marker.
            if (lifetimes.Count > 1)
            {
                throw new InvalidOperationException(
                    $"{implementationType.FullName} implements multiple lifetime markers.");
            }

            var lifetime = lifetimes[0];

            // Register the concrete class itself.
            // This is important for sharing the same instance
            // across all implemented interfaces.
            var concreteAlreadyRegistered = services.Any(d =>
                d.ServiceType == implementationType &&
                d.ImplementationType == implementationType &&
                d.Lifetime == lifetime);

            if (!concreteAlreadyRegistered)
            {
                services.Add(
                    ServiceDescriptor.Describe(
                        implementationType,
                        implementationType,
                        lifetime));
            }

            // Register against every implemented interface, except the lifetime markers.
            var serviceInterfaces = implementedInterfaces
                .Where(i => !markerInterfaces.Contains(i) && !i.ContainsGenericParameters)
                .ToArray();

            foreach (var serviceInterface in serviceInterfaces)
            {
                if (serviceInterfaces.Length == 1)
                {
                    // One interface: a plain "interface -> implementation type" descriptor.
                    // It keeps ImplementationType visible to tools and tests, which a factory hides.
                    var exists = services.Any(d =>
                        d.ServiceType == serviceInterface && d.ImplementationType == implementationType);

                    if (!exists)
                        services.Add(ServiceDescriptor.Describe(serviceInterface, implementationType, lifetime));

                    continue;
                }

                // Several interfaces: forward each one to the concrete registration above,
                // so all of them resolve to the SAME instance (the bonus rule).
                var alreadyRegistered = services.Any(d =>
                    d.ServiceType == serviceInterface &&
                    d.Lifetime == lifetime &&
                    d.ImplementationFactory?.Target is ServiceFactory factory &&
                    factory.ImplementationType == implementationType);

                if (alreadyRegistered)
                    continue;

                var serviceFactory = new ServiceFactory(implementationType);
                services.Add(ServiceDescriptor.Describe(serviceInterface, serviceFactory.Create, lifetime));
            }
        }

        return services;
    }
    
    private sealed class ServiceFactory(Type implementationType)
    {
        public Type ImplementationType { get; } = implementationType;

        public object Create(IServiceProvider provider)
        {
            return provider.GetRequiredService(ImplementationType);
        }
    }

    /// <summary>
    /// TASK 3a: register every closed IHandler&lt;T&gt; implementation in <paramref name="assembly"/> as transient.
    ///  - a class implementing IHandler&lt;A&gt; and IHandler&lt;B&gt; is registered for both
    ///  - open generic handlers (e.g. LoggingHandler&lt;T&gt;) and abstract classes are skipped
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetTypes())
        {
            // IsGenericTypeDefinition filters out open generics such as LoggingHandler<T>.
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                continue;

            // A closed IHandler<X> is a generic interface whose DEFINITION is IHandler<>.
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Messaging.IHandler<>));

            foreach (var handlerInterface in handlerInterfaces)
            {
                // TryAddEnumerable allows several handlers per message type,
                // but never the same (interface, implementation) pair twice.
                services.TryAddEnumerable(ServiceDescriptor.Transient(handlerInterface, type));
            }
        }

        return services;
    }
}
