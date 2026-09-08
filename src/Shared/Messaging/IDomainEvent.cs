namespace SmoPmo.Shared.Messaging;

/// <summary>
/// Something that happened inside a module, published for other modules (and the
/// roll-up worker) to react to. This is how modules talk without referencing each other.
/// </summary>
public interface IDomainEvent;
