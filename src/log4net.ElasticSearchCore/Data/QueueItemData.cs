using MessagePack;
using System;

namespace log4net.ElasticSearchCore.Data
{
	[MessagePackObject]
	public class QueueItemData : IQueueItemData
	{
		[Key("id")]
		public Guid Id { get; set; }

		[Key("indiceName")]
		public string IndiceName { get; set; }

		[Key("message")]
		public string Message { get; set; }

		[Key("retryCount")]
		public int RetryCount { get; set; } = 1;

		[Key("lastErrorMessage")]
		public string LastErrorMessage { get; set; }
	}
}