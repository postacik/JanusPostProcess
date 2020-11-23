using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace JanusPostProcess
{
    class Program
    {


        class MediaFile
        {
            public string fileName { get; set; }
            public bool isVideo { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public long timestamp { get; set; }
            public bool isConverted { get; set; }
        }

        static void Main(string[] args)
        {
            //int r, c;
            //for (var i = 3; i < 1000; i++)
            //{
            //    int w = 1024;
            //    int h = 768;
            //    Utils.CalcDimensions(w, h, i, out r, out c);
            //    double aspectRatio = (double)(w /c) / (h / r);
            //    Console.WriteLine($"rows: {r} cols: {c} aspectRatio: {aspectRatio}");
            //}

            Utils.ReadSettings();
            if (Utils.AppSettings.JanusHost == "" || Utils.AppSettings.HostUser == "" || Utils.AppSettings.HostPassword == "")
            {
                Console.WriteLine("Please fill in Janus Host credentials...");
                return;
            }
            ConnectionInfo ConnNfo = new ConnectionInfo(Utils.AppSettings.JanusHost, 22, Utils.AppSettings.HostUser,
                new AuthenticationMethod[] { new PasswordAuthenticationMethod(Utils.AppSettings.HostUser, Utils.AppSettings.HostPassword) }
            );
            using (var client = new SshClient(ConnNfo))
            {
                Console.WriteLine("Connecting...");
                try
                {
                    client.Connect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not connect to Janus server:" + ex.Message);
                    return;
                }
                Console.WriteLine("Getting mjr files...");
                SshCommand sshCommand;
                Process proc;
                string arguments = "";
                var command = $"ls {Utils.AppSettings.RecordingPath}/{Utils.AppSettings.RoomName}*.mjr";
                var files = client.RunCommand(command).Result.Split(new[] { '\n' });
                if (files.Length == 0 || files[0] == "")
                {
                    Console.WriteLine("No media files found on server");
                    return;
                }
                List<MediaFile> mediaFiles = new List<MediaFile>();
                //Convert each mjr file to its corresponding container file using janus-pp-rec and add it to its job file
                files.ToList().ForEach(file =>
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string outputFile = "";
                    if (file.EndsWith("-audio.mjr"))
                        outputFile = $"{Utils.AppSettings.RecordingPath}/{fileName}.opus";
                    else if (file.EndsWith("-video.mjr"))
                        outputFile = $"{Utils.AppSettings.RecordingPath}/{fileName}.webm";
                    if (!string.IsNullOrEmpty(outputFile))
                    {
                        long timestamp = 0;
                        command = $"{Utils.AppSettings.Januspprec} -j {file}";
                        sshCommand = client.RunCommand(command);
                        Console.WriteLine(command + " - " + sshCommand.ExitStatus);
                        //Console.WriteLine(sshCommand.Result);
                        if (sshCommand.ExitStatus == 0)
                        {
                            long.TryParse(Utils.StringBetween(sshCommand.Result, "\"u\": ", "}"), out timestamp);
                            if (timestamp > 0)
                            {
                                command = $"{Utils.AppSettings.Januspprec} {file} {outputFile}";
                                sshCommand = client.RunCommand(command);
                                Console.WriteLine(command + " - " + sshCommand.ExitStatus);
                                //Console.WriteLine(sshCommand.Result);
                                if (sshCommand.ExitStatus == 0)
                                {
                                    var mediaFile = new MediaFile { fileName = outputFile, height = 0, width = 0, isVideo = outputFile.EndsWith("webm") };
                                    mediaFile.timestamp = timestamp;
                                    if (mediaFile.isVideo)
                                    {
                                        command = $"ffprobe -v quiet -select_streams v:0 -show_entries stream=width,height -of csv=p=0 {outputFile}";
                                        sshCommand = client.RunCommand(command);
                                        if (sshCommand.ExitStatus == 0)
                                        {
                                            var result = sshCommand.Result;
                                            Console.WriteLine(result);
                                            if (!string.IsNullOrEmpty(result))
                                            {
                                                var parts = result.Split(',');
                                                if (parts.Length == 2)
                                                {
                                                    mediaFile.width = Convert.ToInt32(parts[0]);
                                                    mediaFile.height = Convert.ToInt32(parts[1]);
                                                }
                                            }
                                        }
                                    }
                                    mediaFiles.Add(mediaFile);
                                }
                            }
                        }
                    }
                });
                client.Disconnect();
                if (mediaFiles.Count == 0)
                {
                    Console.WriteLine("No files found!");
                    return;
                }
                using (var sftp = new SftpClient(ConnNfo))
                {
                    if (!Directory.Exists(Utils.AppSettings.DownloadDirectory)) Directory.CreateDirectory(Utils.AppSettings.DownloadDirectory);
                    sftp.Connect();
                    mediaFiles.ForEach(mediaFile =>
                    {
                        var fileName = Path.GetFileName(mediaFile.fileName);
                        var newFileName = Path.Combine(Utils.AppSettings.DownloadDirectory, fileName);
                        using (Stream newFile = File.OpenWrite(newFileName))
                        {
                            Console.WriteLine("Downloading: " + fileName);
                            sftp.DownloadFile(mediaFile.fileName, newFile);
                            mediaFile.fileName = newFileName; //Change filename to the new file name
                            Console.WriteLine("Done...");
                        }

                    });
                    sftp.Disconnect();
                }
                //Sort files according to timestamp
                mediaFiles.Sort((m1, m2) =>
                {
                    return m1.timestamp.CompareTo(m2.timestamp);
                });
                List<MediaFile> videoFiles = mediaFiles.FindAll((m) => m.isVideo);
                List<MediaFile> audioFiles = mediaFiles.FindAll((m) => !m.isVideo);
                long firstTimeStamp = (videoFiles.Count > 0) ? videoFiles[0].timestamp : audioFiles[0].timestamp; // if there is at least 1 video file, the act starts with the video file
                int rows = 0;
                int cols = 0;
                int videoParticipantCount = videoFiles.Count;
                Utils.CalcDimensions(Utils.AppSettings.VideoFileWidth, Utils.AppSettings.VideoFileHeight, videoParticipantCount, out rows, out cols);
                int targetWidth = Utils.AppSettings.VideoFileWidth / cols;
                int targetHeight = Utils.AppSettings.VideoFileHeight / rows;
                if (targetWidth % 2 != 0) targetWidth--;
                if (targetHeight % 2 != 0) targetHeight--;
                Console.WriteLine($"participants: {videoParticipantCount}, rows: {rows}, cols: {cols}");
                Console.WriteLine($"targetWidth:{targetWidth} targetHeight:{targetHeight}");

                string targetFileName;
                string videoInputFiles = "";
                string audioInputFiles = "";
                string lastV = "";
                string lastA = "";
                List<string> filter = new List<string>();

                //Scale video files to target size
                int index = 0;
                videoFiles.ForEach(videoFile =>
                {
                    targetFileName = Path.Combine(Utils.AppSettings.DownloadDirectory, Path.GetFileNameWithoutExtension(videoFile.fileName) + ".mp4");
                    string scale = "";
                    if (targetWidth > videoFile.width || targetHeight > videoFile.height)
                        scale = $"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=increase,crop={targetWidth}:{targetHeight}"; //increase size
                    else
                        scale = $"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease,pad={targetWidth}:{targetHeight}:-1:-1:color={Utils.AppSettings.BackgroundColor}"; //decrease size
                    arguments = $"-y -i \"{videoFile.fileName}\" -vf \"{scale}\" -movflags faststart -profile:v high -r 25 {targetFileName}";
                    Console.WriteLine($"ffmpeg {arguments}");
                    proc = FFMpegService.StartFFMpeg(arguments);
                    proc.WaitForExit();
                    videoFile.isConverted = proc.ExitCode == 0;
                    if (videoFile.isConverted)
                    {
                        if (index == 0)
                        {
                            filter.Add($"[{index}:v]pad={Utils.AppSettings.VideoFileWidth}:{Utils.AppSettings.VideoFileHeight}[step{index}]");
                        }
                        else
                        {
                            double delta = Math.Round((videoFile.timestamp - firstTimeStamp) / 1000d);
                            string deltaS = (delta / 1000).ToString().Replace(",", ".");
                            filter.Add($"[{index}:v]setpts=PTS-STARTPTS+{deltaS}/TB[v{index}]");
                        }
                        videoFile.fileName = targetFileName;
                        index++;
                    }
                    else
                    {
                        Console.WriteLine($"{videoFile.fileName} could not be converted!!!");
                    }
                });
                videoFiles = videoFiles.FindAll((m) => m.isConverted);
                for (var i = 0; i < videoFiles.Count; i++)
                {
                    var videoFile = videoFiles[i];
                    videoInputFiles += $"-i \"{videoFile.fileName}\" ";
                    if (i > 0)
                    {
                        int col = i % cols;
                        int row = i / cols;
                        filter.Add($"[step{i - 1}][v{i}]overlay=x={col * targetWidth}:y={row * targetHeight}[step{i}]");
                    }
                }
                if (videoFiles.Count > 0) lastV = $" -map \"[step{videoFiles.Count - 1}]\" ";
                index = videoFiles.Count;
                if (audioFiles.Count > 0)
                {
                    for (var i = 0; i < audioFiles.Count; i++)
                    {
                        var audioFile = audioFiles[i];
                        double delta = Math.Round((audioFile.timestamp - firstTimeStamp) / 1000d);
                        if (delta < 0) delta = 0;
                        audioInputFiles += $"-i \"{audioFile.fileName}\" ";
                        filter.Add($"[{index + i}:a]adelay={delta}|{delta}[a{i}]");
                    }
                    var prefix = "";
                    for (var i = 0; i < audioFiles.Count; i++)
                    {
                        prefix += $"[a{i}]";
                    }
                    filter.Add($"{prefix}amix=inputs={audioFiles.Count}[a]");
                    lastA = $" -map \"[a]\" ";
                }

                targetFileName = Path.Combine(Utils.AppSettings.DownloadDirectory, Utils.AppSettings.RoomName + ".mp4");
                string filterString = string.Join("; ", filter.ToArray());
                arguments = $"-y {videoInputFiles} {audioInputFiles} -filter_complex \"{filterString}\" {lastV} {lastA} {targetFileName}";
                Console.WriteLine($"ffmpeg {arguments}");
                proc = FFMpegService.StartFFMpeg(arguments);
                proc.WaitForExit();
                if (proc.ExitCode == 0)
                {
                    Console.WriteLine($"Files were merged into {targetFileName}");
                } else
                {
                    Console.WriteLine($"Files could not be merged. ExitCode={proc.ExitCode}");
                }
            }
        }
    }
}
