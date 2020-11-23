using Newtonsoft.Json;
using System;
using System.IO;

namespace JanusPostProcess
{
    class Utils
    {
        public static string AppPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.G‌​etEntryAssembly().Lo‌​cation);

        public class Settings
        {
            public string Januspprec = "/opt/janus/bin/janus-pp-rec";
            public string RecordingPath = "/opt/janus/record";
            public string DownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Download");
            public string RoomName = "videoroom-1234";
            public string JanusHost = "";
            public string HostUser = "";
            public string HostPassword = "";
            public int VideoFileWidth = 640;
            public int VideoFileHeight = 480;
            public string BackgroundColor = "black";
        }

        public static Settings AppSettings;

        public static void ReadSettings()
        {
            AppSettings = new Settings();
            try
            {
                var settings = File.ReadAllText(Path.Combine(AppPath, "settings.json"));
                AppSettings = JsonConvert.DeserializeObject<Settings>(settings);
                if (AppSettings != null)
                {
                    Console.WriteLine("Application settings were loaded");
                }
                else
                {
                    throw new Exception("Application settings could not be loaded");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading application settings: " + ex.Message);
                throw;
            }

        }

        public static void CalcDimensions(int width, int height, int participantCount, out int rows, out int cols)
        {
            const double targetAspectRatio = 640d / 480;
            bool isPortrait = height > width;

            int divider = 1;
            int targetWidth = 0;
            int targetHeight = 0;
            bool doIncrease = false;
            if (participantCount == 1)
            {
                rows = 1;
                cols = 1;
            }
            else if (participantCount == 2)
            {
                rows = isPortrait ? 2 : 1;
                cols = isPortrait ? 1 : 2;
            }
            else
            {
                //iterate until you find the optimum aspect ratio
                do
                {
                    rows = isPortrait ? Convert.ToInt32(Math.Ceiling((double)participantCount / divider)) : divider;
                    cols = isPortrait ? divider : Convert.ToInt32(Math.Ceiling((double)participantCount / divider));
                    targetWidth = width / cols;
                    targetHeight = height / rows;
                    double aspectRatio = (double)targetWidth / targetHeight;
                    //Console.WriteLine($"rows: {rows} cols: {cols} aspectRatio: {aspectRatio}");

                    int dividerNext = divider + 1;
                    int rowsNext = isPortrait ? Convert.ToInt32(Math.Ceiling((double)participantCount / dividerNext)) : dividerNext;
                    int colsNext = isPortrait ? dividerNext : Convert.ToInt32(Math.Ceiling((double)participantCount / dividerNext));
                    int targetWidthNext = width / colsNext;
                    int targetHeightNext = height / rowsNext;
                    double aspectRatioNext = (double)targetWidthNext / targetHeightNext;
                    //Console.WriteLine($"rows: {rowsNext} cols: {colsNext} aspectRatioNext: {aspectRatioNext}");

                    double delta = Math.Abs(aspectRatio - targetAspectRatio);
                    double deltaNext = Math.Abs(aspectRatioNext - targetAspectRatio);
                    //Console.WriteLine($"delta: {delta} deltaNext: {deltaNext}");

                    doIncrease = (deltaNext < delta) || (Math.Abs(deltaNext - delta) < 0.05 && aspectRatioNext > targetAspectRatio);
                    if (doIncrease) divider++;
                } while (doIncrease);
            }
        }


        public static string StringBetween(string source, string startStr, string endStr)
        {
            string functionReturnValue = null;
            functionReturnValue = "";
            int startIndex = source.IndexOf(startStr);
            if (startIndex >= 0)
            {
                startIndex += startStr.Length;
                int endIndex = source.IndexOf(endStr, startIndex);
                if (endIndex >= 0)
                {
                    functionReturnValue = source.Substring(startIndex, endIndex - startIndex);
                }
            }
            return functionReturnValue;
        }

        public static bool IsDebugRelease
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

    }

}

