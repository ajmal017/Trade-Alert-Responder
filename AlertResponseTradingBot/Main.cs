﻿using Core;
using CefSharp;
using CefSharp.WinForms;
using MetroFramework.Forms;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CoreDiscord;
using CoreTwitter;
using System.Runtime.CompilerServices;
using CoreScreen;
using System.Diagnostics;
using FontAwesome.Sharp;
using System.Drawing;
using CoreTelegram;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;

namespace TradeAlertResponder
{
    public partial class Main : MetroForm
    {

        // BROWSER SPECIFIC
        // TradingView Specific
        private ChromiumWebBrowser ChromeBrowserTradingView;
        private bool IsTradingViewBrowserAlertScanning = false;
        // Video Tab Specific
        private ChromiumWebBrowser ChromeBrowserVideos;
        //private ChromiumWebBrowser ChromeBrowserIndicatorsExplained;
        private ChromiumWebBrowser ChromeBrowserContribute;
        private ChromiumWebBrowser ChromeBrowserShop;

        // Bot Actions that have been scanned
        List<Alert> Alerts = new List<Alert>();
        int LastAlertsSaveCount = 0;
        int LastAlertsTextUpdateCount = 0;

        //Grid Data
        private BindingSource AlertsSource = new BindingSource();

        // Social Media Settings
        public TwitterSettings TwitterSettings { get; set; } = new TwitterSettings();
        public DiscordSettings DiscordSettings { get; set; } = new DiscordSettings();
        public Discord Discord = new Discord("", 0, 0);
        public Twitter Twitter = new Twitter("","","","");
        public Telegram Telegram = new Telegram("");

        // Alert Settings
        private AlertSettings AlertSettings = new AlertSettings();

        // Screenshot Settings
        private ScreenshotSettings ScreenshotSettings = new ScreenshotSettings();

        // Screenshot class
        private CoreScreen.Screen Screen = new CoreScreen.Screen();

        //Heartbeat
        private bool Heartbeating = true;


        public Main()
        {
            InitializeComponent();

            InitializeChromium();

            grdAlerts.DataSource = AlertsSource;

            LoadAlerts().GetAwaiter().GetResult();
            LoadSettings().GetAwaiter().GetResult();

            
            tabMainView.SelectedIndex = 0;

            HideItems().GetAwaiter().GetResult();

            Task.Run(() => AppHeartbeat());

            SelectNavButton(btnTradingViewTab);

            HideFocus();

            Logging.Log(Logging.LogLevel.Info, "Started.");

            Notification.Icon = SystemIcons.Application; // Required for balloon notification to show

            //Telegram.ChannelMessage("Testing form load.");
        }

        private async Task AppHeartbeat()
        {
            while (Heartbeating)
            {
                using (MemoryMappedFile memoryMappedFile = MemoryMappedFile.CreateNew("tradealertresponder", 10000))
                {
                    using (MemoryMappedViewAccessor viewAccessor = memoryMappedFile.CreateViewAccessor())
                    {
                        try
                        {
                            //if (Alerts.Any())
                            //{
                            //    List<Alert> CurrentAlerts = Alerts;
                            //    var binFormatter = new BinaryFormatter();
                            //    var mStream = new MemoryStream();
                            //    binFormatter.Serialize(mStream, CurrentAlerts);

                            //    byte[] textBytes = mStream.ToArray();  //This gives you the byte array.
                            //    viewAccessor.WriteArray(0, textBytes, 0, textBytes.Length);
                            //}
                        }
                        catch(Exception ex)
                        {

                        }
                    }

                    Thread.Sleep(500);
                }
                Thread.Sleep(500);
            }
        }

        private async Task HideItems()
        {
            // If we need to hide anything, do it here
        }

        private async Task LoadSettings()
        {
            LoadTwitterSettings().GetAwaiter().GetResult();
            LoadDiscordSettings().GetAwaiter().GetResult();
            LoadAlertSettings().GetAwaiter().GetResult();
            LoadScreenshotSettings().GetAwaiter().GetResult();
        }

        private void LoadAlertsGrid()
        {
            DataTable AlertsTable = DataHelper.ConvertToDataTable(Alerts);
            AlertsSource.DataSource = AlertsTable;

            try
            {
                

                grdAlerts.Columns["Ticker"].DisplayIndex = 0;
                grdAlerts.Columns["Action"].DisplayIndex = 1;
                grdAlerts.Columns["Price"].DisplayIndex = 2;
                grdAlerts.Columns["Exchange"].DisplayIndex = 3;
                grdAlerts.Columns["CreationTime"].DisplayIndex = 4;
                grdAlerts.Columns["BaseAsset"].DisplayIndex = 5;
                grdAlerts.Columns["QuoteAsset"].DisplayIndex = 6;
                grdAlerts.Columns["BaseAssetFullName"].DisplayIndex = 7;
                grdAlerts.Columns["Resolution"].DisplayIndex = 8;
                grdAlerts.Columns["Note"].DisplayIndex = 9;
                grdAlerts.Columns["URL"].DisplayIndex = 10;

                grdAlerts.Columns["Id"].DisplayIndex = 11;
                grdAlerts.Columns["TimeOnAlert"].DisplayIndex = 12;
                grdAlerts.Columns["Id"].Visible = false;
                grdAlerts.Columns["TimeOnAlert"].Visible = false;
            }
            catch(Exception ex)
            {

            }
            
        }

        private async Task LoadAlerts()
        {
            Alerts = await FileHelper.ImportAlerts();
            LastAlertsSaveCount = Alerts.Count;

            LoadAlertsGrid();
            await SetAlertTabText();
        }

        private async Task LoadAlertSettings()
        {
            AlertSettings = await FileHelper.ImportAlertSettings();
        }

        private async Task LoadDiscordSettings()
        {
            DiscordSettings = await FileHelper.ImportDiscordSettings();
            if(DiscordSettings != null)
            {
                Discord = new Discord(DiscordSettings.BotToken, DiscordSettings.GuildServerId, DiscordSettings.TextChannelId);
            }
            else
            {
                DiscordSettings = new DiscordSettings();
            }
            //
        }
        private async Task LoadTwitterSettings()
        {
            TwitterSettings = await FileHelper.ImportTwitterSettings();
            if(TwitterSettings != null)
            {
                Twitter = new Twitter(TwitterSettings.ConsumerKey, TwitterSettings.ConsumerSecret, TwitterSettings.AccessToken, TwitterSettings.AccessTokenSecret);
            }
            else
            {
                TwitterSettings = new TwitterSettings();
            }
            
        }

        private async Task LoadScreenshotSettings()
        {
            ScreenshotSettings = await FileHelper.ImportScreenshotSettings();
        }

        private async Task SetAlertTabText()
        {
            if (Alerts.Count > LastAlertsTextUpdateCount)
            {
                pnlAlertsTab.Invoke(new Action(() => pnlAlertsTab.Text = "Alerts (" + Alerts.Count.ToString() + ")"));
                LastAlertsTextUpdateCount = Alerts.Count;
            }
        }

        private async Task SaveAlerts()
        {
            await FileHelper.ExportAlerts(Alerts);
            LastAlertsSaveCount = Alerts.Count;

            LoadAlertsGrid();
        }

        private void HideFocus()
        {
            btnFocus.Select();
        }

        private void InitializeChromium()
        {
            // ------------- TRADINGVIEW TAB -----------------
            CefSettings settings = new CefSettings();
            string CachePath = Constants.AppFolder(Constants.AppDirectory.Browser);

            settings.CachePath = CachePath;
            settings.PersistSessionCookies = true;

            // Initialize cef with the provided settings
            Cef.Initialize(settings);
            // Create a browser component
            ChromeBrowserTradingView = new ChromiumWebBrowser("https://tradingview.com")
            {

                RequestHandler = new MyCefTradingViewBrowserRequestHandler()

            };

            // Add it to the form and fill it to the form window.
            pnlTradingViewBrowser.Controls.Add(ChromeBrowserTradingView);
            ChromeBrowserTradingView.Dock = DockStyle.Fill;

            // ------------ OTHER TABS --------------------
            ChromeBrowserVideos = new ChromiumWebBrowser();
            pnlVideoBrowser.Controls.Add(ChromeBrowserVideos);
            ChromeBrowserVideos.Dock = DockStyle.Fill;
            ChromeBrowserVideos.LoadHtml("<html><iframe width=\"100%\" height=\"100%\" src=\"https://www.youtube.com/embed/videoseries?list=PLM0BBafRCnRNhvMxyiqScZWV_h41raut6\" frameborder=\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe></html>");

            ChromeBrowserShop = new ChromiumWebBrowser();
            pnlShopBrowser.Controls.Add(ChromeBrowserShop);
            ChromeBrowserShop.Dock = DockStyle.Fill;
            ChromeBrowserShop.LoadHtml("<html><iframe width=\"100%\" height=\"100%\" src=\"https://bigbits.io/product-category/all/bigbits/?orderby=price-desc\" frameborder =\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe></html>");

            ChromeBrowserContribute = new ChromiumWebBrowser();
            pnlContributeBrowser.Controls.Add(ChromeBrowserContribute);
            ChromeBrowserContribute.Dock = DockStyle.Fill;
            ChromeBrowserContribute.LoadHtml("<html><iframe width=\"100%\" height=\"100%\" src=\"https://bigbits.io/bigbits-referrals/\" frameborder=\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe></html>");

            //ChromeBrowserIndicatorsExplained = new ChromiumWebBrowser();
            //pnlIndicatorsExplainedTab.Controls.Add(ChromeBrowserIndicatorsExplained);
            //ChromeBrowserIndicatorsExplained.Dock = DockStyle.Fill;
            //ChromeBrowserIndicatorsExplained.LoadHtml("<html><iframe width=\"100%\" height=\"100%\" src=\"https://www.youtube.com/embed/videoseries?list=PLM0BBafRCnRNFHv5lG54qYqbCebOMxz-U\" frameborder=\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe></html>");

        }

        private async Task ScanningForTradingViewAlerts()
        {
            while (IsTradingViewBrowserAlertScanning)
            {
                try
                {
                    string source = await ChromeBrowserTradingView.GetBrowser().MainFrame.GetSourceAsync();

                    List<Alert> ScannedAlerts = await TradingViewAlerts.GetTradingViewAlerts(source, AlertSettings.MyBotName);

                    List<Alert> AlertsAtScan = Alerts; // Doing this helps with exceptions - should reconsider if alerts get duplicated.

                    //List<StockScreenerAlert> stockScreenerAlerts = await TradingViewAlerts.GetTradingViewStockScreenerAlerts(source);



                    Parallel.ForEach(ScannedAlerts, ThisAlert =>//Alert ThisAlert in ScannedAlerts)
                    {
                        if (ThisAlert != null)
                            if (ThisAlert.Id != "" && ThisAlert.TimeOnAlert != "")
                                if (!AlertsAtScan.Any(a => a.TimeOnAlert == ThisAlert.TimeOnAlert && a.Exchange == ThisAlert.Exchange && a.Ticker == ThisAlert.Ticker))
                                {
                                    Alerts.Add(ThisAlert);
                                    Task.Run(() => ProcessAlert(ThisAlert));
                                }
                    });

                    await SetAlertTabText(); // This method checks for changes.

                    //if(!ScannedAlerts.Any() && btnScanTradingViewBrowserAlerts.Text != "Pull up alerts")
                    //    btnScanTradingViewBrowserAlerts.Invoke(new Alert(() => btnScanTradingViewBrowserAlerts.Text = "Pull up alerts"));
                }
                catch (Exception ex)
                {

                }

                Thread.Sleep(1000);
            }
        }

        private async Task ProcessAlert(Alert Alert)
        {
            try
            {
                if (AlertSettings.NotificationOnAlert)
                    Task.Run(() => AlertNotification(Alert));

                bool DiscordOnAlert = AlertSettings.DiscordOnAlert;
                bool AddURLScreenshotToDiscord = DiscordSettings.ScreenshotsInAlerts;
                bool TweetOnAlert = AlertSettings.TweetOnAlert;
                bool AddURLScreenshotToTweet = TwitterSettings.ScreenshotsInAlerts;

                bool WillGenerateMessage = (TweetOnAlert && TwitterSettings.Enabled && TwitterSettings.VerifiedByUserAsWorking)
                    || (DiscordOnAlert && DiscordSettings.Enabled && DiscordSettings.VerifiedByUserAsWorking);
                bool WillHaveScreenShot = WillGenerateMessage && (AddURLScreenshotToDiscord || AddURLScreenshotToTweet);

                bool WillTweet = (TweetOnAlert && TwitterSettings.Enabled && TwitterSettings.VerifiedByUserAsWorking);
                bool WillDiscord = (DiscordOnAlert && DiscordSettings.Enabled && DiscordSettings.VerifiedByUserAsWorking);




                if (WillGenerateMessage)
                {
                    string DirectoryPath = Constants.AppFolder(Constants.AppDirectory.Screenshots);

                    CoreScreen.Screen.ScreenshotResult ScreenshotResult = WillHaveScreenShot ? Screen.Screenshot(Alert.URL, DirectoryPath, ScreenshotSettings.IncludeLogoWatermark, ScreenshotSettings.CropStartPoint, ScreenshotSettings.CropSize, ScreenshotSettings.DoCropImage).GetAwaiter().GetResult() : null;

                    DateTime NowTime = DateTime.UtcNow;

                    string Message = (AlertSettings.MyBotName != "" ? AlertSettings.MyBotName : "") + (AlertSettings.MyBotStatus != "" ? " [" + AlertSettings.MyBotStatus + "]" : "") + (Alert.BaseAsset != "" ? " $" + Alert.BaseAsset : "") + (Alert.BaseAssetFullName != "" ? " #" + Alert.BaseAssetFullName : "") +
                                        "\n" +
                                        (Alert.Ticker != "" ? "\nTicker: " + Alert.Ticker : "") +
                                        ((Alert.Action.ToString() != "" || Alert.Action.ToString() == "None") ? "\nAction: " + Alert.Action : "") +
                                        (Alert.Resolution != "" ? "\nResolution: " + Alert.Resolution : "") +
                                        (Alert.Price != "" ? "\nPrice: " + Alert.Price : "") +
                                        (AlertSettings.ShowUTCTimeStamp ? "\nDate: " + NowTime.ToShortDateString() + " " + NowTime.ToShortTimeString() + " UTC" : "") +
                                        (Alert.URL != "" ? "\n\nURL: " + Alert.URL : "") +
                                        (Alert.Note != "" ? "\nNote: " + Alert.Note : "") +
                                        (AlertSettings.ReferralURL != "" ? "\nReferrals: " + AlertSettings.ReferralURL : "") +
                                        (AlertSettings.Disclaimertext != "" ? "\n\n" + AlertSettings.Disclaimertext : "");

                    if(WillTweet)
                    {
                        if(AddURLScreenshotToTweet)
                        {
                            if(ScreenshotResult != null)
                            {
                                if(ScreenshotResult.Succeeded)
                                {
                                    Task.Run(() => Twitter.TweetWithPngImage(Message, ScreenshotResult.ImageFilePath));
                                }
                                else
                                {
                                    //still send message here
                                    Task.Run(() => Twitter.Tweet(Message));
                                }
                            }
                            else
                            {
                                //still send message here
                                Task.Run(() => Twitter.Tweet(Message));
                            }
                        }
                        else
                        {
                            //still send message here
                            Task.Run(() => Twitter.Tweet(Message));
                        }
                    }
                    if (WillDiscord)
                    {
                        if (AddURLScreenshotToDiscord)
                        {
                            if (ScreenshotResult != null)
                            {
                                if (ScreenshotResult.Succeeded)
                                {
                                    Task.Run(() => Discord.SendFile(ScreenshotResult.ImageFilePath, Message, DiscordSettings.TagHere, DiscordSettings.TagEveryone));
                                }
                                else
                                {
                                    //still send message here
                                    Task.Run(() => Discord.SendMessage(Message, DiscordSettings.TagHere, DiscordSettings.TagEveryone));
                                }
                            }
                            else
                            {
                                //still send message here
                                Task.Run(() => Discord.SendMessage(Message, DiscordSettings.TagHere, DiscordSettings.TagEveryone));
                            }
                        }
                        else
                        {
                            //still send message here
                            Task.Run(() => Discord.SendMessage(Message, DiscordSettings.TagHere, DiscordSettings.TagEveryone));
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                string stopper = "stopped";
            }
            
        }

        private async Task AlertNotification(Alert Alert)
        {
            Notification.ShowBalloonTip(1000, Constants.ProjectName + " " + AlertSettings.MyBotName + " - Alert!", "The application has found a new alert.  " + (Alert.Ticker != "" ? "Ticker: " + Alert.Ticker : "") + ((Alert.Action.ToString() != "" || Alert.Action.ToString() == "None") ? " Action: " + Alert.Action : ""), ToolTipIcon.Info);
        }

        private void btnScanTradingViewBrowserAlerts_Click(object sender, EventArgs e)
        {
            HideFocus();

            if(IsTradingViewBrowserAlertScanning)
            {
                IsTradingViewBrowserAlertScanning = false;
                btnScanTradingViewBrowserAlerts.Text = "Resume Scan";
                spinScanning.Spinning = false;
                spinScanning.Style = MetroFramework.MetroColorStyle.Red;
                btnScanTradingViewBrowserAlerts.BackColor = System.Drawing.Color.Transparent;
            }
            else
            {
                IsTradingViewBrowserAlertScanning = true;
                btnScanTradingViewBrowserAlerts.Text = "Stop Scan";
                spinScanning.Spinning = true;
                spinScanning.Style = MetroFramework.MetroColorStyle.Green;
                btnScanTradingViewBrowserAlerts.BackColor = System.Drawing.Color.Gray;
                Task.Run(() => ScanningForTradingViewAlerts());
            }
            
            
        }

        private void btnTradingViewBrowserHome_Click(object sender, EventArgs e)
        {
            HideFocus();

            ChromeBrowserTradingView.Load("https://www.tradingview.com");
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            Heartbeating = false;

            ChromeBrowserTradingView.Dispose();
            //ChromeBrowserIndicatorsExplained.Dispose();
            ChromeBrowserVideos.Dispose();

            Screen.QuitChrome().GetAwaiter().GetResult();

            SaveAlerts().GetAwaiter().GetResult();
        }

        private void pnlAlerts_TextChanged(object sender, EventArgs e)
        {
            if (Alerts.Count > LastAlertsSaveCount)
            {
                try
                {
                    SaveAlerts().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {

                }

            }
        }

        private void btnDiscordTest_Click(object sender, EventArgs e)
        {
            HideFocus();

            if (DiscordSettings.Enabled && DiscordSettings.VerifiedByUserAsWorking)
                Task.Run(() => Discord.SendMessage("Test", false));
        }

        private void btnTwitterTest_Click(object sender, EventArgs e)
        {
            HideFocus();

            if (TwitterSettings.Enabled && TwitterSettings.VerifiedByUserAsWorking)
            {
                //Task.Run(() => Twitter.Tweet("Test"));
                string DirectoryPath = Constants.AppFolder(Constants.AppDirectory.Screenshots);
                Task.Run(() => Twitter.TweetWithPngImage("Testing with screen in new mode.", Screen.Screenshot("https://www.bigbits.io", DirectoryPath, ScreenshotSettings.IncludeLogoWatermark, ScreenshotSettings.CropStartPoint, ScreenshotSettings.CropSize, ScreenshotSettings.DoCropImage).GetAwaiter().GetResult().ImageFilePath));
            }
        }

        private void btnDiscordSettings_Click(object sender, EventArgs e)
        {
            HideFocus();

            DiscordSettings RESULT = DiscordSettings;
            DiscordSettingsForm DSF = new DiscordSettingsForm(ref RESULT);
            //DialogResult DR = DSF.ShowDialog();
            DSF.Show();
            Discord = new Discord(DiscordSettings.BotToken, DiscordSettings.GuildServerId, DiscordSettings.TextChannelId);
        }

        private void btnTwitterSettings_Click(object sender, EventArgs e)
        {
            HideFocus();

            TwitterSettings RESULT = TwitterSettings;
            TwitterSettingsForm TSF = new TwitterSettingsForm(ref RESULT);
            //DialogResult DR = TSF.ShowDialog();
            TSF.Show();
            Twitter = new Twitter(TwitterSettings.ConsumerKey, TwitterSettings.ConsumerSecret, TwitterSettings.AccessToken, TwitterSettings.AccessTokenSecret);
        }

        private void btnTradingViewTab_Click(object sender, EventArgs e)
        {
            HideFocus();
                SelectNavButton((IconButton)sender);
            
        }

        private void btnAlertsTab_Click(object sender, EventArgs e)
        {
            HideFocus();
                SelectNavButton((IconButton)sender);
        }

        private void btnSettingsTab_Click(object sender, EventArgs e)
        {
            HideFocus();
                SelectNavButton((IconButton)sender);
        }

        private void btnVideoTab_Click(object sender, EventArgs e)
        {
            HideFocus();
                SelectNavButton((IconButton)sender);
        }

        private void SelectNavButton(IconButton Button)
        {
            switch(Button.Name)
            {
                case "btnTradingViewTab":
                    tabMainView.SelectedTab = pnlTradingViewTab;
                    break;
                case "btnAlertsTab":
                    tabMainView.SelectedTab = pnlAlertsTab;
                    break;
                case "btnSettingsTab":
                    tabMainView.SelectedTab = pnlSettingsTab;
                    break;
                case "btnVideoTab":
                    tabMainView.SelectedTab = pnlVideoTab;
                    break;
                case "btnContributeTab":
                    tabMainView.SelectedTab = pnlContributeTab;
                    break;
                case "btnShopTab":
                    tabMainView.SelectedTab = pnlShopTab;
                    break;
            }

            btnTradingViewTab.BackColor = Button.Name == "btnTradingViewTab" ? System.Drawing.Color.Gray : System.Drawing.Color.FromArgb(0, 0, 51, 160);
            btnAlertsTab.BackColor = Button.Name == "btnAlertsTab" ? System.Drawing.Color.Gray : System.Drawing.Color.FromArgb(0, 0, 51, 160);
            btnSettingsTab.BackColor = Button.Name == "btnSettingsTab" ? System.Drawing.Color.Gray : System.Drawing.Color.FromArgb(0, 0, 51, 160);
            btnVideoTab.BackColor = Button.Name == "btnVideoTab" ? System.Drawing.Color.Gray : System.Drawing.Color.FromArgb(0, 0, 51, 160);
        }

        private void btnPineScriptTutorialPlaylist_Click(object sender, EventArgs e)
        {
            HideFocus();
            ChromeBrowserVideos.LoadHtml("<html><iframe width=\"100%\" height=\"100%\" src=\"https://www.youtube.com/embed/videoseries?list=PLM0BBafRCnRNhvMxyiqScZWV_h41raut6\" frameborder=\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe></html>");
        }

        private void btnIndicatorsExplainedPlaylist_Click(object sender, EventArgs e)
        {
            HideFocus();
            ChromeBrowserVideos.LoadHtml("<html><iframe width=\"100%\" height=\"100%\" src=\"https://www.youtube.com/embed/videoseries?list=PLM0BBafRCnRNFHv5lG54qYqbCebOMxz-U\" frameborder=\"0\" allow=\"accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture\" allowfullscreen></iframe></html>");
        }

        private void picLogo_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.bigbits.io");
        }

        private void btnAlertsReset_Click(object sender, EventArgs e)
        {
            Alerts = new List<Alert>();
            Task.Run(() => FileHelper.ExportAlerts(Alerts));
            LoadAlertsGrid();
        }

        private void btnSupportThisProjectTab_Click(object sender, EventArgs e)
        {
            HideFocus();
            SelectNavButton((IconButton)sender);
        }

        private void btnAlertInfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Alert element: <mybot></mybot>" +
                "\nAll alert properties must be within the alert element above." +
                "\n" +
                "\nAlert properties" +
                "\nAction/Signal: <act></act>" +
                "\nURL: <url></url>" +
                "\nNote: <note></note>" +
                "\nBase Asset: <base></base>" +
                "\nQuote Asset: <quote></quote>" +
                "\nTrading Pairl: <pair></pair>" +
                "\nResolution: <res></res>" +
                "\nAction/Signal: <act></act>" +
                "\nBase Full Name: <basefull></basefull>" +
                "\nExchange: <ex></ex>" +
                "\nTime: <time></time>"
                , "Alert Info", MessageBoxButtons.OK, MessageBoxIcon.Information
                );
        }

        private void btnShopTab_Click(object sender, EventArgs e)
        {
            HideFocus();
            SelectNavButton((IconButton)sender);
        }

        private void btnAlertSettings_Click(object sender, EventArgs e)
        {
            HideFocus();

            AlertSettings RESULT = AlertSettings;
            AlertSettingsForm DSF = new AlertSettingsForm(ref RESULT);
            //DialogResult DR = DSF.ShowDialog();
            DSF.Show();
        }

        private void btnScreenshotSettings_Click(object sender, EventArgs e)
        {
            HideFocus();

            ScreenshotSettings RESULT = ScreenshotSettings;
            ScreenshotSettingsForm DSF = new ScreenshotSettingsForm(ref RESULT);
            //DialogResult DR = DSF.ShowDialog();
            DSF.Show();
        }
    }
}
