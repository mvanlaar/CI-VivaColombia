using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CI_VivaColombia
{
    class Program
    {
        static void Main(string[] args)
        {
            // Source Page: https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios
            //Uri address = new Uri(@"https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios");
            //HttpWebRequest request = WebRequest.Create(address) as HttpWebRequest;
            //request.Method = "GET";
            //const string ua = "Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 46.0.2486.0 Safari / 537.36 Edge / 13.10586";
            //const string referer = "https://www.vivacolombia.co/";            
            string dataDir = AppDomain.CurrentDomain.BaseDirectory + "\\data";
            Directory.CreateDirectory(dataDir);
            string path = AppDomain.CurrentDomain.BaseDirectory + "data\\Luchthavens.html";
            Uri url = new Uri("https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios");
            const string ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            const string referer = "https://www.vivacolombia.co/"; 
            
            WebRequest.DefaultWebProxy = null;
            using (System.Net.WebClient wc = new WebClient())
            {
                wc.Headers.Add("user-agent", ua);
                wc.Headers.Add("Referer", referer);
                wc.Proxy = null;
                Console.WriteLine("Downloading latest skyteam timetable pdf file...");
                wc.DownloadFile(url, path);
                Console.WriteLine("Download ready...");
            }
            StreamReader reader = new StreamReader(path);
            string html = reader.ReadToEnd();
            int start = html.IndexOf("airports: ") + 10;
            int end = html.IndexOf("internationalAirports:", start);
            string RouteJson = html.Substring(start, end - start);
            RouteJson = RouteJson.Trim();
            // replace last , with nothing
            Console.WriteLine(RouteJson);
            Console.ReadLine();

        }
    }
}
