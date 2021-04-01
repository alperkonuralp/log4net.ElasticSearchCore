using MessagePack;
using System;

namespace log4net.ElasticSearchCore
{
	[MessagePack.Union(0, typeof(Data.QueueItemData))]
	public interface IQueueItemData
	{
		Guid Id { get; set; }
		string IndiceName { get; set; }
		string Message { get; set; }
		int RetryCount { get; set; }
		string LastErrorMessage { get; set; }
	}
}