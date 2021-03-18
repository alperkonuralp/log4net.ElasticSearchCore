using System;
using System.Collections.Generic;
using System.Linq;

namespace log4net.ElasticSearchCore
{
	internal class ConnectionStringManager
	{
		private static readonly object _lockObject = new object();
		private readonly char[] _splitPartChar = new char[] { ';' };
		private readonly char[] _splitSegmentChar = new char[] { '=' };
		private string[] _hostList = Array.Empty<string>();
		private int _lastHostIndex = 0;
		private string _host = null;
		private int? _port = null;

		public ConnectionStringManager()
		{

		}

		public ConnectionStringManager(string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString)) return;

			var parts = connectionString.Split(_splitPartChar, StringSplitOptions.RemoveEmptyEntries);

			foreach (var part in parts.Where(x => x.Contains('=')).Select(x => x.Trim()))
			{
				var segments = part.Split(_splitSegmentChar, StringSplitOptions.RemoveEmptyEntries);
				if (segments.Length >= 2)
				{
					var key = segments[0].Trim();
					var value = segments[1].Trim();

					AddToProperties(key, value);
				}
			}
		}


		public string Host
		{
			get => _host;
			private set
			{
				_host = value;
				ReGenerateHostList();
			}
		}

		public int? Port
		{
			get => _port;
			private set
			{
				_port = value;
				ReGenerateHostList();
			}
		}


		public string[] Hosts { get; private set; } = Array.Empty<string>();

		public Dictionary<string, string> Others { get; set; }



		public string GetBulkApiUrl()
		{
			var hp = GetHostAndPortAndGoNext();
			if (string.IsNullOrWhiteSpace(hp)) return string.Empty;

			var u = new Uri($"http://{hp}/_bulk");

			return u.ToString();
		}


		private void ReGenerateHostList()
		{
			var l = new List<string>();
			if (!string.IsNullOrWhiteSpace(_host))
			{
				l.Add($"{_host}:{(_port.HasValue ? _port.ToString() : "9700")}");
			}
			if (Hosts.Length > 0)
			{
				l.AddRange(Hosts);
			}

			_hostList = l.ToArray();
		}

		private string GetHostAndPortAndGoNext()
		{
			if (_hostList.Length == 0) return string.Empty;
			lock (_lockObject)
			{
				var current = _hostList[_lastHostIndex];

				_lastHostIndex = (_lastHostIndex + 1) % _hostList.Length;

				return current;
			}
		}

		private void AddToProperties(string key, string value)
		{
			switch (key.ToLower())
			{
				case "host":
					_host = value;
					break;

				case "hosts":
					Hosts = value.Split(',');
					break;

				case "port":
					if (int.TryParse(value, out int p))
						_port = p;
					break;

				default:
					Others[key] = value;
					break;
			}

			ReGenerateHostList();
		}

	}
}