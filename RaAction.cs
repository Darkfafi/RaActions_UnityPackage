using System.Collections.Generic;
using System;

namespace RaActions
{
	public interface IRaActionResult
	{
		bool Success
		{
			get;
		}
	}

	public abstract class RaAction
	{
		public delegate bool SuccessHandler();
		public delegate void RaActionHandler(RaAction action);
		public delegate void RaActionCancelHandler(RaAction action, object source);

		public readonly ulong Id;
		private static ulong _counter = 0L;

		public RaActionState State
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

		public bool IsDirty
		{
			get; private set;
		}

		public object CancelSource
		{
			get; internal set;
		}

		public virtual bool Success => MainMethodSuccessStatus;

		internal bool MainMethodSuccessStatus;

		private SuccessHandler _mainAction = null;
		private RaActionHandler _preMethod = null;
		private RaActionHandler _postMethod = null;
		private RaActionCancelHandler _cancelMethod = null;

		public RaAction(SuccessHandler executeMethod)
		{
			Id = _counter++;
			MainMethodSuccessStatus = false;
			_mainAction = executeMethod;
		}

		public bool Execute(RaActionsProcessor processor)
		{
			return processor.InternalProcess(this);
		}

		public bool Cancel(RaActionsProcessor processor, object source)
		{
			return processor.InternalCancel(this, source);
		}

		public void MarkAsDirty()
		{
			IsDirty = true;
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

		internal virtual bool InvokeMainMethod()
		{
			return _mainAction.Invoke();
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

		internal void ClearDirtyMark()
		{
			IsDirty = false;
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

	public abstract class RaAction<TParameters, TResult> : RaAction
		where TResult : IRaActionResult
	{
		public delegate TResult MainHandler(TParameters parameters);

		public TParameters Parameters
		{
			get; private set;
		}

		public TResult Result
		{
			get; private set;
		}

		internal readonly MainHandler MainMethod;

		public RaAction(MainHandler executeMethod, TParameters parameters)
			: base(null)
		{
			MainMethod = executeMethod;
			Parameters = parameters;
		}

		public void SetParameters(TParameters parameters)
		{
			Parameters = parameters;
		}


		public bool Execute(RaActionsProcessor processor, out TResult result)
		{
			return Execute(processor, out result, out _);
		}

		public bool Execute(RaActionsProcessor processor, out TResult result, out TParameters parameters)
		{
			bool success = processor.InternalProcess(this);
			result = Result;
			parameters = Parameters;
			return success;
		}

		internal override bool InvokeMainMethod()
		{
			Result = MainMethod.Invoke(Parameters);
			return Result != null && Result.Success;
		}
	}
}