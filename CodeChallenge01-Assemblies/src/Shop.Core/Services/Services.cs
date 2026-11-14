using Shop.Core.Registration;

namespace Shop.Core.Services;

public interface IOrderService { string PlaceOrder(int id); }
public interface IPriceCalculator { decimal Total(decimal price, int quantity); }
public interface IClock { DateTimeOffset Now { get; } }
public interface IAuditService { void Write(string entry); }
public interface IReportReader { string Read(); }
public interface IReportWriter { void Write(string line); }

public sealed class OrderService : IOrderService, IScopedService
{
    public string PlaceOrder(int id) => $"order {id} placed";
}

public sealed class PriceCalculator : IPriceCalculator, ISingletonService
{
    public decimal Total(decimal price, int quantity) => price * quantity;
}

public sealed class SystemClock : IClock, ITransientService
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

// Internal on purpose: must still be registered.
internal sealed class AuditService : IAuditService, IScopedService
{
    public void Write(string entry) { }
}

// Implements two service interfaces.
public sealed class ReportService : IReportReader, IReportWriter, ISingletonService
{
    private readonly List<string> _lines = [];
    public string Read() => string.Join('\n', _lines);
    public void Write(string line) => _lines.Add(line);
}

// Abstract: must NOT be registered.
[SkipRegistration]
public abstract class ServiceBase : IScopedService;

// Opted out: must NOT be registered (otherwise IOrderService would have two implementations).
[SkipRegistration]
public sealed class LegacyOrderService : IOrderService, IScopedService
{
    public string PlaceOrder(int id) => "legacy";
}
