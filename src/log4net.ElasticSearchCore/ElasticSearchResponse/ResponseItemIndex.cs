using MessagePack;

namespace log4net.ElasticSearchCore.ElasticSearchResponse
{
	[MessagePackObject]
	public class ResponseItemIndex
	{
		[Key("_index")]
		public string IndexName { get; set; }

		[Key("_type")]
		public string TypeName { get; set; }

		[Key("_id")]
		public string Id { get; set; }

		[Key("_version")]
		public int? Version { get; set; }

		[Key("result")]
		public string Result { get; set; }

		[Key("_shards")]
		public ResponseItemIndexShards Shards { get; set; }

		[Key("_seq_no")]
		public int? SeqNo { get; set; }

		[Key("_primary_term")]
		public int? PrimaryTerm { get; set; }

		[Key("status")]
		public int Status { get; set; }

		[Key("error")]
		public ResponseItemIndexError Error { get; set; }
	}
}