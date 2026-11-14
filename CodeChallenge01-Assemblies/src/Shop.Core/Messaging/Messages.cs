namespace Shop.Core.Messaging;

public interface IHandler<TMessage>
{
    string Handle(TMessage message);
}

public sealed record OrderPlaced(int OrderId);
public sealed record PaymentReceived(int OrderId, decimal Amount);
public sealed record FraudDetected(int OrderId);
public sealed record UnhandledMessage;

public sealed class OrderPlacedHandler : IHandler<OrderPlaced>
{
    public string Handle(OrderPlaced message) => $"order {message.OrderId} placed";
}

public sealed class PaymentReceivedHandler : IHandler<PaymentReceived>
{
    public string Handle(PaymentReceived message) => $"payment {message.Amount} for order {message.OrderId}";
}

// One class, two handler interfaces.
public sealed class NotificationHandler : IHandler<OrderPlaced>, IHandler<PaymentReceived>
{
    public string Handle(OrderPlaced message) => $"notify: order {message.OrderId}";
    public string Handle(PaymentReceived message) => $"notify: payment {message.OrderId}";
}

public sealed class FraudHandler : IHandler<FraudDetected>
{
    public string Handle(FraudDetected message) =>
        throw new InvalidOperationException($"fraud on order {message.OrderId}");
}

// Open generic: must NOT be picked up by assembly scanning.
public sealed class LoggingHandler<T> : IHandler<T>
{
    public string Handle(T message) => $"log: {message}";
}
