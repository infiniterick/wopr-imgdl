using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MassTransit;
using ServiceStack.Redis;
using Wopr.Core;
using System;
using System.IO;
using System.Net.Http;

namespace Wopr.ImageDownloader{
      public class ContentReceivedConsumer : IConsumer<ContentReceived> {
        private static readonly HttpClient downloader = new HttpClient();

        //leave this static for security. containers can map in a path
        public const string ImageCachePath = "./imagecache"; 
        public int SizeLimit = Convert.ToInt32(10.0 * 1e+6);
        IRedisClientsManager redis;
    
        public ContentReceivedConsumer(IRedisClientsManager redis){
            this.redis = redis;
            
        }

        private bool ShouldHandle(string channelId){
            using(var client = redis.GetClient()){
                if(client.SetContainsItem(RedisPaths.ImageDLChannelWatch, channelId))
                    return true;
                else
                    return false;
            }    
        }

        public async Task Consume(ConsumeContext<ContentReceived> context){
            
            if(!ShouldHandle(context.Message.ChannelId))
                return;
            
            var channelKey = $"{RedisPaths.ModelChannel}:{context.Message.Channel.Id}";

            var url = string.Empty;
            //content is null on updates for some reason, ignore them to prevent nuking of original messages
            if(context.Message.Content != null){
                if(context.Message.AttatchmentUri != null){
                    url = context.Message.AttatchmentUri;
                } else{
                    var urlExtractor = new Regex(@"\b(?:https?://)?(?:(?i:[-a-z]+\.)+)[^\s,]+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    var firsturl = urlExtractor.Matches(context.Message.Content)
                        .Select(item => item.Value)
                        .FirstOrDefault();

                    if(!string.IsNullOrEmpty(firsturl)){
                        url = firsturl;
                    }
                }                     
            }

            await DownloadImage(context.Message.MessageId, url, skipExisting: true);
            await context.Publish(new ImageDownloaded(){
                Timestamp = DateTime.UtcNow,
                ChannelId = context.Message.ChannelId,
                MessageId = context.Message.MessageId
            });
        }

        private async Task DownloadImage(string messageId, string uri, bool skipExisting){
            
            Console.Write($"DownloadImage {messageId} ");
            var imagePath = Path.Combine(ImageCachePath, messageId);
            if(skipExisting && File.Exists(imagePath)){
                Console.WriteLine("EXISTS");
                return;
            }
                
            var request = new HttpRequestMessage(HttpMethod.Head, uri);
            var response = await downloader.SendAsync(request);

            if( response.IsSuccessStatusCode && 
                response.Content.Headers.ContentLength <= SizeLimit &&
                response.Content.Headers.ContentType.MediaType.ToUpper().Contains("IMAGE")){
                
                using(var responseStream = await downloader.GetStreamAsync(uri))
                using(var fileStream = File.Open(imagePath, FileMode.Create)){
                    responseStream.CopyTo(fileStream);
                }
                Console.WriteLine("OK");
            }
            else{
                Console.WriteLine("INVALID");
            }            
        }
    }
}