using System;
using System.Collections.Generic;

namespace RaActions
{
	public class RaActionsProcessor : IDisposable
	{
		public delegate void ActionStageHandler(RaAction action, RaAction.ActionStage stage);

		public event ActionStageHandler ActionExecutedEvent;
		public event ActionStageHandler PreActionChainEvent;
		public event ActionStageHandler PostActionChainEvent;

		public event RaAction.Handler SetRootActionEvent;
		public event RaAction.Handler ClearedRootActionEvent;

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

			PostActionChainEvent = null;
			PreActionChainEvent = null;
			ActionExecutedEvent = null;
		}

		public bool TryContinueChain(RaAction action)
		{
			if(action.LastFinishedChainStage == action.CurrentStage)
			{
				return false;
			}

			switch(action.CurrentStage)
			{
				case RaAction.ActionStage.PreProcessing:
				case RaAction.ActionStage.Cancelled:
				case RaAction.ActionStage.Processing:
				case RaAction.ActionStage.PostProcessing:
					return TryContinueChain(action, action.CurrentStage);
				default:
					return false;
			}
		}

		private void TryProcess()
		{
			if(IsProcessing)
			{
				return;
			}

			// Initial Start
			PushToExecutionStack(_queuedActions.Dequeue());

			RaAction rootAction = null;

			while(_executionStack.Count > 0)
			{
				RaAction currentAction = _executionStack.Peek();

				if(rootAction == null)
				{
					rootAction = currentAction;
					SetRootActionEvent?.Invoke(rootAction);
				}

				currentAction._chainData = rootAction._chainData;
				currentAction._chainTags = rootAction._chainTags;

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

				// If we are not the root action, clean and continue
				if(currentAction != rootAction)
				{
					currentAction.Dispose();
					continue;
				}

				// The Root Action has Finished, so the chain has been Completed. 
				// We can clean the root action and its chain links now
				RaAction preRootAction = rootAction;
				rootAction = null;

				ClearedRootActionEvent?.Invoke(preRootAction);
				preRootAction._chainData.Clear();
				preRootAction._chainTags.Clear();
				preRootAction.Dispose();

				// If there are still actions in the queue, then push the next to the execution stack
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
					ActionExecutedEvent?.Invoke(action, stage);
				}

				PreActionChainEvent?.Invoke(action, stage);
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
				PostActionChainEvent?.Invoke(action, action.LastFinishedChainStage);
				return true;
			}
			return false;
		}
	}
}