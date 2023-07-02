using System;
using System.Collections.Generic;

namespace RaActions
{
	public class RaActionsProcessor : IDisposable
	{
		public delegate void EventHandler(RaAction action);
		public delegate void EventSourceHandler(RaAction action, object source);
		public delegate void EventStateHandler(RaAction action, RaAction.RaActionState state);

		public event EventHandler StartedProcessingRootActionEvent;
		public event EventHandler EndedProcessingRootActionEvent;

		public event EventHandler StartedProcessingActionEvent;
		public event EventHandler EndedProcessingActionEvent;

		public event EventHandler ExecutedPreActionEvent;
		public event EventHandler ExecutedMainActionEvent;
		public event EventHandler ExecutedPostActionEvent;

		public event EventSourceHandler CancelledActionEvent;


		/// <summary>
		/// This event is called after main execution and every iteration 
		/// </summary>
		public event EventStateHandler ReactToActionHookEvent;

		private Stack<RaAction> _currentActionStack = new Stack<RaAction>();

		public bool Process(RaAction action)
		{
			return InternalProcess(action);
		}

		public bool Process<TParameters, TResult>(RaAction<TParameters, TResult> action, out TResult result)
			where TResult : IRaActionResult
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
			ReactionHookProcessing(action);
			ExecutedPreActionEvent?.Invoke(action);

			// -- Cancellation Check --
			if(action.IsCancelled)
			{
				// No need to Pop, for within the cancellation method this is already done
				return action.Success;
			}

			// -- Main Execution --
			action.SetState(RaAction.RaActionState.MainExecution);
			action.MainMethodSuccessStatus = action.InvokeMainMethod();
			ReactionHookProcessing(action);
			ExecutedMainActionEvent?.Invoke(action);

			// -- Post Execution --
			action.SetState(RaAction.RaActionState.PostExecution);
			action.InvokePostMethod();
			ReactionHookProcessing(action);
			ExecutedPostActionEvent?.Invoke(action);

			// -- End --
			_currentActionStack.Pop();
			action.SetState(RaAction.RaActionState.Completed);

			if(isRootAction)
			{
				EndedProcessingRootActionEvent?.Invoke(action);
			}
			// Mark the parent as dirty for the child behaviour has been executed
			else if(action.Success)
			{
				parentAction.MarkAsDirty();
			}

			EndedProcessingActionEvent?.Invoke(action);
			return action.Success;
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

		private void ReactionHookProcessing(RaAction action)
		{
			do
			{
				action.ClearDirtyMark();
				ReactToActionHookEvent?.Invoke(action, action.State);
			}
			while(action.IsDirty);
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

			ReactToActionHookEvent = null;

			_currentActionStack.Clear();
		}
	}
}