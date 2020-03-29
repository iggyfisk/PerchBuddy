using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Net.Http;

namespace PerchBuddy
{
    /// <summary>
    /// Way too much logic in here, and not using data binding at all, just trying to get this out the door
    /// </summary>
    public partial class PlayerView : UserControl
    {
        public static readonly DependencyProperty CurrentPlayerProperty =
             DependencyProperty.Register("CurrentPlayer", typeof(Player), typeof(PlayerView));

        public Player CurrentPlayer
        {
            get { return (Player)GetValue(CurrentPlayerProperty); }
            set { SetValue(CurrentPlayerProperty, value); }
        }

        private readonly BackgroundWorker replayFetcher = new BackgroundWorker();
        //public const string BaseURL = "http://localhost:5000";
        public const string BaseURL = "https://highper.ch";

        public PlayerView()
        {
            InitializeComponent();
            grdRoot.DataContext = this;

            replayFetcher.DoWork += fetchReplaysStart;
            replayFetcher.RunWorkerCompleted += fetchReplaysComplete;
        }

        private async void fetchReplaysStart(object sender, DoWorkEventArgs e)
        {
            try
            {
                this.Dispatcher.Invoke(() => {
                    CurrentPlayer.Replays.Clear();
                    lblNoResults.Visibility = Visibility.Collapsed;
                });

                var nameToSearch = e.Argument.ToString();
                nameToSearch += nameToSearch.Length <= 4 ? "#" : string.Empty;

                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(string.Format("{0}/api/search?player_name={1}&max_size=5", BaseURL, System.Net.WebUtility.UrlEncode(nameToSearch)));
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                foreach (dynamic replay in Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(responseBody))
                {
                    this.Dispatcher.Invoke(() => CurrentPlayer.Replays.Add(new Replay()
                    {
                        Name = replay.name,
                        UploadDate = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64((string)replay.timestamp)).DateTime,
                        GameType = replay.type,
                        URL = replay.url,
                        Official = replay.official
                    }));
                };

                this.Dispatcher.Invoke(() =>
                {
                    if (grdReplays.HasItems)
                    {
                        grdReplays.Visibility = Visibility.Visible;
                        lblNoResults.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        grdReplays.Visibility = Visibility.Hidden;
                        lblNoResults.Visibility = Visibility.Visible;
                    }
                });
            }
            catch (Exception exc)
            {
                this.Dispatcher.Invoke(() => MainWindow.Log(exc.Message));
            }
        }

        private void fetchReplaysComplete(object sender, RunWorkerCompletedEventArgs e)
        {

        }

        // Absolutely disgusting but let's gogogo
        bool initialized = false;
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (initialized) return;
            initialized = true;

            grdReplays.ItemsSource = CurrentPlayer.Replays;

            if (CurrentPlayer.Confidence.HasValue && CurrentPlayer.Confidence.Value < 0.6)
            {
                btnForce.IsEnabled = true;
            }
            else
            {
                replayFetcher.RunWorkerAsync(CurrentPlayer.Name);
            }
        }

        private void TxtName_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            NameChange();
        }

        private void TxtName_LostFocus(object sender, RoutedEventArgs e)
        {
            NameChange();
        }

        private void NameChange()
        {
            if (!txtName.Text.Equals(CurrentPlayer.Name))
            {
                CurrentPlayer.Name = txtName.Text;
                CurrentPlayer.Confidence = null;
                lblConfidence.Content = string.Empty;
                btnForce.IsEnabled = false;
                replayFetcher.RunWorkerAsync(CurrentPlayer.Name);
            }
        }

        private void BtnForce_Click(object sender, RoutedEventArgs e)
        {
            btnForce.IsEnabled = false;
            replayFetcher.RunWorkerAsync(CurrentPlayer.Name);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
