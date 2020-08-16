using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Redis;
using Wopr.Core;

namespace Wopr.ImageDownloader
{
    public class ImageDownloadHabit
    {
        RedisManagerPool redisPool;
        RedisPubSubServer redisPubSub;
        CancellationToken cancel;
        
        //leave this static for security. if consumers don't like the location, they can map a docker path to it
        public const string ImageCachePath = "./imagecache"; 
        public int SizeLimit = Convert.ToInt32(10.0 * 1e+6);

        private static readonly HttpClient downloader = new HttpClient();

        public ImageDownloadHabit(Secrets secrets, CancellationToken cancel)
        {
            redisPool = new RedisManagerPool(secrets.RedisToken);
            redisPubSub = new RedisPubSubServer(redisPool); 
            redisPubSub.OnMessage += NewDataReady;
            redisPubSub.ChannelsMatching = new string[]{
                $"{RedisPaths.ModelReaction}:*", 
                $"{RedisPaths.ModelChannel}:*"
            };
            this.cancel = cancel;
        }

        public void Start(){
            if(!Directory.Exists(ImageCachePath))
                Directory.CreateDirectory(ImageCachePath);

            WatchForImages();
            redisPubSub.Start();
        }

        public void Stop(){
            redisPubSub.Stop();
        }

     
        private List<string> GetWatchedChannels(){
            using(var client = redisPool.GetClient()){
                return client.GetAllItemsFromList(RedisPaths.WatchedContent);
            }
        }

        public void WatchForImages(){
            
            var watchlist = GetWatchedChannels();
            var urlmap = new Dictionary<string, string>();
            foreach(var watch in watchlist){
                foreach(var url in ScanChannelForLinks(watch))
                    urlmap[url.Key] = url.Value;
            }

            using(var client = redisPool.GetClient())
            using(var pipeline = client.CreatePipeline()){
                
                foreach(var url in urlmap){

                    //fixup urls that dont start with http...
                    //if(!url.Value.StartsWith("HTTP"))
                    DownloadImage(url.Key, url.Value, skipExisting: true).Wait();
                    var watchkey = $"{RedisPaths.WatchedContent}:{url.Key}";
                    pipeline.QueueCommand(c => c.SetEntryInHash(watchkey, "url", url.Value));
                    pipeline.QueueCommand(c => c.SetEntryInHashIfNotExists(watchkey, "status", "new"));
                }
                pipeline.Flush();
            }
        }

        private Dictionary<string, string> ScanChannelForLinks(string channel){
            var urlExtractor = new Regex(@"\b(?:https?://)?(?:(?i:[-a-z]+\.)+)[^\s,]+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            
            var urlmap = new Dictionary<string,string>();
            using(var outerClient = redisPool.GetClient()){
                foreach(var entry in outerClient.ScanAllHashEntries(channel)){
                    if(entry.Key != "name"){
                        try{
                            using(var client = redisPool.GetClient()){
                                var raw = client.GetValueFromHash(RedisPaths.ModelContent, entry.Key);
                                var msg = JsonSerializer.Deserialize<ContentReceived>(raw);

                                
                                if(msg.Content != null){
                                    var firsturl = urlExtractor.Matches(msg.Content)
                                        .Select(item => item.Value)
                                        .FirstOrDefault();
                                    if(!string.IsNullOrEmpty(firsturl))
                                        urlmap[msg.MessageId] = firsturl;
                                }
                                if(msg.AttatchmentUri != null){
                                    urlmap[msg.MessageId] = msg.AttatchmentUri;
                                }                            
                            }
                        }
                        catch(Exception ex){
                            Console.WriteLine("Exception scanning for urls: " + ex.Message);;
                        }
                    }
                }
            }

            return urlmap;
        }

        private async Task DownloadImage(string messageId, string uri, bool skipExisting){
            
            Console.Write($"DownloadImage {messageId} ");
            var imagePath = Path.Combine(ImageCachePath, messageId);
            if(skipExisting && File.Exists(imagePath)){
                Console.WriteLine("EXISTS");
                return;
            }
                
            var request = new HttpRequestMessage(HttpMethod.Head, uri);
            var response = await downloader.SendAsync(request, this.cancel);

            if( response.IsSuccessStatusCode && 
                response.Content.Headers.ContentLength <= SizeLimit &&
                response.Content.Headers.ContentType.MediaType.ToUpper().Contains("IMAGE")){
                
                using(var responseStream = await downloader.GetStreamAsync(uri))
                using(var fileStream = File.Open(imagePath, FileMode.Create)){
                    responseStream.CopyTo(fileStream);
                }

                using(var client = redisPool.GetClient()){
                    client.PublishMessage(RedisPaths.WatchedContent, messageId);
                }
                Console.WriteLine("OK");
            }
            else{
                Console.WriteLine("INVALID");
            }
            
        }

        ///Redis pubsub will call this when new control messages are available in the fresh list
        private void NewDataReady(string channel, string message){
            Console.WriteLine($"Event on {channel} - {message}");
            WatchForImages();
        }

        
    }
}
