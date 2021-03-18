using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace log4net.ElasticSearchCore
{
	internal class QueueManager : IQueueManager
	{
		private readonly ConcurrentDictionary<string, IQueueData> _queues =
			new ConcurrentDictionary<string, IQueueData>();

		public bool IsReadyForSendMessages()
		{
			return _queues.Any(x => x.Value.IsReadyForSendMessages());
		}

		public void AddToQueue(string name, string connectionString, string indiceName, string message, int bufferSize)
		{
			_queues
				.GetOrAdd(name, _ => new QueueData(_, connectionString, bufferSize, this))
				.AddToQueue(new QueueItemData() { IndiceName = indiceName, Message = message, Id = Guid.NewGuid() });
		}

		public IEnumerable<(IQueueData, IReadOnlyList<IQueueItemData>)> GetQueuesToHasMessage()
		{
			foreach (var item in _queues.Where(x => x.Value.HasMessage()))
			{
				yield return (item.Value, item.Value.GetAllMessages());
			}
		}

		public IEnumerable<IQueueData> GetQueuesToReadyToSendMessages()
		{
			foreach (var item in _queues.Where(x => x.Value.IsReadyForSendMessages()))
			{
				yield return item.Value;
			}
		}
		public IEnumerable<IQueueData> GetQueues()
		{
			return _queues.Values.AsEnumerable();
		}
	}
}