using System;
using System.Collections.Generic;

namespace log4net.ElasticSearchCore
{
	public interface IQueueData
	{
		int BufferSize { get; set; }
		string ConnectionString { get; }
		DateTimeOffset LastOperationTime { get; set; }
		string Name { get; }
		IQueueManager QueueManager { get; }
		TimeSpan WaitTimeout { get; set; }

		void AddToQueue(IQueueItemData data);

		void AddToQueue(IEnumerable<IQueueItemData> messages);

		IReadOnlyList<IQueueItemData> GetQueueItemDatas();

		bool HasMessage();

		bool IsReadyForSendMessages();

		IReadOnlyList<IQueueItemData> GetAllMessages();
		int MessageCount();
	}
}