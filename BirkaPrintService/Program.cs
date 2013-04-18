using Bend.Util;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConsoleApplication1
{
    public static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
    class Program
    {
        

        static public int Main(String[] args)
        {
            Console.Title = "Сервис печати бирок";
            IntPtr hWnd = NativeMethods.FindWindow(null, Console.Title);

            if (hWnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hWnd, 0); // 0 = SW_HIDE
            }

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
