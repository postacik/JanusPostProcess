using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace JanusPostProcess
{
    public static class FFMpegService
    {
        public const string FFMpegExeName = "ffmpeg.exe";
        private static string AppPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.G‌​etEntryAssembly().Lo‌​cation);
        private static string FFMpegFolder = AppPath;

        public static bool FFMpegExists
        {
            get
            {
                // FFMpeg folder
                if (!string.IsNullOrWhiteSpace(FFMpegFolder))
                {
                    var path = Path.Combine(FFMpegFolder, FFMpegExeName);

                    if (File.Exists(path))
                        return true;
                }

                // application directory
                var cpath = Path.Combine(Assembly.GetExecutingAssembly().Location, FFMpegExeName);

                if (File.Exists(cpath))
                    return true;

                // Current working directory
                if (File.Exists(FFMpegExeName))
                    return true;

                // PATH
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = FFMpegExeName,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    return true;
                }
                catch { return false; }
            }
        }

        public static string FFMpegExePath
        {
            get
            {
                // FFMpeg folder
                if (!string.IsNullOrWhiteSpace(FFMpegFolder))
                {
                    var path = Path.Combine(FFMpegFolder, FFMpegExeName);

                    if (File.Exists(path))
                        return path;
                }

                // application directory
                var cpath = Path.Combine(Assembly.GetExecutingAssembly().Location, FFMpegExeName);

                return File.Exists(cpath) ? cpath : FFMpegExeName;
            }
        }

        public static Process StartFFMpeg(string Arguments)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = FFMpegExePath,
                    Arguments = Arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                },
                EnableRaisingEvents = true
            };
                        
            //AUZ Do this later process.ErrorDataReceived += (s, e) => FFMpegLog.Instance.Write(e.Data);

            process.Start();

            process.BeginErrorReadLine();
            
            return process;
        }
    }
}
