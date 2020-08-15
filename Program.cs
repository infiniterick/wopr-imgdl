using System;
using System.Threading;
using System.Linq;
using Wopr.Core;

namespace Wopr.ImageDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            var secretsDir = args.Any() ? args[0] : ".";
            var secrets = Secrets.Load(secretsDir);

            if(!string.IsNullOrEmpty(secrets.StackToken)){
                Console.WriteLine("Running service stack in licensed mode");
                ServiceStack.Licensing.RegisterLicense(secrets.StackToken);
            }

            
            CancellationTokenSource cancel = new CancellationTokenSource();
            var habit = new ImageDownloadHabit(secrets, cancel.Token);

            Console.CancelKeyPress += (s, e) => {
                habit.Stop();
                cancel.Cancel();
            };
            habit.Start();

            cancel.Token.WaitHandle.WaitOne();
        }
    }
}
