using log4net.Core;
using log4net.ElasticSearchCore.ElasticSearchResponse;
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
		public IErrorHandler ErrorHandler { get; set; }

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
			ErrorHandler.Error($"{Thread.CurrentThread.ManagedThreadId} - {Thread.CurrentThread.Name} Started.");

			for (var i = 0; i < 3; i++)
			{
				while (q.HasMessage())
				{
					var time = DateTimeOffset.Now - q.LastOperationTime;
					if (q.MessageCount() < (q.BufferSize / 10) && time < q.WaitTimeout) continue;

					var tn = $"[{Thread.CurrentThread.ManagedThreadId}:{Thread.CurrentThread.Name}]";

					RemoveAndSendMessages(q, tn);
					i = 0;
				}
				Thread.Sleep(WaitMilliSeconds);
			}
			ErrorHandler.Error($"{Thread.CurrentThread.ManagedThreadId} - {Thread.CurrentThread.Name} End.");
		}

		private void SendAllDataToElasticSearch()
		{
			List<Task> threads = _queueManager
				.GetQueuesToHasMessage()
				.Select(q => Task.Run(() =>
				{
					var bs = q.Item1.BufferSize * 2;
					if (q.Item2.Count <= bs)
					{
						SendMessages(q.Item1, q.Item2);
					}
					else
					{
						for (var i = 0; i < q.Item2.Count; i += bs)
						{
							SendMessages(q.Item1, q.Item2.Skip(i).Take(bs));
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

			var sw = Stopwatch.StartNew();
			if (!SendMessages(queueData, messages))
			{
				queueData.AddToQueue(messages);
			}
			else
				ErrorHandler.Error($"{tn} - {messages.Count} message send. {sw.Elapsed}");
		}

		private bool SendMessages(IQueueData queueData, IEnumerable<IQueueItemData> messages)
		{
			if (!messages.Any()) return true;

			using (MemoryStream ms = new MemoryStream())
			using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))
			{
				foreach (var item in messages)
				{
					sw.WriteLine($"{{\"index\":{{ \"_index\" : \"{item.IndiceName}\", \"_id\":\"{item.Id}\"}}}}");
					sw.WriteLine(item.Message);
				}
				sw.Flush();

				var cli = GetOrCreateHttpClient();
				var connectionStringManager =
					_connectionStringManagers.GetOrAdd(queueData.ConnectionString, cs => new ConnectionStringManager(cs));

				var url = connectionStringManager.GetBulkApiUrl();

				var result = PostMessages(ms, cli, url).GetAwaiter().GetResult();

				_httpClients.Enqueue(cli);

				return result != null && !result.HasErrors;
			}
		}

		private async Task<Response> PostMessages(MemoryStream ms, HttpClient cli, string url)
		{
			ErrorHandler.Error($"[{Thread.CurrentThread.ManagedThreadId}:{Thread.CurrentThread.Name}] - Message Posted to {url}");
			ms.Position = 0;
			var content = new StreamContent(ms);
			content.Headers.ContentType = _jsonMimeType;

			var post = await cli.PostAsync(url, content);

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
					ErrorHandler.Error("Deserialization Error : " + ex.Message, ex);
				}
			}
			else
			{
				// error
				ErrorHandler.Error($"HttpPost operation returns to error({post.StatusCode} - {url}) : {await post.Content.ReadAsStringAsync()}");
			}

			return null;
		}

		private HttpClient GetOrCreateHttpClient()
		{
			if (_httpClients.TryDequeue(out HttpClient cli))
			{
				return cli;
			}
			return new HttpClient();
		}
	}
}