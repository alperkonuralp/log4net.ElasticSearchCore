using MessagePack;

namespace log4net.ElasticSearchCore.ElasticSearchResponse
{
	[MessagePackObject]
	public class ResponseItem
	{
		[Key("index")]
		public ResponseItemIndex Index { get; set; }
	}
}