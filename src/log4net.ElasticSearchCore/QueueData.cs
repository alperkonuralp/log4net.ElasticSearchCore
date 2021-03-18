using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace log4net.ElasticSearchCore
{
	internal class QueueData : IQueueData
	{
		private static readonly List<IQueueItemData> _emptyList = new List<IQueueItemData>();
		private readonly ConcurrentQueue<IQueueItemData> _queue = new ConcurrentQueue<IQueueItemData>();

		public QueueData(string name, string connectionString, int bufferSize, IQueueManager queueManager)
		{
			Name = name;
			ConnectionString = connectionString;
			QueueManager = queueManager;
			BufferSize = bufferSize;
		}

		public string Name { get; }
		public string ConnectionString { get; }
		public IQueueManager QueueManager { get; }

		public int BufferSize { get; set; }

		public TimeSpan WaitTimeout { get; set; } = new TimeSpan(0, 0, 1);

		public DateTimeOffset LastOperationTime { get; set; } = DateTimeOffset.Now;

		public int MessageCount()
		{
			return _queue.Count;
		}

		public void AddToQueue(IQueueItemData data)
		{
			_queue.Enqueue(data);
		}

		public void AddToQueue(IEnumerable<IQueueItemData> messages)
		{
			foreach (var item in messages)
				_queue.Enqueue(item);
		}

		public bool IsReadyForSendMessages()
		{
			if (BufferSize <= _queue.Count) return true;
			if ((DateTimeOffset.Now - LastOperationTime) > WaitTimeout && _queue.Count > 0) return true;
			return false;
		}

		public IReadOnlyList<IQueueItemData> GetQueueItemDatas()
		{
			//if (_queue.Count < (BufferSize / 10)) return _emptyList;
			List<IQueueItemData> messages = new List<IQueueItemData>(BufferSize);

			for (var i = 0; i < BufferSize && _queue.Count > 0; i++)
			{
				if (_queue.TryDequeue(out IQueueItemData queueItemData))
				{
					messages.Add(queueItemData);
				}
			}
			LastOperationTime = DateTimeOffset.Now;
			return messages;
		}

		public bool HasMessage()
		{
			return _queue.Count > 0;
		}

		public IReadOnlyList<IQueueItemData> GetAllMessages()
		{
			List<IQueueItemData> messages = new List<IQueueItemData>(BufferSize);

			while (_queue.Count > 0)
			{
				if (_queue.TryDequeue(out IQueueItemData queueItemData))
				{
					messages.Add(queueItemData);
				}
			}

			return messages;
		}
	}
}