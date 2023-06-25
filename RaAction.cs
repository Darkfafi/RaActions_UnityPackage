namespace RaActions
{
	public abstract class RaAction<TParameters, TResult> : RaAction
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

		internal override void InvokeMainMethod()
		{
			Result = MainMethod.Invoke(Parameters);
		}
	}
}