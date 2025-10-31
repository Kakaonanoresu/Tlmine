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

namespace Tlmine
{
    public partial class Form1 : Form
    {
        private FlowLayoutPanel sidePanel;
        private FlowLayoutPanel tabButtonsPanel;
        private Panel bookmarksPanel;
        private Panel extensionsPanel;
        private Panel bookmarksContent;
        private Panel extensionsContent;
        private TextBox searchBar;
        private Panel searchBarPanel;
        private Button addTabButton;
        private Button backButton;
        private Button forwardButton;
        private Button reloadButton;
        private ProgressBar downloadProgressBar;
        private Label downloadLabel;
        private Panel downloadPanel;

        private List<ChromiumWebBrowser> browsers = new List<ChromiumWebBrowser>();
        private List<Button> tabButtons = new List<Button>();
        private List<BookmarkItem> bookmarks = new List<BookmarkItem>();
        private List<ExtensionItem> extensions = new List<ExtensionItem>();
        private Dictionary<ChromiumWebBrowser, string> browserTitles = new Dictionary<ChromiumWebBrowser, string>();

        private const string searchBarPlaceholder = "検索またはURLを入力";
        private const string bookmarksFilePath = "bookmarks.json";
        private const string extensionsFilePath = "extensions.json";

        public Form1()
        {
            InitializeComponent();
            LoadBookmarks();
            LoadExtensions();
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

            // タブボタンパネル
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
                string title = "新しいブックマーク";

                if (browserTitles.ContainsKey(currentBrowser) && !string.IsNullOrEmpty(browserTitles[currentBrowser]))
                {
                    title = browserTitles[currentBrowser];
                }
                else
                {
                    var tabContainer = FindTabContainer(currentBrowser);
                    var tabButton = tabContainer?.Controls.OfType<Button>().FirstOrDefault(btn => btn.Name == "tabButton");
                    if (tabButton != null && !string.IsNullOrEmpty(tabButton.Text) && tabButton.Text != "新しいタブ")
                    {
                        title = tabButton.Text.Replace("...", "");
                    }
                }

                var dialog = new BookmarkDialog(currentBrowser.Address, title);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var bookmark = new BookmarkItem
                    {
                        Title = dialog.BookmarkTitle,
                        Url = dialog.BookmarkUrl
                    };
                    bookmarks.Add(bookmark);
                    SaveBookmarks();
                    RefreshBookmarksList();
                }
            }
        }

        private void RefreshBookmarksList()
        {
            var controlsToRemove = bookmarksContent.Controls.OfType<Control>()
                .Where(c => c.Tag?.ToString() == "bookmark").ToList();
            foreach (var control in controlsToRemove)
            {
                bookmarksContent.Controls.Remove(control);
                control.Dispose();
            }

            int yPos = 35;
            foreach (var bookmark in bookmarks)
            {
                var panel = new Panel()
                {
                    Width = bookmarksContent.Width - 15,
                    Height = 25,
                    Top = yPos,
                    Left = 5,
                    Tag = "bookmark"
                };

                var linkLabel = new LinkLabel()
                {
                    Text = bookmark.Title.Length > 20 ? bookmark.Title.Substring(0, 17) + "..." : bookmark.Title,
                    LinkColor = Color.LightBlue,
                    Width = panel.Width - 25,
                    Height = 25,
                    Left = 0,
                    Top = 0,
                    Tag = bookmark.Url
                };
                linkLabel.LinkClicked += (s, e) =>
                {
                    AddNewTab(linkLabel.Tag.ToString());
                };

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
                deleteBtn.Click += (s, e) =>
                {
                    bookmarks.Remove((BookmarkItem)deleteBtn.Tag);
                    SaveBookmarks();
                    RefreshBookmarksList();
                };

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

            var webStoreBtn = new Button()
            {
                Text = "Chrome Web Store",
                Height = 25,
                Width = extensionsContent.Width - 10,
                BackColor = Color.FromArgb(90, 90, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Top = 5,
                Left = 5
            };
            webStoreBtn.FlatAppearance.BorderSize = 0;
            webStoreBtn.Click += (s, e) => AddNewTab("https://chromewebstore.google.com/");
            extensionsContent.Controls.Add(webStoreBtn);

            RefreshExtensionsList();
        }

        private void RefreshExtensionsList()
        {
            var controlsToRemove = extensionsContent.Controls.OfType<Control>()
                .Where(c => c.Tag?.ToString() == "extension").ToList();
            foreach (var control in controlsToRemove)
            {
                extensionsContent.Controls.Remove(control);
                control.Dispose();
            }

            int yPos = 35;
            foreach (var extension in extensions)
            {
                var panel = new Panel()
                {
                    Width = extensionsContent.Width - 15,
                    Height = 25,
                    Top = yPos,
                    Left = 5,
                    Tag = "extension"
                };

                var label = new Label()
                {
                    Text = extension.Name.Length > 18 ? extension.Name.Substring(0, 15) + "..." : extension.Name,
                    ForeColor = Color.LightGray,
                    Width = panel.Width - 45,
                    Height = 25,
                    Left = 0,
                    Top = 0
                };

                var toggleBtn = new Button()
                {
                    Text = extension.Enabled ? "ON" : "OFF",
                    Width = 30,
                    Height = 20,
                    Left = panel.Width - 50,
                    Top = 2,
                    BackColor = extension.Enabled ? Color.Green : Color.Gray,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 7),
                    Tag = extension
                };
                toggleBtn.FlatAppearance.BorderSize = 0;
                toggleBtn.Click += (s, e) =>
                {
                    extension.Enabled = !extension.Enabled;
                    toggleBtn.Text = extension.Enabled ? "ON" : "OFF";
                    toggleBtn.BackColor = extension.Enabled ? Color.Green : Color.Gray;
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
                    Tag = extension
                };
                deleteBtn.FlatAppearance.BorderSize = 0;
                deleteBtn.Click += (s, e) =>
                {
                    extensions.Remove((ExtensionItem)deleteBtn.Tag);
                    SaveExtensions();
                    RefreshExtensionsList();
                };

                panel.Controls.Add(label);
                panel.Controls.Add(toggleBtn);
                panel.Controls.Add(deleteBtn);
                extensionsContent.Controls.Add(panel);

                yPos += 30;
            }
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

            downloadPanel = new Panel()
            {
                Width = 200,
                Height = 28,
                BackColor = Color.FromArgb(240, 240, 240),
                Visible = false
            };

            downloadProgressBar = new ProgressBar()
            {
                Width = 150,
                Height = 20,
                Left = 5,
                Top = 4,
                Style = ProgressBarStyle.Continuous
            };

            downloadLabel = new Label()
            {
                Width = 40,
                Height = 20,
                Left = 160,
                Top = 6,
                Font = new Font("Segoe UI", 8),
                Text = "0%"
            };

            downloadPanel.Controls.Add(downloadProgressBar);
            downloadPanel.Controls.Add(downloadLabel);

            var navigationPanel = new Panel()
            {
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.FromArgb(240, 240, 240),
            };
            searchBarPanel.Controls.Add(navigationPanel);
            searchBarPanel.Controls.Add(downloadPanel);

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

            downloadPanel.Left = navigationPanel.Left - downloadPanel.Width - 10;
            downloadPanel.Top = 4;

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
                searchBar.SelectAll();
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

        private async void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string text = searchBar.Text.Trim();
                if (string.IsNullOrEmpty(text) || text == searchBarPlaceholder) return;

                string url = "";
                string suggestedUrl = "";

                if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = text;
                }
                else if (IsValidDomain(text))
                {
                    url = "https://" + text;
                }
                else
                {
                    url = $"https://www.google.com/search?q={Uri.EscapeDataString(text)}";
                    suggestedUrl = GetUrlSuggestion(text);
                }

                var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
                if (currentBrowser != null)
                {
                    currentBrowser.Load(url);

                    if (!string.IsNullOrEmpty(suggestedUrl))
                    {
                        await Task.Delay(1500);
                        await ShowUrlSuggestion(suggestedUrl);
                    }
                    else
                    {
                        await ClearUrlSuggestion();
                    }
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private string GetUrlSuggestion(string searchQuery)
        {
            var query = searchQuery.ToLower();

            var suggestions = new Dictionary<string, string>
            {
                {"youtube", "https://www.youtube.com"},
                {"twitter", "https://twitter.com"},
                {"facebook", "https://www.facebook.com"},
                {"instagram", "https://www.instagram.com"},
                {"github", "https://github.com"},
                {"stackoverflow", "https://stackoverflow.com"},
                {"amazon", "https://www.amazon.co.jp"},
                {"楽天", "https://www.rakuten.co.jp"},
                {"yahoo", "https://www.yahoo.co.jp"},
                {"ニコニコ", "https://www.nicovideo.jp"},
                {"ニコニコ動画", "https://www.nicovideo.jp"},
                {"wikipedia", "https://ja.wikipedia.org"},
                {"ウィキペディア", "https://ja.wikipedia.org"},
                {"gmail", "https://mail.google.com"},
                {"outlook", "https://outlook.com"}
            };

            foreach (var suggestion in suggestions)
            {
                if (query.Contains(suggestion.Key))
                {
                    return suggestion.Value;
                }
            }

            return "";
        }

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

        private bool IsValidDomain(string text)
        {
            if (text.Contains(" ")) return false;

            if (text.Contains("."))
            {
                var parts = text.Split('.');
                if (parts.Length >= 2)
                {
                    var tld = parts[parts.Length - 1];
                    if (tld.Length >= 2 && tld.All(c => char.IsLetter(c)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

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

        private async Task ShowUrlSuggestion(string targetUrl)
        {
            string script = $@"
                (function() {{
                    var existing = document.getElementById('tlmine-url-suggestion');
                    if (existing) existing.remove();
                    
                    var div = document.createElement('div');
                    div.id = 'tlmine-url-suggestion';
                    div.style.cssText = `
                        padding: 12px 20px;
                        background: linear-gradient(135deg, #4CAF50, #45a049);
                        border: none;
                        font-family: 'Segoe UI', Arial, sans-serif;
                        font-size: 14px;
                        position: fixed;
                        top: 0;
                        left: 0;
                        width: 100%;
                        z-index: 99999;
                        text-align: center;
                        color: white;
                        box-shadow: 0 2px 10px rgba(0,0,0,0.3);
                        animation: slideDown 0.3s ease-out;
                    `;
                    
                    var style = document.createElement('style');
                    if (!document.getElementById('tlmine-suggestion-styles')) {{
                        style.id = 'tlmine-suggestion-styles';
                        style.textContent = `
                            @keyframes slideDown {{
                                from {{ transform: translateY(-100%); opacity: 0; }}
                                to {{ transform: translateY(0); opacity: 1; }}
                            }}
                            #tlmine-url-suggestion a {{
                                color: #fff !important;
                                text-decoration: underline !important;
                                font-weight: bold !important;
                                margin-left: 10px !important;
                                cursor: pointer !important;
                            }}
                            #tlmine-url-suggestion a:hover {{
                                color: #e8f5e8 !important;
                                background-color: rgba(255,255,255,0.1) !important;
                                padding: 2px 4px !important;
                                border-radius: 3px !important;
                            }}
                            #tlmine-close-suggestion {{
                                background: rgba(255,255,255,0.2) !important;
                                border: none !important;
                                color: white !important;
                                padding: 4px 8px !important;
                                margin-left: 15px !important;
                                border-radius: 3px !important;
                                cursor: pointer !important;
                                font-size: 12px !important;
                            }}
                            #tlmine-close-suggestion:hover {{
                                background: rgba(255,255,255,0.3) !important;
                            }}
                        `;
                        document.head.appendChild(style);
                    }}
                    
                    div.innerHTML = `
                        🌐 このURLをお探しですか？ 
                        <a href=""#"" id=""tlmine-direct-link"">{targetUrl.Replace("\"", "\\\"")}</a>
                        <button id=""tlmine-close-suggestion"">×</button>
                    `;
                    
                    document.body.prepend(div);
                    
                    document.getElementById('tlmine-direct-link').addEventListener('click', function(e) {{
                        e.preventDefault();
                        window.location.href = '{targetUrl.Replace("'", "\\'")}';
                    }});
                    
                    document.getElementById('tlmine-close-suggestion').addEventListener('click', function() {{
                        document.getElementById('tlmine-url-suggestion').remove();
                    }});
                    
                    setTimeout(function() {{
                        var suggestion = document.getElementById('tlmine-url-suggestion');
                        if (suggestion) {{
                            suggestion.style.animation = 'slideDown 0.3s ease-out reverse';
                            setTimeout(() => suggestion.remove(), 300);
                        }}
                    }}, 10000);
                }})();
            ";

            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null)
            {
                try
                {
                    await currentBrowser.EvaluateScriptAsync(script);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"JavaScript実行エラー: {ex.Message}");
                }
            }
        }

        private async Task ClearUrlSuggestion()
        {
            var currentBrowser = browsers.FirstOrDefault(b => b.Visible);
            if (currentBrowser != null)
            {
                string script = @"
                    (function() {
                        var el = document.getElementById('tlmine-url-suggestion');
                        if(el) el.remove();
                    })();
                ";
                try
                {
                    await currentBrowser.EvaluateScriptAsync(script);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"JavaScript実行エラー: {ex.Message}");
                }
            }
        }

        private void InitializeChromium()
        {
            if (!(Cef.IsInitialized ?? false))
            {
                var settings = new CefSettings();

                // 複数インスタンス起動時の問題を解決: 各プロセスごとに独立したキャッシュディレクトリを使用
                var processId = Process.GetCurrentProcess().Id;
                var cachePath = Path.Combine(Path.GetTempPath(), "Tlmine", $"Cache_{processId}");
                settings.CachePath = cachePath;

                // 動画再生を改善するための設定
                settings.CefCommandLineArgs.Add("enable-media-stream");
                settings.CefCommandLineArgs.Add("enable-usermedia-screen-capturing");
                settings.CefCommandLineArgs.Add("enable-speech-synthesis");
                settings.CefCommandLineArgs.Add("enable-web-bluetooth");
                settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");
                settings.CefCommandLineArgs.Add("disable-features", "VizDisplayCompositor");
                settings.CefCommandLineArgs.Add("enable-gpu-rasterization");
                settings.CefCommandLineArgs.Add("enable-oop-rasterization");
                settings.CefCommandLineArgs.Add("enable-zero-copy");

                // H.264コーデックサポート
                settings.CefCommandLineArgs.Add("enable-proprietary-codecs");

                // 複数プロセス分離
                settings.MultiThreadedMessageLoop = true;

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

            browser.FrameLoadEnd += Browser_FrameLoadEnd;
            browser.TitleChanged += Browser_TitleChanged;
            browser.AddressChanged += Browser_AddressChanged;
            browser.LoadingStateChanged += Browser_LoadingStateChanged;

            browsers.Add(browser);
            this.Controls.Add(browser);

            var tabContainer = new Panel()
            {
                Width = tabButtonsPanel.Width - 20,
                Height = 35,
                BackColor = Color.FromArgb(70, 70, 70),
                Margin = new Padding(5, 2, 5, 2)
            };

            var closeBtn = new Button()
            {
                Text = "×",
                Width = 25,
                Height = 35,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0),
                Tag = browser,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Right,
                Name = "closeButton"
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += CloseTabButton_Click;
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
                Margin = new Padding(0),
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
            tabBtn.Click += TabButton_Click;

            tabContainer.Controls.Add(closeBtn);
            tabContainer.Controls.Add(tabBtn);

            var addButtonIndex = tabButtonsPanel.Controls.IndexOf(addTabButton);
            tabButtonsPanel.Controls.Add(tabContainer);
            tabButtonsPanel.Controls.SetChildIndex(tabContainer, addButtonIndex);

            tabButtons.Add(tabBtn);

            SelectTab(browser, tabContainer);

            CreateDefaultIcon(tabBtn);
        }

        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            // ページの読み込み状態が変わるたびにナビゲーションボタンを更新
            UpdateNavigationButtons();
        }

        private void Browser_AddressChanged(object sender, AddressChangedEventArgs e)
        {
            var browser = sender as ChromiumWebBrowser;
            if (browser != null && browser.Visible)
            {
                this.Invoke(new Action(() =>
                {
                    if (!searchBar.Focused)
                    {
                        searchBar.Text = e.Address;
                        searchBar.ForeColor = Color.Black;
                    }
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
                var tabContainer = clickedButton.Parent as Panel;
                SelectTab(associatedBrowser, tabContainer);
            }
        }

        private void CloseTabButton_Click(object sender, EventArgs e)
        {
            var closeButton = sender as Button;
            var associatedBrowser = closeButton?.Tag as ChromiumWebBrowser;

            if (associatedBrowser != null)
            {
                CloseTab(associatedBrowser);
            }
        }

        private void CloseTab(ChromiumWebBrowser browserToClose)
        {
            if (browsers.Count <= 1)
            {
                MessageBox.Show("最後のタブは閉じることができません。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool wasSelected = browserToClose.Visible;

            Panel tabContainerToRemove = null;
            foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
            {
                var tabButton = container.Controls.OfType<Button>().FirstOrDefault(btn => btn.Tag == browserToClose);
                if (tabButton != null)
                {
                    tabContainerToRemove = container;
                    break;
                }
            }

            if (tabContainerToRemove != null)
            {
                tabButtonsPanel.Controls.Remove(tabContainerToRemove);
                tabContainerToRemove.Dispose();
            }

            var tabButtonToRemove = tabButtons.FirstOrDefault(btn => btn.Tag == browserToClose && btn.Name == "tabButton");
            if (tabButtonToRemove != null)
            {
                tabButtons.Remove(tabButtonToRemove);
            }

            if (browserTitles.ContainsKey(browserToClose))
            {
                browserTitles.Remove(browserToClose);
            }

            browsers.Remove(browserToClose);
            this.Controls.Remove(browserToClose);
            browserToClose.Dispose();

            if (wasSelected && browsers.Count > 0)
            {
                var nextBrowser = browsers.Last();
                var nextTabContainer = FindTabContainer(nextBrowser);
                if (nextTabContainer != null)
                {
                    SelectTab(nextBrowser, nextTabContainer);
                }
            }
        }

        private Panel FindTabContainer(ChromiumWebBrowser browser)
        {
            foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
            {
                var tabButton = container.Controls.OfType<Button>().FirstOrDefault(btn => btn.Tag == browser && btn.Name == "tabButton");
                if (tabButton != null)
                {
                    return container;
                }
            }
            return null;
        }

        private void Browser_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            var browser = sender as ChromiumWebBrowser;
            var tabContainer = FindTabContainer(browser);
            var tabButton = tabContainer?.Controls.OfType<Button>().FirstOrDefault(btn => btn.Name == "tabButton");

            if (tabButton != null && !string.IsNullOrEmpty(e.Title))
            {
                browserTitles[browser] = e.Title;

                string title = e.Title.Length > 22 ? e.Title.Substring(0, 19) + "..." : e.Title;

                if (tabButton.InvokeRequired)
                {
                    tabButton.Invoke(new Action(() =>
                    {
                        tabButton.Text = title;
                        LoadFavicon(browser, tabButton);
                    }));
                }
                else
                {
                    tabButton.Text = title;
                    LoadFavicon(browser, tabButton);
                }
            }
        }

        private void LoadFavicon(ChromiumWebBrowser browser, Button tabButton)
        {
            try
            {
                if (!string.IsNullOrEmpty(browser.Address))
                {
                    var uri = new Uri(browser.Address);
                    string faviconUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";

                    var webClient = new System.Net.WebClient();
                    webClient.DownloadDataCompleted += (s, e) =>
                    {
                        try
                        {
                            if (e.Error == null && e.Result != null && e.Result.Length > 0)
                            {
                                using (var ms = new System.IO.MemoryStream(e.Result))
                                {
                                    var favicon = Image.FromStream(ms);
                                    var resizedFavicon = new Bitmap(favicon, new Size(16, 16));

                                    if (tabButton.InvokeRequired)
                                    {
                                        tabButton.Invoke(new Action(() =>
                                        {
                                            tabButton.Image = resizedFavicon;
                                        }));
                                    }
                                    else
                                    {
                                        tabButton.Image = resizedFavicon;
                                    }
                                }
                            }
                            else
                            {
                                SetDefaultIcon(tabButton);
                            }
                        }
                        catch
                        {
                            SetDefaultIcon(tabButton);
                        }
                    };

                    webClient.DownloadDataAsync(new Uri(faviconUrl));
                }
                else
                {
                    SetDefaultIcon(tabButton);
                }
            }
            catch
            {
                SetDefaultIcon(tabButton);
            }
        }

        private void SetDefaultIcon(Button tabButton)
        {
            if (tabButton.InvokeRequired)
            {
                tabButton.Invoke(new Action(() => CreateDefaultIcon(tabButton)));
            }
            else
            {
                CreateDefaultIcon(tabButton);
            }
        }

        private void CreateDefaultIcon(Button tabButton)
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
        }

        private void SelectTab(ChromiumWebBrowser browser, Panel tabContainer)
        {
            foreach (var b in browsers)
            {
                b.Visible = false;
            }

            foreach (Panel container in tabButtonsPanel.Controls.OfType<Panel>())
            {
                container.BackColor = Color.FromArgb(70, 70, 70);
                foreach (Button btn in container.Controls.OfType<Button>())
                {
                    if (btn.Name == "tabButton")
                    {
                        btn.BackColor = Color.FromArgb(70, 70, 70);
                    }
                    else if (btn.Name == "closeButton")
                    {
                        btn.BackColor = Color.FromArgb(70, 70, 70);
                    }
                }
            }

            browser.Visible = true;
            browser.BringToFront();

            tabContainer.BackColor = Color.FromArgb(100, 100, 100);
            foreach (Button btn in tabContainer.Controls.OfType<Button>())
            {
                if (btn.Name == "tabButton")
                {
                    btn.BackColor = Color.FromArgb(100, 100, 100);
                }
                else if (btn.Name == "closeButton")
                {
                    btn.BackColor = Color.FromArgb(100, 100, 100);
                }
            }

            UpdateSearchBarUrl();
            UpdateNavigationButtons();
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain)
            {
                UpdateNavigationButtons();
            }
        }

        public void AddNewTabFromUrl(string url)
        {
            AddNewTab(url);
        }

        public void UpdateDownloadProgress(int percentage, string fileName)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    downloadPanel.Visible = true;
                    downloadProgressBar.Value = Math.Min(100, Math.Max(0, percentage));
                    downloadLabel.Text = $"{percentage}%";
                }));
            }
            else
            {
                downloadPanel.Visible = true;
                downloadProgressBar.Value = Math.Min(100, Math.Max(0, percentage));
                downloadLabel.Text = $"{percentage}%";
            }
        }

        public void HideDownloadProgress()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    downloadPanel.Visible = false;
                }));
            }
            else
            {
                downloadPanel.Visible = false;
            }
        }

        private void SaveBookmarks()
        {
            try
            {
                var json = JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(bookmarksFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ブックマークの保存に失敗しました: {ex.Message}");
            }
        }

        private void LoadBookmarks()
        {
            try
            {
                if (File.Exists(bookmarksFilePath))
                {
                    var json = File.ReadAllText(bookmarksFilePath);
                    bookmarks = JsonSerializer.Deserialize<List<BookmarkItem>>(json) ?? new List<BookmarkItem>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ブックマークの読み込みに失敗しました: {ex.Message}");
                bookmarks = new List<BookmarkItem>();
            }
        }

        private void SaveExtensions()
        {
            try
            {
                var json = JsonSerializer.Serialize(extensions, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(extensionsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拡張機能の保存に失敗しました: {ex.Message}");
            }
        }

        private void LoadExtensions()
        {
            try
            {
                if (File.Exists(extensionsFilePath))
                {
                    var json = File.ReadAllText(extensionsFilePath);
                    extensions = JsonSerializer.Deserialize<List<ExtensionItem>>(json) ?? new List<ExtensionItem>();
                }
                else
                {
                    extensions = new List<ExtensionItem>
                    {
                        new ExtensionItem { Name = "AdBlocker", Enabled = true },
                        new ExtensionItem { Name = "Password Manager", Enabled = false }
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拡張機能の読み込みに失敗しました: {ex.Message}");
                extensions = new List<ExtensionItem>();
            }
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

                    return true;
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

        private class CustomDownloadHandler : IDownloadHandler
        {
            private readonly Form1 form;

            public CustomDownloadHandler(Form1 form)
            {
                this.form = form;
            }

            public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod)
            {
                return true;
            }

            public bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
            {
                var fileName = downloadItem.SuggestedFileName;
                var fileSize = downloadItem.TotalBytes > 0 ? $" ({downloadItem.TotalBytes / 1024 / 1024} MB)" : "";

                var result = MessageBox.Show(
                    $"ファイル '{fileName}'{fileSize} をダウンロードしますか？",
                    "ダウンロード確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    var fullPath = Path.Combine(downloadsPath, fileName);
                    callback.Continue(fullPath, false);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
            {
                if (downloadItem.IsInProgress)
                {
                    var percentage = downloadItem.TotalBytes > 0 ?
                        (int)((downloadItem.ReceivedBytes * 100) / downloadItem.TotalBytes) : 0;
                    form.UpdateDownloadProgress(percentage, downloadItem.SuggestedFileName);
                }
                else if (downloadItem.IsComplete)
                {
                    form.HideDownloadProgress();

                    var result = MessageBox.Show(
                        $"'{downloadItem.SuggestedFileName}' のダウンロードが完了しました。\nファイルを開きますか？",
                        "ダウンロード完了",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = downloadItem.FullPath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"ファイルを開けませんでした: {ex.Message}");
                        }
                    }
                }
                else if (downloadItem.IsCancelled)
                {
                    form.HideDownloadProgress();
                }
            }
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

    public class BookmarkItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class ExtensionItem
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = false;
    }

    public partial class BookmarkDialog : Form
    {
        private TextBox titleTextBox;
        private TextBox urlTextBox;
        private Button okButton;
        private Button cancelButton;

        public string BookmarkTitle => titleTextBox.Text;
        public string BookmarkUrl => urlTextBox.Text;

        public BookmarkDialog(string url, string title)
        {
            InitializeDialog();
            urlTextBox.Text = url ?? "";
            titleTextBox.Text = title ?? "新しいブックマーク";
        }

        private void InitializeDialog()
        {
            this.Text = "ブックマーク追加";
            this.Size = new Size(400, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label()
            {
                Text = "タイトル:",
                Location = new Point(12, 15),
                Size = new Size(60, 23)
            };
            this.Controls.Add(titleLabel);

            titleTextBox = new TextBox()
            {
                Location = new Point(78, 12),
                Size = new Size(300, 23)
            };
            this.Controls.Add(titleTextBox);

            var urlLabel = new Label()
            {
                Text = "URL:",
                Location = new Point(12, 44),
                Size = new Size(60, 23)
            };
            this.Controls.Add(urlLabel);

            urlTextBox = new TextBox()
            {
                Location = new Point(78, 41),
                Size = new Size(300, 23)
            };
            this.Controls.Add(urlTextBox);

            okButton = new Button()
            {
                Text = "OK",
                Location = new Point(222, 80),
                Size = new Size(75, 23),
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(okButton);

            cancelButton = new Button()
            {
                Text = "キャンセル",
                Location = new Point(303, 80),
                Size = new Size(75, 23),
                DialogResult = DialogResult.Cancel
            };
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
}
