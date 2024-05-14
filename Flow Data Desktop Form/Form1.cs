/* No longer using Gdal
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using MaxRev.Gdal.Core;
*/
using Microsoft.Web.WebView2.Core;
using System.Data;
using System.Diagnostics;
using System.Net;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;
using System.Runtime.InteropServices;
using System;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Text;
using Microsoft.VisualBasic.Logging;

namespace Flow_Data_Desktop_Form
{

    //Add gridview linked to datatable (within dataset)
    public partial class Form1 : Form
    {
        Task webviewready;
        TaskCompletionSource tcs = new TaskCompletionSource();
        Task mapReadyForRender; // JScript signals when map is ready to render

        public Form1()
        {
            InitializeComponent();

            mapReadyForRender = tcs.Task;
            webviewready = InitializeAsync();

            webView21.Source = new Uri(Path.Combine(Environment.CurrentDirectory, @"..\..\..\html\leafletMap.html"));
            //webView21.Source = new Uri("C:\\Users\\user\\source\\repos\\Flow Data Desktop Form\\Flow Data Desktop Form\\html\\leafletMap.html");
            webView21.Visible = true;
        }

        private Task InitializeAsync()
        {
            return InitializeAsync(webView21);
        }

        async Task InitializeAsync(Microsoft.Web.WebView2.WinForms.WebView2 webView)
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.WebMessageReceived += MessageReceived;
            webView.CoreWebView2.ProcessFailed += WebView_ProcessFailed;
        }

        void WebView_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs args)
        {
            MessageBox.Show("WebView Process Failed");
        }

        void MessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            String content = args.TryGetWebMessageAsString();

            if (content == "DOMContentLoaded")
            {
                tcs.SetResult();  //set mapreadyforrender as complete
            }

            /*else System.Threading.SynchronizationContext.Current.Post((_) =>
            {
                webView_MouseDown(content);
            }, null);*/
        }

        public async void getHTMLData()
        {
            await webView21.ExecuteScriptAsync("window.chrome.webview.postMessage(document.getElementById('comid').value)");
        }

        //This block retrieves a message from the JavaScript to 
        private void webView21_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            //For retrieving message from JS when map is clicked
            string[] message = e.TryGetWebMessageAsString().Split('|');


            //Automatically switches tabs to object user clicked and enters id for object into search field
            if (message[0] == "comid")//specific tab name
            {
                tabControl1.SelectedTab = tabPage1;
                textBox1.Text = message[1];
            }
            if (message[0] == "gauge")
            {
                tabControl1.SelectedTab = tabPage2;
                textBox2.Text = message[1];
            }
            
        }

        //This block sends a message to the JavaScript that changes the webpage to better reflect the tab the user is on
        private async void tabPage_Click(object sender, EventArgs e)
        {
            //Changes attributes of search functionalities within JS code to simplify search for the user
            if (tabControl1.SelectedTab == tabPage2)
            {
                string message = "SwitchSearch|gauge";
                webView21.CoreWebView2.PostWebMessageAsString(message);
                //Debug.WriteLine("Page 2");
            }
            else
            {
                string message = "SwitchSearch|comid";
                webView21.CoreWebView2.PostWebMessageAsString(message);
                //Debug.WriteLine("Page 1");
            }
        }

        //Used for the COMID tab and its functionalities
        private async void button1_Click(object sender, EventArgs e)
        {
            /* TODO:
             * Properly check comid before python. Currently implemented in JS at lines 617 and 717
             * Perhaps attempt use of IronPython (more info in testbuild.py)
             * Display output outside command prompt
             * Make headless command promopt
             * Format file better, identify prints(comid, streamflow, velocity) in output log
             * Finish implementing functionality for multiple comids
             */

            //Create identifiers for input objects from user
            var dateOne = dateTimePicker1.Value;
            var dateTwo = dateTimePicker2.Value;
            string sDate = dateOne.ToString("yyyy-MM-dd");
            string eDate = dateTwo.ToString("yyyy-MM-dd");
            string comids = textBox1.Text;
            //string pat = Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + firstFile);

            //Focus on comid user is requesting data of
            string message = "FocusOn|" + comids;
            webView21.CoreWebView2.PostWebMessageAsString(message);

            string path = Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\testbuild.exe");

            //Create file list and date list that can be lengthen depending on how much of a timespan user requests
            List<string> fileList = new List<string>();
            List<string[]> dateList = new List<string[]>();

            //Populate file list with ouput file names and date list with arrays of dates for processes to use
            var diff = dateTwo - dateOne;
            double days = diff.TotalDays;
            double daysLeft = days;
            var tempStartDate = dateTimePicker1.Value;
            var endDate = dateTimePicker2.Value;
            for (int i = 0; i < days; i += 1461)
            {
                string newFile = "output" + i / 1461 + ".txt";
                fileList.Add(newFile);

                using (FileStream fs = File.Create(Path.Combine(Environment.CurrentDirectory, newFile))) ;

                if (daysLeft > 1461)
                {
                    var newEnd = endDate;
                    if (tempStartDate == dateOne)
                    {
                        newEnd = tempStartDate.AddDays(1461);
                    }
                    else
                    {
                        newEnd = tempStartDate.AddDays(1460);
                    }
                    string[] dates = { tempStartDate.ToString("yyyy-MM-dd"), newEnd.ToString("yyyy-MM-dd") };
                    dateList.Add(dates);

                    tempStartDate = newEnd.AddDays(1);

                    daysLeft -= 1461;
                }
                else
                {
                    string[] dates = { tempStartDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd") };
                    dateList.Add(dates);
                }
            }

            //Runs each process according to the list of file names and date arrays. Stores them in a list for tracking process completion
            List<Process> procsList = new List<Process>();
            int processNum = 0;
            foreach (string file in fileList)
            {
                string paramsString = comids + " " + dateList[processNum][0] + " " + dateList[processNum][1] + " " + file;
                Process process = Process.Start(path, paramsString);
                procsList.Add(process);
                //procs[processNum] = Process.Start(path, paramsString);
                processNum++;
            }

            //Checks processes to ensure all of them have closed before code continues further
            foreach (Process proc in procsList)
            {
                if (proc != null)
                {
                    try
                    {
                        Process.GetProcessById(proc.Id);
                        proc.WaitForExit();
                    }
                    catch (ArgumentException)
                    {
                        //process not running anymore
                    }
                }
            }

            //Conglomerates data from all output files into one list that contains all the data retrieved

            //File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + fileName + ".txt"));
            string[] data = { };
            foreach (string file in fileList)
            {
                //string[] tempData = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + firstFile));
                string[] tempData = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, file));
                List<string> tempList = new List<string>();
                tempList.AddRange(data);
                tempList.AddRange(tempData);
                data = tempList.ToArray();
            }
            //string[] data = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\output.txt"));
            //Debug.WriteLine(data);

            //Sets name of conglomerated output file
            DateTime cTime = DateTime.Now;
            string currentTime = cTime.ToString("yyyy-MM-dd HH.mm.ss");

            string fileName = "(" + comids + "_" + sDate + "_" + eDate + ") [" + currentTime + "]";
            //string filename = "COMID: " + comids + " [" + currentTime + "]";

            //Writes collective data into a file within the logs
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(Environment.CurrentDirectory, @"..\..\..\logs\ComID Data Requests\" + fileName + ".txt")))
            {
                outputFile.WriteLine("[" + comids + "]");
                foreach (string line in data)
                {
                    outputFile.WriteLine(line);
                }
            }

            //Puts retrieved data into a data table to be read by the use in the application
            DataTable dt = new DataTable();
            dt.Columns.Add("date", typeof(string));
            dt.Columns.Add("streamflow", typeof(double));
            dt.Columns.Add("velocity", typeof(double));
            for (int i = 0; i < data.Length; i++) //Here you will read each row from file and fill variables dateFromFile, flowFromFile, and velocityFromFile
            {
                //Debug.WriteLine(data[i]);
                if (i != 0)
                {
                    DataRow dr = dt.NewRow();
                    string[] val = data[i].Split("*");

                    dr["date"] = val[2];

                    double streamDouble = Double.Parse(val[0].Substring(1, val[0].Length - 2));
                    dr["streamflow"] = streamDouble;

                    double velocityDouble = Double.Parse(val[0].Substring(1, val[0].Length - 2));
                    dr["velocity"] = velocityDouble;

                    dt.Rows.Add(dr);

                }
            }
            //dataGridView1 is the C# DataGridView
            Debug.WriteLine(dt);
            dataGridView1.DataSource = dt;
        }



        // To be used once the Gauge tab is its functionalities. Some are still missing
        private void button2_Click(object sender, EventArgs e)
        {
            /* TODO:
             * Check gauge existence before lookup. Currently implemented in JS at line 625
             * Make gauge output on DataGridView like with comid tab
             * Ensure program can pull data from both inactive(before 1950) and active(after 1950) gauges
             * Format output file correctly, and determine why data is not downloading
             */
            /*
             * Manual esting link for inactive gauges https://nwis.waterdata.usgs.gov/nwis/dv?site_no=02217900. 
             * Change out number for inactive gauge being tested and set ouput format to Table or Tab-separated
             */
            /* Manual testing link for active gauges https://nwis.waterdata.usgs.gov/nwis/uv?search_criteria=search_site_no&submitted_form=introduction. 
             * Enter site number, select data you want to retrieve (streamflow and veloctiy needed for gauge), select date range at bottom, choose either table of data or tab-separated under "output options"
             */

            //Creates identifiers for objects from user
            var dateOne = dateTimePicker4.Value;
            var dateTwo = dateTimePicker3.Value;
            string sDate = dateOne.ToString("yyyy-MM-dd");
            string eDate = dateTwo.ToString("yyyy-MM-dd");
            string gageid = textBox2.Text;

            //string path = Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\testbuild.exe");
            var erroMsg = "";
            //Focuses on checks gauge here. Need better method, no way for code to wait for response from JS on whether gauge exists or not.
            webView21.CoreWebView2.PostWebMessageAsString("FocusOnGauge|" + gageid);

            //bool gaugeCheck = false;

            //if (gaugeCheck)
            //{

            //}


            //Calls function to build url and retrieve gauge data
            var gaugeData = GetGaugeData(out erroMsg, gageid, sDate, eDate);

            using (StreamWriter outputFile = new StreamWriter(Path.Combine(Environment.CurrentDirectory, @"..\..\..\logs\Gage Data Requests\GaugeTestOutput.txt")))
            {
                outputFile.WriteLine("[" + gageid + "]");
                foreach (string line in gaugeData)
                {
                    outputFile.WriteLine(line);
                }
            }


            //Placeholder for planned DataGridView
            DataTable dt = new DataTable();
        }

        //Used for receiving message from JS to test if gauge is real, but JS sends message after C# has already moved on
        private bool GaugeMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            //Attempt at receiving message from JS whether there is a gauge or not
            string[] message = e.TryGetWebMessageAsString().Split('|');

            if (message[0] == "noGauge")//your specific tabname
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        //Function used to contrsut url that looks up gauge, but provides little other information. Potentially used for checking gauge existence
        private static string ConstructLookupURL(int gageID)
        {
            //https://nwis.waterdata.usgs.gov/nwis/uv?
            //search_site_no=02217770
            //&search_site_no_match_type=exact&index_pmcode_00055=1&index_pmcode_00060=1&index_pmcode_00061=1&index_pmcode_00065=1&group_key=huc_cd&sitefile_output_format=html_table&column_name=agency_cd&column_name=site_no&column_name=station_nm&column_name=site_tp_cd&column_name=dec_lat_va&column_name=dec_long_va&column_name=coord_acy_cd&column_name=alt_va&column_name=tz_cd&column_name=rt_bol&column_name=peak_begin_date&column_name=peak_end_date&column_name=peak_count_nu&column_name=qw_begin_date&column_name=qw_end_date&column_name=qw_count_nu&range_selection=date_range&
            //begin_date=2023-01-01&
            //end_date=2024-01-01&
            //format=rdb&date_format=YYYY-MM-DD&rdb_compression=value&list_of_search_criteria=search_site_no%2Crealtime_parameter_selection
            StringBuilder sb = new StringBuilder();
            sb.Append(@"https://waterdata.usgs.gov/nwis/uv?");
            sb.Append(@"search_site_no=" + gageID.ToString() + "&");
            sb.Append(@"&search_site_no_match_type=anywhere&");
            sb.Append(@"group_key=NONE&");
            sb.Append(@"format=sitefile_output&");
            sb.Append(@"sitefile_output_format=rdb&");
            sb.Append(@"column_name=agency_cd&");
            sb.Append(@"column_name=site_no&");
            sb.Append(@"column_name=station_nm&");
            sb.Append(@"column_name=site_tp_cd&");
            sb.Append(@"column_name=dec_lat_va&");
            sb.Append(@"column_name=dec_long_va&");
            sb.Append(@"range_selection=days&");
            sb.Append(@"period=7&");
            sb.Append(@"begin_date=2020-12-09&");
            sb.Append(@"end_date=2020-12-16&");
            sb.Append(@"date_format=YYYY-MM-DD&");
            sb.Append(@"rdb_compression=file&");
            sb.Append(@"list_of_search_criteria=lat_long_bounding_box%2Crealtime_parameter_selection");
            //https:/ /waterdata.usgs.gov/nwis/uv?search_site_no=02217770&&search_site_no_match_type=anywhere&group_key=NONE&format=sitefile_output&sitefile_output_format=rdb&column_name=agency_cd&column_name=site_no&column_name=station_nm&column_name=site_tp_cd&column_name=dec_lat_va&column_name=dec_long_va&range_selection=days&period=7&begin_date=2020-12-09&end_date=2020-12-16&date_format=YYYY-MM-DD&rdb_compression=file&list_of_search_criteria=lat_long_bounding_box%2Crealtime_parameter_selection

            return sb.ToString();
        }

        //function used to download gauge data
        private async Task<string> DownloadData(string url, int retries)
        {
            string data = "";
            HttpClient hc = new HttpClient();
            HttpResponseMessage wm = new HttpResponseMessage();
            int maxRetries = 10;

            try
            {
                string status = "";

                while (retries < maxRetries && !status.Contains("OK"))
                {
                    wm = await hc.GetAsync(url);
                    var response = wm.Content;
                    status = wm.StatusCode.ToString();
                    data = await wm.Content.ReadAsStringAsync();
                    retries += 1;
                    if (!status.Contains("OK"))
                    {
                        Thread.Sleep(1000 * retries);
                    }
                }
            }
            catch (Exception ex)
            {
                //debug.writeline was previously log.warning
                if (retries < maxRetries)
                {
                    retries += 1;
                    Debug.WriteLine("Error: Failed to download usgs stream gauge data. Retry {0}:{1}, Url: {2}", retries, maxRetries, url);
                    Random r = new Random();
                    Thread.Sleep(5000 + (r.Next(10) * 1000));
                    return this.DownloadData(url, retries).Result;
                }
                wm.Dispose();
                hc.Dispose();
                Debug.WriteLine(ex, "Error: Failed to download usgs stream gauge data.");
                return null;
            }
            wm.Dispose();
            hc.Dispose();
            return data;
        }

        //Function that csontructs the url for downloading gauge data. Handles data both before and after 1950
        private static string ConstructURL(out string errorMsg, string stationID, string startDate, string endDate)
        {
            errorMsg = "";
            //if (!cInput.Geometry.GeometryMetadata.ContainsKey("gaugestation"))
            //{
            //    errorMsg = "Stream Gauge station id not found. 'gaugestation' required in Geometry MetaData.";
            //    return null;
            //}
            //string stationID = cInput.Geometry.GeometryMetadata["gaugestation"];

            //https://nwis.waterdata.usgs.gov/nwis/uv?
            //search_site_no=02217770
            //&search_site_no_match_type=exact&index_pmcode_00055=1&index_pmcode_00060=1&index_pmcode_00061=1&index_pmcode_00065=1&group_key=huc_cd&sitefile_output_format=html_table&column_name=agency_cd&column_name=site_no&column_name=station_nm&column_name=site_tp_cd&column_name=dec_lat_va&column_name=dec_long_va&column_name=coord_acy_cd&column_name=alt_va&column_name=tz_cd&column_name=rt_bol&column_name=peak_begin_date&column_name=peak_end_date&column_name=peak_count_nu&column_name=qw_begin_date&column_name=qw_end_date&column_name=qw_count_nu&range_selection=date_range&
            //begin_date=2023-01-01&
            //end_date=2024-01-01&
            //format=rdb&date_format=YYYY-MM-DD&rdb_compression=value&list_of_search_criteria=search_site_no%2Crealtime_parameter_selection

            //Checks the year the use is looking for.
            //Need to implement method to retrieve gauge's earliest date in case user attempts to call date before gauge was installed
            string sYear = startDate.Substring(0, 4);
            Debug.WriteLine(sYear);

            if (Int32.Parse(sYear) >= 1950)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(@"https://waterdata.usgs.gov/nwis/uv?");
                sb.Append(@"search_site_no=" + stationID + "&");
                sb.Append(@"&search_site_no_match_type=exact&index_pmcode_00055=1&index_pmcode_00060=1&index_pmcode_00061=1&index_pmcode_00065=1&group_key=huc_cd&sitefile_output_format=html_table&column_name=agency_cd&column_name=site_no&column_name=station_nm&column_name=site_tp_cd&column_name=dec_lat_va&column_name=dec_long_va&column_name=coord_acy_cd&column_name=alt_va&column_name=tz_cd&column_name=rt_bol&column_name=peak_begin_date&column_name=peak_end_date&column_name=peak_count_nu&column_name=qw_begin_date&column_name=qw_end_date&column_name=qw_count_nu&range_selection=date_range&");
                sb.Append(@"begin_date=" + startDate + "&");
                sb.Append(@"end_date=" + endDate + "&");
                sb.Append(@"format=rdb&date_format=YYYY-MM-DD&rdb_compression=value&list_of_search_criteria=search_site_no%2Crealtime_parameter_selection");

                return sb.ToString();
            }
            else
            {
                string eYear = endDate.Substring(0, 4);

                if (Int32.Parse(eYear) >= 1950)
                {
                    endDate = "1949-12-31";
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(@"https://nwis.waterdata.usgs.gov/nwis/dv?cb_00060=on&format=rdb&");
                sb.Append(@"search_site_no=" + stationID + "&");
                sb.Append(@"legacy=&referred_module=sw&period=&");
                sb.Append(@"begin_date=" + startDate + "&");
                sb.Append(@"end_date=" + endDate);

                return sb.ToString();
            }
            //https:/ /nwis.waterdata.usgs.gov/nwis/dv?cb_00060=on&format=rdb&site_no=02217900&legacy=&referred_module=sw&period=&begin_date=1930-04-02&end_date=1949-04-15
        }

        //Function that gathers and returns the data
        public List<string> GetGaugeData(out string errorMsg, string stationID, string startDate, string endDate)
        {
            errorMsg = "";

            // Constructs the url for the USGS stream gauge data request and it's query string.
            string url = ConstructURL(out errorMsg, stationID, startDate, endDate);
            if (errorMsg.Contains("ERROR")) { return null; }

            // Uses the constructed url to download time series data.
            string data = DownloadData(url, 0).Result;
            Debug.WriteLine(data);
            if (errorMsg.Contains("ERROR") || data == null) { return null; }

            return new List<string>() { data, url };
        }
    }
}