using MessagePack;

namespace log4net.ElasticSearchCore.ElasticSearchResponse
{
	[MessagePackObject]
	public class ResponseItemIndexShards
	{
		[Key("total")]
		public int Total { get; set; }

		[Key("successful")]
		public int Successful { get; set; }

		[Key("failed")]
		public int Failed { get; set; }
	}
}