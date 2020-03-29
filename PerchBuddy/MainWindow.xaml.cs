using System;
using System.Windows;
using System.ComponentModel;
// For local file dry runs
using System.Drawing;

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

        public static void Log(string message)
        {
            Data.Log(message);
        }

        public MainWindow()
        {
            InitializeComponent();
            lstLog.ItemsSource = Data.LogContent;
            icPlayers.ItemsSource = Data.Players;

            loadScraper.DoWork += loadScrapeStart;
            loadScraper.RunWorkerCompleted += loadScrapeCompleted;
        }

        private void loadScrapeStart(object sender, DoWorkEventArgs e)
        {
            var allies = (bool)e.Argument;
            var screencap = Screenshot.Capture(Screenshot.enmScreenCaptureMode.Window);
            // For local file dry runs
            //screencap = System.Drawing.Image.FromFile(@"C:\Projects\PerchBuddy\4k_" + screencount++.ToString() + ".png") as Bitmap;

            this.Dispatcher.Invoke(() =>
            {
                imgScreen.Source = Screenshot.BitmapToImageSource(screencap);
                Data.Players.Clear();
            });

            try
            {
                e.Result = allies
                    ? Screenscrape.ScrapeAlliesScreen(screencap, this.Dispatcher)
                    : Screenscrape.ScrapeLoadScreen(screencap, this.Dispatcher);
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

        private void HotKeyPressed(bool allies = false)
        {
            Log(allies ? "Allies window hotkey pressed" : "Load screen hotkey pressed");

            if (loading)
            {
                Log("Ignoring double scrape");
                return;
            }

            loading = true;
            loadScraper.RunWorkerAsync(allies);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Hotkey.Start(this, (() => HotKeyPressed()), (() => HotKeyPressed(true)));
        }

        protected override void OnClosed(EventArgs e)
        {
            Hotkey.Stop();
            base.OnClosed(e);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Top = this.Top;
            Properties.Settings.Default.Left = this.Left;
            Properties.Settings.Default.Height = this.Height;
            Properties.Settings.Default.Width = this.Width;

            Properties.Settings.Default.Save();
        }
    }
}
