using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using MaxRev.Gdal.Core;
using Microsoft.Web.WebView2.Core;
using System.Data;
using System.Diagnostics;
using System.Net;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using System.Windows.Forms;

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
            GdalBase.ConfigureAll();
            GdalBase.ConfigureGdalDrivers();
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

        private void button1_Click(object sender, EventArgs e)
        {
            // textBox4.Text = "";

            //opt 1: run separate python executable
            //Process.Start(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\test.exe"));
            string sDate = dateTimePicker1.Value.ToString("yyyy-MM-dd");
            string eDate = dateTimePicker2.Value.ToString("yyyy-MM-dd");
            string comids = textBox1.Text;
            string paramsString = comids + " " + sDate + " " + eDate;

            string path = Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\testbuild.exe");

            Process process = Process.Start(path, paramsString);
            // Process process2 = Process.Start(path, paramsString);
            // Process process3 = Process.Start(path, paramsString);
            process.WaitForExit();
            // process2.WaitForExit();
            // process3.WaitForExit();
            /*
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";

            string path = Path.Combine(Environment.CurrentDirectory, @"..\..\..\python");
            process.StartInfo.Arguments = "python ..\\..\\..\\python\\test.py";
            process.Start();
            process.WaitForExit();
            */

            DateTime cTime = DateTime.Now;
            string currentTime = cTime.ToString("yyyy-MM-dd HH");

            string fileName = comids + "_" + sDate + "_" + eDate + "_" + currentTime;

            //string text = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + fileName + ".txt"));
            string text = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\output.txt"));
            Console.WriteLine(text);

            //string[] data = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\" + fileName + ".txt"));
            string[] data = File.ReadAllLines(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\output.txt"));
            Console.WriteLine(data);

            // textBox4.Text = text;

            DataTable dt = new DataTable();
            dt.Columns.Add("date", typeof(string));
            dt.Columns.Add("streamflow", typeof(double));
            dt.Columns.Add("velocity", typeof(double));
            for (int i = 0; i < data.Length; i++) //Here you will read each row from file and fill variables dateFromFile, flowFromFile, and velocityFromFile
            {
                DataRow dr = dt.NewRow();
                string[] val = data[i].Split("*");
                dr["date"] = val[2];
                dr["streamflow"] = val[0];
                dr["velocity"] = val[1];
                dt.Rows.Add(dr);
            }
            //dataGridView1 is the C# DataGridView you have on your form
            dataGridView1.DataSource = dt;

            //string output = process.StandardOutput.ReadToEnd();

            /*
            var process = Process.Start(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\test.exe"));
            process.WaitForExit();

            textBox4.AppendText("Process Finshed:");
            textBox4.AppendText(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"..\..\..\python\dist\test.txt")));
            */


            /*
            //opt 2: use GDAL to read zarr data
            //var driver = Gdal.GetDriverByName("GTiff");
            /*var driver = Gdal.GetDriverByName("Zarr");
            //var driver2 = Gdal.GetDriverByName("XArray");
            // Register GDAL drivers
            Gdal.AllRegister();
            Ogr.RegisterAll();

            // Set AWS credentials and region if not already set in environment variables
            Gdal.SetConfigOption("AWS_REGION", "us-east-1");
            Gdal.SetConfigOption("AWS_ACCESS_KEY_ID", "");
            Gdal.SetConfigOption("AWS_SECRET_ACCESS_KEY", "");
            //Gdal.SetConfigOption("AWS_SESSION_TOKEN", "your-session-token"); //Optional
            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
            Gdal.SetConfigOption("USE_ZMETADATA", "YES");
            Gdal.SetConfigOption("DIM_X", "feature_id: 7994");
            Gdal.SetConfigOption("DIM_Y", "time: 367439");

            // Specify the path to the Zarr file in the S3 bucket
            //string zarrPath = "/vsis3/your-bucket-name/your-zarr-file-path";
            string zarrPath = "/vsis3/noaa-nwm-retrospective-2-1-zarr-pds/chrtout.zarr";

            //textBox4.Text = Gdal.GetCacheMax() + "";

            // Open the dataset
            Dataset dataset = Gdal.Open(zarrPath, Access.GA_ReadOnly);
            if (dataset == null)
            {
                textBox4.Text = "Cannot open dataset";
                Console.WriteLine("Cannot open dataset");
                return;
            }

            // Access the raster band (assuming a single band for simplicity)
            OSGeo.GDAL.Band band = dataset.GetRasterBand(1);
            if (band == null)
            {
                textBox4.Text = "Cannot get raster band";
                Console.WriteLine("Cannot get raster band");
                return;
            }

            // Read a block of data (e.g., 100x100 pixels)
            int blockXSize = 100;
            int blockYSize = 100;
            int[] buffer = new int[blockXSize * blockYSize];
            //band.ReadBlock(0, 0, buffer);

            // Process the data as needed
            // ...

            // Clean up
            band.Dispose();
            dataset.Dispose();
            */
        }
    }
}