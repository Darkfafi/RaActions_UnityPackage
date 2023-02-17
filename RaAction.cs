using RaCollection;
using System;
using System.Collections.Generic;

namespace RaActions
{
	public class RaAction
	{
		public delegate void Handler(RaAction action);

		public string Name
		{
			get; private set;
		}

		internal ActionStage LastEnteredChainStage = ActionStage.None;
		internal ActionStage CurrentStage = ActionStage.None;
		internal ActionStage LastFinishedChainStage = ActionStage.None;
		internal bool IsMarkedAsCancelled = false;

		internal RaElementCollection<RaActionData> _chainData = new RaElementCollection<RaActionData>();
		internal HashSet<string> _chainTags = new HashSet<string>();

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

		public static RaAction Create(Handler mainMethod)
		{
			return new RaAction(mainMethod, null, null, null).Build_SetName("-anonymous-");
		}

		public static RaAction Create<T>(Handler mainMethod)
		{
			return new RaAction(mainMethod, null, null, null).Build_SetName<T>();
		}

		public static RaAction Create(string name, Handler mainMethod)
		{
			return new RaAction(mainMethod, null, null, null).Build_SetName(name);
		}

		public static RaAction Create<T>(string nameSuffix, Handler mainMethod)
		{
			return new RaAction(mainMethod, null, null, null).Build_SetName<T>(nameSuffix);
		}

		internal RaAction(Handler mainMethod, Handler preMethod, Handler postMethod, Handler cancelledMethod)
		{
			_method = mainMethod;
			_preMethod = preMethod;
			_postMethod = postMethod;
			_cancelledMethod = cancelledMethod;
		}

		public RaAction Build_SetName<T>()
		{
			ThrowIfNotConstruction(nameof(Build_SetName));
			Name = CreateName<T>();
			return this;
		}

		public RaAction Build_SetName<T>(string nameSuffix)
		{
			ThrowIfNotConstruction(nameof(Build_SetName));
			Name = CreateName<T>(nameSuffix);
			return this;
		}

		public RaAction Build_SetName(string name)
		{
			ThrowIfNotConstruction(nameof(Build_SetName));
			Name = CreateName(name);
			return this;
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

		public bool HasName(string name) => Name.Equals(CreateName(name));
		public bool HasName<T>() => Name.Equals(CreateName<T>());
		public bool HasName<T>(string nameSuffix) => Name.Equals(CreateName<T>(nameSuffix));

		public bool HasTag(string tag, bool inChain = false) => _tags.Contains(tag) || (inChain && _chainTags.Contains(tag));

		public bool HasData(string key, bool inChain = false) => _data.Contains(key) || (inChain && _chainData.Contains(key));

		public RaAction SetTag(string tag, bool inChain = false)
		{
			ThrowIfDisposedState(nameof(SetTag));
			_tags.Add(tag);
			if(inChain)
			{
				_chainTags.Add(tag);
			}
			return this;
		}

		public RaAction RemoveTag(string tag, bool inChain = false)
		{
			ThrowIfDisposedState(nameof(SetTag));
			_tags.Remove(tag);
			if(inChain)
			{
				_chainTags.Remove(tag);
			}
			return this;
		}

		public RaAction RemoveData(string id, bool inChain = false)
		{
			ThrowIfDisposedState(nameof(RemoveData));
			_data.Remove(id);
			if(inChain)
			{
				_chainData.Remove(id);
			}
			return this;
		}

		public RaAction SetData(string id, object data, bool inChain = false)
		{
			ThrowIfDisposedState(nameof(SetData));
			RaActionData actionData = RaActionData.Create(id, data);
			_data.Add(actionData);
			if(inChain)
			{
				_chainData.Add(actionData);
			}
			return this;
		}

		public T GetData<T>(bool inChain = false)
		{
			if(TryGetData(out T data, inChain))
			{
				return data;
			}

			return default;
		}

		public T GetData<T>(string key)
		{
			TryGetData(key, out T data);
			return data;
		}

		public bool TryGetData<T>(out T value, bool inChain = false)
		{
			if(_data.TryGetItem(out RaActionData data, (x) => x.Value is T))
			{
				value = (T)data.Value;
				return true;
			}

			if(inChain)
			{
				if(_chainData.TryGetItem(out data, (x) => x.Value is T))
				{
					value = (T)data.Value;
					return true;
				}
			}

			value = default;
			return false;
		}

		public bool TryGetData<T>(string key, out T value, bool inChain = false)
		{
			if(_data.TryGetItem(key, out RaActionData data) && data.Value is T castedValue)
			{
				value = castedValue;
				return true;
			}

			if(inChain)
			{
				if(_chainData.TryGetItem(key, out data) && data.Value is T castedChainValue)
				{
					value = castedChainValue;
					return true;
				}
			}

			value = default;
			return false;
		}

		public List<T> GetAllData<T>(bool inChain = false)
		{
			List<T> values = new List<T>();

			_data.ForEach((data, index) =>
			{
				if(data.Value is T castedValue)
				{
					values.Add(castedValue);
				}
			});

			if(inChain)
			{
				_chainData.ForEach((data, index) =>
				{
					if(data.Value is T castedValue)
					{
						values.Add(castedValue);
					}
				});
			}

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

			_chainData = null;
			_chainTags = null;
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

		private string CreateName(string name)
		{
			return name;
		}

		private string CreateName<T>()
		{
			return typeof(T).FullName;
		}

		private string CreateName<T>(string suffix)
		{
			return string.Concat(CreateName<T>(), "_", suffix);
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