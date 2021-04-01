using log4net.Config;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace log4net.ElasticSearchCore.CoreConsoleTest
{
	internal class Program
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private static readonly Random _random = new Random();

		private static async Task Main(string[] args)
		{
			// Load configuration
			var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
			XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

			FirstDemo(args);

			//await SecondDemo();

			Console.WriteLine($"{DateTime.Now} - Hit enter");
			Console.ReadLine();
			Console.WriteLine($"{DateTime.Now} - End");
		}

		private static void FirstDemo(string[] args)
		{
			log.Info("Hello logging world!");

			log.Warn(new { Id = 1, Name = "Alper" });

			log.Error(new { Id = 1, Name = "Alper" }, new ArgumentNullException(nameof(args)));
		}

		private static async Task SecondDemo()
		{
			var sw = Stopwatch.StartNew();
			Console.WriteLine($"{DateTime.Now} - Started.");

			var lists = Enumerable.Range(0, 20).Select(x => Task.Run(() => AddLog(x, 500)));

			await Task.WhenAll(lists);

			Console.WriteLine(sw.Elapsed);
		}

		public static async Task AddLog(int id, int innerCount)
		{
			await Task.Delay(_random.Next(250, 1000));
			ILog log1 = LogManager.GetLogger("Test.Log_" + id);
			ILog log2 = LogManager.GetLogger("Test2.Log_" + id);

			for (int i = 0; i < innerCount; i++)
			{
				log1.Info($"Info Message - {id} - {i}");
				if (i % 2 == 0) log2.Info($"Info Message - {id} - {i}");
				//await Task.Delay(_random.Next(50, 501));
			}
		}
	}
}