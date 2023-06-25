using RaCollection;
using System;

namespace RaActions
{
	public struct RaActionData : IRaCollectionElement
	{
		public string Id
		{
			get;
		}

		public object Value
		{
			get;
		}

		public Action Callback
		{
			get;
		}

		public static RaActionData Create(string id, object value)
		{
			return new RaActionData(id, value);
		}

		public static RaActionData Create(string id, Action callback)
		{
			return new RaActionData(id, callback);
		}

		public RaActionData(string id, object value)
		{
			Id = id;
			Value = value;
			Callback = null;
		}

		public RaActionData(string id, Action callback)
		{
			Id = id;
			Value = null;
			Callback = callback;
		}
	}
}