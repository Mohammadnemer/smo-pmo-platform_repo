namespace SmoPmo.Shared.Messaging;

/// <summary>A request that changes state and returns no result.</summary>
public interface ICommand;

/// <summary>A request that changes state and returns a result.</summary>
public interface ICommand<TResult>;
