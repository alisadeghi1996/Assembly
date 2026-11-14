using Microsoft.Extensions.DependencyInjection;
using Shop.Core.Messaging;
using Shop.Core.Registration;

namespace Shop.Tests;

public class Task3_HandlerTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection()
            .AddHandlersFromAssembly(typeof(OrderPlaced).Assembly);
        services.AddTransient<MessageDispatcher>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Closed_handlers_are_registered()
    {
        var services = new ServiceCollection().AddHandlersFromAssembly(typeof(OrderPlaced).Assembly);

        var orderHandlers = services.Where(d => d.ServiceType == typeof(IHandler<OrderPlaced>))
            .Select(d => d.ImplementationType).ToList();

        Assert.Contains(typeof(OrderPlacedHandler), orderHandlers);
        Assert.Contains(typeof(NotificationHandler), orderHandlers);
        Assert.Contains(services, d => d.ServiceType == typeof(IHandler<PaymentReceived>)
                                    && d.ImplementationType == typeof(NotificationHandler));
    }

    [Fact]
    public void Open_generic_handlers_are_skipped()
    {
        var services = new ServiceCollection().AddHandlersFromAssembly(typeof(OrderPlaced).Assembly);

        Assert.DoesNotContain(services, d => d.ImplementationType is { IsGenericTypeDefinition: true });
        Assert.DoesNotContain(services, d => d.ServiceType.ContainsGenericParameters);
    }

    [Fact]
    public void Dispatch_calls_every_handler_for_the_runtime_type()
    {
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<MessageDispatcher>();

        object message = new OrderPlaced(42);   // static type is object on purpose
        var results = dispatcher.Dispatch(message);

        Assert.Equal(2, results.Count);
        Assert.Contains("order 42 placed", results);
        Assert.Contains("notify: order 42", results);
    }

    [Fact]
    public void Dispatch_without_handlers_returns_empty()
    {
        using var provider = BuildProvider();

        Assert.Empty(provider.GetRequiredService<MessageDispatcher>().Dispatch(new UnhandledMessage()));
    }

    [Fact]
    public void Handler_exception_keeps_its_original_type()
    {
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<MessageDispatcher>();

        var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(new FraudDetected(7)));
        Assert.Equal("fraud on order 7", ex.Message);
    }
}
