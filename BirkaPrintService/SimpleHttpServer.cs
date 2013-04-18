using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;

namespace Bend.Util {

    public class HttpProcessor: IDisposable {
        public TcpClient socket;        
        public HttpServer srv;

        private Stream inputStream;
        public StreamWriter outputStream;

        public String http_method;
        public String http_url;
        public String http_protocol_versionstring;
        public Hashtable httpHeaders = new Hashtable();


        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public HttpProcessor(TcpClient s, HttpServer srv) {
            this.socket = s;
            this.srv = srv;                   
        }
        

        private string streamReadLine(Stream inputStream) {
            int next_char;
            string data = "";
            while (true) {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }            
            return data;
        }
        public void process() {                        
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
            try {
                parseRequest();
                readHeaders();
                if (http_method.Equals("GET")) {
                    handleGETRequest();
                } else if (http_method.Equals("POST")) {
                    handlePOSTRequest();
                }
            } catch (Exception e) {
                Console.WriteLine("Exception: " + e.ToString());
                writeFailure();
            }
            outputStream.Flush();
            // bs.Flush(); // flush any remaining output
            inputStream = null; outputStream = null; // bs = null;            
            socket.Close();             
        }

        public void parseRequest() {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3) {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];

            Console.WriteLine("starting: " + request);
        }

        public void readHeaders() {
            Console.WriteLine("readHeaders()");
            String line;
            while ((line = streamReadLine(inputStream)) != null) {
                if (line.Equals("")) {
                    Console.WriteLine("got headers");
                    return;
                }
                
                int separator = line.IndexOf(':');
                if (separator == -1) {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' ')) {
                    pos++; // strip any spaces
                }
                    
                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}",name,value);
                httpHeaders[name] = value;
            }
        }

        public void handleGETRequest() {
            srv.handleGETRequest(this);
        }

        private const int BUF_SIZE = 4096;
        public void handlePOSTRequest() {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            int content_len = 0;
            MemoryStream ms = new MemoryStream();
            if (this.httpHeaders.ContainsKey("Content-Length")) {
                 content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                 if (content_len > MAX_POST_SIZE) {
                     throw new Exception(
                         String.Format("POST Content-Length({0}) too big for this simple server",
                           content_len));
                 }
                 byte[] buf = new byte[BUF_SIZE];              
                 int to_read = content_len;
                 while (to_read > 0) {  
                     Console.WriteLine("starting Read, to_read={0}",to_read);

                     int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                     Console.WriteLine("read finished, numread={0}", numread);
                     if (numread == 0) {
                         if (to_read == 0) {
                             break;
                         } else {
                             throw new Exception("client disconnected during post");
                         }
                     }
                     to_read -= numread;
                     ms.Write(buf, 0, numread);
                 }
                 ms.Seek(0, SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            srv.handlePOSTRequest(this, new StreamReader(ms, Encoding.Default));

        }

        public void writeSuccess(string content_type="text/html") {
            outputStream.WriteLine("HTTP/1.0 200 OK");            
            outputStream.WriteLine("Content-Type: " + content_type);
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }

        public void writeFailure() {
            outputStream.WriteLine("HTTP/1.0 404 File not found");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }

        public void Dispose()
        {
            outputStream.Dispose();
            outputStream = null;
            srv = null;
        }
    }

    public abstract class HttpServer {

        protected int port;
        TcpListener listener;
        bool is_active = true;
       
        public HttpServer(int port) {
            this.port = port;
        }

        public void listen() {
            listener = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
            listener.Start();
            while (is_active) {                
                TcpClient s = listener.AcceptTcpClient();
                HttpProcessor processor = new HttpProcessor(s, this);
                Thread thread = new Thread(new ThreadStart(processor.process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        public abstract void handleGETRequest(HttpProcessor p);
        public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);
    }

    public class MyHttpServer : HttpServer {
        public MyHttpServer(int port)
            : base(port) {
        }
        public override void handleGETRequest (HttpProcessor p)
		{

            Console.WriteLine("request: {0}", p.http_url);
            p.writeSuccess();
            if (p.http_url == "/")
            {
                p.outputStream.Write(@"<!DOCTYPE HTML>
<html lang=""ru-RU"">
<head>
    <meta charset=""UTF-8"">
    <title>Печать бирок</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no"" />
    <style type=""text/css"" media=""screen"">
        body{background: #97b6d6;}
        input{margin: 5px; height: 25px; font-size: 21px; line-height: 25px; width: 95%; border: 1px solid #000000;}
        input[type=submit]{width: 150px;height: 40px; border: 1px solid #000000;}
        label{border-bottom: 1px solid #000000;width: 150px;float: left;margin-right: 10px;line-height: 35px;}
        header{text-align: center;}
        form{display: block;}
        article{min-width:480px; display: block;margin: 0 auto; width: 40%;
                padding: 10px;
                   background: #FFF;
                    -moz-box-shadow: 0 0 3px 3px #b9d0e7;
                    -webkit-box-shadow: 0 0 3px 3px #b9d0e7;
                    box-shadow: 0 0 3px 3px #b9d0e7;
                    border-left:1px solid #97b6d6;
                    border-right:1px solid #97b6d6;
}
footer{text-align: center;}
    </style>
    <style type=""text/css"" media=""handheld,only screen and (max-device-width:480px)"">
        article{min-width:260px;width: 95%;margin: 0;}
        input{width: 95%;height: 35px; line-height: 35px;}
        label{width: 95%;}
        header{display: none;}
    </style>
<script type=""text/javascript"">
  function check(){
        if (document.getElementById('n1').value == undefined || document.getElementById('n1').value == """" ||document.getElementById('n2').value == undefined || document.getElementById('n2').value == """"|| document.getElementById('n3').value == undefined || document.getElementById('n3').value == """"||document.getElementById('n1').value == undefined || document.getElementById('n3').value == """" ||
document.getElementById('n4').value == undefined || document.getElementById('n4').value == """"|| document.getElementById('n5').value == undefined || document.getElementById('n5').value == """") { alert(""Не все данные введены."");return false;}else{
document.getElementById('a').submit();return true;}
    }
</script>
</head>
<body>
<header>
    <h2>Печать складских бирок</h2>
</header>
<article>
    <form action=""."" method=""post"" id=""a"">
        <label for=""n1"">Код оборудования</label>
        <input type=""text"" name=""n1"" id=n1>
        <br/>
        <label for=n2>Наименование узла</label>
        <input type=""text"" name=""n2"" id=n2>
<br/>
        <label for=n3>Код ВАЗа</label>
        <input type=""number"" name=""n3"" id=n3>
<br/>
        <label for=n4>Тип</label>
        <input type=""text"" name=""n4"" id=n4>
<br/>
        <label for=n5>Фамилия</label>
        <input type=""text"" name=""n5"" id=n5>
<br/>
        <label for=n6>Неисправность</label>
        <input type=""text"" name=""n6"" id=n6>
<br/>
        <label for=n7>№ заявки на ремонт</label>
        <input type=""number"" name=""n7"" id=n7>
<br/>
        <input type=""submit"" value=""Отправить"" onclick=""check();return false;"" id=n8>
<br/>
    </form>
    <section></section>
</article>
<footer>
    <section style=""font-size: 12px; color: #000000;"">Разработано бригадой №621 ОЭТС. т.№: 03776024</section>
</footer>
</body>
</html>");
            }
            else
            {
                p.outputStream.Write(@"<!DOCTYPE HTML>
<html lang=""ru-RU"">
<head>
    <meta charset=""UTF-8"">
    <title>Печать бирок</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no"" />
    <style type=""text/css"" media=""screen"">
        body{background: #97b6d6;}
        article{min-width:480px; display: block;margin: 0 auto; width: 40%;
                padding: 10px;
                   background: #FFF;
                    -moz-box-shadow: 0 0 3px 3px #b9d0e7;
                    -webkit-box-shadow: 0 0 3px 3px #b9d0e7;
                    box-shadow: 0 0 3px 3px #b9d0e7;
                    border-left:1px solid #97b6d6;
                    border-right:1px solid #97b6d6;
min-height: 350px;
text-align: center; vertical-align: middle;
}
a{position: relative; top: 150px; line-height: 35px; color: #000000; font-size: 20px; text-decoration: none; background: #ffffff; border: 1px solid #000000; padding: 15px;}
    </style>
    <style type=""text/css"" media=""handheld,only screen and (max-device-width:480px)"">
        article{min-width:260px;width: 95%;margin: 0;-moz-box-shadow: 0 0 3px 3px #b9d0e7;
                    -webkit-box-shadow: 0 0 3px 3px #b9d0e7;
                    box-shadow: 0 0 3px 3px #b9d0e7;
                    border-left:1px solid #97b6d6;
                    border-right:1px solid #97b6d6;
min-height: 350px;
text-decoration: center;}
        input{width: 95%;height: 35px; line-height: 35px;}
        label{width: 95%;}
        header{display: none;}
    </style>
</head>
<body>
    <article>
        <a href=""/stp""><span>По СТП</span></a>&nbsp;<a href=""/old""><span>Старая</span></a>
    </article>
</body>
</html>
");
            }
        }

        public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData) {
            Console.WriteLine("POST request: {0}", p.http_url);


            string data = inputData.ReadToEnd();

            p.writeSuccess();
            p.outputStream.WriteLine(@"<!DOCTYPE HTML>
<html lang=""ru-RU"">
<head>
    <meta charset=""UTF-8"">
    <title>Печать бирок</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no"" /></head><body>");
            p.outputStream.WriteLine("<a href=/>Вернуться</a><p>");
            var s = data.Split('&');
            p.outputStream.WriteLine("Код оборудования: {0} <br/>", g(s[0]));
            p.outputStream.WriteLine("Наименование узла: {0} <br/>", g(s[1]));
            p.outputStream.WriteLine("Код ВАЗа: {0} <br/>", g(s[2]));
            p.outputStream.WriteLine("Тип: {0} <br/>", g(s[3]));
            p.outputStream.WriteLine("Фамилия: {0} <br/>", g(s[4]));
            p.outputStream.WriteLine("Неисправность: {0} <br/>", g(s[5]));
            p.outputStream.WriteLine("№ заявки на ремонт: {0} <br/>", g(s[6]));
            p.outputStream.WriteLine("Дата: {0} <br/>", DateTime.Now.ToShortDateString());
            p.outputStream.WriteLine("</body></html>");
            
            Print(s);

        }

        public string g(string s) {
            return HttpUtility.UrlDecode(s.Split('=')[1]);
        }

        System.Drawing.Printing.PrintDocument printDocument1;
        string[] ss;
        public void Print(string[] s)
        {
            ss = s;
            if (printDocument1 == null)
            {
                printDocument1 = new System.Drawing.Printing.PrintDocument();
                printDocument1.DocumentName = "Бирка";
                printDocument1.EndPrint += new System.Drawing.Printing.PrintEventHandler(printDocument1_EndPrint);
                printDocument1.PrintPage += new System.Drawing.Printing.PrintPageEventHandler(printDocument1_PrintPage);
            }
            printDocument1.Print();
        }

        int leftm = 0;
        System.Drawing.Printing.PrintPageEventArgs evv;
        private void printDocument1_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs ev)
        {
            evv = ev;
            PrintStpText(ss);
            ev = evv;
            ev.HasMorePages = false;
        }

        public void PrintStpText(string[] s)
        {
            if (!String.IsNullOrEmpty(g(s[6])))
            {
                CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_Z.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_Z.Split('/')[1]), String.Format("З.№:{0}", g(s[6])));
            }
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_P.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_P.Split('/')[1]), "МСП к. 15/2");
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_CO.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_CO.Split('/')[1]), g(s[0]));
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_SU.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_SU.Split('/')[1]), g(s[1]));
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_CB.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_CB.Split('/')[1]), g(s[2]));
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_SN.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_SN.Split('/')[1]), g(s[3]));
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_D.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_D.Split('/')[1]), DateTime.Now.ToString("dd.M.yyyy"));
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_FIO.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_FIO.Split('/')[1]), g(s[4]));
            CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_T.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_T.Split('/')[1]), "12-32-77");
            if (g(s[5]).Length > 0)
                CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[1]), Slice(g(s[5]), 0, 24));
            if (g(s[5]).Length > 24)
                CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[1]) + 5, Slice(g(s[5]), 24, 48));
            if (g(s[5]).Length > 48)
                CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[1]) + 10, Slice(g(s[5]), 48, 72));
            if (g(s[5]).Length > 72)
                CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[1]) + 15, Slice(g(s[5]), 72, 96));
            if (g(s[5]).Length > 96)
                CreateText(Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[0]), Convert.ToDouble(ConsoleApplication1.Properties.Settings.Default.STP_N.Split('/')[1]) + 20, Slice(g(s[5]), 96, 120));
        }

        private void CreateText(double x, double y, string text, float size = 11f)
        {
            printDocument1.OriginAtMargins = true;
            if (leftm == 0)
                leftm = evv.MarginBounds.Left / 2;
            evv.PageSettings.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
            var font = new Font("Courier New", size);
            y = y - ConsoleApplication1.Properties.Settings.Default.Koeff_STP;
            evv.Graphics.DrawString(text, font, Brushes.Black, in_mm(leftm + x + 15), in_mm(y));
        }

        int in_mm(double value)
        {
            return Convert.ToInt32(value / 25.4 * 96);
        }

        public static string Slice(string source, int start, int end)
        {
            if (end < 0)
                end = source.Length + end;
            int len = end - start;
            if (source.Length - start < len)
                return source.Substring(start, source.Length - start);
            else
                return source.Substring(start, len);
        }
        private void printDocument1_EndPrint(object sender, System.Drawing.Printing.PrintEventArgs e)
        {
            evv = null;
            printDocument1.Dispose();
            printDocument1 = null;
        }

    }

}



