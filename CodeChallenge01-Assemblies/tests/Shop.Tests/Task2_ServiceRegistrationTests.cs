using Microsoft.Extensions.DependencyInjection;
using Shop.Core.Registration;
using Shop.Core.Services;

namespace Shop.Tests;

public class Task2_ServiceRegistrationTests
{
    private static readonly System.Reflection.Assembly CoreAssembly = typeof(OrderService).Assembly;

    private static IServiceCollection Scan() => new ServiceCollection().AddServicesFromAssembly(CoreAssembly);

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, d => d.ServiceType == typeof(TService));

    [Fact]
    public void Lifetime_comes_from_the_marker_interface()
    {
        var services = Scan();

        Assert.Equal(ServiceLifetime.Scoped, Single<IOrderService>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Single<IPriceCalculator>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Transient, Single<IClock>(services).Lifetime);
    }

    [Fact]
    public void Service_is_registered_against_its_interface_with_the_right_implementation()
    {
        using var provider = Scan().BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<OrderService>(scope.ServiceProvider.GetRequiredService<IOrderService>());
        Assert.IsType<PriceCalculator>(scope.ServiceProvider.GetRequiredService<IPriceCalculator>());
    }

    [Fact]
    public void Internal_classes_are_registered()
    {
        Assert.Equal(ServiceLifetime.Scoped, Single<IAuditService>(Scan()).Lifetime);
    }

    [Fact]
    public void Marker_interfaces_are_not_registered_as_services()
    {
        var services = Scan();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IScopedService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISingletonService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ITransientService));
    }

    [Fact]
    public void Abstract_and_skipped_classes_are_ignored()
    {
        var services = Scan();

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(ServiceBase));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(LegacyOrderService));
        Assert.Equal(typeof(OrderService), Single<IOrderService>(services).ImplementationType);
    }

    [Fact]
    public void A_class_with_two_interfaces_is_registered_for_both()
    {
        using var provider = Scan().BuildServiceProvider();

        Assert.NotNull(provider.GetService<IReportReader>());
        Assert.NotNull(provider.GetService<IReportWriter>());
    }

    [Fact]
    public void Calling_twice_does_not_duplicate_registrations()
    {
        var once = Scan();
        var twice = new ServiceCollection()
            .AddServicesFromAssembly(CoreAssembly)
            .AddServicesFromAssembly(CoreAssembly);

        Assert.Equal(once.Count, twice.Count);
    }

    [Fact]
    [Trait("Category", "Bonus")]
    public void Bonus_two_interfaces_resolve_to_the_same_singleton_instance()
    {
        using var provider = Scan().BuildServiceProvider();

        provider.GetRequiredService<IReportWriter>().Write("hello");

        Assert.Equal("hello", provider.GetRequiredService<IReportReader>().Read());
    }
}
