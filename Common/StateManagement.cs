using System;
using System.Collections.Generic;

namespace Cataphract.Common.Utilities;

public record StateID (string name, int id)
{
	public override string ToString() => string.IsNullOrEmpty(name) ? $"State#{id}" : $"{name}#{id}";
}

public readonly record struct StateEventArgs<T>(StateID StateId, bool Success, T[] Parameters) where T : struct;

public abstract class State<T> where T : struct
{
    public required StateID stateID;
    public T stateData;
    internal StateController<T>? enclosingController;

    /// <summary>
    /// Called when the state is pushed onto the stack; perform any necessary setup here. Use Resource Acquisition Is Initialization (RAII) as your entity does not necessarily know what is going on inside the state.
    /// </summary>
    /// <param name="parameters">A struct parameter to use inside the state.</param>
    /// <returns></returns>
    public abstract bool Enter(params T[] parameters);
    /// <summary>
    /// Called when the state is popped from the stack; perform any necessary cleanup of unmanaged resources here.
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public abstract bool Exit(params T[] parameters);
    public abstract bool Update(params T[] parameters);

	public bool IsActive => enclosingController?.CurrentState == this;

	public bool PopSelf(params T[] parameters) =>
		enclosingController?.PopState(stateID, parameters) ?? false;
}

public class StateController<T> where T : struct
{
	public StateController()
	{
		States = new Stack<State<T>>();
	}

	protected Stack<State<T>> States;
	public State<T>? CurrentState => States.TryPeek(out var state) ? state : null;

	public event EventHandler<StateEventArgs<T>>? StatePushed;
	public event EventHandler<StateEventArgs<T>>? StatePopped;

	private int _nextId;
	public bool PushState<S>(S state, params T[] arguments) where S : State<T>
	{
		if (state == null)
			throw new ArgumentNullException(nameof(state));

		arguments ??= Array.Empty<T>();
		state.enclosingController = this;
		state.stateID = new StateID(state.GetType().Name, _nextId++);

		States.Push(state);
		bool entered = state.Enter(arguments);

		if (!entered)
		{
			States.Pop();
			state.enclosingController = null;
			state.stateID = default;
			return false;
		}

		StatePushed?.Invoke(this, new StateEventArgs<T>(state.stateID, true, arguments));
		return true;
	}

	public bool TryPeek(out State<T> state) => States.TryPeek(out state);

	public bool ReplaceState<S>(S state, params T[] arguments) where S : State<T>
	{
		if (!PopCurState(arguments))
			return false;
		return PushState(state, arguments);
	}

	public bool PopCurState(params T[] arguments)
	{
		arguments ??= Array.Empty<T>();
		if (!States.TryPop(out var state))
			return false;

		bool exited = state.Exit(arguments);
		state.enclosingController = null;
		StatePopped?.Invoke(this, new StateEventArgs<T>(state.stateID, exited, arguments));
		state.stateID = new StateID(string.Empty, -1);
        
		return exited;
	}

	public bool PopState(StateID id, params T[] arguments)
	{
		if (States.TryPeek(out var currentState) && currentState.stateID == id)
			return PopCurState(arguments);
		return false;
	}

	public bool PopState(string name, params T[] arguments)
	{
		if (States.TryPeek(out var currentState) && string.Equals(currentState.stateID.name, name, StringComparison.Ordinal))
			return PopCurState(arguments);
		return false;
	}

	public void Clear(params T[] arguments)
	{
		while (States.Count > 0)
			PopCurState(arguments);
	}

	public bool Update(params T[] parameters)
	{
		if (States.TryPeek(out var currentState))
		{
			return currentState.Update(parameters);
		}
		return false;
	}
}