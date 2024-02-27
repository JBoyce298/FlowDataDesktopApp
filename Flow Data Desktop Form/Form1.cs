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
            //webView21.Source = new Uri("C:\\Users\\jboyce\\source\\repos\\Flow Data Desktop Form\\Flow Data Desktop Form\\html\\leafletMap.html");
            webView21.Visible = true;
            //label1.Text = Environment.CurrentDirectory + "";
            //GdalBase.ConfigureAll();
            //GdalBase.ConfigureGdalDrivers();
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

        private void webView21_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            //For retrieving comid when map is clicked
            textBox1.Text = e.TryGetWebMessageAsString();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
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

                if(daysLeft > 1461)
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
            foreach(string file in fileList)
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
                if(proc != null)
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

            //string[] data = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + fileName + ".txt"));
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

            DateTime cTime = DateTime.Now;
            string currentTime = cTime.ToString("yyyy-MM-dd HH.mm.ss");

            string fileName = "(" + comids + "_" + sDate + "_" + eDate + ") [" + currentTime + "]";

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

        // To be used once the Gage Data tab is ready. For now, the tab has no functionality. 
        private void button2_Click(object sender, EventArgs e)
        {
            /*
            var dateOne = dateTimePicker4.Value;
            var dateTwo = dateTimePicker3.Value;
            string sDate = dateOne.ToString("yyyy-MM-dd");
            string eDate = dateTwo.ToString("yyyy-MM-dd");
            string gageids = textBox2.Text;

            //string path = Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\testbuild.exe");

            List<string> fileList = new List<string>();
            List<string[]> dateList = new List<string[]>();

            var diff = dateTwo - dateOne;
            double days = diff.TotalDays;
            //bool firstRun = true;
            double daysLeft = days;
            var tempStartDate = dateTimePicker4.Value;
            var endDate = dateTimePicker3.Value;
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
                        //firstRun = false;
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
        

            // Process Handling
             
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
            

            string[] data = { };
            foreach (string file in fileList)
            {
                string[] tempData = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, file));
                List<string> tempList = new List<string>();
                tempList.AddRange(data);
                tempList.AddRange(tempData);
                data = tempList.ToArray();
            }

            DateTime cTime = DateTime.Now;
            string currentTime = cTime.ToString("yyyy-MM-dd HH.mm.ss");

            string fileName = "(" + gageids + "_" + sDate + "_" + eDate + ") [" + currentTime + "]";

            //StreamWriter outputFile = new StreamWriter(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + fileName + ".txt"));

            using (StreamWriter outputFile = new StreamWriter(Path.Combine(Environment.CurrentDirectory, @"..\..\..\logs\Gage Data Requests\" + fileName + ".txt")))
            {
                outputFile.WriteLine("[" + gageids + "]");
                foreach (string line in data)
                {
                    outputFile.WriteLine(line);
                }
            }

            DataTable dt = new DataTable();
            /*
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

            //dataGridView1 is the C# DataGridView you have on your form
            Debug.WriteLine(dt);
            
            dataGridView2.DataSource = dt;
            */
        }
    }
}