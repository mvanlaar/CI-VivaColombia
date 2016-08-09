using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CI_VivaColombia
{
    class Program
    {
        static void Main(string[] args)
        {                       
            string dataDir = AppDomain.CurrentDomain.BaseDirectory + "\\data";
            Directory.CreateDirectory(dataDir);
            string path = AppDomain.CurrentDomain.BaseDirectory + "data\\Luchthavens.html";
            Uri url = new Uri("https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios");
            const string ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            const string referer = "https://www.vivacolombia.co/";
            CultureInfo ci = new CultureInfo("es-ES");

            string TEMP_FromIATA = null;
            string TEMP_ToIATA = null;
            DateTime TEMP_ValidFrom = new DateTime();
            DateTime TEMP_ValidTo = new DateTime();
            int TEMP_Conversie = 0;
            Boolean TEMP_FlightMonday = false;
            Boolean TEMP_FlightTuesday = false;
            Boolean TEMP_FlightWednesday = false;
            Boolean TEMP_FlightThursday = false;
            Boolean TEMP_FlightFriday = false;
            Boolean TEMP_FlightSaterday = false;
            Boolean TEMP_FlightSunday = false;
            DateTime TEMP_DepartTime = new DateTime();
            DateTime TEMP_ArrivalTime = new DateTime();
            Boolean TEMP_FlightCodeShare = false;
            string TEMP_FlightNumber = null;
            string TEMP_Aircraftcode = null;
            TimeSpan TEMP_DurationTime = TimeSpan.MinValue;
            Boolean TEMP_FlightNextDayArrival = false;
            int TEMP_FlightNextDays = 0;
            List<CIFLight> CIFLights = new List<CIFLight> { };

            WebRequest.DefaultWebProxy = null;
            using (System.Net.WebClient wc = new WebClient())
            {
                wc.Headers.Add("user-agent", ua);
                wc.Headers.Add("Referer", referer);
                wc.Proxy = null;
                Console.WriteLine("Downloading Route list from VivaColombia Website...");
                wc.DownloadFile(url, path);
                Console.WriteLine("Download ready...");
            }
            StreamReader reader = new StreamReader(path);
            string html = reader.ReadToEnd();
            int start = html.IndexOf("airports: ") + 10;
            int end = html.IndexOf("internationalAirports:", start);
            string RouteJson = html.Substring(start, end - start);
            // Remove Newline
            RouteJson = RouteJson.TrimEnd(System.Environment.NewLine.ToCharArray());
            // Trim the string
            RouteJson = RouteJson.Trim();
            // Remove last , from the string
            RouteJson = RouteJson.Remove(RouteJson.Length - 1);
            // Parse the JSON reponse.
            dynamic dynJson = JsonConvert.DeserializeObject(RouteJson);
            foreach (var from in dynJson)
            {
                Console.WriteLine("Parsing flights from: {0} - {1}", from.Name, from.Code);               
                foreach (var to in from.Connections)
                {
                    Console.WriteLine("Getting flight: {0} - {1}", from.Name, to.Name);
                    var baseAddress = "https://www.vivacolombia.co/FlightSchedulePart/Search";
                    const string referersearch = "https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios";
                    var http = (HttpWebRequest)WebRequest.Create(new Uri(baseAddress));
                    http.Accept = "application/json";
                    http.ContentType = "application/json";
                    http.Method = "POST";
                    http.Referer = referersearch;
                    http.UserAgent = ua;
                    DateTime dt = DateTime.Now;
                    // Build Json Request.
                    var flight = new { from = from.Code.ToString(), to = to.Code.ToString(), outbound = dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), inbound = dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), isRoundTrip = false, fromName = from.Name.ToString(), toName = to.Name.ToString() };
                    string parsedContent = JsonConvert.SerializeObject(flight);
                    ASCIIEncoding encoding = new ASCIIEncoding();
                    Byte[] bytes = encoding.GetBytes(parsedContent);

                    Stream newStream = http.GetRequestStream();
                    newStream.Write(bytes, 0, bytes.Length);
                    newStream.Close();
                    var response = http.GetResponse();
                    var stream = response.GetResponseStream();
                    var sr = new StreamReader(stream);
                    var flightresponse = sr.ReadToEnd();
                    
                    // Parse the Response.
                    dynamic FlightResponseJson = JsonConvert.DeserializeObject(flightresponse);
                    string FlightWeek = FlightResponseJson.OutboundHeader;
                    // Cleaning TEMP variable
                    TEMP_FromIATA = from.Code.ToString();
                    TEMP_ToIATA = to.Code.ToString();
                    // Cleaning All but From and To 
                    TEMP_ValidFrom = new DateTime();
                    TEMP_ValidTo = new DateTime();
                    TEMP_Conversie = 0;
                    TEMP_FlightMonday = false;
                    TEMP_FlightTuesday = false;
                    TEMP_FlightWednesday = false;
                    TEMP_FlightThursday = false;
                    TEMP_FlightFriday = false;
                    TEMP_FlightSaterday = false;
                    TEMP_FlightSunday = false;
                    TEMP_DepartTime = new DateTime();
                    TEMP_ArrivalTime = new DateTime();
                    TEMP_FlightNumber = null;
                    TEMP_Aircraftcode = null;
                    TEMP_DurationTime = TimeSpan.MinValue;
                    TEMP_FlightCodeShare = false;
                    TEMP_FlightNextDayArrival = false;
                    TEMP_FlightNextDays = 0;

                    // Parsing To and From Date
                    Regex rgxdate2 = new Regex(@"(([0-9])|([0-2][0-9])|([3][0-1])) (ene|feb|mar|abr|may|jun|jul|ago|sep|oct|nov|dic) ([0-9]{4})");
                    MatchCollection matches = rgxdate2.Matches(FlightWeek);

                    string validfrom = matches[0].Value;
                    string validto = matches[1].Value;
                    
                    DateTime ValidFrom = DateTime.ParseExact(validfrom, "dd MMM yyyy", ci);
                    DateTime ValidTo = DateTime.ParseExact(validto, "dd MMM yyyy", ci);


                    foreach (var Schedules in FlightResponseJson.OutboundSchedeles)
                    {
                        TEMP_FlightNumber = Schedules.FlightNumber;
                        TEMP_DepartTime = Schedules.DepartureTime;
                        TEMP_ArrivalTime = Schedules.ArrivalTime;
                        if (Schedules.DepartDate == Schedules.ArrivalDate) { TEMP_FlightNextDayArrival = false; TEMP_FlightNextDays = 0; } else { TEMP_FlightNextDayArrival = true; TEMP_FlightNextDays = 1; }
                        // Query flight days

                        // Add Flight to CIFlights
                    }
                        Console.ReadLine();

                }                
                // Console.ReadLine();
            }
            //Console.ReadLine();

        }
    }

    public class CIFLight
    {
        // Auto-implemented properties. 
        public string FromIATA;
        public string ToIATA;
        public DateTime FromDate;
        public DateTime ToDate;
        public Boolean FlightMonday;
        public Boolean FlightTuesday;
        public Boolean FlightWednesday;
        public Boolean FlightThursday;
        public Boolean FlightFriday;
        public Boolean FlightSaterday;
        public Boolean FlightSunday;
        public DateTime DepartTime;
        public DateTime ArrivalTime;
        public String FlightNumber;
        public String FlightAirline;
        public String FlightOperator;
        public String FlightAircraft;
        public String FlightDuration;
        public Boolean FlightCodeShare;
        public Boolean FlightNextDayArrival;
        public int FlightNextDays;
    }
}
