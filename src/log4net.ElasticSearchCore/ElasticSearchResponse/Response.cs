using MessagePack;

namespace log4net.ElasticSearchCore.ElasticSearchResponse
{
	[MessagePackObject]
	public class Response
	{
		[Key("took")]
		public int Took { get; set; }

		[Key("errors")]
		public bool HasErrors { get; set; }

		[Key("items")]
		public ResponseItem[] Items { get; set; }
	}
}