using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;
using System.IO;
using System.Windows.Threading;

namespace PerchBuddy
{
    public static class Screenscrape
    {
        private static EncoderParameters _noCompression;
        private static ImageCodecInfo _tiff;

        private class ScreenMap
        {
            /// <summary>
            /// X Coordinate of a clearly #FFD428 pixel in the team name ("Team1")
            /// </summary>
            public int TeamNameX { get; set; }
            /// <summary>
            /// Key: Players per team, Value: Y coordinate of the team name, in the middle of the E in "Team1"
            /// </summary>
            public SortedDictionary<int, int> TeamNameY { get; set; }
            /// <summary>
            /// Distance from the middle of the E to the top of the first player name
            /// </summary>
            public int TeamNameMargin { get; set; }
            public int Team1Left { get; set; }
            public int Team2Left { get; set; }
            public int NameWidth { get; set; }
            public int NameHeight { get; set; }
            /// <summary>
            /// Distance from top top of a player name to the top of the next player name
            /// </summary>
            public int NameMargin { get; set; }
        }

        private static readonly Dictionary<Tuple<int, int>, ScreenMap> Resolutions = new Dictionary<Tuple<int, int>, ScreenMap>()
        {
            [new Tuple<int, int>(1920, 1080)] = new ScreenMap()
            {
                TeamNameX = 256,
                TeamNameY = new SortedDictionary<int, int>()
                {
                    [1] = 493,
                    [2] = 449,
                    [3] = 404,
                    [4] = 360
                },
                TeamNameMargin = 32,
                Team1Left = 332,
                Team2Left = 1365,
                NameWidth = 200,
                NameHeight = 23,
                NameMargin = 88
            }
        };

        static Screenscrape()
        {
            _noCompression = new EncoderParameters(1);
            _noCompression.Param[0] = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionNone);

            foreach (var enc in ImageCodecInfo.GetImageEncoders())
            {
                if (enc.MimeType == "image/tiff")
                {
                    _tiff = enc;
                    break;
                }
            }
        }

        // Common scraper mistakes
        private static readonly Dictionary<string, string> Remap = new Dictionary<string, string>()
        {
            ["timgastrok"] = "TIMG4STRok",
            ["timg4astrok"] = "TIMG4STRok"
        };

        private static List<Rect> GetNameBoxes(ScreenMap screenMap, int playerCount)
        {
            // Todo: Adapt to player counts and screen sizes
            var result = new List<Rect>();
            int firstNameY = screenMap.TeamNameY[playerCount] + screenMap.TeamNameMargin;
            for (int p = 0; p < playerCount; ++p)
            {
                result.Add(new Rect(screenMap.Team1Left, firstNameY + (screenMap.NameMargin * p), screenMap.NameWidth, screenMap.NameHeight));
                result.Add(new Rect(screenMap.Team2Left, firstNameY + (screenMap.NameMargin * p), screenMap.NameWidth, screenMap.NameHeight));
            }

            return result;
        }

        public static List<Tuple<string, float>> ScrapePlayerNames(Bitmap screenshot, Dispatcher dispatcher)
        {
            var names = new List<Tuple<string, float>>();
            var resolution = new Tuple<int, int>(screenshot.Width, screenshot.Height);

            if (!Resolutions.ContainsKey(resolution))
            {
                throw new Exception(string.Format("Resolution {0}x{1} not mapped, take a screenshot of the loadscreen and send to iggy", resolution.Item1, resolution.Item2));
            }

            var screenMap = Resolutions[resolution];
            int playerCount = 0;
            foreach (var kvp in screenMap.TeamNameY)
            {
                var color = screenshot.GetPixel(screenMap.TeamNameX, kvp.Value);
                if (color.R == 255 && color.G == 212 && color.B == 40)
                {
                    playerCount = kvp.Key;
                    break;
                }
            }

            if (playerCount == 0)
            {
                throw new Exception("Couldn't detect game type");
            }

            dispatcher.Invoke(() => MainWindow.Log(string.Format("Detected gametype {0}v{0}", playerCount)));

            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                foreach (var box in GetNameBoxes(screenMap, playerCount))
                {
                    var images = new List<Bitmap>();

                    // Bunch of slightly different cropped images of the name, the OCR can produce wildly different results for each of them
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1, box.Y1, box.Width, box.Height), PixelFormat.Format32bppArgb));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1 - 1, box.Width, box.Height), PixelFormat.Format32bppArgb));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1 - 2, box.Width, box.Height), PixelFormat.Format32bppArgb));

                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1, box.Y1, box.Width, box.Height), PixelFormat.Format4bppIndexed));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1 - 1, box.Width, box.Height), PixelFormat.Format4bppIndexed));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1 - 2, box.Width, box.Height), PixelFormat.Format4bppIndexed));

                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1, box.Y1, box.Width, box.Height), PixelFormat.Format8bppIndexed));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1, box.Width, box.Height), PixelFormat.Format8bppIndexed));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1 - 1, box.Width, box.Height), PixelFormat.Format8bppIndexed));
                    images.Add(screenshot.Clone(new System.Drawing.Rectangle(box.X1 - 1, box.Y1 - 2, box.Width, box.Height), PixelFormat.Format8bppIndexed));

                    var results = new List<Tuple<string, float>>();
                    foreach (var image in images)
                    {
                        // Todo: try with tesseract cropping instead
                        MemoryStream byteStream = new MemoryStream();
                        image.Save(byteStream, _tiff, _noCompression);

                        using (var img = Pix.LoadTiffFromMemory(byteStream.ToArray()))
                        {
                            using (var page = engine.Process(img, PageSegMode.SingleLine))
                            {
                                var text = page.GetText();
                                var confidence = page.GetMeanConfidence();

                                if (!string.IsNullOrWhiteSpace(text) && confidence > 0)
                                {
                                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s", "");
                                    results.Add(new Tuple<string, float>(text, confidence));
                                    if (confidence > 0.9) break;
                                }
                            }
                        }
                    }

                    if (results.Count == 0)
                    {
                        dispatcher.Invoke(() => MainWindow.Log(string.Format("No results for box {0},{1}-{2}{3}", box.X1, box.Y1, box.X2, box.Y2)));
                        continue;
                    }

                    var bestResult = results.OrderByDescending(r => r.Item2).First();
                    //Console.WriteLine("Player: {0} Confidence: {1}", bestResult.Item1, bestResult.Item2);
                    var loweredName = bestResult.Item1.ToLower();
                    var name = new Tuple<string, float>(Remap.ContainsKey(loweredName) ? Remap[loweredName] : bestResult.Item1, bestResult.Item2);
                    names.Add(name);

                    var player = new Player() { Name = name.Item1, Confidence = name.Item2 };
                    if (!player.IsKnown)
                    {
                        dispatcher.Invoke(() => MainWindow.Data.Players.Add(player));
                    }
                    else
                    {
                        dispatcher.Invoke(() => MainWindow.Log(string.Format("Ignoring known player {0}", player.Name)));
                    }
                }
            }
            return names;
        }
    }
}
