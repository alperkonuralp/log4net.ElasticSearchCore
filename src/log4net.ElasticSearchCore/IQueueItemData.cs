using System;

namespace log4net.ElasticSearchCore
{
	public interface IQueueItemData
	{
		Guid Id { get; set; }
		string IndiceName { get; set; }
		string Message { get; set; }
	}
}