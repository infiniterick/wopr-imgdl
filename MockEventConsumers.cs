
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MassTransit;
using ServiceStack.Redis;
using Wopr.Core;

namespace Wopr.ImageDownloader{

    public class ContentReceivedConsumerMock : IConsumer<ContentReceived> {

        IRedisClientsManager redis;
    
        public ContentReceivedConsumerMock(IRedisClientsManager redis){
            this.redis = redis;
        }

        public Task Consume(ConsumeContext<ContentReceived> context){
            Console.WriteLine(JsonSerializer.Serialize(context.Message));
            return Task.CompletedTask;
        }
    }
    

  }