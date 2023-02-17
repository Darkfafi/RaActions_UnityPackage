using RaCollection;
using System;
using System.Collections.Generic;

namespace RaActions
{
	public class RaAction
	{
		public delegate void Handler(RaAction action);

		internal ActionStage LastEnteredChainStage = ActionStage.None;
		internal ActionStage CurrentStage = ActionStage.None;
		internal ActionStage LastFinishedChainStage = ActionStage.None;
		internal bool IsMarkedAsCancelled = false;

		private bool _isConstructed = false;

		private readonly RaElementCollection<RaActionData> _data = new RaElementCollection<RaActionData>();
		private readonly HashSet<string> _tags = new HashSet<string>();

		private Queue<RaAction> _preChain = new Queue<RaAction>();
		private Queue<RaAction> _chain = new Queue<RaAction>();
		private Queue<RaAction> _postChain = new Queue<RaAction>();
		private Queue<RaAction> _cancelledChain = new Queue<RaAction>();

		private ActionStage _lastExecutedStage = ActionStage.None;

		private Handler _preMethod = null;
		private Handler _method = null;
		private Handler _postMethod = null;
		private Handler _cancelledMethod = null;

		public static RaAction Create(Handler mainMethod, Handler preMethod = null, Handler postMethod = null, Handler cancelledMethod = null)
		{
			return new RaAction(mainMethod, preMethod, postMethod, cancelledMethod);
		}

		internal RaAction(Handler mainMethod, Handler preMethod, Handler postMethod, Handler cancelledMethod)
		{
			_method = mainMethod;
			_preMethod = preMethod;
			_postMethod = postMethod;
			_cancelledMethod = cancelledMethod;
		}

		public RaAction Build_SetMethod(Handler method)
		{
			ThrowIfNotConstruction(nameof(Build_SetMethod));
			_method = method;
			return this;
		}

		public RaAction Build_SetCancelledMethod(Handler method)
		{
			ThrowIfNotConstruction(nameof(Build_SetCancelledMethod));
			_cancelledMethod = method;
			return this;
		}

		public RaAction Build_SetPostMethod(Handler method)
		{
			ThrowIfNotConstruction(nameof(Build_SetPostMethod));
			_postMethod = method;
			return this;
		}

		public RaAction Build_SetPreMethod(Handler method)
		{
			ThrowIfNotConstruction(nameof(Build_SetPreMethod));
			_preMethod = method;
			return this;
		}

		public RaAction Build_Result()
		{
			_isConstructed = true;
			return this;
		}

		public bool HasTag(string tag) => _tags.Contains(tag);

		public bool HasData(string key) => _data.Contains(key);

		public RaAction SetTag(string tag)
		{
			ThrowIfDisposedState(nameof(SetTag));
			_tags.Add(tag);
			return this;
		}

		public RaAction RemoveTag(string tag)
		{
			ThrowIfDisposedState(nameof(SetTag));
			_tags.Remove(tag);
			return this;
		}

		public RaAction RemoveData(string id)
		{
			ThrowIfDisposedState(nameof(RemoveData));
			_data.Remove(id);
			return this;
		}

		public RaAction SetData(string id, object data)
		{
			ThrowIfDisposedState(nameof(SetData));
			_data.Add(RaActionData.Create(id, data));
			return this;
		}

		public T GetData<T>()
		{
			TryGetData(out T data);
			return data;
		}

		public bool TryGetData<T>(out T value)
		{
			if(_data.TryGetItem(out RaActionData data, (x) => x.Value is T))
			{
				value = (T)data.Value;
				return true;
			}

			value = default;
			return false;
		}

		public bool TryGetData<T>(string key, out T value)
		{
			if(_data.TryGetItem(key, out RaActionData data) && data.Value is T castedValue)
			{
				value = castedValue;
				return true;
			}
			value = default;
			return false;
		}

		public List<T> GetAllData<T>()
		{
			List<T> values = new List<T>();
			_data.ForEach((data, index) =>
			{
				if(data.Value is T castedValue)
				{
					values.Add(castedValue);
				}
			});
			return values;
		}

		public void MarkAsCancelled()
		{
			ThrowIfDisposedState(nameof(MarkAsCancelled));
			IsMarkedAsCancelled = true;
		}

		public void ChainAction(RaAction action)
		{
			ChainAction(action, LastEnteredChainStage);
		}

		public void ChainAction(RaAction action, ActionStage stage)
		{
			if(HasPassedStage(stage))
			{
				throw new InvalidOperationException($"Can't Chain action {action} to {this}. The Stage {stage} has already passed. Current Stage {LastEnteredChainStage}");
			}
			GetChain(stage)?.Enqueue(action);
		}

		public bool HasPassedStage(ActionStage stage)
		{
			return LastFinishedChainStage >= stage;
		}

		internal void Dispose()
		{
			ThrowIfDisposedState(nameof(Dispose));

			LastEnteredChainStage =
				LastFinishedChainStage =
				CurrentStage =
				_lastExecutedStage = ActionStage.Disposed;


			_preMethod = null;
			_method = null;
			_postMethod = null;
			_cancelledMethod = null;

			_preChain.Clear();
			_chain.Clear();
			_postChain.Clear();
			_cancelledChain.Clear();

			_data.Clear();
			_tags.Clear();
		}

		internal bool TryDequeueChain(ActionStage stage, out RaAction action)
		{
			ThrowIfDisposedState(nameof(TryDequeueChain));
			Queue<RaAction> chain = GetChain(stage);

			if(chain != null)
			{
				if(chain.Count > 0)
				{
					action = chain.Dequeue();
					return true;
				}
			}

			action = null;
			return false;
		}

		internal bool TryExecute(ActionStage stage)
		{
			ThrowIfDisposedState(nameof(TryExecute));
			if(_lastExecutedStage < stage)
			{
				_lastExecutedStage = stage;

				Handler method = null;
				switch(_lastExecutedStage)
				{
					case ActionStage.PreProcessing:
						method = _preMethod;
						_preMethod = null;
						break;
					case ActionStage.Processing:
						method = _method;
						_method = null;
						break;
					case ActionStage.PostProcessing:
						method = _postMethod;
						_postMethod = null;
						break;
					case ActionStage.Cancelled:
						method = _cancelledMethod;
						_cancelledMethod = null;
						break;
				}
				method?.Invoke(this);
				return true;
			}

			return false;
		}

		private Queue<RaAction> GetChain(ActionStage stage)
		{
			switch(stage)
			{
				case ActionStage.PreProcessing:
					return _preChain;
				case ActionStage.Processing:
					return _chain;
				case ActionStage.PostProcessing:
					return _postChain;
				case ActionStage.Cancelled:
					return _cancelledChain;
			}

			return null;
		}

		private void ThrowIfNotConstruction(string methodName)
		{
			ThrowIfDisposedState(methodName);
			if(_isConstructed)
			{
				throw new InvalidOperationException($"Can't perform {methodName} for {nameof(RaAction)} is no longer under construction");
			}
		}

		private void ThrowIfDisposedState(string methodName)
		{
			if(HasPassedStage(ActionStage.Disposed))
			{
				throw new InvalidOperationException($"Can't perform {methodName} on dispossed Action");
			}
		}

		public enum ActionStage
		{
			None = 0,
			Chaining = 1,
			PreProcessing = 10,
			Processing = 20,
			PostProcessing = 30,
			Cancelled = 100,
			Disposed = 9999,
		}
	}
}