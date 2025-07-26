using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using System.Security.Cryptography.X509Certificates;

namespace Tlmine
{
    public partial class Form1 : Form
    {
        private FlowLayoutPanel sidePanel;
        private FlowLayoutPanel tabButtonsPanel;
        private Panel bookmarksPanel;
        private Panel extensionsPanel;
        private TextBox searchBar;
        private Panel searchBarPanel;
        private Button addTabButton;
        private Button backButton;
        private Button forwardButton;
        private Button reloadButton;

        private List<ChromiumWebBrowser> browsers = new List<ChromiumWebBrowser>();
        private List<Button> tabButtons = new List<Button>();

        private const string searchBarPlaceholder = "検索またはURLを入力";

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            InitializeChromium();
            AddNewTab("https://www.google.com");
        }

        private void InitializeUI()
        {
            this.Text = "Tlmine";
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.Font = new Font("Segoe UI", 9);

            // サイドパネル
            sidePanel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.FromArgb(45, 45, 48),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };
            this.Controls.Add(sidePanel);

            InitializeBookmarksPanel();
            InitializeExtensionsPanel();

            // タブボタンパネル（ブックマークと拡張機能の後に配置）
            tabButtonsPanel = new FlowLayoutPanel()
            {
                Height = 200,
                Width = sidePanel.Width - 20,
                BackColor = Color.FromArgb(50, 50, 54),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Margin = new Padding(3)
            };
            sidePanel.Controls.Add(tabButtonsPanel);

            // タブセクションのヘッダー
            var tabHeader = new Label()
            {
                Text = "タブ ▼",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Height = 32,
                Width = tabButtonsPanel.Width - 10,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(50, 50, 54),
                Margin = new Padding(0, 0, 0, 5)
            };
            tabButtonsPanel.Controls.Add(tabHeader);

            // 新規タブ追加ボタン
            addTabButton = new Button()
            {
                Text = "+ 新しいタブ",
                Width = tabButtonsPanel.Width - 20,
                Height = 35,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(5, 2, 5, 2),
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            addTabButton.FlatAppearance.BorderSize = 0;
            addTabButton.Click += AddTabButton_Click;
            tabButtonsPanel.Controls.Add(addTabButton);
            InitializeSearchBar();
        }

        private void AddTabButton_Click(object sender, EventArgs e)
        {
            AddNewTab("https://www.google.com");
        }

        private void InitializeBookmarksPanel()
        {
            bookmarksPanel = new Panel()
            {
                Height = 32,
                Width = sidePanel.Width - 20,
                BackColor = Color.FromArgb(50, 50, 54),
                Margin = new Padding(3),
            };
            sidePanel.Controls.Add(bookmarksPanel);

            var bmHeader = new Label()
            {
                Text = "Bookmarks ▼",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
            };
            bookmarksPanel.Controls.Add(bmHeader);

            Panel bmContent = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(70, 70, 74),
                Visible = true
            };
            bookmarksPanel.Controls.Add(bmContent);

            bmContent.SendToBack();
            bmHeader.BringToFront();

            bmHeader.Click += (s, e) =>
            {
                bmContent.Visible = !bmContent.Visible;
                bookmarksPanel.Height = bmContent.Visible ? 150 : 32;
                bmHeader.Text = bmContent.Visible ? "Bookmarks ▼" : "Bookmarks ▶";
            };

            var bmExampleLink = new LinkLabel()
            {
                Text = "Google",
                LinkColor = Color.LightBlue,
                Dock = DockStyle.Top,
                Height = 25,
                Padding = new Padding(5)
            };
            bmExampleLink.LinkClicked += (s, e) =>
            {
                AddNewTab("https://www.google.com");
            };
            bmContent.Controls.Add(bmExampleLink);
        }

        private void InitializeExtensionsPanel()
        {
            extensionsPanel = new Panel()
            {
                Height = 32,
                Width = sidePanel.Width - 20,
                BackColor = Color.FromArgb(50, 50, 54),
                Margin = new Padding(3),
            };
            sidePanel.Controls.Add(extensionsPanel);

            var extHeader = new Label()
            {
                Text = "Extensions ▼",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
            };
            extensionsPanel.Controls.Add(extHeader);

            Panel extContent = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(70, 70, 74),
                Visible = true
            };
            extensionsPanel.Controls.Add(extContent);

            extContent.SendToBack();
            extHeader.BringToFront();

            extHeader.Click += (s, e) =>
            {
                extContent.Visible = !extContent.Visible;
                extensionsPanel.Height = extContent.Visible ? 150 : 32;
                extHeader.Text = extContent.Visible ? "Extensions ▼" : "Extensions ▶";
            };

            var extExample = new Label()
            {
                Text = "AdBlocker",
                ForeColor = Color.LightGray,
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(5)
            };
            extContent.Controls.Add(extExample);
        }

        private void InitializeSearchBar()
        {
            searchBarPanel = new Panel()
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Padding = new Padding(10, 4, 10, 4),
                BackColor = Color.FromArgb(240, 240, 240),
            };
            this.Controls.Add(searchBarPanel);

            // ナビゲーションボタンパネル
            var navigationPanel = new Panel()
            {
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.FromArgb(240, 240, 240),
            };
            searchBarPanel.Controls.Add(navigationPanel);

            // 戻るボタン
            backButton = new Button()
            {
                Text = "◀",
                Width = 35,
                Height = 28,
                Left = 5,
                Top = 2,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false
            };
            backButton.FlatAppearance.BorderSize = 1;
            backButton.FlatAppearance.BorderColor = Color.Gray;
            backButton.Click += BackButton_Click;
            navigationPanel.Controls.Add(backButton);

            // 進むボタン
            forwardButton = new Button()
            {
                Text = "▶",
                Width = 35,
                Height = 28,
                Left = 42,
                Top = 2,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Enabled = false
            };
            forwardButton.FlatAppearance.BorderSize = 1;
            forwardButton.FlatAppearance.BorderColor = Color.Gray;
            forwardButton.Click += ForwardButton_Click;
            navigationPanel.Controls.Add(forwardButton);

            // リロードボタン
            reloadButton = new Button()
            {
                Text = "⟳",
                Width = 35,
                Height = 28,
                Left = 79,
                Top = 2,
                BackColor = Color.FromArgb(220, 220, 220),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            reloadButton.FlatAppearance.BorderSize = 1;
            reloadButton.FlatAppearance.BorderColor = Color.Gray;
            reloadButton.Click += ReloadButton_Click;
            navigationPanel.Controls.Add(reloadButton);

            searchBar = new TextBox()
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.None,
                ForeColor = Color.Gray,
                BackColor = Color.White,
                Margin = new Padding(0),
                Text = searchBarPlaceholder,
            };

            searchBar.GotFocus += SearchBar_GotFocus;
            searchBar.LostFocus += SearchBar_LostFocus;
            searchBar.KeyDown += SearchBar_KeyDown;

            searchBarPanel.Controls.Add(searchBar);
            searchBarPanel.Paint += SearchBarPanel_Paint;
        }

        private void SearchBar_GotFocus(object sender, EventArgs e)
        {
            if (searchBar.Text == searchBarPlaceholder)
            {
                // 現在のページのURLを表示
                var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
                if (currentBrowser != null && !string.IsNullOrEmpty(currentBrowser.Address))
                {
                    searchBar.Text = currentBrowser.Address;
                }
                else
                {
                    searchBar.Text = "";
                }
                searchBar.ForeColor = Color.Black;
                searchBar.SelectAll(); // URLを全選択
            }
        }

        private void SearchBar_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchBar.Text))
            {
                searchBar.Text = searchBarPlaceholder;
                searchBar.ForeColor = Color.Gray;
            }
        }

        private void SearchBarPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, searchBarPanel.Width - 1, searchBarPanel.Height - 1);
            using (var brush = new SolidBrush(Color.White))
                g.FillRectangle(brush, rect);
            using (var pen = new Pen(Color.LightGray))
                g.DrawRectangle(pen, rect);
        }

        private void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string text = searchBar.Text.Trim();
                if (string.IsNullOrEmpty(text) || text == searchBarPlaceholder) return;

                string url = "";

                // URLかどうか判定を改善
                if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    // 既にプロトコルがある場合はそのまま使用
                    url = text;
                }
                else if (IsValidDomain(text))
                {
                    // ドメイン名っぽい場合のみhttps://を付ける
                    url = "https://" + text;
                }
                else
                {
                    // それ以外は検索クエリとして扱う
                    url = $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";
                }

                if (url.Contains("example")) // 任意条件で変えてください
                {
                    ShowUrlSuggestion("https://www.example.com");
                }
                else
                {
                    ClearUrlSuggestion();
                }

                var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
                if (currentBrowser != null)
                {
                    currentBrowser.Load(url);
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // ナビゲーションボタンのイベントハンドラー
        private void BackButton_Click(object sender, EventArgs e)
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null && currentBrowser.CanGoBack)
            {
                currentBrowser.Back();
            }
        }

        private void ForwardButton_Click(object sender, EventArgs e)
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null && currentBrowser.CanGoForward)
            {
                currentBrowser.Forward();
            }
        }

        private void ReloadButton_Click(object sender, EventArgs e)
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null)
            {
                currentBrowser.Reload();
            }
        }

        // ナビゲーションボタンの状態を更新
        private void UpdateNavigationButtons()
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        backButton.Enabled = currentBrowser.CanGoBack;
                        forwardButton.Enabled = currentBrowser.CanGoForward;
                    }));
                }
                else
                {
                    backButton.Enabled = currentBrowser.CanGoBack;
                    forwardButton.Enabled = currentBrowser.CanGoForward;
                }
            }
        }

        // ドメイン名かどうかを判定するヘルパーメソッド
        private bool IsValidDomain(string text)
        {
            // スペースが含まれている場合は検索クエリとして扱う
            if (text.Contains(" ")) return false;

            // ドット(.)が含まれており、TLDっぽい構造かをチェック
            if (text.Contains("."))
            {
                var parts = text.Split('.');
                if (parts.Length >= 2)
                {
                    // 最後の部分（TLD）が2文字以上で英字のみ
                    var tld = parts[parts.Length - 1];
                    if (tld.Length >= 2 && tld.All(c => char.IsLetter(c)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // タブが切り替わった時に検索バーのURLを更新
        private void UpdateSearchBarUrl()
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null && !searchBar.Focused)
            {
                if (string.IsNullOrEmpty(currentBrowser.Address) || currentBrowser.Address == "about:blank")
                {
                    searchBar.Text = searchBarPlaceholder;
                    searchBar.ForeColor = Color.Gray;
                }
                else
                {
                    searchBar.Text = currentBrowser.Address;
                    searchBar.ForeColor = Color.Black;
                }
            }
        }

        private void ShowUrlSuggestion(string targetUrl)
        {
            string script = $@"
                if (!document.getElementById('tlmine-url-suggestion')) {{
                    var div = document.createElement('div');
                    div.id = 'tlmine-url-suggestion';
                    div.style = 'padding:10px; background:#ff0; border:1px solid #aaa; font-family:sans-serif; position:fixed; top:0; left:0; width:100%; z-index:9999; text-align:center;';
                    div.innerHTML = 'このURLをお探しですか？ <a href=""tlmine://openNewTab?url={targetUrl}"" id=""openNewTabLink"">{targetUrl}</a>';
                    document.body.prepend(div);
                }}
            ";
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            currentBrowser?.ExecuteScriptAsync(script);
        }

        private void ClearUrlSuggestion()
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            string script = "var el=document.getElementById('tlmine-url-suggestion'); if(el) el.remove();";
            currentBrowser?.ExecuteScriptAsync(script);
        }

        private void InitializeChromium()
        {
            if (!(Cef.IsInitialized ?? false))
            {
                var settings = new CefSettings();
                Cef.Initialize(settings);
            }
        }

        private void AddNewTab(string url)
        {
            var browser = new ChromiumWebBrowser(url)
            {
                Dock = DockStyle.Fill,
                Visible = false,
                RequestHandler = new CustomRequestHandler(this)
            };

            browser.FrameLoadEnd += Browser_FrameLoadEnd;
            browser.TitleChanged += Browser_TitleChanged;
            browser.AddressChanged += Browser_AddressChanged; // URLが変更された時のイベント

            browsers.Add(browser);
            this.Controls.Add(browser);

            var tabBtn = new Button()
            {
                Text = "新しいタブ",
                AutoSize = true,
                Height = 30,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(3),
                Tag = browser
            };
            tabBtn.FlatAppearance.BorderSize = 0;
            tabBtn.Click += TabButton_Click;

            var addButtonIndex = tabButtonsPanel.Controls.IndexOf(addTabButton);
            tabButtonsPanel.Controls.Add(tabBtn);
            tabButtonsPanel.Controls.SetChildIndex(tabBtn, addButtonIndex);

            tabButtons.Add(tabBtn);

            SelectTab(browser, tabBtn);
        }

        private void Browser_AddressChanged(object sender, AddressChangedEventArgs e)
        {
            var browser = sender as ChromiumWebBrowser;
            if (browser != null && browser.Visible)
            {
                // 現在表示されているタブのURLが変更された場合、検索バーを更新
                this.Invoke(new Action(() =>
                {
                    if (!searchBar.Focused) // 検索バーにフォーカスがない時のみ更新
                    {
                        searchBar.Text = e.Address;
                        searchBar.ForeColor = Color.Black;
                    }
                    // ナビゲーションボタンの状態を更新
                    UpdateNavigationButtons();
                }));
            }
        }

        private void TabButton_Click(object sender, EventArgs e)
        {
            var clickedButton = sender as Button;
            var associatedBrowser = clickedButton?.Tag as ChromiumWebBrowser;

            if (associatedBrowser != null)
            {
                SelectTab(associatedBrowser, clickedButton);
            }
        }

        private void Browser_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            var browser = sender as ChromiumWebBrowser;
            var tabButton = tabButtons.FirstOrDefault(btn => btn.Tag == browser);

            if (tabButton != null && !string.IsNullOrEmpty(e.Title))
            {
                string title = e.Title.Length > 20 ? e.Title.Substring(0, 17) + "..." : e.Title;

                if (tabButton.InvokeRequired)
                {
                    tabButton.Invoke(new Action(() => tabButton.Text = title));
                }
                else
                {
                    tabButton.Text = title;
                }
            }
        }

        private void SelectTab(ChromiumWebBrowser browser, Button tabButton)
        {
            foreach (var b in browsers)
            {
                b.Visible = false;
            }
            foreach (var btn in tabButtons)
            {
                btn.BackColor = Color.FromArgb(70, 70, 70);
            }

            browser.Visible = true;
            browser.BringToFront();
            tabButton.BackColor = Color.FromArgb(100, 100, 100);

            // タブを切り替えた時に検索バーのURLを更新
            UpdateSearchBarUrl();
            // ナビゲーションボタンの状態を更新
            UpdateNavigationButtons();
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            // ページ読み込み完了時にナビゲーションボタンの状態を更新
            if (e.Frame.IsMain)
            {
                UpdateNavigationButtons();
            }
        }

        public void AddNewTabFromUrl(string url)
        {
            AddNewTab(url);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (var browser in browsers)
            {
                browser.Dispose();
            }

            if (Cef.IsInitialized ?? false)
            {
                Cef.Shutdown();
            }

            base.OnFormClosing(e);
        }

        private class CustomRequestHandler : IRequestHandler
        {
            private readonly Form1 form;

            public CustomRequestHandler(Form1 form)
            {
                this.form = form;
            }

            public bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request,
                bool userGesture, bool isRedirect)
            {
                var url = request.Url;
                if (url.StartsWith("tlmine://openNewTab"))
                {
                    var uri = new Uri(url);
                    var query = ParseQueryString(uri.Query);
                    string openUrl = "https://www.google.com";
                    if (query.TryGetValue("url", out var value))
                    {
                        openUrl = value;
                    }

                    form.Invoke(new Action(() =>
                    {
                        form.AddNewTabFromUrl(openUrl);
                    }));

                    return true; // ナビゲーションをキャンセルして新タブ追加だけ行う
                }
                return false;
            }

            public bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl,
                bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
            {
                return false;
            }

            public bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode,
                string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
            {
                return false;
            }

            public void OnPluginCrashed(IWebBrowser chromiumWebBrowser, IBrowser browser, string pluginPath) { }

            public void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser,
                CefTerminationStatus status, int exitCode, string errorMsg)
            { }

            public void OnDocumentAvailableInMainFrame(IWebBrowser chromiumWebBrowser, IBrowser browser) { }

            public bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
                string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
            {
                return false;
            }

            public IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser,
                IFrame frame, IRequest request, IResponse response)
            {
                return null;
            }

            public bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
                IRequest request, IResponse response)
            {
                return false;
            }

            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser,
                IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator,
                ref bool disableDefaultHandling)
            {
                return null;
            }

            public bool OnSelectClientCertificate(IWebBrowser chromiumWebBrowser, IBrowser browser,
                bool isProxy, string host, int port, X509Certificate2Collection certificates,
                ISelectClientCertificateCallback callback)
            {
                return false;
            }

            public void Dispose() { }

            public bool OnQuotaRequest(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl,
                long newSize, IRequestCallback callback)
            {
                return false;
            }

            public void OnRenderViewReady(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return dict;

            if (query.StartsWith("?")) query = query.Substring(1);

            foreach (var vp in query.Split('&'))
            {
                var parts = vp.Split('=');
                if (parts.Length == 2)
                {
                    dict[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }
            return dict;
        }
    }
}