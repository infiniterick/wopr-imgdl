using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using ServiceStack.Redis;
using Wopr.Core;


namespace Wopr.ImageDownloader
{
    public class ImageDownloadHabit
    {
        RedisManagerPool redisPool;
        CancellationToken cancel;
        IBusControl bus;
        string rabbitToken;
        bool mock = false;
        public const string ImageCachePath = "./imagecache";

        public ImageDownloadHabit(Secrets secrets, CancellationToken cancel)
        {
            redisPool = new RedisManagerPool(secrets.RedisToken);
            this.cancel = cancel;
            this.rabbitToken = secrets.RabbitToken;
        }

        public void Start(){
            if(!Directory.Exists(ImageCachePath))
                Directory.CreateDirectory(ImageCachePath);

            StartMassTransit().Wait();
        }

        public void Stop(){
            
        }

         private Task StartMassTransit(){
            bus = Bus.Factory.CreateUsingRabbitMq(sbc => {
                var parts = rabbitToken.Split('@');
                sbc.Host(new Uri(parts[2]), cfg => {
                    cfg.Username(parts[0]);
                    cfg.Password(parts[1]);
                });
                rabbitToken = string.Empty;

                
                sbc.ReceiveEndpoint("wopr:discord:imgdl", ep => {
   
                    if(mock){
                        ep.Consumer<ContentReceivedConsumerMock>(()=>{return new ContentReceivedConsumerMock(redisPool);}, cc => cc.UseConcurrentMessageLimit(4));
                    }else{
                        ep.Consumer<ContentReceivedConsumer>(()=>{return new ContentReceivedConsumer(redisPool);}, cc => cc.UseConcurrentMessageLimit(4));
                    }
                    
                });
                
            });

            return bus.StartAsync(); // This is important!-
        }        
    }
}
