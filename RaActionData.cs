using RaCollection;

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

		public static RaActionData Create(string id, object value)
		{
			return new RaActionData(id, value);
		}

		public RaActionData(string id, object value)
		{
			Id = id;
			Value = value;
		}
	}
}