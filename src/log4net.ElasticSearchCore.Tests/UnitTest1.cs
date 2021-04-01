using log4net.ElasticSearchCore.Data;
using System;
using System.Collections.Generic;
using Xunit;

namespace log4net.ElasticSearchCore.Tests
{
	public class UnitTest1
	{
		[Fact]
		public void Test1()
		{
			//var undeliverableItems = new List<IQueueItemData>()
			var undeliverableItems = new IQueueItemData[]
			{
				new QueueItemData(){ Id = Guid.NewGuid(), IndiceName = "abc", Message = "{\"demo\":123}", RetryCount = 5 }
			};

			var options = MessagePack.MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

			//var options = MessagePack.Resolvers.TypelessContractlessStandardResolver.Options;
			//var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
			//var options = MessagePack.Resolvers.

			var bson = MessagePack.MessagePackSerializer.Serialize(undeliverableItems, options);
			//var bson = MessagePack.MessagePackSerializer.Serialize(undeliverableItems);
			var str = MessagePack.MessagePackSerializer.ConvertToJson(bson);


		}
	}
}
