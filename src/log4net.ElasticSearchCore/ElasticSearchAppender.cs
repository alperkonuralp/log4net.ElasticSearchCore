using log4net.Appender;
using log4net.Core;
using log4net.Util;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("log4net.ElasticSearchCore.Tests")]
[assembly: InternalsVisibleTo("MessagePack")]

namespace log4net.ElasticSearchCore
{
	public class ElasticSearchAppender : AppenderSkeleton
	{
		private static readonly IQueueManager _queueManager = new QueueManager();
		private static readonly ISender _sender = new Sender(_queueManager);

		static ElasticSearchAppender()
		{
			_sender.Start();
		}

		public ElasticSearchAppender() : base()
		{
		}

		public string ConnectionString { get; set; }

		public string TargetIndexPrefix { get; set; } = "lodash";

		public string UndeliverableItemsLogFolder { get; set; } = null;

		public int UndeliverableItemsRetryCount { get; set; } = 5;

		public int BufferSize { get; set; } = 10;
		public int ElasticVersion { get; set; } = 6;

		protected override void Append(LoggingEvent loggingEvent)
		{
			var msg = RenderLoggingEvent(loggingEvent);
			var today = loggingEvent.TimeStamp.ToUniversalTime().Date;

			_queueManager.AddToQueue(this, $"{TargetIndexPrefix}-{today:yyyyMMdd}", msg);
		}

		protected override void OnClose()
		{
			LogLog.Error(typeof(ElasticSearchAppender), "OnClose()");
			_sender.Stop();
			base.OnClose();
			// asd
		}
	}
}