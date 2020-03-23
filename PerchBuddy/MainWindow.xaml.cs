using System;
using System.Windows;
using System.ComponentModel;

namespace PerchBuddy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static BuddyData Data = new BuddyData();
        private readonly BackgroundWorker loadScraper = new BackgroundWorker();

        // For local file dry runs
        private int screencount = 1;
        private bool loading = false;

        public MainWindow()
        {
            InitializeComponent();
            lstLog.ItemsSource = Data.LogContent;
            icPlayers.ItemsSource = Data.Players;

            loadScraper.DoWork += loadScrapeStart;
            loadScraper.RunWorkerCompleted += loadScrapeCompleted;
        }

        public static void Log(string message)
        {
            Data.Log(message);
        }


        private void loadScrapeStart(object sender, DoWorkEventArgs e)
        {
            var screencap = Screenshot.Capture(Screenshot.enmScreenCaptureMode.Window);
            // For local file dry runs
            //Bitmap screencap = System.Drawing.Image.FromFile(@"C:\Projects\PerchBuddy\Load4_" + screencount++.ToString() + ".png") as Bitmap;

            this.Dispatcher.Invoke(() =>
            {
                imgScreen.Source = Screenshot.BitmapToImageSource(screencap);
                Data.Players.Clear();
            });

            try
            {
                var names = Screenscrape.ScrapePlayerNames(screencap, this.Dispatcher);
                e.Result = names;
            }
            catch (Exception exc)
            {
                this.Dispatcher.Invoke(() => Log(exc.Message));
            }
        }

        private void loadScrapeCompleted(object sender,
                                           RunWorkerCompletedEventArgs e)
        {
            loading = false;
        }


        private void HotkeyPressed()
        {
            Log("Loadscreen hotkey pressed");

            if (loading)
            {
                Log("Ignoring double scrape");
                return;
            }
            loading = true;

            lblHotkey.Content = DateTime.Now.ToLongTimeString();
            loadScraper.RunWorkerAsync();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Hotkey.Start(this, HotkeyPressed);
        }

        protected override void OnClosed(EventArgs e)
        {
            Hotkey.Stop();
            base.OnClosed(e);
        }
    }
}
