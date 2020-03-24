using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PerchBuddy
{
    public class BuddyData
    {
        public ObservableCollection<string> LogContent = new ObservableCollection<string>();
        public void Log(string message)
        {
            LogContent.Add(string.Format("[{0}]: {1}", DateTime.Now.ToLongTimeString(), message));
        }
        
        public ObservableCollection<Player> Players = new ObservableCollection<Player>();
    }

    public class Player
    {
        public static HashSet<string> KnownNames = new HashSet<string>()
        {
            "iggythefisk",
            "bearand",
            "timg4strok",
            "mata",
            "blinn",
            "icebergslim",
            "chuckleman",
            "teelo",
            "talonsarimba"
        };

        public string Name { get; set; }
        public float? Confidence { get; set; }
        public ObservableCollection<Replay> Replays = new ObservableCollection<Replay>();
        public Player Own { get { return this; } }

        public bool IsKnown
        {
            get
            {
                return (!Confidence.HasValue || Confidence.Value > 0.5) && KnownNames.Contains(Name.ToLowerInvariant());
            }
        }
    }

    public class Replay
    {
        public string Name { get; set; }
        public string GameType { get; set; }
        public DateTime UploadDate { get; set; }
        public string URL { get; set; }
        public bool Official { get; set; }
        public string OfficialMarker
        {
            get
            {
                return this.Official ? "✓" : string.Empty;
            }
        }
    }
}
