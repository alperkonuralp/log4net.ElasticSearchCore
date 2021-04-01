using log4net.Core;
using log4net.ElasticSearchCore.ElasticSearchResponse;
using log4net.Util;
using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.ElasticSearchCore
{
	public class Sender : ISender
	{
		private static readonly ConcurrentQueue<HttpClient> _httpClients = new ConcurrentQueue<HttpClient>();

		private static readonly ConcurrentDictionary<string, ConnectionStringManager> _connectionStringManagers
			= new ConcurrentDictionary<string, ConnectionStringManager>();

		private static readonly MediaTypeHeaderValue _jsonMimeType = new MediaTypeHeaderValue("application/json");
		private static readonly object isStopedLock = new object();

		private bool isStoped = false;
		private readonly IQueueManager _queueManager;
		private Thread _workerThread;
		private readonly ConcurrentDictionary<IQueueData, Thread> _queueThreads = new ConcurrentDictionary<IQueueData, Thread>();
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private CancellationToken _cancellationToken;

		public Sender(IQueueManager queueManager)
		{
			_queueManager = queueManager;
			_cancellationToken = _cancellationTokenSource.Token;
		}

		public int WaitMilliSeconds { get; set; } = 500;

		public void Start()
		{
			if (_workerThread == null)
			{
				_workerThread = new Thread(WorkerThreadAction)
				{
					IsBackground = true
				};
			}
			_workerThread.Start(_cancellationToken);
		}

		public void Stop()
		{
			if (!isStoped) return;
			lock (isStopedLock)
			{
				if (!isStoped) return;
				isStoped = true;
				_cancellationTokenSource.Cancel();
				if (_workerThread == null) return;
				_workerThread.Join();
				_workerThread = null;
			}
		}

		private Thread StartNewThreadWithValue(ParameterizedThreadStart action, object value)
		{
			var t = new Thread(action);
			t.Start(value);
			return t;
		}

		private void WorkerThreadAction(object obj)
		{
			Thread.CurrentThread.Name = "WorkerThreadAction";
			var cancellationToken = (CancellationToken)obj;

			var stopwatch = new Stopwatch();
			while (true)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					SendAllDataToElasticSearch();
					break;
				}

				stopwatch.Restart();

				var queues = _queueManager.GetQueues();

				foreach (var queue in queues)
				{
					if (!queue.HasMessage())
					{
						continue;
					}

					var t2 = _queueThreads.GetOrAdd(queue, (q) => StartNewThreadWithValue(QueueThreadAction, q));

					if (t2 != null && !t2.IsAlive)
					{
						_queueThreads.TryRemove(queue, out _);
					}
				}

				Thread.Sleep(WaitMilliSeconds);
			}
		}

		private void QueueThreadAction(object o)
		{
			var q = o as IQueueData;
			Thread.CurrentThread.Name = "QueueThreadAction_" + q.Name;
			LogLog.Error(typeof(Sender), $"{Thread.CurrentThread.ManagedThreadId} - {Thread.CurrentThread.Name} Started.");

			for (var i = 0; i < 3; i++)
			{
				while (q.HasMessage())
				{
					var time = DateTimeOffset.Now - q.LastOperationTime;
					if (q.MessageCount() < (q.Appender.BufferSize / 10) && time < q.WaitTimeout) continue;

					var tn = $"[{Thread.CurrentThread.ManagedThreadId}:{Thread.CurrentThread.Name}]";

					RemoveAndSendMessages(q, tn);
					i = 0;
				}
				Thread.Sleep(WaitMilliSeconds);
			}
			LogLog.Error(typeof(Sender), $"{Thread.CurrentThread.ManagedThreadId} - {Thread.CurrentThread.Name} End.");
		}

		private void SendAllDataToElasticSearch()
		{
			List<Task> threads = _queueManager
				.GetQueuesToHasMessage()
				.Select(q => Task.Run(() =>
				{
					var tn = $"[{Thread.CurrentThread.ManagedThreadId}:{Thread.CurrentThread.Name}]";
					var bs = q.Item1.Appender.BufferSize * 2;
					if (q.Item2.Count <= bs)
					{
						SendMessages(q.Item1, q.Item2, tn);
					}
					else
					{
						for (var i = 0; i < q.Item2.Count; i += bs)
						{
							SendMessages(q.Item1, q.Item2.Skip(i).Take(bs), tn);
						}
					}
				}))
				.ToList();

			if (threads.Count > 0)
				Task.WhenAll(threads).GetAwaiter().GetResult();
		}

		private void RemoveAndSendMessages(IQueueData queueData, string tn)
		{
			var messages = queueData.GetQueueItemDatas();
			if (!messages.Any()) return;

			SendMessages(queueData, messages, tn);
		}

		private void SendMessages(IQueueData queueData, IEnumerable<IQueueItemData> messages, string tn)
		{
			if (!messages.Any()) return;

			var stopwatch = Stopwatch.StartNew();

			using (MemoryStream ms = new MemoryStream())
			using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))
			{
				foreach (var item in messages)
				{
					if (queueData.Appender.ElasticVersion == 6)
					{
						sw.WriteLine($"{{\"index\":{{ \"_index\" : \"{item.IndiceName}\", \"_type\" : \"logEvent\", \"_id\":\"{item.Id}\"}}}}");
					}
					else if (queueData.Appender.ElasticVersion == 7)
					{
						sw.WriteLine($"{{\"index\":{{ \"_index\" : \"{item.IndiceName}\", \"_id\":\"{item.Id}\"}}}}");
					}
					sw.WriteLine(item.Message);
				}
				sw.Flush();

				Response result = SendMessagesToElasticSearch(queueData, ms);

				if (result != null && !result.HasErrors)
				{
					LogLog.Error(typeof(Sender), $"{tn} - {messages.Count()} message send. {stopwatch.Elapsed}");
				}
				else if (result == null)
				{
					foreach (var item in messages)
					{
						item.RetryCount++;
					}
					queueData.AddToQueue(messages);
				}
				else
				{
					var sb = new StringBuilder();
					var a = result.Items
						.Where(x => x.Index.Status != 201)
						.Select(x=>x.Index)
						.Join(messages, x=>x.Id, y=>y.Id.ToString(), (x,y)=>(x,y));
					foreach (var item in a)
					{
						item.y.RetryCount++;
						item.y.LastErrorMessage = $"{item.x.Error?.TypeName} - {item.x.Error?.Reason}";
						sb.AppendLine($"{item.x.Id}: [{item.x.Status}]{item.x.Error?.TypeName} - {item.x.Error?.Reason}");
					}
					queueData.AddToQueue(a.Select(x=>x.y));
					LogLog.Error(typeof(Sender), $"{tn} - Error on sending messages. {stopwatch.Elapsed} - Errors: {sb}");
				}
			}
		}

		private Response SendMessagesToElasticSearch(IQueueData queueData, MemoryStream ms)
		{
			var cli = GetOrCreateHttpClient();
			var connectionStringManager =
				_connectionStringManagers.GetOrAdd(queueData.Appender.ConnectionString, cs => new ConnectionStringManager(cs));

			var url = connectionStringManager.GetBulkApiUrl();
			Response result = null;
			try
			{
				result = PostMessages(ms, cli, url).GetAwaiter().GetResult();
			}
			catch (HttpRequestException exception)
			{
				LogLog.Error(typeof(Sender), "Error fired on ElasticSearch Communication.", exception);
				return null;
			}

			_httpClients.Enqueue(cli);
			return result;
		}

		private static readonly TimeSpan From10Seconds = TimeSpan.FromSeconds(10);

		private async Task<Response> PostMessages(MemoryStream ms, HttpClient cli, string url)
		{
			LogLog.Error(typeof(Sender), $"[{Thread.CurrentThread.ManagedThreadId}:{Thread.CurrentThread.Name}] - Message Posted to {url}");
			ms.Position = 0;
			var content = new StreamContent(ms);
			content.Headers.ContentType = _jsonMimeType;

			HttpResponseMessage post = null;

			try
			{
				post = await cli.PostAsync(url, content);
			}
			catch(TaskCanceledException tce)
			{
				LogLog.Error(typeof(Sender), "Timeout (TaskCanceledException) : " + tce.Message, tce);
				return null;
			}

			if (post.IsSuccessStatusCode)
			{
				try
				{
					// ok
					var json = await post.Content.ReadAsStringAsync();
					var a = MessagePackSerializer.ConvertFromJson(json);
					var b = MessagePackSerializer.Deserialize<Response>(a);

					return b;
				}
				catch (Exception ex)
				{
					LogLog.Error(typeof(Sender), "Deserialization Error : " + ex.Message, ex);
				}
			}
			else
			{
				// error
				LogLog.Error(typeof(Sender), $"HttpPost operation returns to error({post.StatusCode} - {url}) : {await post.Content.ReadAsStringAsync()}");
			}

			return null;
		}

		private HttpClient GetOrCreateHttpClient()
		{
			if (_httpClients.TryDequeue(out HttpClient cli))
			{
				return cli;
			}
			return new HttpClient()
			{
				Timeout = From10Seconds
			};

		}
	}
}