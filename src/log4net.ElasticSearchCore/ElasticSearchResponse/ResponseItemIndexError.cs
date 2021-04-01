using MessagePack;

namespace log4net.ElasticSearchCore.ElasticSearchResponse
{
	[MessagePackObject]
	public class ResponseItemIndexError
	{
		[Key("type")]
		public string TypeName { get; set; }

		[Key("reason")]
		public string Reason { get; set; }


		[Key("index_uuid")]
		public string IndexUuid { get; set; }

		[Key("shard")]
		public string Shard { get; set; }

		[Key("index")]
		public string IndexName { get; set; }

	}
}