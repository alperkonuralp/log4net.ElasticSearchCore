using log4net.Appender;
using log4net.Core;
using System;

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
			if (_sender.ErrorHandler == null) _sender.ErrorHandler = this.ErrorHandler;
		}

		public string ConnectionString { get; set; }
		public string TargetIndexPrefix { get; set; } = "lodash";

		public int BufferSize { get; set; } = 10;

		protected override void Append(LoggingEvent loggingEvent)
		{
			var msg = RenderLoggingEvent(loggingEvent);
			var today = loggingEvent.TimeStamp.ToUniversalTime().Date;

			_queueManager.AddToQueue(Name, ConnectionString, $"{this.TargetIndexPrefix}-{today:yyyyMMdd}", msg, BufferSize);
		}

		protected override void OnClose()
		{
			ErrorHandler.Error("OnClose()");
			_sender.Stop();
			base.OnClose();
			// asd
		}
	}
}