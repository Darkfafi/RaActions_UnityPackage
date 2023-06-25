using System;
using System.Collections.Generic;

namespace RaActions
{
	public class RaActionsProcessor : IDisposable
	{
		public delegate void EventHandler(RaAction action);
		public delegate void EventSourceHandler(RaAction action, object source);

		public event EventHandler StartedProcessingRootActionEvent;
		public event EventHandler EndedProcessingRootActionEvent;

		public event EventHandler StartedProcessingActionEvent;
		public event EventHandler EndedProcessingActionEvent;

		public event EventHandler ExecutedPreActionEvent;
		public event EventHandler ExecutedMainActionEvent;
		public event EventHandler ExecutedPostActionEvent;
		public event EventSourceHandler CancelledActionEvent;

		private Stack<RaAction> _currentActionStack = new Stack<RaAction>();

		public bool Process(RaAction action)
		{
			return InternalProcess(action);
		}

		public bool Process<TParameters, TResult>(RaAction<TParameters, TResult> action, out TResult result)
		{
			return action.Execute(this, out result);
		}

		internal bool InternalProcess(RaAction action)
		{
			if(action.State != RaAction.RaActionState.None)
			{
				throw new InvalidOperationException("Can't Process an action which is not in the state 'None'");
			}

			// -- Start -- 
			action.SetState(RaAction.RaActionState.Started);

			bool isRootAction = true;
			if(_currentActionStack.TryPeek(out RaAction parentAction))
			{
				isRootAction = false;
				parentAction.chainedActions.Add(action);
			}

			_currentActionStack.Push(action);

			if(isRootAction)
			{
				StartedProcessingRootActionEvent?.Invoke(action);
			}

			StartedProcessingActionEvent?.Invoke(action);

			// -- Pre Execution --
			action.SetState(RaAction.RaActionState.PreExecution);
			action.InvokePreMethod();
			ExecutedPreActionEvent?.Invoke(action);

			// -- Cancellation Check --
			if(action.IsCancelled)
			{
				// No need to Pop, for within the cancellation method this is already done
				return false;
			}

			// -- Main Execution --
			action.SetState(RaAction.RaActionState.MainExecution);
			action.InvokeMainMethod();
			ExecutedMainActionEvent?.Invoke(action);

			// -- Post Execution --
			action.SetState(RaAction.RaActionState.PostExecution);
			action.InvokePostMethod();
			ExecutedPostActionEvent?.Invoke(action);

			// -- End --
			_currentActionStack.Pop();
			action.SetState(RaAction.RaActionState.Completed);
			if(isRootAction)
			{
				EndedProcessingRootActionEvent?.Invoke(action);
			}

			EndedProcessingActionEvent?.Invoke(action);
			return true;
		}

		internal void InternalCancel(RaAction action, object source)
		{
			if(action.State > RaAction.RaActionState.PreExecution)
			{
				throw new InvalidOperationException("Can't cancel an action which is past Pre Execution");
			}

			action.SetState(RaAction.RaActionState.Cancelled);
			action.InvokeCancelMethod(source);

			if(action.IsBeingProcessed)
			{
				_currentActionStack.Pop();
			}

			CancelledActionEvent?.Invoke(action, source);
		}

		public void Dispose()
		{
			StartedProcessingRootActionEvent = null;
			EndedProcessingRootActionEvent = null;

			StartedProcessingActionEvent = null;
			EndedProcessingActionEvent = null;

			ExecutedPreActionEvent = null;
			ExecutedMainActionEvent = null;
			ExecutedPostActionEvent = null;
			CancelledActionEvent = null;

			_currentActionStack.Clear();
		}
	}
}