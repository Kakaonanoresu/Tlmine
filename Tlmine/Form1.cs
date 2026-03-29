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
using System.IO.Compression;
using System.Drawing.Drawing2D;

namespace Tlmine
{
    // ── カラーパレット ──────────────────────────────────────────
    static class Theme
    {
        public static readonly Color Bg0 = Color.FromArgb(10, 10, 14);   // 最暗背景
        public static readonly Color Bg1 = Color.FromArgb(18, 18, 24);   // サイドパネル
        public static readonly Color Bg2 = Color.FromArgb(24, 24, 32);   // セクションヘッダ
        public static readonly Color Bg3 = Color.FromArgb(30, 30, 42);   // カード
        public static readonly Color Bg4 = Color.FromArgb(38, 38, 54);   // ホバー / 入力
        public static readonly Color Accent = Color.FromArgb(94, 129, 255);   // 青紫アクセント
        public static readonly Color AccentHover = Color.FromArgb(120, 150, 255);
        public static readonly Color AccentDim = Color.FromArgb(50, 60, 130);
        public static readonly Color Danger = Color.FromArgb(220, 80, 80);
        public static readonly Color Success = Color.FromArgb(60, 200, 120);
        public static readonly Color TextPri = Color.FromArgb(230, 230, 245);
        public static readonly Color TextSec = Color.FromArgb(140, 140, 165);
        public static readonly Color TextDim = Color.FromArgb(80, 80, 105);
        public static readonly Color Border = Color.FromArgb(48, 48, 68);
    }

    public partial class Form1 : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // ── UI コントロール ───────────────────────────────────────
        private FlowLayoutPanel sidePanel;
        private FlowLayoutPanel tabButtonsPanel;
        private Panel bookmarksPanel, bookmarksContent;
        private Panel extensionsPanel, extensionsContent;
        private Panel downloadHistoryPanel, downloadHistoryContent;
        private TextBox searchBar;
        private Panel searchBarPanel;
        private Button addTabButton;
        private Button backButton, forwardButton, reloadButton;
        private Button addBookmarkButton, translateButton, settingsButton;
        private ProgressBar downloadProgressBar;
        private Label downloadLabel;
        private Panel downloadPanel;

        // ── データ ───────────────────────────────────────────────
        private List<ChromiumWebBrowser> browsers = new List<ChromiumWebBrowser>();
        private List<Button> tabButtons = new List<Button>();
        private List<BookmarkItem> bookmarks = new List<BookmarkItem>();
        private List<ExtensionItem> extensions = new List<ExtensionItem>();
        private List<DownloadHistoryItem> downloadHistory = new List<DownloadHistoryItem>();
        private Dictionary<ChromiumWebBrowser, string> browserTitles = new Dictionary<ChromiumWebBrowser, string>();
        private BrowserSettings browserSettings = new BrowserSettings();

        // ── 定数 ─────────────────────────────────────────────────
        private const string searchBarPlaceholder = "検索またはURLを入力";
        private const string bookmarksFilePath = "bookmarks.json";
        private const string extensionsFilePath = "extensions.json";
        private const string downloadHistoryFilePath = "download_history.json";
        private const string settingsFilePath = "settings.json";

        private System.Windows.Forms.Timer updateTimer;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // コンストラクタ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public Form1()
        {
            InitializeComponent();
            LoadSettings();
            LoadBookmarks();
            LoadExtensions();
            LoadDownloadHistory();
            InitializeUI();
            InitializeChromium();
            AddNewTab(browserSettings.HomePage);
            InitializeUpdateTimer();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // タイマー / ダークタイトルバー
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeUpdateTimer()
        {
            updateTimer = new System.Windows.Forms.Timer { Interval = 500 };
            updateTimer.Tick += (s, e) =>
            {
                if (browsers.Any(b => b.Visible) && !searchBar.Focused)
                    UpdateNavigationButtons();
            };
            updateTimer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int v = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // UI 初期化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeUI()
        {
            Text = "Tlmine";
            WindowState = FormWindowState.Maximized;
            BackColor = Theme.Bg0;
            Font = new Font("Yu Gothic UI", 9);

            // ── サイドパネル ──
            sidePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 244,
                BackColor = Theme.Bg1,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0)
            };
            // 上部ロゴ帯
            var logoStrip = new Panel
            {
                Width = 244,
                Height = 48,
                BackColor = Theme.Bg0,
                Margin = new Padding(0)
            };
            var logoLabel = new Label
            {
                Text = "Tlmine",
                ForeColor = Theme.Accent,
                Font = new Font("Yu Gothic UI", 14, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            logoStrip.Controls.Add(logoLabel);
            sidePanel.Controls.Add(logoStrip);

            Controls.Add(sidePanel);

            InitializeBookmarksPanel();
            InitializeExtensionsPanel();
            InitializeDownloadHistoryPanel();

            // ── タブパネル ──
            tabButtonsPanel = new FlowLayoutPanel
            {
                Height = 220,
                Width = 236,
                BackColor = Theme.Bg1,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Margin = new Padding(4, 4, 4, 0)
            };
            sidePanel.Controls.Add(tabButtonsPanel);

            var tabHeader = MakeSectionHeader("タブ", tabButtonsPanel);
            tabButtonsPanel.Controls.Add(tabHeader);

            addTabButton = MakeSideButton("＋  新しいタブ", null);
            addTabButton.Click += (s, e) => AddNewTab(browserSettings.HomePage);
            tabButtonsPanel.Controls.Add(addTabButton);

            InitializeSearchBar();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ヘルパー: セクションヘッダ / サイドボタン
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private Label MakeSectionHeader(string text, Control parent)
        {
            return new Label
            {
                Text = text,
                ForeColor = Theme.Accent,
                Font = new Font("Yu Gothic UI", 9, FontStyle.Bold),
                Height = 26,
                Width = 236,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Theme.Bg2,
                Margin = new Padding(0, 0, 0, 2)
            };
        }

        private Button MakeSideButton(string text, object tag)
        {
            var btn = new Button
            {
                Text = text,
                Width = 228,
                Height = 32,
                BackColor = Theme.Bg3,
                ForeColor = Theme.TextPri,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Yu Gothic UI", 9),
                Tag = tag,
                Margin = new Padding(4, 1, 4, 1),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.Bg4;
            return btn;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ブックマークパネル
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeBookmarksPanel()
        {
            bookmarksPanel = new Panel
            {
                Height = 32,
                Width = 236,
                BackColor = Theme.Bg1,
                Margin = new Padding(4, 2, 4, 0)
            };
            sidePanel.Controls.Add(bookmarksPanel);

            var hdr = new Label
            {
                Text = "▸  Bookmarks",
                ForeColor = Theme.TextSec,
                Font = new Font("Yu Gothic UI", 9, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Theme.Bg2,
                Cursor = Cursors.Hand
            };
            bookmarksPanel.Controls.Add(hdr);

            bookmarksContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg3,
                Visible = false,
                AutoScroll = true
            };
            bookmarksPanel.Controls.Add(bookmarksContent);
            bookmarksContent.SendToBack();
            hdr.BringToFront();

            hdr.Click += (s, e) =>
            {
                bookmarksContent.Visible = !bookmarksContent.Visible;
                bookmarksPanel.Height = bookmarksContent.Visible ? 160 : 32;
                hdr.Text = bookmarksContent.Visible ? "▾  Bookmarks" : "▸  Bookmarks";
            };

            var addBtn = MakeInlineButton("＋ ブックマーク追加", 5, 5);
            addBtn.Click += AddBookmarkBtn_Click;
            bookmarksContent.Controls.Add(addBtn);

            RefreshBookmarksList();
        }

        private void AddBookmarkBtn_Click(object sender, EventArgs e)
        {
            var cur = browsers.FirstOrDefault(b => b.Visible);
            if (cur == null) return;
            string title = browserTitles.TryGetValue(cur, out var t) && !string.IsNullOrEmpty(t) ? t : "新しいブックマーク";
            var dlg = new BookmarkDialog(cur.Address, title);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                bookmarks.Add(new BookmarkItem { Title = dlg.BookmarkTitle, Url = dlg.BookmarkUrl });
                SaveBookmarks();
                RefreshBookmarksList();
            }
        }

        private void RefreshBookmarksList()
        {
            foreach (var c in bookmarksContent.Controls.OfType<Control>().Where(c => c.Tag?.ToString() == "bookmark").ToList())
            { bookmarksContent.Controls.Remove(c); c.Dispose(); }

            int y = 35;
            foreach (var bm in bookmarks)
            {
                var pnl = new Panel { Width = 212, Height = 28, Top = y, Left = 6, Tag = "bookmark", BackColor = Theme.Bg4 };
                var lbl = new LinkLabel
                {
                    Text = bm.Title.Length > 20 ? bm.Title.Substring(0, 17) + "…" : bm.Title,
                    LinkColor = Theme.Accent,
                    ActiveLinkColor = Theme.AccentHover,
                    Width = 184,
                    Height = 28,
                    Tag = bm.Url,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                };
                lbl.LinkClicked += (s, ev) => AddNewTab(lbl.Tag.ToString());

                var del = MakeDeleteButton(186, 4);
                del.Tag = bm;
                del.Click += (s, ev) => { bookmarks.Remove((BookmarkItem)del.Tag); SaveBookmarks(); RefreshBookmarksList(); };

                pnl.Controls.Add(lbl);
                pnl.Controls.Add(del);
                bookmarksContent.Controls.Add(pnl);
                y += 30;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 拡張機能パネル
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeExtensionsPanel()
        {
            extensionsPanel = new Panel
            {
                Height = 32,
                Width = 236,
                BackColor = Theme.Bg1,
                Margin = new Padding(4, 2, 4, 0)
            };
            sidePanel.Controls.Add(extensionsPanel);

            var hdr = new Label
            {
                Text = "▸  Extensions",
                ForeColor = Theme.TextSec,
                Font = new Font("Yu Gothic UI", 9, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Theme.Bg2,
                Cursor = Cursors.Hand
            };
            extensionsPanel.Controls.Add(hdr);

            extensionsContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg3,
                Visible = false,
                AutoScroll = true
            };
            extensionsPanel.Controls.Add(extensionsContent);
            extensionsContent.SendToBack();
            hdr.BringToFront();

            hdr.Click += (s, e) =>
            {
                extensionsContent.Visible = !extensionsContent.Visible;
                extensionsPanel.Height = extensionsContent.Visible ? 160 : 32;
                hdr.Text = extensionsContent.Visible ? "▾  Extensions" : "▸  Extensions";
            };

            var addBtn = MakeInlineButton("＋ 拡張機能追加", 5, 5);
            addBtn.Click += ExtensionAdd_Click;
            extensionsContent.Controls.Add(addBtn);

            RefreshExtensionsList();
        }

        private void ExtensionAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "拡張機能ファイル (*.js;*.crx)|*.js;*.crx",
                Title = "拡張機能を選択"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string ext = Path.GetExtension(dlg.FileName).ToLower();
                    string script = "";
                    string name = Path.GetFileNameWithoutExtension(dlg.FileName);

                    if (ext == ".crx")
                    {
                        var tmp = Path.Combine(Path.GetTempPath(), "TlmineExt_" + Guid.NewGuid());
                        Directory.CreateDirectory(tmp);
                        using (var fs = File.OpenRead(dlg.FileName))
                        {
                            fs.Seek(16, SeekOrigin.Begin);
                            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                                zip.ExtractToDirectory(tmp);
                        }

                        var mfPath = Path.Combine(tmp, "manifest.json");
                        if (File.Exists(mfPath))
                        {
                            try
                            {
                                var mf = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(mfPath));
                                if (mf != null && mf.TryGetValue("name", out var n)) name = n.GetString() ?? name;
                            }
                            catch { }
                        }
                        var js = Directory.GetFiles(tmp, "*.js", SearchOption.AllDirectories);
                        if (js.Length > 0) script = File.ReadAllText(js[0]);
                    }
                    else
                    {
                        script = File.ReadAllText(dlg.FileName);
                    }

                    if (string.IsNullOrWhiteSpace(script))
                    { MessageBox.Show("実行可能なスクリプトが見つかりませんでした。", "エラー"); return; }

                    if (MessageBox.Show(
                        $"⚠ セキュリティ警告\n\n拡張機能 '{name}' を追加します。\n信頼できる送信元のファイルのみ追加してください。\n\n続行しますか？",
                        "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                    extensions.Add(new ExtensionItem { Name = name, Enabled = true, ScriptPath = dlg.FileName, ScriptContent = script });
                    SaveExtensions();
                    RefreshExtensionsList();
                    MessageBox.Show($"拡張機能 '{name}' を追加しました。", "成功");
                }
                catch (Exception ex) { MessageBox.Show($"読み込み失敗: {ex.Message}", "エラー"); }
            } // end using dlg
        }

        private void RefreshExtensionsList()
        {
            foreach (var c in extensionsContent.Controls.OfType<Control>().Where(c => c.Tag?.ToString() == "extension").ToList())
            { extensionsContent.Controls.Remove(c); c.Dispose(); }

            int y = 35;
            foreach (var ex in extensions)
            {
                var pnl = new Panel { Width = 212, Height = 28, Top = y, Left = 6, Tag = "extension", BackColor = Theme.Bg4 };

                pnl.Controls.Add(new Label
                {
                    Text = ex.Name.Length > 18 ? ex.Name.Substring(0, 15) + "…" : ex.Name,
                    ForeColor = Theme.TextPri,
                    Width = 154,
                    Height = 28,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                });

                var tog = new Button
                {
                    Text = ex.Enabled ? "ON" : "OFF",
                    Width = 32,
                    Height = 20,
                    Left = 156,
                    Top = 4,
                    BackColor = ex.Enabled ? Theme.Success : Theme.TextDim,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Yu Gothic UI", 7),
                    Tag = ex
                };
                tog.FlatAppearance.BorderSize = 0;
                tog.Click += (s, ev) =>
                {
                    ex.Enabled = !ex.Enabled;
                    tog.Text = ex.Enabled ? "ON" : "OFF";
                    tog.BackColor = ex.Enabled ? Theme.Success : Theme.TextDim;
                    SaveExtensions();
                };

                var del = MakeDeleteButton(190, 4);
                del.Tag = ex;
                del.Click += (s, ev) => { extensions.Remove((ExtensionItem)del.Tag); SaveExtensions(); RefreshExtensionsList(); };

                pnl.Controls.Add(tog);
                pnl.Controls.Add(del);
                extensionsContent.Controls.Add(pnl);
                y += 30;
            }
        }

        private void InjectExtensions(ChromiumWebBrowser browser)
        {
            if (browser == null) return;
            foreach (var ex in extensions.Where(e => e.Enabled && !string.IsNullOrEmpty(e.ScriptContent)))
            {
                try { browser.GetMainFrame()?.EvaluateScriptAsync(ex.ScriptContent); }
                catch (Exception ex2) { Debug.WriteLine($"拡張機能エラー ({ex.Name}): {ex2.Message}"); }
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ダウンロード履歴パネル
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeDownloadHistoryPanel()
        {
            downloadHistoryPanel = new Panel
            {
                Height = 32,
                Width = 236,
                BackColor = Theme.Bg1,
                Margin = new Padding(4, 2, 4, 0)
            };
            sidePanel.Controls.Add(downloadHistoryPanel);

            var hdr = new Label
            {
                Text = "▸  Downloads",
                ForeColor = Theme.TextSec,
                Font = new Font("Yu Gothic UI", 9, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Theme.Bg2,
                Cursor = Cursors.Hand
            };
            downloadHistoryPanel.Controls.Add(hdr);

            downloadHistoryContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg3,
                Visible = false,
                AutoScroll = true
            };
            downloadHistoryPanel.Controls.Add(downloadHistoryContent);
            downloadHistoryContent.SendToBack();
            hdr.BringToFront();

            hdr.Click += (s, e) =>
            {
                downloadHistoryContent.Visible = !downloadHistoryContent.Visible;
                downloadHistoryPanel.Height = downloadHistoryContent.Visible ? 200 : 32;
                hdr.Text = downloadHistoryContent.Visible ? "▾  Downloads" : "▸  Downloads";
            };

            var clrBtn = MakeInlineButton("履歴をクリア", 5, 5);
            clrBtn.Click += (s, e) =>
            {
                if (MessageBox.Show("ダウンロード履歴をすべて削除しますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { downloadHistory.Clear(); SaveDownloadHistory(); RefreshDownloadHistoryList(); }
            };
            downloadHistoryContent.Controls.Add(clrBtn);
            RefreshDownloadHistoryList();
        }

        private void RefreshDownloadHistoryList()
        {
            foreach (var c in downloadHistoryContent.Controls.OfType<Control>().Where(c => c.Tag?.ToString() == "download").ToList())
            { downloadHistoryContent.Controls.Remove(c); c.Dispose(); }

            int y = 35;
            foreach (var dl in downloadHistory.OrderByDescending(d => d.DownloadDate).Take(20))
            {
                var pnl = new Panel
                {
                    Width = downloadHistoryContent.Width - 12,
                    Height = 44,
                    Top = y,
                    Left = 6,
                    Tag = "download",
                    BackColor = Theme.Bg4
                };
                pnl.Controls.Add(new Label
                {
                    Text = dl.FileName.Length > 24 ? dl.FileName.Substring(0, 21) + "…" : dl.FileName,
                    ForeColor = Theme.TextPri,
                    Width = 158,
                    Height = 20,
                    Left = 5,
                    Top = 3,
                    Font = new Font("Yu Gothic UI", 8, FontStyle.Bold)
                });
                pnl.Controls.Add(new Label
                {
                    Text = dl.DownloadDate.ToString("yyyy/MM/dd HH:mm"),
                    ForeColor = Theme.TextDim,
                    Width = 158,
                    Height = 16,
                    Left = 5,
                    Top = 23,
                    Font = new Font("Yu Gothic UI", 7)
                });

                var openBtn = new Button
                {
                    Text = "開く",
                    Width = 44,
                    Height = 18,
                    Left = 162,
                    Top = 3,
                    BackColor = Theme.AccentDim,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Yu Gothic UI", 7),
                    Tag = dl
                };
                openBtn.FlatAppearance.BorderSize = 0;
                openBtn.Click += (s, ev) =>
                {
                    var item = (DownloadHistoryItem)openBtn.Tag;
                    if (File.Exists(item.FilePath))
                        try { Process.Start(new ProcessStartInfo { FileName = item.FilePath, UseShellExecute = true }); }
                        catch (Exception ex) { MessageBox.Show($"ファイルを開けませんでした: {ex.Message}"); }
                    else MessageBox.Show("ファイルが見つかりません。");
                };

                var del = MakeDeleteButton(162, 23);
                del.Width = 44;
                del.Tag = dl;
                del.Click += (s, ev) => { downloadHistory.Remove((DownloadHistoryItem)del.Tag); SaveDownloadHistory(); RefreshDownloadHistoryList(); };

                pnl.Controls.Add(openBtn);
                pnl.Controls.Add(del);
                downloadHistoryContent.Controls.Add(pnl);
                y += 48;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 検索バー + ナビゲーション
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeSearchBar()
        {
            searchBarPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Theme.Bg0
            };
            Controls.Add(searchBarPanel);

            // ダウンロード進捗
            downloadPanel = new Panel
            {
                Width = 200,
                Height = 32,
                BackColor = Theme.Bg1,
                Visible = false,
                Top = 4
            };
            downloadProgressBar = new ProgressBar { Width = 150, Height = 22, Left = 5, Top = 5 };
            downloadLabel = new Label { Width = 40, Height = 22, Left = 158, Top = 5, Font = new Font("Yu Gothic UI", 8), ForeColor = Theme.TextPri, Text = "0%" };
            downloadPanel.Controls.Add(downloadProgressBar);
            downloadPanel.Controls.Add(downloadLabel);

            // ナビゲーションボタン群
            var navPanel = new Panel { Dock = DockStyle.Right, Width = 230, BackColor = Theme.Bg0 };
            searchBarPanel.Controls.Add(navPanel);
            searchBarPanel.Controls.Add(downloadPanel);

            backButton = CreateNavButton("◀", 4, false);
            forwardButton = CreateNavButton("▶", 42, false);
            reloadButton = CreateNavButton("⟳", 80, true);

            addBookmarkButton = CreateNavButton("★", 118, true);
            addBookmarkButton.ForeColor = Color.Gold;
            addBookmarkButton.Click += AddBookmarkBtn_Click;

            translateButton = CreateNavButton("🌐", 156, true);
            translateButton.Click += (s, e) =>
            {
                var b = browsers.FirstOrDefault(x => x.Visible);
                if (b != null && !string.IsNullOrEmpty(b.Address))
                    b.Load($"https://translate.google.com/translate?sl=auto&tl=ja&u={Uri.EscapeDataString(b.Address)}");
            };

            settingsButton = CreateNavButton("⚙", 194, true);
            settingsButton.Click += (s, e) => OpenSettings();

            backButton.Click += (s, e) => browsers.FirstOrDefault(b => b.Visible)?.Back();
            forwardButton.Click += (s, e) => browsers.FirstOrDefault(b => b.Visible)?.Forward();
            reloadButton.Click += (s, e) => browsers.FirstOrDefault(b => b.Visible)?.Reload();

            navPanel.Controls.Add(backButton);
            navPanel.Controls.Add(forwardButton);
            navPanel.Controls.Add(reloadButton);
            navPanel.Controls.Add(addBookmarkButton);
            navPanel.Controls.Add(translateButton);
            navPanel.Controls.Add(settingsButton);

            downloadPanel.Left = navPanel.Left - downloadPanel.Width - 8;
            downloadPanel.Top = 4;

            searchBar = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Yu Gothic UI", 10),
                BorderStyle = BorderStyle.None,
                ForeColor = Theme.TextDim,
                BackColor = Theme.Bg1,
                Text = searchBarPlaceholder
            };

            searchBar.GotFocus += (s, e) =>
            {
                if (searchBar.Text == searchBarPlaceholder)
                {
                    var cur = browsers.FirstOrDefault(b => b.Visible);
                    searchBar.Text = cur != null && !string.IsNullOrEmpty(cur.Address) ? cur.Address : "";
                    searchBar.ForeColor = Theme.TextPri;
                    searchBar.SelectAll();
                }
            };
            searchBar.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBar.Text))
                { searchBar.Text = searchBarPlaceholder; searchBar.ForeColor = Theme.TextDim; }
            };
            searchBar.KeyDown += SearchBar_KeyDown;
            searchBarPanel.Controls.Add(searchBar);

            // 1px 上ボーダー
            searchBarPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawLine(pen, 0, 0, searchBarPanel.Width, 0);
            };
        }

        private Button CreateNavButton(string text, int left, bool enabled)
        {
            var btn = new Button
            {
                Text = text,
                Width = 34,
                Height = 30,
                Left = left,
                Top = 5,
                BackColor = Theme.Bg1,
                ForeColor = Theme.TextSec,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Emoji", text == "⟳" ? 13 : 11),
                Enabled = enabled,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.Bg4;
            return btn;
        }

        private async void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string text = searchBar.Text.Trim();
            if (string.IsNullOrEmpty(text) || text == searchBarPlaceholder) return;

            string url = BuildUrl(text);
            string suggestion = (!text.StartsWith("http") && !IsValidDomain(text)) ? GetUrlSuggestion(text) : "";

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

        private string BuildUrl(string text)
        {
            if (text.StartsWith("http://") || text.StartsWith("https://")) return text;
            if (IsValidDomain(text)) return "https://" + text;
            string q = Uri.EscapeDataString(text);
            string engine = browserSettings.SearchEngine;
            if (engine == "Bing") return "https://www.bing.com/search?q=" + q;
            if (engine == "DuckDuckGo") return "https://duckduckgo.com/?q=" + q;
            if (engine == "Yahoo Japan") return "https://search.yahoo.co.jp/search?p=" + q;
            if (engine == "Ecosia") return "https://www.ecosia.org/search?q=" + q;
            return "https://www.google.com/search?q=" + q;
        }

        private string GetUrlSuggestion(string q)
        {
            q = q.ToLower();
            var map = new Dictionary<string, string>
            {
                {"youtube", "https://www.youtube.com"}, {"twitter", "https://twitter.com"},
                {"facebook","https://www.facebook.com"},{"instagram","https://www.instagram.com"},
                {"github","https://github.com"},        {"amazon","https://www.amazon.co.jp"},
                {"楽天","https://www.rakuten.co.jp"},   {"yahoo","https://www.yahoo.co.jp"},
                {"ニコニコ","https://www.nicovideo.jp"},{"wikipedia","https://ja.wikipedia.org"},
                {"gmail","https://mail.google.com"}
            };
            return map.FirstOrDefault(p => q.Contains(p.Key)).Value ?? "";
        }

        private bool IsValidDomain(string text)
        {
            if (text.Contains(' ') || !text.Contains('.')) return false;
            var tld = text.Split('.').Last();
            return tld.Length >= 2 && tld.All(char.IsLetter);
        }

        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) return false;
            try { var u = new Uri(url); return !u.IsLoopback && !u.IsFile; }
            catch { return false; }
        }

        private async Task ShowUrlSuggestion(string targetUrl)
        {
            var browser = browsers.FirstOrDefault(b => b.Visible);
            if (browser == null) return;
            string safe = targetUrl.Replace("\"", "\\\"").Replace("'", "\\'");
            string script = $@"
(function(){{
    var e=document.getElementById('tlm-sug');if(e)e.remove();
    var d=document.createElement('div');d.id='tlm-sug';
    d.style.cssText='padding:10px 20px;background:#1a1a2e;border-bottom:2px solid #5e81ff;font-size:13px;position:fixed;top:0;left:0;width:100%;z-index:99999;text-align:center;color:#e6e6f5;font-family:Yu Gothic UI,sans-serif';
    d.innerHTML='🌐 このURLをお探しですか？ <a href=""#"" id=""tl-l"" style=""color:#5e81ff;margin-left:8px"">{safe}</a> <button id=""tl-x"" style=""background:#2a2a3e;border:1px solid #5e81ff;color:#aaa;padding:2px 8px;margin-left:12px;border-radius:4px;cursor:pointer"">×</button>';
    document.body.prepend(d);
    document.getElementById('tl-l').onclick=function(e){{e.preventDefault();window.location.href='{safe}'}};
    document.getElementById('tl-x').onclick=function(){{d.remove()}};
    setTimeout(function(){{d.remove()}},10000);
}})();";
            try { await browser.EvaluateScriptAsync(script); }
            catch (Exception ex) { Debug.WriteLine($"JS error: {ex.Message}"); }
        }

        private void UpdateNavigationButtons()
        {
            var browser = browsers.FirstOrDefault(b => b.Visible);
            if (browser == null) return;
            backButton.Enabled = browser.CanGoBack;
            forwardButton.Enabled = browser.CanGoForward;
            backButton.ForeColor = browser.CanGoBack ? Theme.TextPri : Theme.TextDim;
            forwardButton.ForeColor = browser.CanGoForward ? Theme.TextPri : Theme.TextDim;
        }

        private void UpdateSearchBarUrl()
        {
            var b = browsers.FirstOrDefault(x => x.Visible);
            if (b == null || searchBar.Focused) return;
            bool blank = string.IsNullOrEmpty(b.Address) || b.Address == "about:blank";
            searchBar.Text = blank ? searchBarPlaceholder : b.Address;
            searchBar.ForeColor = blank ? Theme.TextDim : Theme.TextPri;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 設定画面
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void OpenSettings()
        {
            var dlg = new SettingsDialog(browserSettings);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                browserSettings = dlg.Result;
                SaveSettings();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Chromium 初期化 (修正版)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void InitializeChromium()
        {
            // ★ Fix: IsInitialized が null の場合は未初期化なので初期化する
            if (Cef.IsInitialized == true) return;

            var settings = new CefSettings();
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tlmine");
            Directory.CreateDirectory(appData);

            settings.RootCachePath = appData;
            settings.CachePath = Path.Combine(appData, "Cache");
            settings.BrowserSubprocessPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefSharp.BrowserSubprocess.exe");
            settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            settings.Locale = "ja";
            settings.AcceptLanguageList = "ja-JP,ja,en-US,en";

            // ★ Fix: YouTube / ニコニコ 再生に必要なフラグ
            settings.CefCommandLineArgs.Add("disable-gpu-vsync");
            settings.CefCommandLineArgs.Add("disable-background-timer-throttling");
            settings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows");
            settings.CefCommandLineArgs.Add("disable-renderer-backgrounding");
            settings.CefCommandLineArgs.Add("disable-blink-features", "AutomationControlled");
            settings.CefCommandLineArgs.Add("enable-media-stream");
            settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");
            settings.CefCommandLineArgs.Add("enable-proprietary-codecs");          // H.264 / AAC
            settings.CefCommandLineArgs.Add("enable-gpu-rasterization");
            settings.CefCommandLineArgs.Add("enable-zero-copy");
            settings.CefCommandLineArgs.Add("enable-webgl");
            settings.CefCommandLineArgs.Add("use-fake-ui-for-media-stream");
            settings.CefCommandLineArgs.Add("enable-features",
                "NetworkService,NetworkServiceInProcess,PlatformHEVCDecoderSupport,EnableDrm");
            // ★ Fix: Widevine DRM (ニコニコ等)
            settings.CefCommandLineArgs.Add("enable-widevine-cdm");
            settings.CefCommandLineArgs.Add("register-pepper-plugins", "");
            settings.CefCommandLineArgs.Add("lang", "ja-JP");

            settings.PersistSessionCookies = true;
            settings.MultiThreadedMessageLoop = true;
            settings.LogSeverity = LogSeverity.Disable;

            // ★ Fix: Widevine CDM パス自動検出
            TryRegisterWidevineCdm(settings);

            Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null);
        }

        private void TryRegisterWidevineCdm(CefSettings settings)
        {
            // Chrome インストール済みの Widevine CDM を探す
            string[] candidates = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Google\Chrome\User Data\WidevineCdm"),
                @"C:\Program Files\Google\Chrome\Application",
                @"C:\Program Files (x86)\Google\Chrome\Application"
            };
            foreach (var path in candidates)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        // manifest.json を探す
                        var manifests = Directory.GetFiles(path, "manifest.json", SearchOption.AllDirectories);
                        if (manifests.Length > 0)
                        {
                            settings.CefCommandLineArgs["widevine-cdm-path"] = Path.GetDirectoryName(manifests[0]);
                            settings.CefCommandLineArgs["widevine-cdm-version"] = "4.10.2830.0";
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // タブ追加 / 選択 / 閉じる
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void AddNewTab(string url)
        {
            var browser = new ChromiumWebBrowser(url)
            {
                Dock = DockStyle.Fill,
                Visible = false,
                RequestHandler = new CustomRequestHandler(this),
                DownloadHandler = new CustomDownloadHandler(this)
            };

            browser.FrameLoadEnd += (s, e) =>
            {
                if (e.Frame.IsMain)
                {
                    InjectExtensions(browser);
                    InjectJapaneseFontFix(browser);
                }
            };

            browser.TitleChanged += Browser_TitleChanged;
            browser.AddressChanged += (s, e) =>
            {
                string newAddress = e.Address;
                Action update = () =>
                {
                    if (!browser.Visible || searchBar.Focused) return;
                    searchBar.Text = newAddress;
                    searchBar.ForeColor = Theme.TextPri;
                };
                if (InvokeRequired) Invoke(update); else update();
            };

            browsers.Add(browser);
            Controls.Add(browser);

            // ── タブコンテナ ──
            var tabContainer = new Panel
            {
                Width = 228,
                Height = 34,
                BackColor = Theme.Bg3,
                Margin = new Padding(4, 1, 4, 1)
            };

            var closeBtn = new Button
            {
                Text = "×",
                Width = 26,
                Height = 34,
                BackColor = Theme.Bg3,
                ForeColor = Theme.TextDim,
                FlatStyle = FlatStyle.Flat,
                Tag = browser,
                Font = new Font("Yu Gothic UI", 10),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Right,
                Name = "closeButton"
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Theme.Danger;
            closeBtn.Click += (s, e) => CloseTab(browser);
            closeBtn.MouseEnter += (s, e) => { closeBtn.BackColor = Theme.Danger; closeBtn.ForeColor = Color.White; };
            closeBtn.MouseLeave += (s, e) => { closeBtn.BackColor = Theme.Bg3; closeBtn.ForeColor = Theme.TextDim; };

            var tabBtn = new Button
            {
                Text = "新しいタブ",
                Width = tabContainer.Width - 26,
                Height = 34,
                BackColor = Theme.Bg3,
                ForeColor = Theme.TextPri,
                FlatStyle = FlatStyle.Flat,
                Tag = browser,
                Font = new Font("Yu Gothic UI", 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Dock = DockStyle.Fill,
                Name = "tabButton",
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            tabBtn.FlatAppearance.BorderSize = 0;
            tabBtn.FlatAppearance.MouseOverBackColor = Theme.Bg4;
            tabBtn.Click += (s, e) => SelectTab(browser, tabContainer);

            tabContainer.Controls.Add(closeBtn);
            tabContainer.Controls.Add(tabBtn);

            tabButtonsPanel.Controls.Add(tabContainer);
            tabButtonsPanel.Controls.SetChildIndex(tabContainer, tabButtonsPanel.Controls.IndexOf(addTabButton));
            tabButtons.Add(tabBtn);

            SelectTab(browser, tabContainer);
            CreateDefaultIcon(tabBtn);
        }

        private void InjectJapaneseFontFix(ChromiumWebBrowser browser)
        {
            if (browser == null) return;
            try
            {
                browser.GetMainFrame()?.EvaluateScriptAsync(
                    "(function(){if(!document.documentElement.lang)document.documentElement.lang='ja';})();");
            }
            catch { }
        }

        private void CloseTab(ChromiumWebBrowser browserToClose)
        {
            if (browsers.Count <= 1) { Application.Exit(); return; }

            bool wasVisible = browserToClose.Visible;
            Panel found = null;
            foreach (Panel c in tabButtonsPanel.Controls.OfType<Panel>())
                if (c.Controls.OfType<Button>().Any(b => b.Tag == browserToClose))
                { found = c; break; }

            if (found != null) { tabButtonsPanel.Controls.Remove(found); found.Dispose(); }

            var tb = tabButtons.FirstOrDefault(b => b.Tag == browserToClose && b.Name == "tabButton");
            if (tb != null) tabButtons.Remove(tb);
            browserTitles.Remove(browserToClose);
            browsers.Remove(browserToClose);
            Controls.Remove(browserToClose);

            Task.Delay(100).ContinueWith(_ =>
            {
                try { if (InvokeRequired) Invoke((Action)browserToClose.Dispose); else browserToClose.Dispose(); }
                catch { }
            });

            if (wasVisible && browsers.Count > 0)
            {
                var next = browsers.Last();
                Panel nc = null;
                foreach (Panel c in tabButtonsPanel.Controls.OfType<Panel>())
                    if (c.Controls.OfType<Button>().Any(b => b.Tag == next && b.Name == "tabButton"))
                    { nc = c; break; }
                if (nc != null) SelectTab(next, nc);
            }
        }

        private void Browser_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            var browser = sender as ChromiumWebBrowser;
            if (browser == null || string.IsNullOrEmpty(e.Title)) return;

            string title = e.Title.Length > 22 ? e.Title.Substring(0, 19) + "…" : e.Title;
            browserTitles[browser] = e.Title;

            Action update = () =>
            {
                Panel found = null;
                foreach (Panel c in tabButtonsPanel.Controls.OfType<Panel>())
                    if (c.Controls.OfType<Button>().Any(b => b.Tag == browser && b.Name == "tabButton"))
                    { found = c; break; }

                var tabBtn = found?.Controls.OfType<Button>().FirstOrDefault(b => b.Name == "tabButton");
                if (tabBtn == null) return;
                tabBtn.Text = title;
                LoadFavicon(browser, tabBtn);
            };

            if (InvokeRequired) Invoke(update); else update();
        }

        private void LoadFavicon(ChromiumWebBrowser browser, Button tabBtn)
        {
            try
            {
                if (string.IsNullOrEmpty(browser.Address)) { CreateDefaultIcon(tabBtn); return; }
                var uri = new Uri(browser.Address);
                var client = new System.Net.WebClient();
                client.DownloadDataCompleted += (s, e) =>
                {
                    try
                    {
                        if (e.Error == null && e.Result?.Length > 0)
                        {
                            using (var ms = new MemoryStream(e.Result))
                            {
                                var ico = new Bitmap(Image.FromStream(ms), new Size(16, 16));
                                void Set() => tabBtn.Image = ico;
                                if (tabBtn.InvokeRequired) tabBtn.Invoke((Action)Set); else Set();
                            }
                        }
                        else CreateDefaultIcon(tabBtn);
                    }
                    catch { CreateDefaultIcon(tabBtn); }
                };
                client.DownloadDataAsync(new Uri($"{uri.Scheme}://{uri.Host}/favicon.ico"));
            }
            catch { CreateDefaultIcon(tabBtn); }
        }

        private void CreateDefaultIcon(Button tabBtn)
        {
            void Make()
            {
                var bmp = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(Theme.Accent))
                        g.FillEllipse(brush, 1, 1, 14, 14);
                    using (var pen = new Pen(Theme.AccentHover, 1.5f))
                        g.DrawEllipse(pen, 1, 1, 14, 14);
                }
                tabBtn.Image = bmp;
            }
            if (tabBtn.InvokeRequired) tabBtn.Invoke((Action)Make); else Make();
        }

        private void SelectTab(ChromiumWebBrowser browser, Panel tabContainer)
        {
            foreach (var b in browsers)
                if (b != browser && b.Visible) b.Visible = false;

            foreach (Panel c in tabButtonsPanel.Controls.OfType<Panel>())
            {
                bool active = c == tabContainer;
                c.BackColor = active ? Theme.AccentDim : Theme.Bg3;
                foreach (Button btn in c.Controls.OfType<Button>())
                    btn.BackColor = active ? Theme.AccentDim : Theme.Bg3;
            }

            browser.Visible = true;
            browser.BringToFront();
            UpdateSearchBarUrl();
            UpdateNavigationButtons();
            InjectExtensions(browser);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公開メソッド (RequestHandler / DownloadHandler から呼ばれる)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        public void AddNewTabFromUrl(string url) => AddNewTab(url);

        public void UpdateDownloadProgress(int pct, string _)
        {
            void Update()
            {
                downloadPanel.Visible = true;
                downloadProgressBar.Value = Math.Max(0, Math.Min(100, pct));
                downloadLabel.Text = $"{pct}%";
            }
            if (InvokeRequired) Invoke((Action)Update); else Update();
        }

        public void HideDownloadProgress()
        {
            if (InvokeRequired) Invoke((Action)(() => downloadPanel.Visible = false));
            else downloadPanel.Visible = false;
        }

        public void AddToDownloadHistory(string fileName, string filePath)
        {
            downloadHistory.Add(new DownloadHistoryItem { FileName = fileName, FilePath = filePath, DownloadDate = DateTime.Now });
            SaveDownloadHistory();
            if (InvokeRequired) Invoke((Action)RefreshDownloadHistoryList);
            else RefreshDownloadHistoryList();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 永続化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private static readonly JsonSerializerOptions JsonOpt = new JsonSerializerOptions { WriteIndented = true };

        private void SaveBookmarks() => TrySave(bookmarksFilePath, JsonSerializer.Serialize(bookmarks, JsonOpt));
        private void SaveExtensions() => TrySave(extensionsFilePath, JsonSerializer.Serialize(extensions, JsonOpt));
        private void SaveDownloadHistory() => TrySave(downloadHistoryFilePath, JsonSerializer.Serialize(downloadHistory, JsonOpt));
        private void SaveSettings() => TrySave(settingsFilePath, JsonSerializer.Serialize(browserSettings, JsonOpt));

        private void TrySave(string path, string json)
        {
            try { File.WriteAllText(path, json); }
            catch (Exception ex) { Debug.WriteLine($"保存失敗 {path}: {ex.Message}"); }
        }

        private void LoadBookmarks()
        {
            try { if (File.Exists(bookmarksFilePath)) bookmarks = JsonSerializer.Deserialize<List<BookmarkItem>>(File.ReadAllText(bookmarksFilePath)) ?? new List<BookmarkItem>(); }
            catch { bookmarks = new List<BookmarkItem>(); }
        }

        private void LoadExtensions()
        {
            try { if (File.Exists(extensionsFilePath)) extensions = JsonSerializer.Deserialize<List<ExtensionItem>>(File.ReadAllText(extensionsFilePath)) ?? new List<ExtensionItem>(); }
            catch { extensions = new List<ExtensionItem>(); }
        }

        private void LoadDownloadHistory()
        {
            try { if (File.Exists(downloadHistoryFilePath)) downloadHistory = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(File.ReadAllText(downloadHistoryFilePath)) ?? new List<DownloadHistoryItem>(); }
            catch { downloadHistory = new List<DownloadHistoryItem>(); }
        }

        private void LoadSettings()
        {
            try { if (File.Exists(settingsFilePath)) browserSettings = JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(settingsFilePath)) ?? new BrowserSettings(); }
            catch { browserSettings = new BrowserSettings(); }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ヘルパー: インラインボタン / 削除ボタン
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private Button MakeInlineButton(string text, int left, int top)
        {
            var btn = new Button
            {
                Text = text,
                Height = 24,
                Width = 200,
                Left = left,
                Top = top,
                BackColor = Theme.AccentDim,
                ForeColor = Theme.TextPri,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Yu Gothic UI", 8),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.Accent;
            return btn;
        }

        private Button MakeDeleteButton(int left, int top)
        {
            var btn = new Button
            {
                Text = "×",
                Width = 20,
                Height = 20,
                Left = left,
                Top = top,
                BackColor = Theme.Danger,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Yu Gothic UI", 8),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // フォームクローズ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            foreach (var b in browsers.ToList()) { try { b.Dispose(); } catch { } }
            if (Cef.IsInitialized == true) Cef.Shutdown();
            base.OnFormClosing(e);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // カスタム RequestHandler
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private class CustomRequestHandler : IRequestHandler
        {
            private readonly Form1 form;
            public CustomRequestHandler(Form1 form) { this.form = form; }

            public bool OnBeforeBrowse(IWebBrowser cw, IBrowser b, IFrame f, IRequest req, bool userGesture, bool isRedirect)
            {
                if (!req.Url.StartsWith("tlmine://openNewTab")) return false;
                if (!userGesture) return true;

                var query = new Uri(req.Url).Query.TrimStart('?').Split('&')
                    .Select(p => p.Split('=')).Where(p => p.Length == 2)
                    .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));

                if (!query.TryGetValue("url", out var target))
                { form.Invoke((Action)(() => form.AddNewTabFromUrl("https://www.google.com"))); return true; }

                if (!form.IsValidUrl(target))
                { form.Invoke((Action)(() => MessageBox.Show("無効または危険なURLです。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning))); return true; }

                DialogResult res = DialogResult.No;
                form.Invoke((Action)(() => res = MessageBox.Show($"以下のURLを新しいタブで開きますか？\n\n{target}", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question)));
                if (res == DialogResult.Yes) form.Invoke((Action)(() => form.AddNewTabFromUrl(target)));
                return true;
            }

            public bool GetAuthCredentials(IWebBrowser w, IBrowser b, string o, bool p, string h, int po, string r, string s, IAuthCallback c) => false;

            public bool OnCertificateError(IWebBrowser w, IBrowser b, CefErrorCode err, string r, ISslInfo ssl, IRequestCallback c)
            {
                form.Invoke((Action)(() =>
                {
                    var res = MessageBox.Show(
                        $"⚠ セキュリティ警告\n\nURL: {r}\nエラー: {err}\n\n続行しますか？（推奨しません）",
                        "証明書エラー", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    c.Continue(res == DialogResult.Yes);
                }));
                return true;
            }

            public void OnPluginCrashed(IWebBrowser w, IBrowser b, string p) { }
            public void OnRenderProcessTerminated(IWebBrowser w, IBrowser b, CefTerminationStatus s, int e, string m)
            {
                void Reload() => b.Reload();
                if (form.InvokeRequired) form.Invoke((Action)Reload); else Reload();
            }
            public void OnDocumentAvailableInMainFrame(IWebBrowser w, IBrowser b) { }
            public bool OnOpenUrlFromTab(IWebBrowser w, IBrowser b, IFrame f, string u, WindowOpenDisposition d, bool g)
            {
                if (d == WindowOpenDisposition.NewBackgroundTab ||
                    d == WindowOpenDisposition.NewForegroundTab ||
                    d == WindowOpenDisposition.NewWindow ||
                    d == WindowOpenDisposition.NewPopup)
                { form.Invoke((Action)(() => form.AddNewTabFromUrl(u))); return true; }
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

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // カスタム DownloadHandler
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private class CustomDownloadHandler : IDownloadHandler
        {
            private readonly Form1 form;
            private readonly Dictionary<int, string> names = new Dictionary<int, string>();
            public CustomDownloadHandler(Form1 form) { this.form = form; }

            public bool CanDownload(IWebBrowser w, IBrowser b, string u, string m) => true;

            public bool OnBeforeDownload(IWebBrowser w, IBrowser b, DownloadItem d, IBeforeDownloadCallback c)
            {
                names[d.Id] = d.SuggestedFileName;
                string size = d.TotalBytes > 0 ? $" ({d.TotalBytes / 1024 / 1024} MB)" : "";
                if (MessageBox.Show($"'{d.SuggestedFileName}'{size} をダウンロードしますか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    c.Continue(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", d.SuggestedFileName), false);
                    return true;
                }
                return false;
            }

            public void OnDownloadUpdated(IWebBrowser w, IBrowser b, DownloadItem d, IDownloadItemCallback c)
            {
                string fname = names.TryGetValue(d.Id, out var n) ? n : d.SuggestedFileName;
                if (d.IsInProgress)
                    form.UpdateDownloadProgress(d.TotalBytes > 0 ? (int)(d.ReceivedBytes * 100 / d.TotalBytes) : 0, fname);
                else if (d.IsComplete)
                {
                    form.HideDownloadProgress();
                    form.AddToDownloadHistory(fname, d.FullPath);
                    names.Remove(d.Id);

                    string ext = Path.GetExtension(d.FullPath).ToLower();
                    bool dangerous = new[] { ".exe", ".msi", ".bat", ".cmd", ".vbs", ".js", ".ps1", ".scr", ".com", ".pif" }.Contains(ext);
                    string msg = dangerous
                        ? $"⚠ 実行可能ファイル '{fname}' のダウンロードが完了しました。\n信頼できる送信元のみ開いてください。\nファイルを開きますか？"
                        : $"'{fname}' のダウンロードが完了しました。\nファイルを開きますか？";

                    if (MessageBox.Show(msg, dangerous ? "セキュリティ警告" : "完了",
                        MessageBoxButtons.YesNo, dangerous ? MessageBoxIcon.Warning : MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        try { Process.Start(new ProcessStartInfo { FileName = d.FullPath, UseShellExecute = true }); }
                        catch (Exception ex) { MessageBox.Show($"開けませんでした: {ex.Message}"); }
                    }
                }
                else if (d.IsCancelled)
                {
                    form.HideDownloadProgress();
                    names.Remove(d.Id);
                }
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // データモデル
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public class BookmarkItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class ExtensionItem
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = false;
        public string ScriptPath { get; set; } = "";
        public string ScriptContent { get; set; } = "";
    }

    public class DownloadHistoryItem
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime DownloadDate { get; set; }
    }

    public class BrowserSettings
    {
        public string HomePage { get; set; } = "https://www.google.com";
        public string SearchEngine { get; set; } = "Google";
        public bool JavaScriptEnabled { get; set; } = true;
        public bool HardwareAcceleration { get; set; } = true;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 設定ダイアログ
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public class SettingsDialog : Form
    {
        public BrowserSettings Result { get; private set; }
        private TextBox homeBox;
        private ComboBox engineBox;
        private CheckBox jsCheck, hwAccelCheck;

        public SettingsDialog(BrowserSettings current)
        {
            Result = new BrowserSettings
            {
                HomePage = current.HomePage,
                SearchEngine = current.SearchEngine,
                JavaScriptEnabled = current.JavaScriptEnabled,
                HardwareAcceleration = current.HardwareAcceleration
            };

            Text = "Tlmine 設定";
            Size = new Size(420, 280);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            BackColor = Theme.Bg1;
            ForeColor = Theme.TextPri;
            Font = new Font("Yu Gothic UI", 9);

            int y = 16;
            AddLabel("ホームページ:", 12, y);
            homeBox = AddTextBox(current.HomePage, 12, y + 20);
            y += 58;

            AddLabel("検索エンジン:", 12, y);
            engineBox = new ComboBox
            {
                Left = 12,
                Top = y + 20,
                Width = 380,
                BackColor = Theme.Bg4,
                ForeColor = Theme.TextPri,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            engineBox.Items.AddRange(new[] { "Google", "Bing", "DuckDuckGo", "Yahoo Japan", "Ecosia" });
            engineBox.SelectedItem = current.SearchEngine;
            Controls.Add(engineBox);
            y += 58;

            jsCheck = AddCheck("JavaScript を有効にする", 12, y, current.JavaScriptEnabled);
            y += 28;
            hwAccelCheck = AddCheck("ハードウェアアクセラレーションを有効にする", 12, y, current.HardwareAcceleration);
            y += 40;

            var okBtn = new Button
            {
                Text = "保存",
                Left = 230,
                Top = y,
                Width = 80,
                Height = 28,
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            okBtn.FlatAppearance.BorderSize = 0;
            okBtn.Click += (s, e) =>
            {
                Result.HomePage = homeBox.Text.Trim();
                Result.SearchEngine = engineBox.SelectedItem?.ToString() ?? "Google";
                Result.JavaScriptEnabled = jsCheck.Checked;
                Result.HardwareAcceleration = hwAccelCheck.Checked;
            };

            var cancelBtn = new Button
            {
                Text = "キャンセル",
                Left = 318,
                Top = y,
                Width = 80,
                Height = 28,
                BackColor = Theme.Bg3,
                ForeColor = Theme.TextSec,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            cancelBtn.FlatAppearance.BorderSize = 0;

            Controls.Add(okBtn);
            Controls.Add(cancelBtn);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 380,
                Height = 18,
                ForeColor = Theme.TextSec,
                Font = new Font("Yu Gothic UI", 8)
            });
        }

        private TextBox AddTextBox(string text, int x, int y)
        {
            var tb = new TextBox
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 380,
                Height = 22,
                BackColor = Theme.Bg4,
                ForeColor = Theme.TextPri,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(tb);
            return tb;
        }

        private CheckBox AddCheck(string text, int x, int y, bool chk)
        {
            var cb = new CheckBox
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 380,
                Height = 22,
                ForeColor = Theme.TextPri,
                Checked = chk
            };
            Controls.Add(cb);
            return cb;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ブックマークダイアログ
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public partial class BookmarkDialog : Form
    {
        private TextBox titleBox, urlBox;
        public string BookmarkTitle => titleBox.Text;
        public string BookmarkUrl => urlBox.Text;

        public BookmarkDialog(string url, string title)
        {
            Text = "ブックマーク追加";
            Size = new Size(420, 160);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            BackColor = Theme.Bg1;
            ForeColor = Theme.TextPri;
            Font = new Font("Yu Gothic UI", 9);

            Controls.Add(new Label { Text = "タイトル:", Location = new Point(12, 14), Size = new Size(68, 22), ForeColor = Theme.TextSec });
            titleBox = new TextBox { Location = new Point(86, 12), Size = new Size(310, 22), BackColor = Theme.Bg4, ForeColor = Theme.TextPri, BorderStyle = BorderStyle.FixedSingle, Text = title ?? "" };
            Controls.Add(titleBox);

            Controls.Add(new Label { Text = "URL:", Location = new Point(12, 44), Size = new Size(68, 22), ForeColor = Theme.TextSec });
            urlBox = new TextBox { Location = new Point(86, 42), Size = new Size(310, 22), BackColor = Theme.Bg4, ForeColor = Theme.TextPri, BorderStyle = BorderStyle.FixedSingle, Text = url ?? "" };
            Controls.Add(urlBox);

            var ok = new Button
            {
                Text = "OK",
                Location = new Point(234, 84),
                Size = new Size(80, 28),
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            ok.FlatAppearance.BorderSize = 0;

            var cancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(320, 84),
                Size = new Size(80, 28),
                BackColor = Theme.Bg3,
                ForeColor = Theme.TextSec,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            cancel.FlatAppearance.BorderSize = 0;

            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
