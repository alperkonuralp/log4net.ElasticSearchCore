using log4net.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace log4net.ElasticSearchCore.Data
{
	public class QueueData : IQueueData
	{
		private static readonly List<IQueueItemData> _emptyList = new List<IQueueItemData>();
		private readonly ConcurrentQueue<IQueueItemData> _queue = new ConcurrentQueue<IQueueItemData>();

		public QueueData(ElasticSearchAppender appender, IQueueManager queueManager)
		{
			Appender = appender;
			Name = appender.Name;
			QueueManager = queueManager;
		}

		public ElasticSearchAppender Appender { get; }
		public string Name { get; }
		public IQueueManager QueueManager { get; }

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
			if (Appender.BufferSize <= _queue.Count) return true;
			if (DateTimeOffset.Now - LastOperationTime > WaitTimeout && _queue.Count > 0) return true;
			return false;
		}

		private static readonly object GetQueueItemDatasLock = new object();

		public IReadOnlyList<IQueueItemData> GetQueueItemDatas()
		{
			List<IQueueItemData> undeliverableItems = new List<IQueueItemData>();
			try
			{
				lock (GetQueueItemDatasLock)
				{
					List<IQueueItemData> messages = new List<IQueueItemData>(Appender.BufferSize);

					for (var i = 0; i < Appender.BufferSize && _queue.Count > 0; i++)
					{
						if (_queue.TryDequeue(out IQueueItemData queueItemData))
						{
							if (queueItemData.RetryCount > Appender.UndeliverableItemsRetryCount)
							{
								undeliverableItems.Add(queueItemData);
							}
							else
								messages.Add(queueItemData);
						}
					}

					LastOperationTime = DateTimeOffset.Now;
					return messages;
				}
			}
			finally
			{
				if (undeliverableItems.Count > 0)
				{
					if (!string.IsNullOrWhiteSpace(Appender.UndeliverableItemsLogFolder))
					{
						WriteUndeliverableItemsToFile(undeliverableItems);
					}
					else
					{
						WriteUndeliverableItemsToErrorLog(undeliverableItems);
					}
				}
			}
		}

		private void WriteUndeliverableItemsToFile(List<IQueueItemData> undeliverableItems)
		{
			try
			{
				var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;

				var bson = MessagePack.MessagePackSerializer.Typeless.Serialize(undeliverableItems, options);
				var str = MessagePack.MessagePackSerializer.ConvertToJson(bson);
				var folderName = Path.Combine(Appender.UndeliverableItemsLogFolder);
				if (!Directory.Exists(folderName)) Directory.CreateDirectory(folderName);

				var fileName = Path.Combine(folderName, $"UndeliverableItems-{DateTime.Now.ToString("yyyyMMddHHmmssfffffff")}.json");

				File.WriteAllText(fileName, str);
			}
			catch (Exception ex)
			{
				LogLog.Error(typeof(ElasticSearchAppender), "Error when messages writing to file : " + ex.Message, ex);
			}
		}

		private void WriteUndeliverableItemsToErrorLog(List<IQueueItemData> undeliverableItems)
		{
			var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
			var bson = MessagePack.MessagePackSerializer.Typeless.Serialize(undeliverableItems, options);
			var str = MessagePack.MessagePackSerializer.ConvertToJson(bson);
			LogLog.Error(typeof(ElasticSearchAppender), "This Messages undeliverable : " + str);
		}

		public bool HasMessage()
		{
			return _queue.Count > 0;
		}

		public IReadOnlyList<IQueueItemData> GetAllMessages()
		{
			List<IQueueItemData> messages = new List<IQueueItemData>(Appender.BufferSize);

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