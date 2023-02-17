using System;
using System.Collections.Generic;

namespace RaActions
{
	public class RaActionProcessor : IDisposable
	{
		public delegate void ActionStageHandler(RaAction action, RaAction.ActionStage stage);

		public event ActionStageHandler StageExecutedEvent;
		public event ActionStageHandler PreStageChainEvent;
		public event ActionStageHandler PostStageChainEvent;

		public event RaAction.Handler EnqueuedActionEvent;
		public event RaAction.Handler StartedActionEvent;
		public event RaAction.Handler FinishedActionEvent;

		private readonly Queue<RaAction> _queuedActions = new Queue<RaAction>();
		private readonly Stack<RaAction> _executionStack = new Stack<RaAction>();

		public bool IsProcessing => _executionStack.Count > 0;

		public void EnqueueAction(RaAction action)
		{
			action = action.Build_Result();
			_queuedActions.Enqueue(action);
			EnqueuedActionEvent?.Invoke(action);
			TryProcess();
		}

		public void Dispose()
		{
			_queuedActions.Clear();
			_executionStack.Clear();

			FinishedActionEvent = null;
			StartedActionEvent = null;
			EnqueuedActionEvent = null;

			PostStageChainEvent = null;
			PreStageChainEvent = null;
			StageExecutedEvent = null;
		}

		private void TryProcess()
		{
			if(IsProcessing)
			{
				return;
			}

			// Initial Start
			PushToExecutionStack(_queuedActions.Dequeue());

			List<RaAction> actionsToDispose = new List<RaAction>();

			while(_executionStack.Count > 0)
			{
				RaAction currentAction = _executionStack.Peek();

				// Pre-Processing
				if(ProcessStage(currentAction, RaAction.ActionStage.PreProcessing))
				{
					continue;
				}

				// Cancelled
				if(currentAction.IsMarkedAsCancelled)
				{
					if(ProcessStage(currentAction, RaAction.ActionStage.Cancelled))
					{
						continue;
					}
				}

				// Processing
				if(ProcessStage(currentAction, RaAction.ActionStage.Processing))
				{
					continue;
				}

				// Post-Processing
				if(ProcessStage(currentAction, RaAction.ActionStage.PostProcessing))
				{
					continue;
				}

				// Finish
				currentAction = _executionStack.Pop();
				FinishedActionEvent?.Invoke(currentAction);

				// Clean-up
				currentAction.Dispose();

				// On Finish, Start Next if any is waiting
				if(_queuedActions.Count > 0)
				{
					PushToExecutionStack(_queuedActions.Dequeue());
				}
			}
		}

		private void PushToExecutionStack(RaAction action)
		{
			_executionStack.Push(action);
			StartedActionEvent?.Invoke(action);
		}

		private bool ProcessStage(RaAction action, RaAction.ActionStage stage)
		{
			if(EnterChainStage(action, stage))
			{
				if(TryContinueChain(action, stage))
				{
					return true;
				}
				FinishChainStage(action);
			}
			return false;
		}

		private bool EnterChainStage(RaAction action, RaAction.ActionStage stage)
		{
			if(action.CurrentStage != stage && !action.HasPassedStage(stage))
			{
				action.CurrentStage = stage;

				if(stage > action.LastEnteredChainStage)
				{
					action.LastEnteredChainStage = stage;
				}

				if(action.TryExecute(stage))
				{
					StageExecutedEvent?.Invoke(action, stage);
				}

				PreStageChainEvent?.Invoke(action, stage);
				return true;
			}
			return false;
		}

		private bool TryContinueChain(RaAction action, RaAction.ActionStage stage)
		{
			if(action.TryDequeueChain(stage, out RaAction chainAction))
			{
				action.CurrentStage = RaAction.ActionStage.Chaining;
				PushToExecutionStack(chainAction);
				return true;
			}

			return false;
		}

		private bool FinishChainStage(RaAction action)
		{
			if(!action.HasPassedStage(action.LastEnteredChainStage))
			{
				action.LastFinishedChainStage = action.LastEnteredChainStage;
				PostStageChainEvent?.Invoke(action, action.LastFinishedChainStage);
				return true;
			}
			return false;
		}
	}
}