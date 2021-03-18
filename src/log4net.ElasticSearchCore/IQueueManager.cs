using System.Collections.Generic;

namespace log4net.ElasticSearchCore
{
	public interface IQueueManager
	{
		bool IsReadyForSendMessages();

		void AddToQueue(string name, string connectionString, string indiceName, string message, int bufferSize);

		IEnumerable<IQueueData> GetQueuesToReadyToSendMessages();

		IEnumerable<(IQueueData, IReadOnlyList<IQueueItemData>)> GetQueuesToHasMessage();
		IEnumerable<IQueueData> GetQueues();
	}
}