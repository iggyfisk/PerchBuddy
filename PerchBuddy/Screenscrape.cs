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
        private static readonly Color LoadScreenTeamColor = Color.FromArgb(255, 212, 40);
        private static readonly Color LoadScreenPlayerColor = Color.FromArgb(255, 221, 51);

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

            // For the Allies window after the game started
            public int AlliesLeft { get; set; }
            public int AlliesTop { get; set; }
            public int AlliesMargin { get; set; }
            public int AlliesWidth { get; set; }
            public int AlliesHeight { get; set; }

            public int? GetPlayerCount(Bitmap screenshot)
            {
                int? playerCount = null;
                foreach (var kvp in this.TeamNameY)
                {
                    var color = screenshot.GetPixel(this.TeamNameX, kvp.Value);
                    if (color.Equals(LoadScreenTeamColor))
                    {
                        playerCount = kvp.Key;
                        break;
                    }
                }

                return playerCount;
            }

            public IEnumerable<Rect> GetLoadScreenNameBoxes(int playerCount)
            {
                var team1 = new List<Rect>();
                var team2 = new List<Rect>();
                int firstNameY = this.TeamNameY[playerCount] + this.TeamNameMargin;
                for (int p = 0; p < playerCount; ++p)
                {
                    team1.Add(new Rect(this.Team1Left, firstNameY + (this.NameMargin * p), this.NameWidth, this.NameHeight));
                    team2.Add(new Rect(this.Team2Left, firstNameY + (this.NameMargin * p), this.NameWidth, this.NameHeight));
                }

                return team2.Concat(team1);
            }

            public IEnumerable<Rect> GetAlliesNameBoxes()
            {
                var nameBoxes = new List<Rect>();
                for (int p = 0; p < 7; ++p)
                {
                    nameBoxes.Add(new Rect(this.AlliesLeft, this.AlliesTop + (this.AlliesMargin * p), this.AlliesWidth, this.AlliesHeight));
                }
                return nameBoxes;
            }
        }

        private static readonly Dictionary<(int, int), ScreenMap> Resolutions = new Dictionary<(int, int), ScreenMap>()
        {
            [(1920, 1080)] = new ScreenMap()
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
                NameMargin = 88,
                AlliesLeft = 551,
                AlliesTop = 176,
                AlliesMargin = 49,
                AlliesWidth = 240,
                AlliesHeight = 30
            },
            [(2560, 1440)] = new ScreenMap()
            {
                TeamNameX = 401,
                TeamNameY = new SortedDictionary<int, int>()
                {
                    [1] = 662,
                    [2] = 607,
                    [3] = 552,
                    [4] = 497
                },
                TeamNameMargin = 41,
                Team1Left = 497,
                Team2Left = 1792,
                NameWidth = 220,
                NameHeight = 25,
                NameMargin = 110,
                AlliesLeft = 735,
                AlliesTop = 236,
                AlliesMargin = 65,
                AlliesWidth = 290,
                AlliesHeight = 40
            },
            [(3440, 1440)] = new ScreenMap()
            {
                TeamNameX = 842,
                TeamNameY = new SortedDictionary<int, int>()
                {
                    [1] = 662,
                    [2] = 607,
                    [3] = 552,
                    [4] = 497
                },
                TeamNameMargin = 41,
                Team1Left = 936,
                Team2Left = 2231,
                NameWidth = 240,
                NameHeight = 25,
                NameMargin = 110
            },
            [(3840, 2160)] = new ScreenMap()
            {
                TeamNameX = 690,
                TeamNameY = new SortedDictionary<int, int>()
                {
                    [1] = 998,
                    [2] = 922,
                    [3] = 845,
                    [4] = 768
                },
                TeamNameMargin = 57,
                Team1Left = 825,
                Team2Left = 2635,
                NameWidth = 350,
                NameHeight = 32,
                NameMargin = 154,
                AlliesLeft = 1101,
                AlliesTop = 353,
                AlliesMargin = 98,
                AlliesWidth = 365,
                AlliesHeight = 57
            }
        };

        // Common scraper mistakes
        private static readonly Dictionary<string, string> Remap = new Dictionary<string, string>()
        {
            ["timgastrok"] = "TIMG4STRok",
            ["timg4astrok"] = "TIMG4STRok"
        };

        private static EncoderParameters _noCompression;
        private static ImageCodecInfo _tiff;

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

        private static int ColorDistance(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }

        private static Bitmap TextHighlight(Bitmap image, IEnumerable<Rect> areas, Color textColor, int maxDistance = 0, bool blackOnWhite = false)
        {
            foreach (var area in areas)
            {
                for (var x = area.X1; x < area.X2; ++x)
                {
                    for (var y = area.Y1; y < area.Y2; ++y)
                    {
                        if (ColorDistance(image.GetPixel(x, y), textColor) > maxDistance)
                        {
                            image.SetPixel(x, y, blackOnWhite ? Color.White : Color.Black);
                        }
                        else if (blackOnWhite)
                        {
                            image.SetPixel(x, y, Color.Black);
                        }
                    }
                }
            }

            return image;
        }

        public static List<Tuple<string, float>> ScrapeLoadScreen(Bitmap screenshot, Dispatcher dispatcher)
        {
            var resolution = (screenshot.Width, screenshot.Height);
            if (!Resolutions.ContainsKey(resolution))
            {
                throw new Exception(string.Format("Resolution {0}x{1} not mapped, take a screenshot of the load screen and send to iggy", resolution.Item1, resolution.Item2));
            }

            var screenMap = Resolutions[resolution];

            int? playerCount = screenMap.GetPlayerCount(screenshot);
            if (!playerCount.HasValue)
            {
                throw new Exception("Couldn't detect game type");
            }

            dispatcher.Invoke(() => MainWindow.Log(string.Format("Detected gametype {0}v{0}", playerCount)));

            return ScrapeNames(screenshot, screenMap.GetLoadScreenNameBoxes(playerCount.Value), dispatcher);
        }

        public static List<Tuple<string, float>> ScrapeAlliesScreen(Bitmap screenshot, Dispatcher dispatcher)
        {
            var resolution = (screenshot.Width, screenshot.Height);
            if (!Resolutions.ContainsKey(resolution))
            {
                throw new Exception(string.Format("Resolution {0}x{1} not mapped, take a screenshot of the allies screen and send to iggy", resolution.Item1, resolution.Item2));
            }

            var screenMap = Resolutions[resolution];

            return ScrapeNames(screenshot, screenMap.GetAlliesNameBoxes(), dispatcher, true);
        }

        private static List<Tuple<string, float>> ScrapeNames(Bitmap screenshot, IEnumerable<Rect> nameBoxes, Dispatcher dispatcher, bool alliesWindow = false)
        {
            var fullSize = new Rectangle(0, 0, screenshot.Width, screenshot.Height);
            var images = new List<Bitmap>
                {
                    // Slightly different color variations of the screenshot, the OCR can produce wildly different results for each of them
                    screenshot,
                    screenshot.Clone(fullSize, PixelFormat.Format4bppIndexed),
                    screenshot.Clone(fullSize, PixelFormat.Format8bppIndexed),
                };

            if (!alliesWindow)
            {
                images.Add(TextHighlight(screenshot.Clone(fullSize, screenshot.PixelFormat), nameBoxes, LoadScreenPlayerColor));
                images.Add(TextHighlight(screenshot.Clone(fullSize, screenshot.PixelFormat), nameBoxes, LoadScreenPlayerColor, blackOnWhite: true));
                images.Add(TextHighlight(screenshot.Clone(fullSize, screenshot.PixelFormat), nameBoxes, LoadScreenPlayerColor, 30));
            }

            var imagePix = new List<Pix>();
            foreach (var image in images)
            {
                using (var byteStream = new MemoryStream())
                {
                    image.Save(byteStream, _tiff, _noCompression);
                    imagePix.Add(Pix.LoadTiffFromMemory(byteStream.ToArray()));
                }
            }

            var names = new List<Tuple<string, float>>();
            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                bool previousFailed = false;
                foreach (var box in nameBoxes)
                {
                    var rects = new List<Rect>
                    {
                        // Bunch of slightly different cropped areas of the name, the OCR can produce wildly different results for each of them
                        new Rect(box.X1, box.Y1, box.Width, box.Height),
                        new Rect(box.X1 - 1, box.Y1 - 1, box.Width, box.Height),
                        new Rect(box.X1 - 1, box.Y1 - 2, box.Width, box.Height),
                        new Rect(box.X1 - 1, box.Y1 - 2, box.Width, box.Height)
                    };

                    var results = new List<Tuple<string, float>>();
                    foreach (var img in imagePix)
                    {
                        foreach (var rect in rects)
                        {
                            using (var page = engine.Process(img, rect, PageSegMode.SingleLine))
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

                    var bestResult = results.OrderByDescending(r => r.Item2).FirstOrDefault();
                    if (results.Count == 0 ||
                        (alliesWindow && (bestResult.Item1 == "|" || bestResult.Item2 < 0.4)))
                    {
                        dispatcher.Invoke(() => MainWindow.Log(string.Format("No results for box {0},{1}-{2},{3}", box.X1, box.Y1, box.X2, box.Y2)));
                        if (alliesWindow && previousFailed)
                        {
                            dispatcher.Invoke(() => MainWindow.Log("Two failed boxes in a row, aborting"));
                            return names;
                        }

                        previousFailed = true;
                        continue;
                    }
                    previousFailed = false;

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

            dispatcher.Invoke(() => MainWindow.Log(string.Format("{0} names scraped", names.Count)));
            return names;
        }
    }
}
