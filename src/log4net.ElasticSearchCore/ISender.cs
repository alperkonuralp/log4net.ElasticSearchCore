using log4net.Core;

namespace log4net.ElasticSearchCore
{
	public interface ISender
	{

		int WaitMilliSeconds { get; set; }
		IErrorHandler ErrorHandler { get; set; }

		void Start();
		void Stop();
	}
}