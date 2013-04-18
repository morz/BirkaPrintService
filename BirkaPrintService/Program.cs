using Bend.Util;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConsoleApplication1
{
    public class ServiceStarter
    {
        public ServiceStarter()
        {
            HttpServer httpServer = new MyHttpServer(80);
            Thread thread = new Thread(new ThreadStart(httpServer.listen));
            thread.Start();
        }
    }
}
