# JanusPostProcess
Sample C# project to download and post process video and audio files recorded in a Janus video room session.

It downloads all video and audio files of a video room recording session and combines them as a matrix in a single file.

After compiling the project, copy settings.json file into your Bin folder (where JanusPostProcess.exe is created) and edit the properties according to your setup.
```json
{
  "Januspprec": "/opt/janus/bin/janus-pp-rec",
  "RecordingPath": "/opt/janus/record",
  "DownloadDirectory": "C:\\JanusDownload",
  "RoomName": "videoroom-1234",
  "JanusHost": "yourhostname",
  "HostUser": "user",
  "HostPassword": "password",
  "VideoFileWidth": 640,
  "VideoFileHeight": 480,
  "BackgroundColor":  "black"
}
```
* "Januspprec": The path to your janus-pp-rec binary
* "RecordingPath": The path where the video room was recorded
* "DownloadDirectory": Local path on your machine where the media files are going to be downloaded and processed. Use \\\\ for \ character
* "RoomName": The name of the video room
* "JanusHost": Host name or IP address of Janus server
* "HostUser": User name on the server which has rights to access the media files
* "HostPassword": Password of the user
* "VideoFileWidth": The width of the video file the recordings will be merged into
* "VideoFileHeight": The width of the video file the recordings will be merged into
* "BackgroundColor": Background color of the canvas of the video file
