using Bend.Util;
using System;
using System.Threading;

namespace ConsoleApplication1
{
    class Program
    {
        static public int Main(String[] args)
        {
            HttpServer httpServer;
            if (args.GetLength(0) > 0)
            {
                httpServer = new MyHttpServer(Convert.ToInt16(args[0]));
            }
            else
            {
                httpServer = new MyHttpServer(80);
            }
            Thread thread = new Thread(new ThreadStart(httpServer.listen));
            thread.Start();
            Console.WriteLine("Сервер запущен на всех IP на этом компьютере.");
            return 0;
        }

        

    }
}
