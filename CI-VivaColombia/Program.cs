using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CI_VivaColombia
{
    class Program
    {
        public static readonly List<int> _AddDays = new List<int>() { 0, 7, 14, 21, 28, 35, 42, 49, 56, 63, 70, 77, 84 };

        static void Main(string[] args)
        {
            string dataDir = AppDomain.CurrentDomain.BaseDirectory + "\\data";
            Directory.CreateDirectory(dataDir);
            string path = AppDomain.CurrentDomain.BaseDirectory + "data\\Luchthavens.html";
            Uri url = new Uri("https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios");
            const string ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
            const string referer = "https://www.vivacolombia.co/";
            CultureInfo ci = new CultureInfo("es-CO");
            DateTimeFormatInfo dtfi = ci.DateTimeFormat;

            string APIPathAirport = "airport/iata/";
            string APIPathAirline = "airline/iata/";


            //custom month names ene|feb|mar|abr|may|jun|jul|ago|sep|oct|nov|dic
            dtfi.AbbreviatedMonthNames = new string[] { "ene", "feb", "mar",
                                                  "abr", "may", "jun",
                                                  "jul", "ago", "sep",
                                                  "oct", "nov", "dic", "" };
            dtfi.AbbreviatedMonthGenitiveNames = dtfi.AbbreviatedMonthNames;




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
            string TEMP_FlightNumber = String.Empty;
            string TEMP_Aircraftcode = String.Empty;
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
            string html = string.Empty;
            using (StreamReader reader = new StreamReader(path))
            {
                html = reader.ReadToEnd();
            }

            //StreamReader reader = new StreamReader(path);
            //string html = reader.ReadToEnd();
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

                    string fromiata = from.Code.ToString();
                    string toiata = to.Code.ToString();

                    Parallel.ForEach(_AddDays, new ParallelOptions { MaxDegreeOfParallelism = 13 }, (Day) =>
                    {
                        DateTime dt = DateTime.Now;
                        dt = dt.AddDays(Day);
                        Console.WriteLine("Getting flight: {0} - {1} - {2}", from.Name, to.Name, dt.ToString());
                        // Clean TEMP Variables
                        TEMP_ValidFrom = new DateTime();
                        TEMP_ValidTo = new DateTime();
                        var baseAddress = "https://www.vivacolombia.co/FlightSchedulePart/Search";
                        const string referersearch = "https://www.vivacolombia.co/co/viaja-con-vivacolombia/informacion/itinerarios";
                        //var request = (HttpWebRequest)WebRequest.Create("example.com");

                        var http = (HttpWebRequest)WebRequest.Create(new Uri(baseAddress));
                        http.Accept = "application/json";
                        http.ContentType = "application/json";
                        http.Method = "POST";
                        http.Referer = referersearch;
                        http.UserAgent = ua;
                        // Build Json Request.
                        var flight = new { from = fromiata, to = toiata, outbound = dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), inbound = dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), isRoundTrip = false, fromName = from.Name.ToString(), toName = to.Name.ToString() };
                        string parsedContent = JsonConvert.SerializeObject(flight);
                        ASCIIEncoding encoding = new ASCIIEncoding();
                        Byte[] bytes = encoding.GetBytes(parsedContent);
                        Stream newStream = http.GetRequestStream();
                        newStream.Write(bytes, 0, bytes.Length);
                        newStream.Close();
                        string filePath = dataDir + "\\" + fromiata + toiata + dt.ToString("yyyyMMdd") + ".json";
                        //var response = http.GetResponse();
                        using (var response = http.GetResponse())
                        {
                            var stream = response.GetResponseStream();
                            using (StreamReader sr = new StreamReader(stream))                                                       
                            using (JsonReader flightresponse = new JsonTextReader(sr))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                // Parse the Response.
                                dynamic FlightResponseJson = serializer.Deserialize(flightresponse);
                                File.AppendAllText(filePath, FlightResponseJson.ToString());
                                string FlightWeek = FlightResponseJson.OutboundHeader;
                                // Cleaning TEMP variable
                                TEMP_FromIATA = fromiata;
                                TEMP_ToIATA = toiata;

                                // Parsing To and From Date
                                Regex rgxdate2 = new Regex(@"(([0-9])|([0-2][0-9])|([3][0-1])) (ene|feb|mar|abr|may|jun|jul|ago|sep|oct|nov|dic) ([0-9]{4})");
                                MatchCollection matches = rgxdate2.Matches(FlightWeek);

                                string validfrom = matches[0].Value;
                                string validto = matches[1].Value;

                                DateTime ValidFrom = DateTime.ParseExact(validfrom, "d MMM yyyy", dtfi);
                                DateTime ValidTo = DateTime.ParseExact(validto, "d MMM yyyy", dtfi);

                                foreach (var Schedules in FlightResponseJson.OutboundSchedules)
                                {
                                    TEMP_FlightNumber = Schedules.FlightNumber;
                                    TEMP_DepartTime = Schedules.DepartureTime;
                                    TEMP_ArrivalTime = Schedules.ArrivalTime;
                                    // No empty flightnumber flights.
                                    if (TEMP_FlightNumber.Length != 0)
                                    {

                                        TEMP_ValidFrom = ValidFrom;
                                        TEMP_ValidTo = ValidTo;
                                        //TimeSpan.Parse(Schedules.DepartureTime); // 10 PM
                                        //TimeSpan.Parse(Schedules.ArrivalTime);   // 2 AM

                                        if (TimeSpan.Parse(Convert.ToString(Schedules.DepartureTime)) <= TimeSpan.Parse(Convert.ToString(Schedules.ArrivalTime)))
                                        {
                                            TEMP_FlightNextDayArrival = false;
                                            TEMP_FlightNextDays = 0;
                                        }
                                        else
                                        {
                                            TEMP_FlightNextDayArrival = true;
                                            TEMP_FlightNextDays = 1;
                                        }
                                        // Query flight days
                                        foreach (var Days in Schedules.AvailableDays)
                                        {
                                            int.TryParse(Days.ToString(), out TEMP_Conversie);
                                            if (TEMP_Conversie == 1) { TEMP_FlightMonday = true; }
                                            if (TEMP_Conversie == 2) { TEMP_FlightTuesday = true; }
                                            if (TEMP_Conversie == 3) { TEMP_FlightWednesday = true; }
                                            if (TEMP_Conversie == 4) { TEMP_FlightThursday = true; }
                                            if (TEMP_Conversie == 5) { TEMP_FlightFriday = true; }
                                            if (TEMP_Conversie == 6) { TEMP_FlightSaterday = true; }
                                            if (TEMP_Conversie == 7) { TEMP_FlightSunday = true; }
                                        }

                                        // check if flight fly
                                        if (TEMP_FlightMonday | TEMP_FlightTuesday | TEMP_FlightWednesday | TEMP_FlightThursday | TEMP_FlightFriday | TEMP_FlightSaterday | TEMP_FlightSunday)
                                        {
                                            // Add Flight to CIFlights
                                            bool alreadyExists = CIFLights.Exists(x => x.FromIATA == TEMP_FromIATA
                                                && x.ToIATA == TEMP_ToIATA
                                                && x.FromDate == TEMP_ValidFrom
                                                && x.ToDate == TEMP_ValidTo
                                                && x.FlightNumber == TEMP_FlightNumber
                                                && x.FlightAirline == "FC"
                                                && x.FlightMonday == TEMP_FlightMonday
                                                && x.FlightTuesday == TEMP_FlightTuesday
                                                && x.FlightWednesday == TEMP_FlightWednesday
                                                && x.FlightThursday == TEMP_FlightThursday
                                                && x.FlightFriday == TEMP_FlightFriday
                                                && x.FlightSaterday == TEMP_FlightSaterday
                                                && x.FlightSunday == TEMP_FlightSunday);


                                            if (!alreadyExists)
                                            {
                                                // don't add flights that already exists
                                                CIFLights.Add(new CIFLight
                                                {
                                                    FromIATA = TEMP_FromIATA,
                                                    ToIATA = TEMP_ToIATA,
                                                    FromDate = TEMP_ValidFrom,
                                                    ToDate = TEMP_ValidTo,
                                                    ArrivalTime = TEMP_ArrivalTime,
                                                    DepartTime = TEMP_DepartTime,
                                                    FlightAircraft = "A320",
                                                    FlightAirline = "FC",
                                                    FlightMonday = TEMP_FlightMonday,
                                                    FlightTuesday = TEMP_FlightTuesday,
                                                    FlightWednesday = TEMP_FlightWednesday,
                                                    FlightThursday = TEMP_FlightThursday,
                                                    FlightFriday = TEMP_FlightFriday,
                                                    FlightSaterday = TEMP_FlightSaterday,
                                                    FlightSunday = TEMP_FlightSunday,
                                                    FlightNumber = TEMP_FlightNumber,
                                                    FlightOperator = null,
                                                    FlightCodeShare = TEMP_FlightCodeShare,
                                                    FlightNextDayArrival = TEMP_FlightNextDayArrival,
                                                    FlightNextDays = TEMP_FlightNextDays
                                                });
                                            }
                                        }
                                    }
                                    // Cleaning All but From and To and Valid from and Valid To                        
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
                                    TEMP_FlightNumber = String.Empty;
                                    TEMP_Aircraftcode = String.Empty;
                                    TEMP_FlightCodeShare = false;
                                    TEMP_FlightNextDayArrival = false;
                                    TEMP_FlightNextDays = 0;
                                }
                                // End Week parsing.
                            }
                        }





                    });
                    //End Parsing Flights
                }
                // End Parsing Routes
            }
            // Export XML
            // Write the list of objects to a file.
            System.Xml.Serialization.XmlSerializer writer =
            new System.Xml.Serialization.XmlSerializer(CIFLights.GetType());
            string myDir = AppDomain.CurrentDomain.BaseDirectory + "\\output";
            Directory.CreateDirectory(myDir);
            StreamWriter file =
               new System.IO.StreamWriter("output\\output.xml");

            writer.Serialize(file, CIFLights);
            file.Close();

            // GTFS Support

            string gtfsDir = AppDomain.CurrentDomain.BaseDirectory + "\\gtfs";
            System.IO.Directory.CreateDirectory(gtfsDir);

            Console.WriteLine("Creating GTFS Files...");

            Console.WriteLine("Creating GTFS File agency.txt...");
            using (var gtfsagency = new StreamWriter(@"gtfs\\agency.txt"))
            {
                var csv = new CsvWriter(gtfsagency);
                csv.Configuration.Delimiter = ",";
                csv.Configuration.Encoding = Encoding.UTF8;
                csv.Configuration.TrimFields = true;
                // header 
                csv.WriteField("agency_id");
                csv.WriteField("agency_name");
                csv.WriteField("agency_url");
                csv.WriteField("agency_timezone");
                csv.WriteField("agency_lang");
                csv.WriteField("agency_phone");
                csv.WriteField("agency_fare_url");
                csv.WriteField("agency_email");
                csv.NextRecord();
                csv.WriteField("FC");
                csv.WriteField("VivaColombia");
                csv.WriteField("https://www.vivacolombia.co/");
                csv.WriteField("America/Bogota");
                csv.WriteField("ES");
                csv.WriteField("");
                csv.WriteField("");
                csv.WriteField("");
                csv.NextRecord();
            }

            Console.WriteLine("Creating GTFS File routes.txt ...");

            using (var gtfsroutes = new StreamWriter(@"gtfs\\routes.txt"))
            {
                // Route record


                var csvroutes = new CsvWriter(gtfsroutes);
                csvroutes.Configuration.Delimiter = ",";
                csvroutes.Configuration.Encoding = Encoding.UTF8;
                csvroutes.Configuration.TrimFields = true;
                // header 
                csvroutes.WriteField("route_id");
                csvroutes.WriteField("agency_id");
                csvroutes.WriteField("route_short_name");
                csvroutes.WriteField("route_long_name");
                csvroutes.WriteField("route_desc");
                csvroutes.WriteField("route_type");
                csvroutes.WriteField("route_url");
                csvroutes.WriteField("route_color");
                csvroutes.WriteField("route_text_color");
                csvroutes.NextRecord();

                var routes = CIFLights.Select(m => new { m.FromIATA, m.ToIATA, m.FlightAirline }).Distinct().ToList();

                for (int i = 0; i < routes.Count; i++) // Loop through List with for)
                {
                    string FromAirportName = null;
                    string ToAirportName = null;
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + routes[i].FromIATA;
                        var jsonapi = client.DownloadString(urlapi);
                        dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                        FromAirportName = Convert.ToString(AirportResponseJson[0].name);
                    }
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + routes[i].ToIATA;
                        var jsonapi = client.DownloadString(urlapi);
                        dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                        ToAirportName = Convert.ToString(AirportResponseJson[0].name);
                    }

                    csvroutes.WriteField(routes[i].FromIATA + routes[i].ToIATA + routes[i].FlightAirline);
                    csvroutes.WriteField(routes[i].FlightAirline);
                    csvroutes.WriteField(routes[i].FromIATA + routes[i].ToIATA);
                    csvroutes.WriteField(FromAirportName + " - " + ToAirportName);
                    csvroutes.WriteField(""); // routes[i].FlightAircraft + ";" + CIFLights[i].FlightAirline + ";" + CIFLights[i].FlightOperator + ";" + CIFLights[i].FlightCodeShare
                    csvroutes.WriteField(1102);
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.WriteField("");
                    csvroutes.NextRecord();
                }
            }

            // stops.txt

            List<string> agencyairportsiata =
             CIFLights.SelectMany(m => new string[] { m.FromIATA, m.ToIATA })
                     .Distinct()
                     .ToList();

            using (var gtfsstops = new StreamWriter(@"gtfs\\stops.txt"))
            {
                // Route record
                var csvstops = new CsvWriter(gtfsstops);
                csvstops.Configuration.Delimiter = ",";
                csvstops.Configuration.Encoding = Encoding.UTF8;
                csvstops.Configuration.TrimFields = true;
                // header                                 
                csvstops.WriteField("stop_id");
                csvstops.WriteField("stop_name");
                csvstops.WriteField("stop_desc");
                csvstops.WriteField("stop_lat");
                csvstops.WriteField("stop_lon");
                csvstops.WriteField("zone_id");
                csvstops.WriteField("stop_url");
                csvstops.WriteField("stop_timezone");
                csvstops.NextRecord();

                for (int i = 0; i < agencyairportsiata.Count; i++) // Loop through List with for)
                {
                    // Using API for airport Data.
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + agencyairportsiata[i];
                        var jsonapi = client.DownloadString(urlapi);
                        dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);

                        csvstops.WriteField(Convert.ToString(AirportResponseJson[0].code));
                        csvstops.WriteField(Convert.ToString(AirportResponseJson[0].name));
                        csvstops.WriteField("");
                        csvstops.WriteField(Convert.ToString(AirportResponseJson[0].lat));
                        csvstops.WriteField(Convert.ToString(AirportResponseJson[0].lng));
                        csvstops.WriteField("");
                        csvstops.WriteField(Convert.ToString(AirportResponseJson[0].website));
                        csvstops.WriteField(Convert.ToString(AirportResponseJson[0].timezone));
                        csvstops.NextRecord();
                    }
                }
            }


            Console.WriteLine("Creating GTFS File trips.txt, stop_times.txt, calendar.txt ...");

            using (var gtfscalendar = new StreamWriter(@"gtfs\\calendar.txt"))
            {
                using (var gtfstrips = new StreamWriter(@"gtfs\\trips.txt"))
                {
                    using (var gtfsstoptimes = new StreamWriter(@"gtfs\\stop_times.txt"))
                    {
                        // Headers 
                        var csvstoptimes = new CsvWriter(gtfsstoptimes);
                        csvstoptimes.Configuration.Delimiter = ",";
                        csvstoptimes.Configuration.Encoding = Encoding.UTF8;
                        csvstoptimes.Configuration.TrimFields = true;
                        // header 
                        csvstoptimes.WriteField("trip_id");
                        csvstoptimes.WriteField("arrival_time");
                        csvstoptimes.WriteField("departure_time");
                        csvstoptimes.WriteField("stop_id");
                        csvstoptimes.WriteField("stop_sequence");
                        csvstoptimes.WriteField("stop_headsign");
                        csvstoptimes.WriteField("pickup_type");
                        csvstoptimes.WriteField("drop_off_type");
                        csvstoptimes.WriteField("shape_dist_traveled");
                        csvstoptimes.WriteField("timepoint");
                        csvstoptimes.NextRecord();

                        var csvtrips = new CsvWriter(gtfstrips);
                        csvtrips.Configuration.Delimiter = ",";
                        csvtrips.Configuration.Encoding = Encoding.UTF8;
                        csvtrips.Configuration.TrimFields = true;
                        // header 
                        csvtrips.WriteField("route_id");
                        csvtrips.WriteField("service_id");
                        csvtrips.WriteField("trip_id");
                        csvtrips.WriteField("trip_headsign");
                        csvtrips.WriteField("trip_short_name");
                        csvtrips.WriteField("direction_id");
                        csvtrips.WriteField("block_id");
                        csvtrips.WriteField("shape_id");
                        csvtrips.WriteField("wheelchair_accessible");
                        csvtrips.WriteField("bikes_allowed ");
                        csvtrips.NextRecord();

                        var csvcalendar = new CsvWriter(gtfscalendar);
                        csvcalendar.Configuration.Delimiter = ",";
                        csvcalendar.Configuration.Encoding = Encoding.UTF8;
                        csvcalendar.Configuration.TrimFields = true;
                        // header 
                        csvcalendar.WriteField("service_id");
                        csvcalendar.WriteField("monday");
                        csvcalendar.WriteField("tuesday");
                        csvcalendar.WriteField("wednesday");
                        csvcalendar.WriteField("thursday");
                        csvcalendar.WriteField("friday");
                        csvcalendar.WriteField("saturday");
                        csvcalendar.WriteField("sunday");
                        csvcalendar.WriteField("start_date");
                        csvcalendar.WriteField("end_date");
                        csvcalendar.NextRecord();

                        //1101 International Air Service
                        //1102 Domestic Air Service
                        //1103 Intercontinental Air Service
                        //1104 Domestic Scheduled Air Service


                        for (int i = 0; i < CIFLights.Count; i++) // Loop through List with for)
                        {

                            // Calender

                            csvcalendar.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate) + Convert.ToInt32(CIFLights[i].FlightMonday) + Convert.ToInt32(CIFLights[i].FlightTuesday) + Convert.ToInt32(CIFLights[i].FlightWednesday) + Convert.ToInt32(CIFLights[i].FlightThursday) + Convert.ToInt32(CIFLights[i].FlightFriday) + Convert.ToInt32(CIFLights[i].FlightSaterday) + Convert.ToInt32(CIFLights[i].FlightSunday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightMonday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightTuesday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightWednesday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightThursday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightFriday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightSaterday));
                            csvcalendar.WriteField(Convert.ToInt32(CIFLights[i].FlightSunday));
                            csvcalendar.WriteField(String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate));
                            csvcalendar.WriteField(String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate));
                            csvcalendar.NextRecord();

                            // Trips
                            string FromAirportName = null;
                            string ToAirportName = null;
                            using (var client = new WebClient())
                            {
                                client.Encoding = Encoding.UTF8;
                                string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + CIFLights[i].FromIATA;
                                var jsonapi = client.DownloadString(urlapi);
                                dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                                FromAirportName = Convert.ToString(AirportResponseJson[0].name);
                            }
                            using (var client = new WebClient())
                            {
                                client.Encoding = Encoding.UTF8;
                                string urlapi = ConfigurationManager.AppSettings.Get("APIUrl") + APIPathAirport + CIFLights[i].ToIATA;
                                var jsonapi = client.DownloadString(urlapi);
                                dynamic AirportResponseJson = JsonConvert.DeserializeObject(jsonapi);
                                ToAirportName = Convert.ToString(AirportResponseJson[0].name);
                            }


                            csvtrips.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline);
                            csvtrips.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate) + Convert.ToInt32(CIFLights[i].FlightMonday) + Convert.ToInt32(CIFLights[i].FlightTuesday) + Convert.ToInt32(CIFLights[i].FlightWednesday) + Convert.ToInt32(CIFLights[i].FlightThursday) + Convert.ToInt32(CIFLights[i].FlightFriday) + Convert.ToInt32(CIFLights[i].FlightSaterday) + Convert.ToInt32(CIFLights[i].FlightSunday));
                            csvtrips.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate) + Convert.ToInt32(CIFLights[i].FlightMonday) + Convert.ToInt32(CIFLights[i].FlightTuesday) + Convert.ToInt32(CIFLights[i].FlightWednesday) + Convert.ToInt32(CIFLights[i].FlightThursday) + Convert.ToInt32(CIFLights[i].FlightFriday) + Convert.ToInt32(CIFLights[i].FlightSaterday) + Convert.ToInt32(CIFLights[i].FlightSunday));
                            csvtrips.WriteField(ToAirportName);
                            csvtrips.WriteField(CIFLights[i].FlightNumber);
                            csvtrips.WriteField("");
                            csvtrips.WriteField("");
                            csvtrips.WriteField("");
                            csvtrips.WriteField("1");
                            csvtrips.WriteField("");
                            csvtrips.NextRecord();

                            // Depart Record
                            csvstoptimes.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate) + Convert.ToInt32(CIFLights[i].FlightMonday) + Convert.ToInt32(CIFLights[i].FlightTuesday) + Convert.ToInt32(CIFLights[i].FlightWednesday) + Convert.ToInt32(CIFLights[i].FlightThursday) + Convert.ToInt32(CIFLights[i].FlightFriday) + Convert.ToInt32(CIFLights[i].FlightSaterday) + Convert.ToInt32(CIFLights[i].FlightSunday));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].DepartTime));
                            csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].DepartTime));
                            csvstoptimes.WriteField(CIFLights[i].FromIATA);
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("0");
                            csvstoptimes.WriteField("");
                            csvstoptimes.WriteField("");
                            csvstoptimes.NextRecord();
                            // Arrival Record
                            if (!CIFLights[i].FlightNextDayArrival)
                            //if (CIFLights[i].DepartTime.TimeOfDay < TimeSpan.Parse("23:59:59") & (CIFLights[i].ArrivalTime.TimeOfDay > TimeSpan.Parse("00:00:00") & CIFLights[i].ArrivalTime.TimeOfDay < TimeSpan.Parse("06:00:00")))
                            {
                                csvstoptimes.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate) + Convert.ToInt32(CIFLights[i].FlightMonday) + Convert.ToInt32(CIFLights[i].FlightTuesday) + Convert.ToInt32(CIFLights[i].FlightWednesday) + Convert.ToInt32(CIFLights[i].FlightThursday) + Convert.ToInt32(CIFLights[i].FlightFriday) + Convert.ToInt32(CIFLights[i].FlightSaterday) + Convert.ToInt32(CIFLights[i].FlightSunday));
                                csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].ArrivalTime));
                                csvstoptimes.WriteField(String.Format("{0:HH:mm:ss}", CIFLights[i].ArrivalTime));
                                csvstoptimes.WriteField(CIFLights[i].ToIATA);
                                csvstoptimes.WriteField("2");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("");
                                csvstoptimes.NextRecord();
                            }
                            else
                            {
                                //add 24 hour for the gtfs time
                                int hour = CIFLights[i].ArrivalTime.Hour;
                                hour = hour + 24;
                                int minute = CIFLights[i].ArrivalTime.Minute;
                                string strminute = minute.ToString();
                                if (strminute.Length == 1) { strminute = "0" + strminute; }
                                csvstoptimes.WriteField(CIFLights[i].FromIATA + CIFLights[i].ToIATA + CIFLights[i].FlightAirline + CIFLights[i].FlightNumber.Replace(" ", "") + String.Format("{0:yyyyMMdd}", CIFLights[i].FromDate) + String.Format("{0:yyyyMMdd}", CIFLights[i].ToDate) + Convert.ToInt32(CIFLights[i].FlightMonday) + Convert.ToInt32(CIFLights[i].FlightTuesday) + Convert.ToInt32(CIFLights[i].FlightWednesday) + Convert.ToInt32(CIFLights[i].FlightThursday) + Convert.ToInt32(CIFLights[i].FlightFriday) + Convert.ToInt32(CIFLights[i].FlightSaterday) + Convert.ToInt32(CIFLights[i].FlightSunday));
                                csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                                csvstoptimes.WriteField(hour + ":" + strminute + ":00");
                                csvstoptimes.WriteField(CIFLights[i].ToIATA);
                                csvstoptimes.WriteField("2");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("0");
                                csvstoptimes.WriteField("");
                                csvstoptimes.WriteField("");
                                csvstoptimes.NextRecord();
                            }
                        }
                    }
                }
            }

            // Create Zip File
            string startPath = gtfsDir;
            string zipPath = myDir + "\\VivaColombia.zip";
            if (File.Exists(zipPath)) { File.Delete(zipPath); }
            ZipFile.CreateFromDirectory(startPath, zipPath, CompressionLevel.Fastest, false);
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
        public Boolean FlightCodeShare;
        public Boolean FlightNextDayArrival;
        public int FlightNextDays;
    }

    public class IATAAirport
    {
        public string stop_id;
        public string stop_name;
        public string stop_desc;
        public string stop_lat;
        public string stop_lon;
        public string zone_id;
        public string stop_url;
    }
}
