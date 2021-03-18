using System;

namespace log4net.ElasticSearchCore
{
	internal class QueueItemData : IQueueItemData
	{
		public Guid Id { get; set; }
		public string IndiceName { get; set; }

		public string Message { get; set; }
	}
}