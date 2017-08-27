using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using System.Xml.Serialization;
using CsvHelper;

namespace Spl2Sml
{
    [Serializable]
    [XmlType(TypeName = "audio")]
    public class Playout
    {
        [XmlAttribute("ID")]
        public string Id { get; set; }
        [XmlElement(ElementName = "type")] // D
        public AudioType Type { get; set; }
        [XmlElement(ElementName = "status")]
        public string Status { get; set; } = "Playing";
        [XmlElement(ElementName = "played_time")] // B
        public string PlayedTime { get; set; }
        public string Length { get; set; }
        [XmlElement(ElementName = "length_in_seconds")] // G
        public int LengthInSeconds { get; set; }
        [XmlElement(ElementName = "title")] // F
        public string Title { get; set; }
        [XmlElement(ElementName = "artist")] // E
        public string Artist { get; set; }
        [XmlElement(ElementName = "category")] // H
        public string Category { get; set; }
    }

    public enum AudioType
    {
        Song = 0,
        Promo = 1,
        M3U = 3,
        OtherPromo = 5,
        SkippedTrack = 7,
        OtherActionOrObject = 8,
        Error = 9
    }

    public class Converter
    {
        private readonly string _sourceDir;
        private readonly string _destinationDir;
        private readonly bool _convertAll;
        private readonly int _playtimeOffset;
        private readonly Timer _conversionTimer = new Timer();
        //private readonly int _windowBuffer;

        public Converter(string sourceDir, string destinationDir, bool convertAll, int interval, int playtimeOffset)
        {
            _sourceDir = sourceDir;
            _destinationDir = destinationDir;
            _convertAll = convertAll;
            _playtimeOffset = playtimeOffset;
            //_windowBuffer = window * 60 * 1000;

            if (!convertAll)
            {
                //var adjust = 60 - (DateTime.Now.Minute % 60);
                //var nextInterval = (adjust * 60 * 1000) + _windowBuffer;

                _conversionTimer.Interval = interval*60*1000;
                _conversionTimer.Elapsed += ConversionTimerOnElapsed;
                _conversionTimer.Start();

                Log(
                    $"Running next conversion at {DateTime.Now.AddMilliseconds(_conversionTimer.Interval).ToString("h:mmtt")}");
            }
            else
            {
                RunConversion();
            }

            Console.ReadKey();
        }

        private void Log(string txt)
        {
            Console.WriteLine($"{DateTime.Now} -- {txt}\r\n");
        }

        private void RunConversion()
        {
            try
            {
                var dirInfo = new DirectoryInfo(_sourceDir);

                var csvFiles =
                    dirInfo.GetFiles()
                        .Where(x => x.Extension == ".csv").ToList();

                if (_convertAll)
                {
                    foreach (var fileInfo in csvFiles)
                    {
                        Convert(fileInfo);
                    }

                    Log("Conversion complete.");
                }
                else
                {
                    var lastWrittenFile = csvFiles.OrderByDescending(y => y.LastWriteTime)
                        .FirstOrDefault();

                    if (lastWrittenFile != null)
                        Convert(lastWrittenFile);
                }
            }
            catch (Exception e)
            {
                Log($"ERROR: {e.Message} {e.InnerException?.Message}");
            }
        }

        private void Convert(FileInfo fileInfo) {
            Log($"Reading {fileInfo.FullName}...");

            var playoutList = GeneratePlayoutList(fileInfo.FullName);

            var outputPath = Path.Combine(_destinationDir, Path.ChangeExtension(fileInfo.Name, "xml"));

            using (var fs = new FileStream(outputPath, FileMode.Create))
            {
                new XmlSerializer(typeof(List<Playout>), new XmlRootAttribute("nexgen_audio_export")).Serialize(fs,
                    playoutList);
            }

            Log($"Converted {playoutList.Count} entries to {outputPath}");
        }

        private void ConversionTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            // var nextInterval = (60 * 60 * 1000) + _windowBuffer;
            // _conversionTimer.Interval = nextInterval;

            RunConversion();

            Log($"Running next conversion at {DateTime.Now.AddMilliseconds(_conversionTimer.Interval).ToString("h:mmtt")}");
        }

        private List<Playout> GeneratePlayoutList(string path)
        {
            var textReader = File.OpenText(path);

            var loggerList = new List<Playout>();

            var counter = 1;

            using (var csv = new CsvReader(textReader))
            {
                while (csv.Read())
                {
                    try
                    {
                        DateTime date;
                        csv.TryGetField(0, out date);

                        string playedTime;
                        csv.TryGetField(1, out playedTime);

                        var playedTimeHoursMinsSeconds = playedTime.Split(':');

                        var playedDateTime = new DateTime(
                            date.Year,
                            date.Month,
                            date.Day,
                            int.Parse(playedTimeHoursMinsSeconds[0]),
                            int.Parse(playedTimeHoursMinsSeconds[1]),
                            int.Parse(playedTimeHoursMinsSeconds[2]));

                        var playedDatetimeWithOffset = playedDateTime.AddMilliseconds(_playtimeOffset);

                        AudioType audioType;
                        csv.TryGetField(3, out audioType);

                        string lengthAsString;
                        var length = new TimeSpan();

                        if (csv.TryGetField(6, out lengthAsString))
                        {
                            if (!string.IsNullOrEmpty(lengthAsString))
                            {
                                var ms = lengthAsString.Split(':');
                                length = new TimeSpan(0, 0, int.Parse(ms[0]), int.Parse(ms[1]));
                            }
                        }

                        string artist;
                        csv.TryGetField(5, out artist);

                        string title;
                        csv.TryGetField(4, out title);

                        string category;
                        csv.TryGetField(7, out category);

                        var record = new Playout
                        {
                            PlayedTime = playedDatetimeWithOffset.ToString("h:mm:ss"),
                            Type = audioType,
                            Artist = artist,
                            Title = title,
                            LengthInSeconds = (int)length.TotalSeconds,
                            Category = category
                        };

                        loggerList.Add(record);
                    }
                    catch (Exception)
                    {
                        Log($"Unable to parse row {counter}. One of the values is not in the correct format.");
                        continue;
                    }

                    counter++;
                }
            }

            return loggerList;
        }
    }
}
