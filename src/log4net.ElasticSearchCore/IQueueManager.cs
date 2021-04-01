using System.Collections.Generic;

namespace log4net.ElasticSearchCore
{
	public interface IQueueManager
	{
		bool IsReadyForSendMessages();

		void AddToQueue(ElasticSearchAppender appender, string indiceName, string message);

		IEnumerable<IQueueData> GetQueuesToReadyToSendMessages();

		IEnumerable<(IQueueData, IReadOnlyList<IQueueItemData>)> GetQueuesToHasMessage();
		IEnumerable<IQueueData> GetQueues();
	}
}