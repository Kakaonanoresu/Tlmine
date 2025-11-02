using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Tlmine
{
    public partial class Form1 : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private FlowLayoutPanel sidePanel;
        private FlowLayoutPanel tabButtonsPanel;
        private Panel bookmarksPanel;
        private Panel extensionsPanel;
        private Panel downloadHistoryPanel;
        private Panel bookmarksContent;
        private Panel extensionsContent;
        private Panel downloadHistoryContent;
        private TextBox searchBar;
        private Panel searchBarPanel;
        private Button addTabButton;
        private Button backButton;
        private Button forwardButton;
        private Button reloadButton;
        private Button addBookmarkButton;
        private ProgressBar downloadProgressBar;
        private Label downloadLabel;
        private Panel downloadPanel;

        private List<ChromiumWebBrowser> browsers = new List<ChromiumWebBrowser>();
        private List<Button> tabButtons = new List<Button>();
        private List<BookmarkItem> bookmarks = new List<BookmarkItem>();
        private List<ExtensionItem> extensions = new List<ExtensionItem>();
        private List<DownloadHistoryItem> downloadHistory = new List<DownloadHistoryItem>();
        private Dictionary<ChromiumWebBrowser, string> browserTitles = new Dictionary<ChromiumWebBrowser, string>();

        private const string searchBarPlaceholder = "検索またはURLを入力";
        private const string bookmarksFilePath = "bookmarks.json";
        private const string extensionsFilePath = "extensions.json";
        private const string downloadHistoryFilePath = "download_history.json";

        public Form1()
        {
            InitializeComponent();
            LoadBookmarks();
            LoadExtensions();
            LoadDownloadHistory();
            InitializeUI();
            InitializeChromium();
            AddNewTab("https://www.google.com");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnableDarkTitleBar();
        }

        private void EnableDarkTitleBar()
        {
            if (this.Handle != IntPtr.Zero)
            {
                int darkModeValue = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));
            }
        }

        private void InitializeUI()
        {
            this.Text = "Tlmine Browser";
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.Font = new Font("Segoe UI", 9);

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
            InitializeDownloadHistoryPanel();

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
            addTabButton.Click += (s, e) => AddNewTab("https://www.google.com");
            tabButtonsPanel.Controls.Add(addTabButton);
            InitializeSearchBar();
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

            bookmarksContent = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(70, 70, 74),
                Visible = true,
                AutoScroll = true
            };
            bookmarksPanel.Controls.Add(bookmarksContent);

            bookmarksContent.SendToBack();
            bmHeader.BringToFront();

            bmHeader.Click += (s, e) =>
            {
                bookmarksContent.Visible = !bookmarksContent.Visible;
                bookmarksPanel.Height = bookmarksContent.Visible ? 150 : 32;
                bmHeader.Text = bookmarksContent.Visible ? "Bookmarks ▼" : "Bookmarks ▶";
            };

            var addBookmarkBtn = new Button()
            {
                Text = "+ ブックマーク追加",
                Height = 25,
                Width = bookmarksContent.Width - 10,
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Top = 5,
                Left = 5
            };
            addBookmarkBtn.FlatAppearance.BorderSize = 0;
            addBookmarkBtn.Click += AddBookmarkBtn_Click;
            bookmarksContent.Controls.Add(addBookmarkBtn);

            RefreshBookmarksList();
        }

        private void AddBookmarkBtn_Click(object sender, EventArgs e)
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null)
            {
                string title = browserTitles.ContainsKey(currentBrowser) && !string.IsNullOrEmpty(browserTitles[currentBrowser])
                    ? browserTitles[currentBrowser] : "新しいブックマーク";

                var dialog = new BookmarkDialog(currentBrowser.Address, title);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    bookmarks.Add(new BookmarkItem { Title = dialog.BookmarkTitle, Url = dialog.BookmarkUrl });
                    SaveBookmarks();
                    RefreshBookmarksList();
                }
            }
        }

        private void RefreshBookmarksList()
        {
            foreach (var control in bookmarksContent.Controls.OfType<Control>().Where(c => c.Tag?.ToString() == "bookmark").ToList())
            {
                bookmarksContent.Controls.Remove(control);
                control.Dispose();
            }

            int yPos = 35;
            foreach (var bookmark in bookmarks)
            {
                var panel = new Panel() { Width = bookmarksContent.Width - 15, Height = 25, Top = yPos, Left = 5, Tag = "bookmark" };

                var linkLabel = new LinkLabel()
                {
                    Text = bookmark.Title.Length > 20 ? bookmark.Title.Substring(0, 17) + "..." : bookmark.Title,
                    LinkColor = Color.LightBlue,
                    Width = panel.Width - 25,
                    Height = 25,
                    Tag = bookmark.Url
                };
                linkLabel.LinkClicked += (s, e) => AddNewTab(linkLabel.Tag.ToString());

                var deleteBtn = new Button()
                {
                    Text = "×",
                    Width = 20,
                    Height = 20,
                    Left = panel.Width - 25,
                    Top = 2,
                    BackColor = Color.FromArgb(200, 70, 70),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8),
                    Tag = bookmark
                };
                deleteBtn.FlatAppearance.BorderSize = 0;
                deleteBtn.Click += (s, e) => { bookmarks.Remove((BookmarkItem)deleteBtn.Tag); SaveBookmarks(); RefreshBookmarksList(); };

                panel.Controls.Add(linkLabel);
                panel.Controls.Add(deleteBtn);
                bookmarksContent.Controls.Add(panel);
                yPos += 30;
            }
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

            extensionsContent = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(70, 70, 74),
                Visible = true,
                AutoScroll = true
            };
            extensionsPanel.Controls.Add(extensionsContent);

            extensionsContent.SendToBack();
            extHeader.BringToFront();

            extHeader.Click += (s, e) =>
            {
                extensionsContent.Visible = !extensionsContent.Visible;
                extensionsPanel.Height = extensionsContent.Visible ? 150 : 32;
                extHeader.Text = extensionsContent.Visible ? "Extensions ▼" : "Extensions ▶";
            };

            var addExtensionBtn = new Button()
            {
                Text = "+ 拡張機能追加",
                Height = 25,
                Width = extensionsContent.Width - 10,
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Top = 5,
                Left = 5
            };
            addExtensionBtn.FlatAppearance.BorderSize = 0;
            addExtensionBtn.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "JavaScript Files (*.js)|*.js", Title = "拡張機能スクリプトを選択" })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            var newExt = new ExtensionItem
                            {
                                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                                Enabled = true,
                                ScriptPath = dialog.FileName,
                                ScriptContent = File.ReadAllText(dialog.FileName)
                            };
                            extensions.Add(newExt);
                            SaveExtensions();
                            RefreshExtensionsList();
                            MessageBox.Show($"拡張機能 '{newExt.Name}' を追加しました。", "成功");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"読み込み失敗: {ex.Message}", "エラー");
                        }
                    }
                }
            };
            extensionsContent.Controls.Add(addExtensionBtn);

            RefreshExtensionsList();
        }

        private void RefreshExtensionsList()
        {
            foreach (var control in extensionsContent.Controls.OfType<Control>().Where(c => c.Tag?.ToString() == "extension").ToList())
            {
                extensionsContent.Controls.Remove(control);
                control.Dispose();
            }

            int yPos = 35;
            foreach (var ext in extensions)
            {
                var panel = new Panel() { Width = extensionsContent.Width - 15, Height = 25, Top = yPos, Left = 5, Tag = "extension" };

                panel.Controls.Add(new Label()
                {
                    Text = ext.Name.Length > 18 ? ext.Name.Substring(0, 15) + "..." : ext.Name,
                    ForeColor = Color.LightGray,
                    Width = panel.Width - 45,
                    Height = 25
                });

                var toggleBtn = new Button()
                {
                    Text = ext.Enabled ? "ON" : "OFF",
                    Width = 30,
                    Height = 20,
                    Left = panel.Width - 50,
                    Top = 2,
                    BackColor = ext.Enabled ? Color.Green : Color.Gray,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7),
                    Tag = ext
                };
                toggleBtn.FlatAppearance.BorderSize = 0;
                toggleBtn.Click += (s, e) =>
                {
                    ext.Enabled = !ext.Enabled;
                    toggleBtn.Text = ext.Enabled ? "ON" : "OFF";
                    toggleBtn.BackColor = ext.Enabled ? Color.Green : Color.Gray;
                    SaveExtensions();
                };

                var deleteBtn = new Button()
                {
                    Text = "×",
                    Width = 15,
                    Height = 20,
                    Left = panel.Width - 20,
                    Top = 2,
                    BackColor = Color.FromArgb(200, 70, 70),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7),
                    Tag = ext
                };
                deleteBtn.FlatAppearance.BorderSize = 0;
                deleteBtn.Click += (s, e) => { extensions.Remove((ExtensionItem)deleteBtn.Tag); SaveExtensions(); RefreshExtensionsList(); };

                panel.Controls.Add(toggleBtn);
                panel.Controls.Add(deleteBtn);
                extensionsContent.Controls.Add(panel);
                yPos += 30;
            }
        }

        private void InjectExtensions(ChromiumWebBrowser browser)
        {
            if (browser == null) return;
            foreach (var ext in extensions.Where(e => e.Enabled && !string.IsNullOrEmpty(e.ScriptContent)))
            {
                try { browser.GetMainFrame()?.EvaluateScriptAsync(ext.ScriptContent); }
                catch (Exception ex) { Debug.WriteLine($"拡張機能エラー ({ext.Name}): {ex.Message}"); }
            }
        }

        private void InitializeDownloadHistoryPanel()
        {
            downloadHistoryPanel = new Panel()
            {
                Height = 32,
                Width = sidePanel.Width - 20,
                BackColor = Color.FromArgb(50, 50, 54),
                Margin = new Padding(3),
            };
            sidePanel.Controls.Add(downloadHistoryPanel);

            var dlHeader = new Label()
            {
                Text = "Downloads ▼",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
            };
            downloadHistoryPanel.Controls.Add(dlHeader);

            downloadHistoryContent = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(70, 70, 74),
                Visible = true,
                AutoScroll = true
            };
            downloadHistoryPanel.Controls.Add(downloadHistoryContent);

            downloadHistoryContent.SendToBack();
            dlHeader.BringToFront();

            dlHeader.Click += (s, e) =>
            {
                downloadHistoryContent.Visible = !downloadHistoryContent.Visible;
                downloadHistoryPanel.Height = downloadHistoryContent.Visible ? 200 : 32;
                dlHeader.Text = downloadHistoryContent.Visible ? "Downloads ▼" : "Downloads ▶";
            };

            var clearBtn = new Button()
            {
                Text = "履歴をクリア",
                Height = 25,
                Width = downloadHistoryContent.Width - 10,
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Top = 5,
                Left = 5
            };
            clearBtn.FlatAppearance.BorderSize = 0;
            clearBtn.Click += (s, e) =>
            {
                if (MessageBox.Show("ダウンロード履歴をすべて削除しますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    downloadHistory.Clear();
                    SaveDownloadHistory();
                    RefreshDownloadHistoryList();
                }
            };
            downloadHistoryContent.Controls.Add(clearBtn);

            RefreshDownloadHistoryList();
        }

        private void RefreshDownloadHistoryList()
        {
            foreach (var control in downloadHistoryContent.Controls.OfType<Control>().Where(c => c.Tag?.ToString() == "download").ToList())
            {
                downloadHistoryContent.Controls.Remove(control);
                control.Dispose();
            }

            int yPos = 35;
            foreach (var dl in downloadHistory.OrderByDescending(d => d.DownloadDate).Take(20))
            {
                var panel = new Panel() { Width = downloadHistoryContent.Width - 15, Height = 40, Top = yPos, Left = 5, Tag = "download", BackColor = Color.FromArgb(60, 60, 64) };

                panel.Controls.Add(new Label()
                {
                    Text = dl.FileName.Length > 25 ? dl.FileName.Substring(0, 22) + "..." : dl.FileName,
                    ForeColor = Color.LightGray,
                    Width = panel.Width - 60,
                    Height = 20,
                    Left = 5,
                    Top = 2,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold)
                });

                panel.Controls.Add(new Label()
                {
                    Text = dl.DownloadDate.ToString("yyyy/MM/dd HH:mm"),
                    ForeColor = Color.Gray,
                    Width = panel.Width - 60,
                    Height = 15,
                    Left = 5,
                    Top = 22,
                    Font = new Font("Segoe UI", 7)
                });

                var openBtn = new Button()
                {
                    Text = "開く",
                    Width = 45,
                    Height = 18,
                    Left = panel.Width - 50,
                    Top = 2,
                    BackColor = Color.FromArgb(70, 130, 180),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7),
                    Tag = dl
                };
                openBtn.FlatAppearance.BorderSize = 0;
                openBtn.Click += (s, e) =>
                {
                    var item = (DownloadHistoryItem)openBtn.Tag;
                    if (File.Exists(item.FilePath))
                    {
                        try { Process.Start(new ProcessStartInfo { FileName = item.FilePath, UseShellExecute = true }); }
                        catch (Exception ex) { MessageBox.Show($"ファイルを開けませんでした: {ex.Message}", "エラー"); }
                    }
                    else MessageBox.Show("ファイルが見つかりません。", "エラー");
                };

                var deleteBtn = new Button()
                {
                    Text = "×",
                    Width = 45,
                    Height = 18,
                    Left = panel.Width - 50,
                    Top = 20,
                    BackColor = Color.FromArgb(200, 70, 70),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7),
                    Tag = dl
                };
                deleteBtn.FlatAppearance.BorderSize = 0;
                deleteBtn.Click += (s, e) => { downloadHistory.Remove((DownloadHistoryItem)deleteBtn.Tag); SaveDownloadHistory(); RefreshDownloadHistoryList(); };

                panel.Controls.Add(openBtn);
                panel.Controls.Add(deleteBtn);
                downloadHistoryContent.Controls.Add(panel);
                yPos += 45;
            }
        }

        private void InitializeSearchBar()
        {
            searchBarPanel = new Panel()
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Padding = new Padding(10, 4, 10, 4),
                BackColor = Color.FromArgb(45, 45, 48),
            };
            this.Controls.Add(searchBarPanel);

            downloadPanel = new Panel() { Width = 200, Height = 28, BackColor = Color.FromArgb(45, 45, 48), Visible = false };
            downloadProgressBar = new ProgressBar() { Width = 150, Height = 20, Left = 5, Top = 4 };
            downloadLabel = new Label() { Width = 40, Height = 20, Left = 160, Top = 6, Font = new Font("Segoe UI", 8), ForeColor = Color.White, Text = "0%" };
            downloadPanel.Controls.Add(downloadProgressBar);
            downloadPanel.Controls.Add(downloadLabel);

            var navPanel = new Panel() { Dock = DockStyle.Right, Width = 160, BackColor = Color.FromArgb(45, 45, 48) };
            searchBarPanel.Controls.Add(navPanel);
            searchBarPanel.Controls.Add(downloadPanel);

            backButton = CreateNavButton("◀", 5, false);
            forwardButton = CreateNavButton("▶", 42, false);
            reloadButton = CreateNavButton("⟳", 79, true);
            addBookmarkButton = new Button()
            {
                Text = "★",
                Width = 35,
                Height = 28,
                Left = 116,
                Top = 2,
                BackColor = Color.FromArgb(60, 60, 64),
                ForeColor = Color.Gold,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            addBookmarkButton.FlatAppearance.BorderSize = 0;
            addBookmarkButton.Click += AddBookmarkBtn_Click;

            backButton.Click += (s, e) => browsers.FirstOrDefault(b => b.Visible)?.Back();
            forwardButton.Click += (s, e) => browsers.FirstOrDefault(b => b.Visible)?.Forward();
            reloadButton.Click += (s, e) => browsers.FirstOrDefault(b => b.Visible)?.Reload();

            navPanel.Controls.Add(backButton);
            navPanel.Controls.Add(forwardButton);
            navPanel.Controls.Add(reloadButton);
            navPanel.Controls.Add(addBookmarkButton);

            downloadPanel.Left = navPanel.Left - downloadPanel.Width - 10;
            downloadPanel.Top = 4;

            searchBar = new TextBox()
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.None,
                ForeColor = Color.Gray,
                BackColor = Color.FromArgb(60, 60, 64),
                Text = searchBarPlaceholder,
            };

            searchBar.GotFocus += (s, e) =>
            {
                if (searchBar.Text == searchBarPlaceholder)
                {
                    var cur = browsers.FirstOrDefault(b => b.Visible);
                    searchBar.Text = cur != null && !string.IsNullOrEmpty(cur.Address) ? cur.Address : "";
                    searchBar.ForeColor = Color.White;
                    searchBar.SelectAll();
                }
            };

            searchBar.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBar.Text))
                {
                    searchBar.Text = searchBarPlaceholder;
                    searchBar.ForeColor = Color.Gray;
                }
            };

            searchBar.KeyDown += SearchBar_KeyDown;
            searchBarPanel.Controls.Add(searchBar);
            searchBarPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rect = new Rectangle(0, 0, searchBarPanel.Width - 1, searchBarPanel.Height - 1);
                using (var brush = new SolidBrush(Color.FromArgb(60, 60, 64)))
                    g.FillRectangle(brush, rect);
                using (var pen = new Pen(Color.FromArgb(80, 80, 84)))
                    g.DrawRectangle(pen, rect);
            };
        }

        private Button CreateNavButton(string text, int left, bool enabled)
        {
            var btn = new Button()
            {
                Text = text,
                Width = 35,
                Height = 28,
                Left = left,
                Top = 2,
                BackColor = Color.FromArgb(60, 60, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", text == "⟳" ? 12 : 10, FontStyle.Bold),
                Enabled = enabled
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private async void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string text = searchBar.Text.Trim();
                if (string.IsNullOrEmpty(text) || text == searchBarPlaceholder) return;

                string url = text.StartsWith("http://") || text.StartsWith("https://") ? text
                    : IsValidDomain(text) ? "https://" + text
                    : $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";

                string suggestion = IsValidDomain(text) || text.StartsWith("http") ? "" : GetUrlSuggestion(text);

                var browser = browsers.FirstOrDefault(b => b.Visible);
                if (browser != null)
                {
                    browser.Load(url);
                    if (!string.IsNullOrEmpty(suggestion))
                    {
                        await Task.Delay(1500);
                        await ShowUrlSuggestion(suggestion);
                    }
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private string GetUrlSuggestion(string query)
        {
            var q = query.ToLower();
            var suggestions = new Dictionary<string, string>
            {
                {"youtube", "https://www.youtube.com"}, {"twitter", "https://twitter.com"},
                {"facebook", "https://www.facebook.com"}, {"instagram", "https://www.instagram.com"},
                {"github", "https://github.com"}, {"amazon", "https://www.amazon.co.jp"},
                {"楽天", "https://www.rakuten.co.jp"}, {"yahoo", "https://www.yahoo.co.jp"},
                {"ニコニコ", "https://www.nicovideo.jp"}, {"wikipedia", "https://ja.wikipedia.org"},
                {"gmail", "https://mail.google.com"}
            };
            return suggestions.FirstOrDefault(s => q.Contains(s.Key)).Value ?? "";
        }

        private bool IsValidDomain(string text)
        {
            if (text.Contains(" ")) return false;
            if (text.Contains("."))
            {
                var parts = text.Split('.');
                if (parts.Length >= 2)
                {
                    var tld = parts[parts.Length - 1];
                    return tld.Length >= 2 && tld.All(char.IsLetter);
                }
            }
            return false;
        }

        private async Task ShowUrlSuggestion(string targetUrl)
        {
            var browser = browsers.FirstOrDefault(b => b.Visible);
            if (browser == null) return;

            string script = $@"
(function(){{
    var e=document.getElementById('tlmine-url-suggestion');
    if(e)e.remove();
    var div=document.createElement('div');
    div.id='tlmine-url-suggestion';
    div.style.cssText='padding:12px 20px;background:linear-gradient(135deg,#4CAF50,#45a049);font-size:14px;position:fixed;top:0;left:0;width:100%;z-index:99999;text-align:center;color:white;box-shadow:0 2px 10px rgba(0,0,0,0.3)';
    div.innerHTML='🌐 このURLをお探しですか？ <a href=""#"" id=""tl-link"" style=""color:#fff;text-decoration:underline;margin-left:10px"">{targetUrl.Replace("\"", "\\\"")}</a> <button id=""tl-close"" style=""background:rgba(255,255,255,0.2);border:none;color:white;padding:4px 8px;margin-left:15px;border-radius:3px;cursor:pointer"">×</button>';
    document.body.prepend(div);
    document.getElementById('tl-link').onclick=function(e){{e.preventDefault();window.location.href='{targetUrl.Replace("'", "\\'")}'}};
    document.getElementById('tl-close').onclick=function(){{div.remove()}};
    setTimeout(function(){{div.remove()}},10000);
}})();";

            try { await browser.EvaluateScriptAsync(script); }
            catch (Exception ex) { Debug.WriteLine($"JS実行エラー: {ex.Message}"); }
        }

        private void UpdateNavigationButtons()
        {
            var browser = browsers.FirstOrDefault(b => b.Visible);
            if (browser != null)
            {
                if (InvokeRequired)
                    Invoke(new Action(() =>
                    {
                        backButton.Enabled = browser.CanGoBack;
                        forwardButton.Enabled = browser.CanGoForward;
                        backButton.ForeColor = browser.CanGoBack ? Color.White : Color.Gray;
                        forwardButton.ForeColor = browser.CanGoForward ? Color.White : Color.Gray;
                    }));
                else
                {
                    backButton.Enabled = browser.CanGoBack;
                    forwardButton.Enabled = browser.CanGoForward;
                    backButton.ForeColor = browser.CanGoBack ? Color.White : Color.Gray;
                    forwardButton.ForeColor = browser.CanGoForward ? Color.White : Color.Gray;
                }
            }
        }

        private void UpdateSearchBarUrl()
        {
            var browser = browsers.FirstOrDefault(b => b.Visible);
            if (browser != null && !searchBar.Focused)
            {
                searchBar.Text = string.IsNullOrEmpty(browser.Address) || browser.Address == "about:blank" ? searchBarPlaceholder : browser.Address;
                searchBar.ForeColor = searchBar.Text == searchBarPlaceholder ? Color.Gray : Color.White;
            }
        }

        private void InitializeChromium()
        {
            if (!(Cef.IsInitialized ?? false))
            {
                var settings = new CefSettings();
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tlmine");
                Directory.CreateDirectory(appData);
                settings.RootCachePath = appData;
                settings.CachePath = Path.Combine(appData, "Cache");
                settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

                settings.CefCommandLineArgs.Add("disable-gpu-vsync");
                settings.CefCommandLineArgs.Add("disable-background-timer-throttling");
                settings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows");
                settings.CefCommandLineArgs.Add("disable-renderer-backgrounding");
                settings.CefCommandLineArgs.Add("disable-blink-features", "AutomationControlled");
                settings.CefCommandLineArgs.Add("enable-media-stream");
                settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");
                settings.CefCommandLineArgs.Add("enable-proprietary-codecs");
                settings.CefCommandLineArgs.Add("enable-gpu-rasterization");
                settings.CefCommandLineArgs.Add("enable-zero-copy");
                settings.CefCommandLineArgs.Add("enable-webgl");

                settings.PersistSessionCookies = true;
                settings.MultiThreadedMessageLoop = true;
                settings.LogSeverity = LogSeverity.Disable;

                Cef.Initialize(settings);
            }
        }

        private void AddNewTab(string url)
        {
            var browser = new ChromiumWebBrowser(url)
            {
                Dock = DockStyle.Fill,
                Visible = false,
                RequestHandler = new CustomRequestHandler(this),
                DownloadHandler = new CustomDownloadHandler(this)
            };

            browser.FrameLoadEnd += (s, e) => { if (e.Frame.IsMain) { UpdateNavigationButtons(); InjectExtensions(browser); } };
            browser.TitleChanged += Browser_TitleChanged;
            browser.AddressChanged += (s, e) =>
            {
                if (browser.Visible)
                {
                    if (InvokeRequired)
                        Invoke(new Action(() => { if (!searchBar.Focused) { searchBar.Text = e.Address; searchBar.ForeColor = Color.White; } UpdateNavigationButtons(); }));
                    else { if (!searchBar.Focused) { searchBar.Text = e.Address; searchBar.ForeColor = Color.White; } UpdateNavigationButtons(); }
                }
            };
            browser.LoadingStateChanged += (s, e) => UpdateNavigationButtons();

            browsers.Add(browser);
            Controls.Add(browser);

            var tabContainer = new Panel() { Width = tabButtonsPanel.Width - 20, Height = 35, BackColor = Color.FromArgb(70, 70, 70), Margin = new Padding(5, 2, 5, 2) };

            var closeBtn = new Button()
            {
                Text = "×",
                Width = 25,
                Height = 35,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Tag = browser,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Right,
                Name = "closeButton"
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => CloseTab(browser);
            closeBtn.MouseEnter += (s, e) => closeBtn.BackColor = Color.FromArgb(200, 70, 70);
            closeBtn.MouseLeave += (s, e) => closeBtn.BackColor = Color.FromArgb(70, 70, 70);

            var tabBtn = new Button()
            {
                Text = "新しいタブ",
                Width = tabContainer.Width - 25,
                Height = 35,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Tag = browser,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 5, 0),
                Dock = DockStyle.Fill,
                ImageAlign = ContentAlignment.MiddleLeft,
                Name = "tabButton",
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            tabBtn.FlatAppearance.BorderSize = 0;
            tabBtn.Click += (s, e) => SelectTab(browser, tabContainer);

            tabContainer.Controls.Add(closeBtn);
            tabContainer.Controls.Add(tabBtn);

            tabButtonsPanel.Controls.Add(tabContainer);
            tabButtonsPanel.Controls.SetChildIndex(tabContainer, tabButtonsPanel.Controls.IndexOf(addTabButton));
            tabButtons.Add(tabBtn);

            SelectTab(browser, tabContainer);
            CreateDefaultIcon(tabBtn);
        }

        private void CloseTab(ChromiumWebBrowser browserToClose)
        {
            if (browsers.Count <= 1) { MessageBox.Show("最後のタブは閉じることができません。", "情報"); return; }

            bool wasSelected = browserToClose.Visible;
            Panel tabContainer = null;
            foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
            {
                if (container.Controls.OfType<Button>().Any(btn => btn.Tag == browserToClose))
                {
                    tabContainer = container;
                    break;
                }
            }

            if (tabContainer != null) { tabButtonsPanel.Controls.Remove(tabContainer); tabContainer.Dispose(); }

            var tabBtn = tabButtons.FirstOrDefault(btn => btn.Tag == browserToClose && btn.Name == "tabButton");
            if (tabBtn != null) tabButtons.Remove(tabBtn);

            if (browserTitles.ContainsKey(browserToClose)) browserTitles.Remove(browserToClose);

            browsers.Remove(browserToClose);
            Controls.Remove(browserToClose);
            browserToClose.Dispose();

            if (wasSelected && browsers.Count > 0)
            {
                var nextBrowser = browsers.Last();
                Panel nextContainer = null;
                foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
                {
                    if (container.Controls.OfType<Button>().Any(btn => btn.Tag == nextBrowser && btn.Name == "tabButton"))
                    {
                        nextContainer = container;
                        break;
                    }
                }
                if (nextContainer != null) SelectTab(nextBrowser, nextContainer);
            }
        }

        private void Browser_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            var browser = sender as ChromiumWebBrowser;
            Panel tabContainer = null;
            foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
            {
                if (container.Controls.OfType<Button>().Any(btn => btn.Tag == browser && btn.Name == "tabButton"))
                {
                    tabContainer = container;
                    break;
                }
            }

            var tabButton = tabContainer?.Controls.OfType<Button>().FirstOrDefault(btn => btn.Name == "tabButton");
            if (tabButton != null && !string.IsNullOrEmpty(e.Title))
            {
                browserTitles[browser] = e.Title;
                string title = e.Title.Length > 22 ? e.Title.Substring(0, 19) + "..." : e.Title;

                if (tabButton.InvokeRequired)
                    tabButton.Invoke(new Action(() => { tabButton.Text = title; LoadFavicon(browser, tabButton); }));
                else { tabButton.Text = title; LoadFavicon(browser, tabButton); }
            }
        }

        private void LoadFavicon(ChromiumWebBrowser browser, Button tabButton)
        {
            try
            {
                if (!string.IsNullOrEmpty(browser.Address))
                {
                    var uri = new Uri(browser.Address);
                    var webClient = new System.Net.WebClient();
                    webClient.DownloadDataCompleted += (s, e) =>
                    {
                        try
                        {
                            if (e.Error == null && e.Result != null && e.Result.Length > 0)
                            {
                                using (var ms = new MemoryStream(e.Result))
                                {
                                    var favicon = new Bitmap(Image.FromStream(ms), new Size(16, 16));
                                    if (tabButton.InvokeRequired)
                                        tabButton.Invoke(new Action(() => tabButton.Image = favicon));
                                    else tabButton.Image = favicon;
                                }
                            }
                            else CreateDefaultIcon(tabButton);
                        }
                        catch { CreateDefaultIcon(tabButton); }
                    };
                    webClient.DownloadDataAsync(new Uri($"{uri.Scheme}://{uri.Host}/favicon.ico"));
                }
                else CreateDefaultIcon(tabButton);
            }
            catch { CreateDefaultIcon(tabButton); }
        }

        private void CreateDefaultIcon(Button tabButton)
        {
            var action = new Action(() =>
            {
                var bitmap = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.FillEllipse(Brushes.LightBlue, 1, 1, 14, 14);
                    g.DrawEllipse(Pens.DarkBlue, 1, 1, 14, 14);
                    g.DrawEllipse(Pens.Green, 3, 4, 10, 8);
                    g.DrawEllipse(Pens.Green, 5, 2, 6, 4);
                }
                tabButton.Image = bitmap;
            });

            if (tabButton.InvokeRequired) tabButton.Invoke(action);
            else action();
        }

        private void SelectTab(ChromiumWebBrowser browser, Panel tabContainer)
        {
            foreach (var b in browsers) b.Visible = false;
            foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
            {
                container.BackColor = Color.FromArgb(70, 70, 70);
                foreach (Button btn in container.Controls.OfType<Button>())
                    btn.BackColor = Color.FromArgb(70, 70, 70);
            }

            browser.Visible = true;
            browser.BringToFront();

            tabContainer.BackColor = Color.FromArgb(100, 100, 100);
            foreach (Button btn in tabContainer.Controls.OfType<Button>())
                btn.BackColor = Color.FromArgb(100, 100, 100);

            UpdateSearchBarUrl();
            UpdateNavigationButtons();
            InjectExtensions(browser);
        }

        public void AddNewTabFromUrl(string url) => AddNewTab(url);

        public void UpdateDownloadProgress(int percentage, string fileName)
        {
            if (InvokeRequired)
                Invoke(new Action(() => { downloadPanel.Visible = true; downloadProgressBar.Value = Math.Min(100, Math.Max(0, percentage)); downloadLabel.Text = $"{percentage}%"; }));
            else { downloadPanel.Visible = true; downloadProgressBar.Value = Math.Min(100, Math.Max(0, percentage)); downloadLabel.Text = $"{percentage}%"; }
        }

        public void HideDownloadProgress()
        {
            if (InvokeRequired) Invoke(new Action(() => downloadPanel.Visible = false));
            else downloadPanel.Visible = false;
        }

        public void AddToDownloadHistory(string fileName, string filePath)
        {
            downloadHistory.Add(new DownloadHistoryItem { FileName = fileName, FilePath = filePath, DownloadDate = DateTime.Now });
            SaveDownloadHistory();
            if (InvokeRequired) Invoke(new Action(() => RefreshDownloadHistoryList()));
            else RefreshDownloadHistoryList();
        }

        private void SaveBookmarks()
        {
            try { File.WriteAllText(bookmarksFilePath, JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { MessageBox.Show($"保存失敗: {ex.Message}"); }
        }

        private void LoadBookmarks()
        {
            try
            {
                if (File.Exists(bookmarksFilePath))
                    bookmarks = JsonSerializer.Deserialize<List<BookmarkItem>>(File.ReadAllText(bookmarksFilePath)) ?? new List<BookmarkItem>();
            }
            catch (Exception ex) { MessageBox.Show($"読み込み失敗: {ex.Message}"); bookmarks = new List<BookmarkItem>(); }
        }

        private void SaveExtensions()
        {
            try { File.WriteAllText(extensionsFilePath, JsonSerializer.Serialize(extensions, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { MessageBox.Show($"保存失敗: {ex.Message}"); }
        }

        private void LoadExtensions()
        {
            try
            {
                if (File.Exists(extensionsFilePath))
                    extensions = JsonSerializer.Deserialize<List<ExtensionItem>>(File.ReadAllText(extensionsFilePath)) ?? new List<ExtensionItem>();
            }
            catch (Exception ex) { MessageBox.Show($"読み込み失敗: {ex.Message}"); extensions = new List<ExtensionItem>(); }
        }

        private void SaveDownloadHistory()
        {
            try { File.WriteAllText(downloadHistoryFilePath, JsonSerializer.Serialize(downloadHistory, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { Debug.WriteLine($"保存失敗: {ex.Message}"); }
        }

        private void LoadDownloadHistory()
        {
            try
            {
                if (File.Exists(downloadHistoryFilePath))
                    downloadHistory = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(File.ReadAllText(downloadHistoryFilePath)) ?? new List<DownloadHistoryItem>();
            }
            catch (Exception ex) { Debug.WriteLine($"読み込み失敗: {ex.Message}"); downloadHistory = new List<DownloadHistoryItem>(); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (var browser in browsers) browser.Dispose();
            if (Cef.IsInitialized ?? false) Cef.Shutdown();
            base.OnFormClosing(e);
        }

        private class CustomRequestHandler : IRequestHandler
        {
            private readonly Form1 form;
            public CustomRequestHandler(Form1 form) { this.form = form; }

            public bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
            {
                if (request.Url.StartsWith("tlmine://openNewTab"))
                {
                    var uri = new Uri(request.Url);
                    var query = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=')).Where(p => p.Length == 2).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
                    form.Invoke(new Action(() => form.AddNewTabFromUrl(query.ContainsKey("url") ? query["url"] : "https://www.google.com")));
                    return true;
                }
                return false;
            }

            public bool GetAuthCredentials(IWebBrowser w, IBrowser b, string o, bool p, string h, int po, string r, string s, IAuthCallback c) => false;
            public bool OnCertificateError(IWebBrowser w, IBrowser b, CefErrorCode e, string r, ISslInfo s, IRequestCallback c) => false;
            public void OnPluginCrashed(IWebBrowser w, IBrowser b, string p) { }
            public void OnRenderProcessTerminated(IWebBrowser w, IBrowser b, CefTerminationStatus s, int e, string m) { if (form.InvokeRequired) form.Invoke(new Action(() => b.Reload())); }
            public void OnDocumentAvailableInMainFrame(IWebBrowser w, IBrowser b) { }
            public bool OnOpenUrlFromTab(IWebBrowser w, IBrowser b, IFrame f, string u, WindowOpenDisposition d, bool g)
            {
                if (d == WindowOpenDisposition.NewBackgroundTab || d == WindowOpenDisposition.NewForegroundTab)
                {
                    form.Invoke(new Action(() => form.AddNewTabFromUrl(u)));
                    return true;
                }
                return false;
            }
            public IResponseFilter GetResourceResponseFilter(IWebBrowser w, IBrowser b, IFrame f, IRequest r, IResponse re) => null;
            public bool OnResourceResponse(IWebBrowser w, IBrowser b, IFrame f, IRequest r, IResponse re) => false;
            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser w, IBrowser b, IFrame f, IRequest r, bool n, bool d, string i, ref bool di) => null;
            public bool OnSelectClientCertificate(IWebBrowser w, IBrowser b, bool p, string h, int po, X509Certificate2Collection c, ISelectClientCertificateCallback ca) => false;
            public void Dispose() { }
            public bool OnQuotaRequest(IWebBrowser w, IBrowser b, string o, long n, IRequestCallback c) { c.Continue(true); return true; }
            public void OnRenderViewReady(IWebBrowser w, IBrowser b) { }
        }

        private class CustomDownloadHandler : IDownloadHandler
        {
            private readonly Form1 form;
            public CustomDownloadHandler(Form1 form) { this.form = form; }

            public bool CanDownload(IWebBrowser w, IBrowser b, string u, string m) => true;

            public bool OnBeforeDownload(IWebBrowser w, IBrowser b, DownloadItem d, IBeforeDownloadCallback c)
            {
                var size = d.TotalBytes > 0 ? $" ({d.TotalBytes / 1024 / 1024} MB)" : "";
                if (MessageBox.Show($"ファイル '{d.SuggestedFileName}'{size} をダウンロードしますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    c.Continue(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", d.SuggestedFileName), false);
                    return true;
                }
                return false;
            }

            public void OnDownloadUpdated(IWebBrowser w, IBrowser b, DownloadItem d, IDownloadItemCallback c)
            {
                if (d.IsInProgress)
                    form.UpdateDownloadProgress(d.TotalBytes > 0 ? (int)((d.ReceivedBytes * 100) / d.TotalBytes) : 0, d.SuggestedFileName);
                else if (d.IsComplete)
                {
                    form.HideDownloadProgress();
                    form.AddToDownloadHistory(d.SuggestedFileName, d.FullPath);
                    if (MessageBox.Show($"'{d.SuggestedFileName}' のダウンロードが完了しました。\nファイルを開きますか？", "完了", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        try { Process.Start(new ProcessStartInfo { FileName = d.FullPath, UseShellExecute = true }); }
                        catch (Exception ex) { MessageBox.Show($"開けませんでした: {ex.Message}"); }
                    }
                }
                else if (d.IsCancelled) form.HideDownloadProgress();
            }
        }
    }

    public class BookmarkItem { public string Title { get; set; } = ""; public string Url { get; set; } = ""; }
    public class ExtensionItem { public string Name { get; set; } = ""; public bool Enabled { get; set; } = false; public string ScriptPath { get; set; } = ""; public string ScriptContent { get; set; } = ""; }
    public class DownloadHistoryItem { public string FileName { get; set; } = ""; public string FilePath { get; set; } = ""; public DateTime DownloadDate { get; set; } }

    public partial class BookmarkDialog : Form
    {
        private TextBox titleTextBox, urlTextBox;
        public string BookmarkTitle => titleTextBox.Text;
        public string BookmarkUrl => urlTextBox.Text;

        public BookmarkDialog(string url, string title)
        {
            Text = "ブックマーク追加";
            Size = new Size(400, 150);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            BackColor = Color.FromArgb(45, 45, 48);

            Controls.Add(new Label { Text = "タイトル:", Location = new Point(12, 15), Size = new Size(60, 23), ForeColor = Color.White });
            titleTextBox = new TextBox { Location = new Point(78, 12), Size = new Size(300, 23), BackColor = Color.FromArgb(60, 60, 64), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Text = title ?? "" };
            Controls.Add(titleTextBox);

            Controls.Add(new Label { Text = "URL:", Location = new Point(12, 44), Size = new Size(60, 23), ForeColor = Color.White });
            urlTextBox = new TextBox { Location = new Point(78, 41), Size = new Size(300, 23), BackColor = Color.FromArgb(60, 60, 64), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Text = url ?? "" };
            Controls.Add(urlTextBox);

            var okBtn = new Button { Text = "OK", Location = new Point(222, 80), Size = new Size(75, 23), DialogResult = DialogResult.OK, BackColor = Color.FromArgb(70, 130, 180), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            okBtn.FlatAppearance.BorderSize = 0;
            Controls.Add(okBtn);

            var cancelBtn = new Button { Text = "キャンセル", Location = new Point(303, 80), Size = new Size(75, 23), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(80, 80, 84), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cancelBtn.FlatAppearance.BorderSize = 0;
            Controls.Add(cancelBtn);

            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }
    }
}
