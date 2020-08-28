
using System.Threading.Tasks;
using GreenPipes;
using MassTransit;
using ServiceStack.Redis;
using Wopr.Core;

namespace Wopr.ImageDownloader{

    public class WoprMessageIdFilter<T> : IFilter<ConsumeContext<T>>
        where T : class, IMessageFilterable {
        
        IRedisClientsManager redis;
        
        public WoprMessageIdFilter(IRedisClientsManager redis){
            this.redis = redis;
        }

        public void Probe(ProbeContext context){
            context.CreateFilterScope("wopr-message");
        }

        public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next){
            using(var client = redis.GetClient()){

                //TODO: decide how to microcache filters from redis 
                if(client.SetContainsItem(RedisPaths.ImageDLMessageWatch, context.Message.MessageId))
                    return next.Send(context);
                else
                    return Task.CompletedTask;
            }    
        }
    }

    public class WoprChannelIdFilter<T> : IFilter<ConsumeContext<T>>
        where T : class, IChannelFilterable {
        
        IRedisClientsManager redis;
        
        public WoprChannelIdFilter(IRedisClientsManager redis){
            this.redis = redis;
        }

        public void Probe(ProbeContext context){
            context.CreateFilterScope("wopr-channel");
        }

        public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next){
            using(var client = redis.GetClient()){

                //TODO: decide how to microcache filters from redis 
                if(client.SetContainsItem(RedisPaths.ImageDLChannelWatch, context.Message.ChannelId))
                    return next.Send(context);
                else
                    return Task.CompletedTask;
            }    
        }
    }

    public class WoprAuthorIdFilter<T> : IFilter<ConsumeContext<T>>
        where T : class, IAuthorFilterable {
        
        IRedisClientsManager redis;
        
        public WoprAuthorIdFilter(IRedisClientsManager redis){
            this.redis = redis;
        }

        public void Probe(ProbeContext context){
            context.CreateFilterScope("wopr-channel");
        }

        public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next){
            using(var client = redis.GetClient()){

                //TODO: decide how to microcache filters from redis 
                if(client.SetContainsItem(RedisPaths.ImageDLAuthorWatch, context.Message.AuthorId))
                    return next.Send(context);
                else
                    return Task.CompletedTask;
            }    
        }
    }
}