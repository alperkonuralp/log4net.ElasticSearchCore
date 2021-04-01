using log4net.Core;

namespace log4net.ElasticSearchCore
{
	public interface ISender
	{

		int WaitMilliSeconds { get; set; }

		void Start();
		void Stop();
	}
}