using System.Collections.Generic;
using System;

namespace RaActions
{
	public abstract class RaAction
	{
		public delegate void RaActionHandler(RaAction action);
		public delegate void RaActionCancelHandler(RaAction action, object source);

		internal RaActionState State
		{
			get; private set;
		}

		public bool IsEditable => State <= RaActionState.Started;
		public bool IsBeingProcessed => State >= RaActionState.Started && State < RaActionState.Completed;
		public bool IsProcessed => State >= RaActionState.Completed;
		public bool IsCompleted => State == RaActionState.Completed;
		public bool IsCancelled => State == RaActionState.Cancelled;

		internal List<RaAction> chainedActions = new List<RaAction>();

		public IReadOnlyList<RaAction> ChainedActions => chainedActions;

		private Action _mainAction = null;
		private RaActionHandler _preMethod = null;
		private RaActionHandler _postMethod = null;
		private RaActionCancelHandler _cancelMethod = null;

		public RaAction(Action executeMethod)
		{
			_mainAction = executeMethod;
		}

		public bool Execute(RaActionsProcessor processor)
		{
			return processor.InternalProcess(this);
		}

		public void Cancel(RaActionsProcessor processor, object source)
		{
			processor.InternalCancel(this, source);
		}

		public void SetPreExecuteMethod(RaActionHandler method)
		{
			if(!IsEditable)
			{
				throw new InvalidOperationException($"Can't {nameof(SetPreExecuteMethod)} on action where {nameof(IsEditable)} is False");
			}

			_preMethod = method;
		}

		public void SetPostExecuteMethod(RaActionHandler method)
		{
			if(!IsEditable)
			{
				throw new InvalidOperationException($"Can't {nameof(SetPostExecuteMethod)} on action where {nameof(IsEditable)} is False");
			}

			_postMethod = method;
		}
		public void SetCancelExecuteMethod(RaActionCancelHandler method)
		{
			if(!IsEditable)
			{
				throw new InvalidOperationException($"Can't {nameof(SetCancelExecuteMethod)} on action where {nameof(IsEditable)} is False");
			}

			_cancelMethod = method;
		}

		internal virtual void InvokePreMethod()
		{
			_preMethod?.Invoke(this);
		}

		internal virtual void InvokeMainMethod()
		{
			_mainAction.Invoke();
		}

		internal virtual void InvokePostMethod()
		{
			_postMethod?.Invoke(this);
		}

		internal virtual void InvokeCancelMethod(object source)
		{
			_cancelMethod?.Invoke(this, source);
		}

		internal void SetState(RaActionState state)
		{
			if(State != state)
			{
				State = state;
			}
		}

		public enum RaActionState
		{
			None = 0,
			Started = 1,
			PreExecution = 2,
			MainExecution = 3,
			PostExecution = 4,
			Completed = 5,
			Cancelled = 6,
		}
	}
}