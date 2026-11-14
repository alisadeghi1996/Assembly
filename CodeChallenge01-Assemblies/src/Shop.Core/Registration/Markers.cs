namespace Shop.Core.Registration;

// Marker interfaces: a class that implements one of these is registered
// automatically with the matching lifetime.
public interface IScopedService;

public interface ISingletonService;

public interface ITransientService;

/// <summary>Classes marked with this attribute must never be auto-registered.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SkipRegistrationAttribute : Attribute;
