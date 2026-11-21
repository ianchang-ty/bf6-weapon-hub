using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace BF6WeaponHub
{
    public class MainWindow : Window
    {
        private static readonly Brush BgDark = BrushHex("#0B0E14");
        private static readonly Brush BgCard = BrushHex("#131822");
        private static readonly Brush BgCard2 = BrushHex("#1B2230");
        private static readonly Brush BorderSubtle = BrushHex("#263042");
        private static readonly Brush AccentOrange = BrushHex("#FF6B2B");
        private static readonly Brush TextMain = BrushHex("#F1F5F9");
        private static readonly Brush TextMuted = BrushHex("#94A3B8");
        private static readonly Brush GoodGreen = BrushHex("#22C55E");
        private static readonly Brush BadRed = BrushHex("#EF4444");
        private static readonly Brush WarnAmber = BrushHex("#F59E0B");

        private static readonly Dictionary<string, string> SlotNames = new Dictionary<string, string>
        {
            { "muzzle", "枪口" }, { "barrel", "枪管" }, { "bottomrail", "下挂" },
            { "toprail", "上导轨" }, { "rightrail", "右导轨" }, { "leftrail", "左导轨" },
            { "sight", "光学瞄具" }, { "secondarysight", "副瞄具/镜配件" },
            { "magazine", "弹匣" }, { "ammo", "弹药" }, { "ergonomic", "人体工学" }
        };

        private static readonly HashSet<string> RequiredSlots = new HashSet<string> { "barrel", "magazine", "ammo" };

        private readonly Dictionary<string, object> _root;
        private readonly List<Dictionary<string, object>> _weapons = new List<Dictionary<string, object>>();
        private readonly Dictionary<string, Dictionary<string, object>> _wByKey = new Dictionary<string, Dictionary<string, object>>();
        private readonly Dictionary<string, Dictionary<string, object>> _rawByKey = new Dictionary<string, Dictionary<string, object>>();
        private readonly GunsmithEngine _engine;

        // UI state
        private string _activeTab = "hall";
        private string _bandFilter = "all";
        private string _clsFilter = "all";
        private string _tierFilter = "all";
        private string _searchQuery = "";
        private string _selectedKey = "pkm";
        private string _selectedRole = null;

        // Custom Gunsmith state
        private readonly Dictionary<string, string> _customChoices = new Dictionary<string, string>();
        private bool _condLaser = true;
        private bool _condLight = true;
        private bool _condBipod = false;

        // Comparator state
        private string _cmpKeyB = "hk433";
        private string _cmpRoleB = "factory";

        // Layout containers
        private StackPanel _tabBarPanel;
        private StackPanel _bandBarPanel;
        private ComboBox _clsCombo;
        private ComboBox _tierCombo;
        private TextBox _searchBox;
        private TextBlock _listCountText;
        private StackPanel _weaponListPanel;
        private ScrollViewer _rightScroll;
        private StackPanel _rightContent;

        private static SolidColorBrush BrushHex(string hex)
        {
            SolidColorBrush b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            b.Freeze();
            return b;
        }

        public MainWindow(Dictionary<string, object> bundle)
        {
            _root = bundle;
            _engine = new GunsmithEngine(DataHelper.AsDict(bundle["tables"]));

            IList wList = DataHelper.AsList(bundle["weapons"]);
            foreach (object o in wList)
            {
                Dictionary<string, object> w = DataHelper.AsDict(o);
                string k = DataHelper.Str(w, "key", "");
                _weapons.Add(w);
                _wByKey[k] = w;
            }
            Dictionary<string, object> gsMap = DataHelper.AsDict(bundle["gunsmith"]);
            foreach (KeyValuePair<string, object> kv in gsMap)
                _rawByKey[kv.Key] = DataHelper.AsDict(kv.Value);

            Dictionary<string, object> meta = DataHelper.AsDict(bundle["meta"]);
            string verDate = DataHelper.Str(meta, "date", "2026-11-10");

            Title = "战地风云6 · 武器战术工作台 (BF6 Weapon Hub) — 离线最小权限版";
            Width = 1260;
            Height = 820;
            MinWidth = 960;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgDark;
            Foreground = TextMain;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");

            BuildShell(verDate);
            ResetCustomToFactory(_selectedKey);
            RefreshAll();
        }

        private void BuildShell(string verDate)
        {
            Grid rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Header
            Border header = new Border
            {
                Background = BgCard,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 12, 18, 12)
            };
            DockPanel headerDock = new DockPanel { LastChildFill = false };

            StackPanel titleStack = new StackPanel { Orientation = Orientation.Vertical };
            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = "战地风云6 · 武器战术工作台",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = TextMain,
                VerticalAlignment = VerticalAlignment.Center
            });
            titleRow.Children.Add(MakeBadge("数据 v" + verDate + " · 全 " + _weapons.Count + " 把武器", BgCard2, TextMuted, new Thickness(10, 0, 0, 0)));
            titleRow.Children.Add(MakeBadge("🔒 离线 · 零网络 · 零写盘 · asInvoker最小权限", BgCard2, GoodGreen, new Thickness(6, 0, 0, 0)));
            titleStack.Children.Add(titleRow);
            titleStack.Children.Add(new TextBlock
            {
                Text = "电脑端职责：100点自由枪匠实时重算 · 双枪同屏横向PK · 副屏置顶配装对照（与游戏进程完全物理隔离）",
                FontSize = 12,
                Foreground = TextMuted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            DockPanel.SetDock(titleStack, Dock.Left);
            headerDock.Children.Add(titleStack);

            // Right utility + tabs
            StackPanel rightControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            CheckBox pinCheck = new CheckBox
            {
                Content = "📌 窗口置顶",
                Foreground = TextMain,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Cursor = Cursors.Hand,
                ToolTip = "仅设置本窗口 TopMost 方便副屏对照配枪，绝不注入或读取游戏进程"
            };
            pinCheck.Checked += delegate { Topmost = true; };
            pinCheck.Unchecked += delegate { Topmost = false; };
            rightControls.Children.Add(pinCheck);

            _tabBarPanel = new StackPanel { Orientation = Orientation.Horizontal };
            rightControls.Children.Add(_tabBarPanel);

            DockPanel.SetDock(rightControls, Dock.Right);
            headerDock.Children.Add(rightControls);
            header.Child = headerDock;
            Grid.SetRow(header, 0);
            rootGrid.Children.Add(header);

            // Main Split Body
            Grid bodyGrid = new Grid { Margin = new Thickness(14) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left Column: Filters + Weapon List
            Border leftCard = new Border
            {
                Background = BgCard,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12)
            };
            Grid leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Search
            _searchBox = new TextBox
            {
                Background = BgDark,
                Foreground = TextMain,
                CaretBrush = TextMain,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                ToolTip = "输入武器名称或内部代号，如 Z84M、pkm、M433、KV9"
            };
            _searchBox.TextChanged += delegate
            {
                _searchQuery = _searchBox.Text ?? "";
                RenderWeaponList();
            };
            StackPanel searchWrap = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            searchWrap.Children.Add(new TextBlock { Text = "搜索武器（名称 / 英文代号 / 定位）：", FontSize = 11, Foreground = TextMuted, Margin = new Thickness(0, 0, 0, 4) });
            searchWrap.Children.Add(_searchBox);
            Grid.SetRow(searchWrap, 0);
            leftGrid.Children.Add(searchWrap);

            // Distance Band Filter Buttons
            _bandBarPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(_bandBarPanel, 1);
            leftGrid.Children.Add(_bandBarPanel);

            // Class & Tier Combo Row
            Grid comboRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            comboRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comboRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            comboRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            _clsCombo = MakeCombo();
            AddComboItem(_clsCombo, "all", "全部枪种 (69)");
            AddComboItem(_clsCombo, "ar", "突击步枪 (12)");
            AddComboItem(_clsCombo, "carbine", "卡宾枪 (10)");
            AddComboItem(_clsCombo, "smg", "冲锋枪 (11)");
            AddComboItem(_clsCombo, "lmg", "机枪 (11)");
            AddComboItem(_clsCombo, "dmr", "射手步枪 (7)");
            AddComboItem(_clsCombo, "sniper", "狙击步枪 (7)");
            AddComboItem(_clsCombo, "shotgun", "霰弹枪 (4)");
            AddComboItem(_clsCombo, "sidearm", "副武器 (7)");
            _clsCombo.SelectedIndex = 0;
            _clsCombo.SelectionChanged += delegate
            {
                ComboBoxItem it = _clsCombo.SelectedItem as ComboBoxItem;
                if (it != null) { _clsFilter = Convert.ToString(it.Tag); RenderWeaponList(); }
            };
            Grid.SetColumn(_clsCombo, 0);
            comboRow.Children.Add(_clsCombo);

            _tierCombo = MakeCombo();
            AddComboItem(_tierCombo, "all", "全部 Tier");
            AddComboItem(_tierCombo, "T0", "仅看 T0");
            AddComboItem(_tierCombo, "T0T1", "T0 + T1");
            _tierCombo.SelectedIndex = 0;
            _tierCombo.SelectionChanged += delegate
            {
                ComboBoxItem it = _tierCombo.SelectedItem as ComboBoxItem;
                if (it != null) { _tierFilter = Convert.ToString(it.Tag); RenderWeaponList(); }
            };
            Grid.SetColumn(_tierCombo, 2);
            comboRow.Children.Add(_tierCombo);

            Grid.SetRow(comboRow, 2);
            leftGrid.Children.Add(comboRow);

            _listCountText = new TextBlock { FontSize = 11, Foreground = TextMuted, Margin = new Thickness(2, 0, 0, 6) };
            Grid.SetRow(_listCountText, 3);
            leftGrid.Children.Add(_listCountText);

            ScrollViewer listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _weaponListPanel = new StackPanel();
            listScroll.Content = _weaponListPanel;
            Grid.SetRow(listScroll, 4);
            leftGrid.Children.Add(listScroll);

            leftCard.Child = leftGrid;
            Grid.SetColumn(leftCard, 0);
            bodyGrid.Children.Add(leftCard);

            // Right Column: Main Workspace
            Border rightCard = new Border
            {
                Background = BgCard,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 16, 18, 16)
            };
            _rightScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _rightContent = new StackPanel();
            _rightScroll.Content = _rightContent;
            rightCard.Child = _rightScroll;
            Grid.SetColumn(rightCard, 2);
            bodyGrid.Children.Add(rightCard);

            Grid.SetRow(bodyGrid, 1);
            rootGrid.Children.Add(bodyGrid);

            Content = rootGrid;
        }

        private void RefreshAll()
        {
            RenderTopTabs();
            RenderBandButtons();
            RenderWeaponList();
            RenderWorkspace();
        }

        private void RenderTopTabs()
        {
            _tabBarPanel.Children.Clear();
            string[,] tabs = new string[,]
            {
                { "hall", "🎯 配装大厅与过渡" },
                { "gunsmith", "🔧 100点自由枪匠" },
                { "compare", "⚖ 双枪横向对比" }
            };
            for (int i = 0; i < tabs.GetLength(0); i++)
            {
                string id = tabs[i, 0];
                string label = tabs[i, 1];
                bool on = _activeTab == id;
                Button b = MakeActionButton(label, on ? AccentOrange : BgCard2, on ? Brushes.White : TextMain, new Thickness(4, 0, 0, 0));
                string captured = id;
                b.Click += delegate { _activeTab = captured; RenderTopTabs(); RenderWorkspace(); };
                _tabBarPanel.Children.Add(b);
            }
        }

        private void RenderBandButtons()
        {
            _bandBarPanel.Children.Clear();
            string[,] bands = new string[,]
            {
                { "all", "全部" }, { "近", "近0-15" }, { "中近", "中近15-35" }, { "中", "中35-60" }, { "远", "远60+" }
            };
            for (int i = 0; i < bands.GetLength(0); i++)
            {
                string id = bands[i, 0];
                string label = bands[i, 1];
                bool on = _bandFilter == id;
                Button b = MakeSmallChip(label, on);
                string captured = id;
                b.Click += delegate
                {
                    _bandFilter = captured;
                    _selectedRole = null;
                    RenderBandButtons();
                    RenderWeaponList();
                    RenderWorkspace();
                };
                _bandBarPanel.Children.Add(b);
            }
        }

        private string GetWeaponTier(Dictionary<string, object> w, string band)
        {
            Dictionary<string, object> meta = DataHelper.AsDict(w["meta"]);
            if (meta == null) return "T2";
            Dictionary<string, object> tierMap = DataHelper.AsDict(meta["tier"]);
            if (tierMap == null) return "T2";
            if (band == "all")
            {
                string best = DataHelper.Str(meta, "best", "近");
                return DataHelper.Str(tierMap, best, "T2");
            }
            return tierMap.ContainsKey(band) ? DataHelper.Str(tierMap, band, null) : null;
        }

        private int GetSortScore(Dictionary<string, object> w, string band)
        {
            string t = GetWeaponTier(w, band) ?? "T9";
            int tIdx = t == "T0" ? 0 : (t == "T1" ? 1 : (t == "T2" ? 2 : 3));
            Dictionary<string, object> meta = DataHelper.AsDict(w["meta"]);
            Dictionary<string, object> rkMap = meta != null ? DataHelper.AsDict(meta["rank"]) : null;
            string rkKey = band == "all" ? DataHelper.Str(meta, "best", "近") : band;
            int rk = (rkMap != null && rkMap.ContainsKey(rkKey)) ? DataHelper.IntKey(rkMap, rkKey, 99) : 99;
            return tIdx * 100 + rk;
        }

        private void RenderWeaponList()
        {
            _weaponListPanel.Children.Clear();
            string q = (_searchQuery ?? "").Trim().ToLowerInvariant();
            List<Dictionary<string, object>> filtered = new List<Dictionary<string, object>>();

            foreach (Dictionary<string, object> w in _weapons)
            {
                string clsId = DataHelper.Str(w, "clsId", "");
                if (_clsFilter != "all" && clsId != _clsFilter) continue;
                string tier = GetWeaponTier(w, _bandFilter);
                if (_bandFilter != "all" && tier == null) continue;
                string effTier = tier ?? "T3";
                if (_tierFilter == "T0" && effTier != "T0") continue;
                if (_tierFilter == "T0T1" && effTier != "T0" && effTier != "T1") continue;

                if (q.Length > 0)
                {
                    Dictionary<string, object> meta = DataHelper.AsDict(w["meta"]);
                    string hay = (DataHelper.Str(w, "name", "") + " " + DataHelper.Str(w, "en", "") + " " +
                                  DataHelper.Str(w, "key", "") + " " + DataHelper.Str(w, "cls", "") + " " +
                                  DataHelper.Str(meta, "arch", "")).ToLowerInvariant();
                    if (!hay.Contains(q)) continue;
                }
                filtered.Add(w);
            }

            filtered.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b)
            {
                return GetSortScore(a, _bandFilter).CompareTo(GetSortScore(b, _bandFilter));
            });

            _listCountText.Text = string.Format("筛选结果：{0} 把武器（点击切换）", filtered.Count);

            bool hasSelected = false;
            foreach (Dictionary<string, object> w in filtered)
                if (DataHelper.Str(w, "key", "") == _selectedKey) { hasSelected = true; break; }
            if (!hasSelected && filtered.Count > 0)
            {
                _selectedKey = DataHelper.Str(filtered[0], "key", "pkm");
                _selectedRole = null;
                ResetCustomToFactory(_selectedKey);
            }

            foreach (Dictionary<string, object> w in filtered)
            {
                string key = DataHelper.Str(w, "key", "");
                bool active = key == _selectedKey;
                string tier = GetWeaponTier(w, _bandFilter) ?? "T2";
                Dictionary<string, object> meta = DataHelper.AsDict(w["meta"]);
                Dictionary<string, object> baseSt = DataHelper.AsDict(w["base"]);

                Border itemBorder = new Border
                {
                    Background = active ? BgCard2 : BgDark,
                    BorderBrush = active ? AccentOrange : BorderSubtle,
                    BorderThickness = new Thickness(active ? 1.5 : 1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };

                StackPanel sp = new StackPanel();
                DockPanel row1 = new DockPanel();
                StackPanel leftRow = new StackPanel { Orientation = Orientation.Horizontal };
                leftRow.Children.Add(MakeTierBadge(tier));
                leftRow.Children.Add(new TextBlock
                {
                    Text = DataHelper.Str(w, "name", ""),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(7, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                leftRow.Children.Add(new TextBlock
                {
                    Text = DataHelper.Str(w, "cls", ""),
                    FontSize = 11,
                    Foreground = TextMuted,
                    VerticalAlignment = VerticalAlignment.Center
                });
                DockPanel.SetDock(leftRow, Dock.Left);
                row1.Children.Add(leftRow);

                Border distPill = MakeBadge("主:" + DataHelper.Str(meta, "best", "近") + "距", BgCard, TextMain, new Thickness(0));
                DockPanel.SetDock(distPill, Dock.Right);
                row1.Children.Add(distPill);
                sp.Children.Add(row1);

                IList bpList = DataHelper.AsList(w["bp"]);
                Dictionary<string, object> bp0 = (bpList != null && bpList.Count > 0) ? DataHelper.AsDict(bpList[0]) : null;
                string subRight = DataHelper.Str(w, "clsId", "") == "sniper"
                    ? ("甜区 " + DataHelper.Str(w, "sweet_spot", "无"))
                    : string.Format("{0}RPM · {1}枪/{2}ms",
                        DataHelper.IntKey(baseSt, "rpm", 0),
                        bp0 != null ? DataHelper.IntKey(bp0, "btk", 4) : 4,
                        bp0 != null ? DataHelper.IntKey(bp0, "ttk", 250) : 250);

                DockPanel row2 = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
                TextBlock archTb = new TextBlock { Text = DataHelper.Str(meta, "arch", ""), FontSize = 11, Foreground = TextMuted, TextTrimming = TextTrimming.CharacterEllipsis };
                TextBlock statTb = new TextBlock { Text = subRight, FontSize = 11, Foreground = TextMuted };
                DockPanel.SetDock(statTb, Dock.Right);
                row2.Children.Add(statTb);
                row2.Children.Add(archTb);
                sp.Children.Add(row2);

                itemBorder.Child = sp;
                string capturedKey = key;
                itemBorder.MouseLeftButtonUp += delegate
                {
                    if (_selectedKey != capturedKey)
                    {
                        _selectedKey = capturedKey;
                        _selectedRole = null;
                        ResetCustomToFactory(_selectedKey);
                        RenderWeaponList();
                        RenderWorkspace();
                    }
                };
                _weaponListPanel.Children.Add(itemBorder);
            }
        }

        private void ResetCustomToFactory(string key)
        {
            _customChoices.Clear();
            if (!_rawByKey.ContainsKey(key)) return;
            Dictionary<string, object> raw = _rawByKey[key];
            IList defIds = DataHelper.AsList(raw["default_build"]);
            HashSet<string> defSet = new HashSet<string>();
            if (defIds != null)
                foreach (object x in defIds) defSet.Add(Convert.ToString(x));

            IList slots = DataHelper.AsList(raw["slots"]);
            if (slots == null) return;
            foreach (object sObj in slots)
            {
                Dictionary<string, object> sDict = DataHelper.AsDict(sObj);
                string sid = DataHelper.Str(sDict, "id", "");
                IList opts = DataHelper.AsList(sDict["opts"]);
                string picked = null;
                if (opts != null)
                {
                    foreach (object oObj in opts)
                    {
                        Dictionary<string, object> oDict = DataHelper.AsDict(oObj);
                        string oid = DataHelper.Str(oDict, "id", "");
                        if (defSet.Contains(oid)) { picked = oid; break; }
                    }
                    if (picked == null && RequiredSlots.Contains(sid) && opts.Count > 0)
                        picked = DataHelper.Str(DataHelper.AsDict(opts[0]), "id", null);
                }
                _customChoices[sid] = picked;
            }
        }

        private void LoadPresetIntoCustom(string key, Dictionary<string, object> buildObj)
        {
            ResetCustomToFactory(key);
            if (!_rawByKey.ContainsKey(key) || buildObj == null) return;
            // Clear optional slots first
            List<string> sids = new List<string>(_customChoices.Keys);
            foreach (string sid in sids)
                if (!RequiredSlots.Contains(sid)) _customChoices[sid] = null;

            Dictionary<string, object> raw = _rawByKey[key];
            IList slots = DataHelper.AsList(raw["slots"]);
            IList items = DataHelper.AsList(buildObj["it"]);
            if (slots == null || items == null) return;

            foreach (object itObj in items)
            {
                Dictionary<string, object> it = DataHelper.AsDict(itObj);
                string targetSlot = DataHelper.Str(it, "slot", "");
                string targetCn = DataHelper.Str(it, "cn", "");
                foreach (object sObj in slots)
                {
                    Dictionary<string, object> sDict = DataHelper.AsDict(sObj);
                    if (DataHelper.Str(sDict, "id", "") != targetSlot) continue;
                    IList opts = DataHelper.AsList(sDict["opts"]);
                    foreach (object oObj in opts)
                    {
                        Dictionary<string, object> oDict = DataHelper.AsDict(oObj);
                        if (DataHelper.Str(oDict, "cn", "") == targetCn)
                        {
                            _customChoices[targetSlot] = DataHelper.Str(oDict, "id", null);
                            break;
                        }
                    }
                }
            }
        }

        private void RenderWorkspace()
        {
            _rightContent.Children.Clear();
            _rightScroll.ScrollToTop();
            if (!_wByKey.ContainsKey(_selectedKey)) return;

            if (_activeTab == "hall") RenderHallDetail();
            else if (_activeTab == "gunsmith") RenderGunsmithSimulator();
            else if (_activeTab == "compare") RenderComparator();
        }

        private void RenderHallDetail()
        {
            Dictionary<string, object> w = _wByKey[_selectedKey];
            Dictionary<string, object> meta = DataHelper.AsDict(w["meta"]);
            Dictionary<string, object> baseSt = DataHelper.AsDict(w["base"]);
            Dictionary<string, object> builds = DataHelper.AsDict(w["builds"]);

            string autoRole = DataHelper.Str(meta, "primary_role", null);
            if (_bandFilter != "all")
            {
                string mapped = _bandFilter == "近" ? "cqb" : (_bandFilter == "远" ? "long" : "mid");
                if (builds.ContainsKey(mapped)) autoRole = mapped;
            }
            if (string.IsNullOrEmpty(autoRole) || !builds.ContainsKey(autoRole))
            {
                foreach (string k in builds.Keys) { autoRole = k; break; }
            }
            string curRole = (!string.IsNullOrEmpty(_selectedRole) && builds.ContainsKey(_selectedRole)) ? _selectedRole : autoRole;
            Dictionary<string, object> bd = DataHelper.AsDict(builds[curRole]);
            Dictionary<string, object> modSt = DataHelper.AsDict(bd["st"]);

            // Weapon Header
            DockPanel headRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            StackPanel titleInfo = new StackPanel();
            StackPanel nameLine = new StackPanel { Orientation = Orientation.Horizontal };
            nameLine.Children.Add(new TextBlock { Text = DataHelper.Str(w, "name", ""), FontSize = 22, FontWeight = FontWeights.Bold });
            nameLine.Children.Add(MakeBadge(DataHelper.Str(w, "cls", "") + " · " + DataHelper.Str(w, "fire", ""), BgCard2, TextMain, new Thickness(10, 0, 0, 0)));
            nameLine.Children.Add(MakeBadge("代号: " + _selectedKey, BgDark, TextMuted, new Thickness(6, 0, 0, 0)));
            titleInfo.Children.Add(nameLine);

            string sweetInfo = !string.IsNullOrEmpty(DataHelper.Str(w, "sweet_spot", null)) ? (" · 胸口一枪死甜区：" + DataHelper.Str(w, "sweet_spot", "")) : "";
            titleInfo.Children.Add(new TextBlock
            {
                Text = "素体定位：" + DataHelper.Str(meta, "arch", "") + " · 最佳距离：" + DataHelper.Str(meta, "best", "近") + "距" + sweetInfo,
                FontSize = 13,
                Foreground = TextMuted,
                Margin = new Thickness(0, 4, 0, 0)
            });
            DockPanel.SetDock(titleInfo, Dock.Left);
            headRow.Children.Add(titleInfo);

            StackPanel actionBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button toGsBtn = MakeActionButton("🔧 载入此方案到自由枪匠", AccentOrange, Brushes.White, new Thickness(0, 0, 8, 0));
            toGsBtn.Click += delegate
            {
                LoadPresetIntoCustom(_selectedKey, bd);
                _activeTab = "gunsmith";
                RenderTopTabs();
                RenderWorkspace();
            };
            actionBtns.Children.Add(toGsBtn);

            Button copyBtn = MakeActionButton("📋 复制配装", BgCard2, TextMain, new Thickness(0));
            copyBtn.Click += delegate
            {
                CopyBuildText(w, bd);
                copyBtn.Content = "✓ 已复制";
            };
            actionBtns.Children.Add(copyBtn);
            DockPanel.SetDock(actionBtns, Dock.Right);
            headRow.Children.Add(actionBtns);
            _rightContent.Children.Add(headRow);

            // Distance Band Tier Strip (for auto weapons)
            if (DataHelper.Str(meta, "group", "") == "auto")
            {
                UniformGrid bandGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
                Dictionary<string, object> tMap = DataHelper.AsDict(meta["tier"]);
                Dictionary<string, object> rMap = DataHelper.AsDict(meta["rank"]);
                string[,] bDefs = new string[,] { { "近", "近战 0–15m" }, { "中近", "中近 15–35m" }, { "中", "中距 35–60m" }, { "远", "远距 60m+" } };
                for (int i = 0; i < 4; i++)
                {
                    string bk = bDefs[i, 0];
                    string bl = bDefs[i, 1];
                    string bt = DataHelper.Str(tMap, bk, "T2");
                    int brk = DataHelper.IntKey(rMap, bk, 0);
                    Border box = MakeInnerCard(new Thickness(0, 0, i < 3 ? 8 : 0, 0));
                    DockPanel dp = new DockPanel();
                    dp.Children.Add(new TextBlock { Text = bl, FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center });
                    Border tb = MakeTierBadge(bt + " · #" + brk);
                    DockPanel.SetDock(tb, Dock.Right);
                    dp.Children.Insert(0, tb);
                    box.Child = dp;
                    bandGrid.Children.Add(box);
                }
                _rightContent.Children.Add(bandGrid);
            }

            // Damage Breakpoints
            _rightContent.Children.Add(MakeSectionHeader(string.Format(
                "素体上胸伤害衰减段 (100 HP)  |  射速 {0} RPM · 初速 {1}→{2} m/s · 弹匣 {3}→{4} · 开镜 {5:0.00}s→{6:0.00}s",
                DataHelper.IntKey(baseSt, "rpm", 0),
                DataHelper.IntKey(baseSt, "v0", 0), DataHelper.IntKey(modSt, "v0", 0),
                DataHelper.IntKey(baseSt, "mag", 0), DataHelper.IntKey(modSt, "mag", 0),
                DataHelper.DblKey(baseSt, "adsTime", 0), DataHelper.DblKey(modSt, "adsTime", 0))));

            IList bpList = DataHelper.AsList(w["bp"]);
            UniformGrid bpGrid = new UniformGrid { Columns = Math.Min(4, Math.Max(1, bpList != null ? bpList.Count : 1)), Margin = new Thickness(0, 0, 0, 14) };
            if (bpList != null)
            {
                for (int i = 0; i < Math.Min(4, bpList.Count); i++)
                {
                    Dictionary<string, object> seg = DataHelper.AsDict(bpList[i]);
                    int f = DataHelper.IntKey(seg, "from", 0);
                    string rng = (f == 0 && seg["to"] == null) ? "全距离无衰减" : (seg["to"] == null ? (f + "m+") : (f + "–" + DataHelper.IntKey(seg, "to", 0) + "m"));
                    Border bCard = MakeInnerCard(new Thickness(0, 0, i < 3 ? 8 : 0, 0));
                    DockPanel dp = new DockPanel();
                    TextBlock valTb = new TextBlock { Text = DataHelper.IntKey(seg, "btk", 0) + "枪 / " + DataHelper.IntKey(seg, "ttk", 0) + "ms", FontWeight = FontWeights.Bold, Foreground = AccentOrange };
                    DockPanel.SetDock(valTb, Dock.Right);
                    dp.Children.Add(valTb);
                    dp.Children.Add(new TextBlock { Text = rng, Foreground = TextMuted });
                    bCard.Child = dp;
                    bpGrid.Children.Add(bCard);
                }
            }
            _rightContent.Children.Add(bpGrid);

            // Role Switcher
            StackPanel roleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            roleRow.Children.Add(new TextBlock { Text = "推荐配装流派切换：", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            foreach (string rk in builds.Keys)
            {
                Dictionary<string, object> bOption = DataHelper.AsDict(builds[rk]);
                bool on = rk == curRole;
                string label = string.Format("{0} ({1}/{2}点)", DataHelper.Str(bOption, "rc", rk), DataHelper.IntKey(bOption, "c", 0), DataHelper.IntKey(w, "budget", 100));
                Button rb = MakeActionButton(label, on ? AccentOrange : BgCard2, on ? Brushes.White : TextMain, new Thickness(0, 0, 8, 0));
                string capturedRole = rk;
                rb.Click += delegate { _selectedRole = capturedRole; RenderWorkspace(); };
                roleRow.Children.Add(rb);
            }
            _rightContent.Children.Add(roleRow);

            // 4-Stat Bars
            UniformGrid statGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 14) };
            statGrid.Children.Add(MakeStatBarCard("后坐控制 (Control)", baseSt["control"], modSt["control"], new Thickness(0, 0, 8, 8)));
            statGrid.Children.Add(MakeStatBarCard("连射精准 (Precision)", baseSt["precision_est"], modSt["precision_est"], new Thickness(0, 0, 0, 8)));
            statGrid.Children.Add(MakeStatBarCard("腰射散布 (Hipfire)", baseSt["hipfire"], modSt["hipfire"], new Thickness(0, 0, 8, 0)));
            statGrid.Children.Add(MakeStatBarCard("机动性能 (Mobility)", baseSt["mobility"], modSt["mobility"], new Thickness(0, 0, 0, 0)));
            _rightContent.Children.Add(statGrid);

            // Full Build Items
            _rightContent.Children.Add(MakeSectionHeader(string.Format("100点满级毕业配件清单（合计 {0} / {1} 点）", DataHelper.IntKey(bd, "c", 0), DataHelper.IntKey(w, "budget", 100))));
            IList itList = DataHelper.AsList(bd["it"]);
            UniformGrid itGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 14) };
            if (itList != null)
            {
                for (int i = 0; i < itList.Count; i++)
                {
                    Dictionary<string, object> it = DataHelper.AsDict(itList[i]);
                    Border card = MakeInnerCard(new Thickness(0, 0, i % 2 == 0 ? 8 : 0, 6));
                    DockPanel dp = new DockPanel();
                    Border cBadge = MakeBadge(DataHelper.IntKey(it, "cost", 0) + "点", BgCard2, TextMain, new Thickness(6, 0, 0, 0));
                    DockPanel.SetDock(cBadge, Dock.Right);
                    dp.Children.Add(cBadge);

                    StackPanel leftTxt = new StackPanel { Orientation = Orientation.Horizontal };
                    leftTxt.Children.Add(new TextBlock { Text = DataHelper.Str(it, "slot_cn", "") + "：", Foreground = TextMuted });
                    leftTxt.Children.Add(new TextBlock { Text = DataHelper.Str(it, "cn", ""), FontWeight = FontWeights.Bold });
                    if (DataHelper.BoolKey(it, "default"))
                        leftTxt.Children.Add(MakeBadge("原厂", BgCard2, GoodGreen, new Thickness(6, 0, 0, 0)));
                    dp.Children.Add(leftTxt);
                    card.Child = dp;
                    itGrid.Children.Add(card);
                }
            }
            _rightContent.Children.Add(itGrid);

            // Pre-Max Progression
            _rightContent.Children.Add(MakeSectionHeader("未达到满级前配枪方向（按单槽位实战贡献从高到低解锁替换）"));
            IList fkList = DataHelper.AsList(bd["fk"]);
            if (fkList != null && fkList.Count > 0)
            {
                List<string> fkParts = new List<string>();
                foreach (object fkObj in fkList)
                {
                    Dictionary<string, object> fk = DataHelper.AsDict(fkObj);
                    fkParts.Add(DataHelper.Str(fk, "slot_cn", "") + "·" + DataHelper.Str(fk, "cn", "") + "(" + DataHelper.IntKey(fk, "cost", 0) + "点)");
                }
                Border fkBox = MakeInnerCard(new Thickness(0, 0, 0, 6));
                fkBox.BorderBrush = GoodGreen;
                fkBox.Child = new TextBlock
                {
                    Text = "✓ Lv.1 原厂自带神件（满级前直接保留，无需更换）：" + string.Join("、", fkParts.ToArray()),
                    Foreground = GoodGreen,
                    TextWrapping = TextWrapping.Wrap
                };
                _rightContent.Children.Add(fkBox);
            }

            IList prList = DataHelper.AsList(bd["pr"]);
            if (prList != null && prList.Count > 0)
            {
                for (int i = 0; i < prList.Count; i++)
                {
                    Dictionary<string, object> p = DataHelper.AsDict(prList[i]);
                    Border pCard = MakeInnerCard(new Thickness(0, 0, 0, 6));
                    StackPanel pStack = new StackPanel();
                    DockPanel topLine = new DockPanel();
                    TextBlock lossTb = new TextBlock { Text = "贡献权重 +" + DataHelper.DblKey(p, "loss", 0).ToString("0.0"), Foreground = GoodGreen, FontWeight = FontWeights.Bold };
                    DockPanel.SetDock(lossTb, Dock.Right);
                    topLine.Children.Add(lossTb);
                    topLine.Children.Add(new TextBlock
                    {
                        Text = string.Format("#{0}  优先升级【{1}】 → {2}（{3}点）", i + 1, DataHelper.Str(p, "slot_cn", ""), DataHelper.Str(p, "cn", ""), DataHelper.IntKey(p, "cost", 0)),
                        FontWeight = FontWeights.Bold
                    });
                    pStack.Children.Add(topLine);

                    IList steps = DataHelper.AsList(p["steps"]);
                    if (steps != null && steps.Count > 0)
                    {
                        List<string> sArr = new List<string>();
                        foreach (object sObj in steps)
                        {
                            Dictionary<string, object> st = DataHelper.AsDict(sObj);
                            sArr.Add(string.Format("{0} ({1}点, ~{2}%效果)", DataHelper.Str(st, "cn", ""), DataHelper.IntKey(st, "cost", 0), DataHelper.IntKey(st, "kept", 0)));
                        }
                        pStack.Children.Add(new TextBlock
                        {
                            Text = "早期低点数平替阶梯：" + string.Join("  →  ", sArr.ToArray()),
                            FontSize = 12,
                            Foreground = TextMuted,
                            Margin = new Thickness(0, 4, 0, 0),
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                    pCard.Child = pStack;
                    _rightContent.Children.Add(pCard);
                }
            }

            IList notes = DataHelper.AsList(w["notes"]);
            if (notes != null && notes.Count > 0)
            {
                List<string> nStrs = new List<string>();
                foreach (object n in notes) nStrs.Add(Convert.ToString(n));
                Border nBox = MakeInnerCard(new Thickness(0, 4, 0, 0));
                nBox.BorderBrush = AccentOrange;
                nBox.Child = new TextBlock { Text = "★ 特殊机制/质变配件：" + string.Join("；", nStrs.ToArray()), Foreground = WarnAmber, TextWrapping = TextWrapping.Wrap };
                _rightContent.Children.Add(nBox);
            }
        }

        private void RenderGunsmithSimulator()
        {
            Dictionary<string, object> w = _wByKey[_selectedKey];
            Dictionary<string, object> raw = _rawByKey[_selectedKey];
            int budget = DataHelper.IntKey(w, "budget", 100);

            IList defIdsRaw = DataHelper.AsList(raw["default_build"]);
            List<string> defIds = new List<string>();
            if (defIdsRaw != null) foreach (object x in defIdsRaw) defIds.Add(Convert.ToString(x));

            List<string> curIds = new List<string>();
            foreach (KeyValuePair<string, string> kv in _customChoices)
                if (!string.IsNullOrEmpty(kv.Value)) curIds.Add(kv.Value);

            ComputedStats baseCs = _engine.Compute(w, raw, defIds, true, true, false);
            ComputedStats curCs = _engine.Compute(w, raw, curIds, _condLaser, _condLight, _condBipod);
            bool overBudget = curCs.TotalCost > budget;

            // Header + Presets
            DockPanel head = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            StackPanel left = new StackPanel();
            left.Children.Add(new TextBlock { Text = "🔧 100点自由枪匠模拟器 — " + DataHelper.Str(w, "name", ""), FontSize = 20, FontWeight = FontWeights.Bold });
            left.Children.Add(new TextBlock { Text = "自由替换任意槽位配件（例如改带消音器或按你当前已解锁配件搭配），实时重算点数、四维与分段 TTK", FontSize = 12, Foreground = TextMuted, Margin = new Thickness(0, 2, 0, 0) });
            DockPanel.SetDock(left, Dock.Left);
            head.Children.Add(left);

            StackPanel btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button resetBtn = MakeActionButton("重置为1级原厂", BgCard2, TextMain, new Thickness(0, 0, 6, 0));
            resetBtn.Click += delegate { ResetCustomToFactory(_selectedKey); RenderWorkspace(); };
            btns.Children.Add(resetBtn);

            Button exportBtn = MakeActionButton("💾 导出配装单 (.txt)", AccentOrange, Brushes.White, new Thickness(0));
            exportBtn.Click += delegate { ExportCustomLoadout(w, raw, curCs, budget); };
            btns.Children.Add(exportBtn);
            DockPanel.SetDock(btns, Dock.Right);
            head.Children.Add(btns);
            _rightContent.Children.Add(head);

            // Budget Meter + Condition Toggles
            Border budgetCard = MakeInnerCard(new Thickness(0, 0, 0, 12));
            budgetCard.BorderBrush = overBudget ? BadRed : BorderSubtle;
            StackPanel bStack = new StackPanel();
            DockPanel bRow = new DockPanel();
            TextBlock costLabel = new TextBlock
            {
                Text = string.Format("当前配件点数：{0} / {1} 点 {2}", curCs.TotalCost, budget, overBudget ? "（⚠ 超出上限，游戏内无法同时装备！）" : "（✓ 合法配装）"),
                FontWeight = FontWeights.Bold,
                Foreground = overBudget ? BadRed : GoodGreen
            };
            DockPanel.SetDock(costLabel, Dock.Left);
            bRow.Children.Add(costLabel);

            StackPanel condRow = new StackPanel { Orientation = Orientation.Horizontal };
            CheckBox cbLaser = new CheckBox { Content = "开启激光", IsChecked = _condLaser, Foreground = TextMain, Margin = new Thickness(12, 0, 0, 0) };
            cbLaser.Click += delegate { _condLaser = cbLaser.IsChecked == true; RenderWorkspace(); };
            CheckBox cbLight = new CheckBox { Content = "开启战术灯", IsChecked = _condLight, Foreground = TextMain, Margin = new Thickness(12, 0, 0, 0) };
            cbLight.Click += delegate { _condLight = cbLight.IsChecked == true; RenderWorkspace(); };
            CheckBox cbBipod = new CheckBox { Content = "架设两脚架", IsChecked = _condBipod, Foreground = WarnAmber, Margin = new Thickness(12, 0, 0, 0) };
            cbBipod.Click += delegate { _condBipod = cbBipod.IsChecked == true; RenderWorkspace(); };
            condRow.Children.Add(cbLaser);
            condRow.Children.Add(cbLight);
            condRow.Children.Add(cbBipod);
            DockPanel.SetDock(condRow, Dock.Right);
            bRow.Children.Add(condRow);
            bStack.Children.Add(bRow);

            ProgressBar pb = new ProgressBar
            {
                Minimum = 0,
                Maximum = budget,
                Value = Math.Min(budget, curCs.TotalCost),
                Height = 8,
                Margin = new Thickness(0, 8, 0, 0),
                Background = BgCard2,
                Foreground = overBudget ? BadRed : AccentOrange,
                BorderThickness = new Thickness(0)
            };
            bStack.Children.Add(pb);
            budgetCard.Child = bStack;
            _rightContent.Children.Add(budgetCard);

            // Live Recalculated Stats
            UniformGrid statGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
            statGrid.Children.Add(MakeStatBarCard("控制", baseCs.Control, curCs.Control, new Thickness(0, 0, 6, 0)));
            statGrid.Children.Add(MakeStatBarCard("精准", baseCs.Precision, curCs.Precision, new Thickness(0, 0, 6, 0)));
            statGrid.Children.Add(MakeStatBarCard("腰射", baseCs.Hipfire, curCs.Hipfire, new Thickness(0, 0, 6, 0)));
            statGrid.Children.Add(MakeStatBarCard("机动", baseCs.Mobility, curCs.Mobility, new Thickness(0)));
            _rightContent.Children.Add(statGrid);

            Border subStats = MakeInnerCard(new Thickness(0, 0, 0, 12));
            subStats.Child = new TextBlock
            {
                Text = string.Format("实时参数对比（原厂 → 当前）：模式 {0} · 射速 {1:0} RPM · 初速 {2:0}→{3:0} m/s · 弹匣 {4}→{5} 发 · 开镜 {6:0.00}s→{7:0.00}s · 战术换弹 {8:0.2f}s · 晃动 ×{9:0.2f}",
                    curCs.FireMode, curCs.Rpm, baseCs.V0, curCs.V0, baseCs.Mag, curCs.Mag, baseCs.AdsTime, curCs.AdsTime, curCs.ReloadTac, curCs.Sway)
                    .Replace("f", ""),
                FontSize = 12,
                Foreground = TextMuted
            };
            _rightContent.Children.Add(subStats);

            // Slot Pickers Grid
            _rightContent.Children.Add(MakeSectionHeader("选择各槽位配件（必选槽位：枪管 / 弹匣 / 弹药；其余可留空省点数）"));
            IList slots = DataHelper.AsList(raw["slots"]);
            UniformGrid slotGrid = new UniformGrid { Columns = 2 };
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    Dictionary<string, object> sDict = DataHelper.AsDict(slots[i]);
                    string sid = DataHelper.Str(sDict, "id", "");
                    string sName = SlotNames.ContainsKey(sid) ? SlotNames[sid] : sid;
                    IList opts = DataHelper.AsList(sDict["opts"]);

                    Border sCard = MakeInnerCard(new Thickness(0, 0, i % 2 == 0 ? 8 : 0, 8));
                    StackPanel sp = new StackPanel();
                    sp.Children.Add(new TextBlock { Text = sName + (RequiredSlots.Contains(sid) ? "（必选）" : "（可选）"), FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

                    ComboBox cb = MakeCombo();
                    string currentAid = _customChoices.ContainsKey(sid) ? _customChoices[sid] : null;
                    int selIdx = 0;
                    int idxCounter = 0;

                    if (!RequiredSlots.Contains(sid))
                    {
                        AddComboItem(cb, "", "[0点] （不安装 / 留空）");
                        if (string.IsNullOrEmpty(currentAid)) selIdx = 0;
                        idxCounter++;
                    }

                    string selectedDesc = "";
                    if (opts != null)
                    {
                        foreach (object oObj in opts)
                        {
                            Dictionary<string, object> oDict = DataHelper.AsDict(oObj);
                            string oid = DataHelper.Str(oDict, "id", "");
                            string ocn = DataHelper.Str(oDict, "cn", "");
                            int ocost = DataHelper.IntKey(oDict, "cost", 0);
                            bool isDef = DataHelper.BoolKey(oDict, "d");
                            string label = string.Format("[{0}点] {1}{2}", ocost, ocn, isDef ? " (原厂)" : "");
                            AddComboItem(cb, oid, label);
                            if (oid == currentAid)
                            {
                                selIdx = idxCounter;
                                selectedDesc = DataHelper.Str(oDict, "desc", "");
                            }
                            idxCounter++;
                        }
                    }

                    cb.SelectedIndex = selIdx;
                    string capturedSid = sid;
                    cb.SelectionChanged += delegate
                    {
                        ComboBoxItem item = cb.SelectedItem as ComboBoxItem;
                        if (item != null)
                        {
                            string tag = Convert.ToString(item.Tag);
                            _customChoices[capturedSid] = string.IsNullOrEmpty(tag) ? null : tag;
                            RenderWorkspace();
                        }
                    };
                    sp.Children.Add(cb);

                    if (!string.IsNullOrEmpty(selectedDesc))
                    {
                        sp.Children.Add(new TextBlock
                        {
                            Text = selectedDesc,
                            FontSize = 11,
                            Foreground = TextMuted,
                            Margin = new Thickness(0, 4, 0, 0),
                            TextWrapping = TextWrapping.Wrap
                        });
                    }
                    sCard.Child = sp;
                    slotGrid.Children.Add(sCard);
                }
            }
            _rightContent.Children.Add(slotGrid);
        }

        private void RenderComparator()
        {
            Dictionary<string, object> wA = _wByKey[_selectedKey];
            Dictionary<string, object> rawA = _rawByKey[_selectedKey];
            List<string> idsA = new List<string>();
            foreach (KeyValuePair<string, string> kv in _customChoices)
                if (!string.IsNullOrEmpty(kv.Value)) idsA.Add(kv.Value);
            ComputedStats csA = _engine.Compute(wA, rawA, idsA, true, true, false);

            if (!_wByKey.ContainsKey(_cmpKeyB)) _cmpKeyB = "hk433";
            Dictionary<string, object> wB = _wByKey[_cmpKeyB];
            Dictionary<string, object> rawB = _rawByKey[_cmpKeyB];
            Dictionary<string, object> buildsB = DataHelper.AsDict(wB["builds"]);
            List<string> idsB = new List<string>();
            if (_cmpRoleB != "factory" && buildsB != null && buildsB.ContainsKey(_cmpRoleB))
            {
                Dictionary<string, object> bdB = DataHelper.AsDict(buildsB[_cmpRoleB]);
                IList itemsB = DataHelper.AsList(bdB["it"]);
                IList slotsB = DataHelper.AsList(rawB["slots"]);
                if (itemsB != null && slotsB != null)
                {
                    foreach (object itObj in itemsB)
                    {
                        Dictionary<string, object> it = DataHelper.AsDict(itObj);
                        string tSlot = DataHelper.Str(it, "slot", "");
                        string tCn = DataHelper.Str(it, "cn", "");
                        foreach (object sObj in slotsB)
                        {
                            Dictionary<string, object> sDict = DataHelper.AsDict(sObj);
                            if (DataHelper.Str(sDict, "id", "") != tSlot) continue;
                            foreach (object oObj in DataHelper.AsList(sDict["opts"]))
                            {
                                Dictionary<string, object> oDict = DataHelper.AsDict(oObj);
                                if (DataHelper.Str(oDict, "cn", "") == tCn)
                                {
                                    idsB.Add(DataHelper.Str(oDict, "id", ""));
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                _cmpRoleB = "factory";
                IList defIdsBRaw = DataHelper.AsList(rawB["default_build"]);
                if (defIdsBRaw != null) foreach (object x in defIdsBRaw) idsB.Add(Convert.ToString(x));
            }
            ComputedStats csB = _engine.Compute(wB, rawB, idsB, true, true, false);

            _rightContent.Children.Add(new TextBlock { Text = "⚖ 双武器同屏横向对比（含子弹飞行延迟真实 TTK）", FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            _rightContent.Children.Add(new TextBlock { Text = "左侧列表点击切换【武器 A（使用自由枪匠当前配装）】，右侧下拉选择【对比武器 B 及配装方案】", FontSize = 12, Foreground = TextMuted, Margin = new Thickness(0, 0, 0, 12) });

            // Selector Row
            Grid pickGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            pickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            pickGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border cardA = MakeInnerCard(new Thickness(0));
            cardA.BorderBrush = AccentOrange;
            cardA.Child = new TextBlock
            {
                Text = string.Format("🅰 {0}（{1} · 当前枪匠 {2}点）", DataHelper.Str(wA, "name", ""), DataHelper.Str(wA, "cls", ""), csA.TotalCost),
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };
            Grid.SetColumn(cardA, 0);
            pickGrid.Children.Add(cardA);

            Border cardB = MakeInnerCard(new Thickness(0));
            DockPanel dpB = new DockPanel();
            dpB.Children.Add(new TextBlock { Text = "🅱 对比目标：", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            ComboBox cbB = MakeCombo();
            int selB = 0, idxB = 0;
            foreach (Dictionary<string, object> cand in _weapons)
            {
                string ck = DataHelper.Str(cand, "key", "");
                AddComboItem(cbB, ck, DataHelper.Str(cand, "name", "") + " (" + DataHelper.Str(cand, "cls", "") + ")");
                if (ck == _cmpKeyB) selB = idxB;
                idxB++;
            }
            cbB.SelectedIndex = selB;
            cbB.SelectionChanged += delegate
            {
                ComboBoxItem it = cbB.SelectedItem as ComboBoxItem;
                if (it != null) { _cmpKeyB = Convert.ToString(it.Tag); _cmpRoleB = "factory"; RenderWorkspace(); }
            };

            ComboBox cbRoleB = MakeCombo();
            cbRoleB.Margin = new Thickness(6, 0, 0, 0);
            cbRoleB.Width = 135;
            AddComboItem(cbRoleB, "factory", "1级原厂素体");
            int selRB = 0, idxRB = 1;
            if (buildsB != null)
            {
                foreach (string rk in buildsB.Keys)
                {
                    Dictionary<string, object> bOpt = DataHelper.AsDict(buildsB[rk]);
                    AddComboItem(cbRoleB, rk, DataHelper.Str(bOpt, "rc", rk));
                    if (rk == _cmpRoleB) selRB = idxRB;
                    idxRB++;
                }
            }
            cbRoleB.SelectedIndex = selRB;
            cbRoleB.SelectionChanged += delegate
            {
                ComboBoxItem it = cbRoleB.SelectedItem as ComboBoxItem;
                if (it != null) { _cmpRoleB = Convert.ToString(it.Tag); RenderWorkspace(); }
            };
            DockPanel.SetDock(cbRoleB, Dock.Right);
            dpB.Children.Add(cbRoleB);
            dpB.Children.Add(cbB);
            cardB.Child = dpB;
            Grid.SetColumn(cardB, 2);
            pickGrid.Children.Add(cardB);
            _rightContent.Children.Add(pickGrid);

            // Distance TTK Comparison Table
            _rightContent.Children.Add(MakeSectionHeader("各交战距离实战致死时间对比（上胸 100HP TTK + 子弹飞行时间，越低越强）"));
            int[] dists = new int[] { 5, 15, 25, 40, 60, 80, 100 };
            foreach (int d in dists)
            {
                int btkA, btkB;
                double rawTtkA, rawTtkB;
                GunsmithEngine.BtkAndTtk(csA, d, out btkA, out rawTtkA);
                GunsmithEngine.BtkAndTtk(csB, d, out btkB, out rawTtkB);
                double realA = GunsmithEngine.RealTtkWithTravel(csA, d);
                double realB = GunsmithEngine.RealTtkWithTravel(csB, d);
                bool aWins = realA < realB - 1.0;
                bool bWins = realB < realA - 1.0;

                Border row = MakeInnerCard(new Thickness(0, 0, 0, 5));
                Grid rg = new Grid();
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                rg.Children.Add(new TextBlock { Text = d + " 米", FontWeight = FontWeights.Bold, Foreground = TextMuted });
                TextBlock tbA = new TextBlock
                {
                    Text = string.Format("{0}：{1}枪 · 实战 {2:0} ms（纯射速 {3:0}ms）", DataHelper.Str(wA, "name", ""), btkA, realA, rawTtkA),
                    Foreground = aWins ? GoodGreen : TextMain,
                    FontWeight = aWins ? FontWeights.Bold : FontWeights.Normal
                };
                Grid.SetColumn(tbA, 1);
                rg.Children.Add(tbA);

                TextBlock tbB = new TextBlock
                {
                    Text = string.Format("{0}：{1}枪 · 实战 {2:0} ms（纯射速 {3:0}ms）", DataHelper.Str(wB, "name", ""), btkB, realB, rawTtkB),
                    Foreground = bWins ? GoodGreen : TextMain,
                    FontWeight = bWins ? FontWeights.Bold : FontWeights.Normal
                };
                Grid.SetColumn(tbB, 2);
                rg.Children.Add(tbB);

                row.Child = rg;
                _rightContent.Children.Add(row);
            }

            // Core Attribute Comparison
            _rightContent.Children.Add(MakeSectionHeader("核心面板属性横向对比"));
            AddCompareMetricRow("后坐控制 (Control)", csA.Control, csB.Control, true, "");
            AddCompareMetricRow("连射精准 (Precision)", csA.Precision ?? 0, csB.Precision ?? 0, true, "");
            AddCompareMetricRow("腰射散布 (Hipfire)", csA.Hipfire ?? 0, csB.Hipfire ?? 0, true, "");
            AddCompareMetricRow("机动性能 (Mobility)", csA.Mobility ?? 0, csB.Mobility ?? 0, true, "");
            AddCompareMetricRow("子弹初速 (Velocity)", csA.V0, csB.V0, true, " m/s");
            AddCompareMetricRow("理论射速 (RPM)", csA.Rpm, csB.Rpm, true, " RPM");
            AddCompareMetricRow("开镜耗时 (ADS)", csA.AdsTime * 1000, csB.AdsTime * 1000, false, " ms");
            AddCompareMetricRow("弹匣容量 (Mag)", csA.Mag, csB.Mag, true, " 发");
        }

        private void AddCompareMetricRow(string label, double valA, double valB, bool higherIsBetter, string unit)
        {
            bool aBetter = higherIsBetter ? (valA > valB + 0.5) : (valA < valB - 0.5);
            bool bBetter = higherIsBetter ? (valB > valA + 0.5) : (valB < valA - 0.5);
            Border row = MakeInnerCard(new Thickness(0, 0, 0, 5));
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            g.Children.Add(new TextBlock { Text = label, Foreground = TextMuted });
            TextBlock tA = new TextBlock { Text = Math.Round(valA) + unit, Foreground = aBetter ? GoodGreen : TextMain, FontWeight = aBetter ? FontWeights.Bold : FontWeights.Normal };
            Grid.SetColumn(tA, 1);
            g.Children.Add(tA);

            TextBlock tB = new TextBlock { Text = Math.Round(valB) + unit, Foreground = bBetter ? GoodGreen : TextMain, FontWeight = bBetter ? FontWeights.Bold : FontWeights.Normal };
            Grid.SetColumn(tB, 2);
            g.Children.Add(tB);

            row.Child = g;
            _rightContent.Children.Add(row);
        }

        private void CopyBuildText(Dictionary<string, object> w, Dictionary<string, object> bd)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("【战地风云6配装】{0} ({1}) · {2} ({3}/{4}点)",
                DataHelper.Str(w, "name", ""), DataHelper.Str(w, "cls", ""),
                DataHelper.Str(bd, "rc", ""), DataHelper.IntKey(bd, "c", 0), DataHelper.IntKey(w, "budget", 100));
            sb.AppendLine();

            IList items = DataHelper.AsList(bd["it"]);
            if (items != null)
            {
                List<string> parts = new List<string>();
                foreach (object x in items)
                {
                    Dictionary<string, object> it = DataHelper.AsDict(x);
                    parts.Add(DataHelper.Str(it, "slot_cn", "") + "：" + DataHelper.Str(it, "cn", "") + "(" + DataHelper.IntKey(it, "cost", 0) + ")");
                }
                sb.AppendLine("满级配件：" + string.Join(" / ", parts.ToArray()));
            }
            Clipboard.SetText(sb.ToString());
        }

        private void ExportCustomLoadout(Dictionary<string, object> w, Dictionary<string, object> raw, ComputedStats cs, int budget)
        {
            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "导出自定义配装单（仅写入您指定的位置）",
                Filter = "文本文件 (*.txt)|*.txt",
                FileName = DataHelper.Str(w, "name", "weapon") + "_配装单.txt"
            };
            if (dlg.ShowDialog(this) != true) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==========================================");
            sb.AppendFormat(" 战地风云6 自定义枪匠配装单 — {0} ({1})", DataHelper.Str(w, "name", ""), DataHelper.Str(w, "cls", ""));
            sb.AppendLine();
            sb.AppendFormat(" 总计点数：{0} / {1} 点", cs.TotalCost, budget);
            sb.AppendLine();
            sb.AppendLine("==========================================");

            IList slots = DataHelper.AsList(raw["slots"]);
            if (slots != null)
            {
                foreach (object sObj in slots)
                {
                    Dictionary<string, object> sDict = DataHelper.AsDict(sObj);
                    string sid = DataHelper.Str(sDict, "id", "");
                    string sName = SlotNames.ContainsKey(sid) ? SlotNames[sid] : sid;
                    string aid = _customChoices.ContainsKey(sid) ? _customChoices[sid] : null;
                    if (string.IsNullOrEmpty(aid)) continue;
                    IList opts = DataHelper.AsList(sDict["opts"]);
                    foreach (object oObj in opts)
                    {
                        Dictionary<string, object> oDict = DataHelper.AsDict(oObj);
                        if (DataHelper.Str(oDict, "id", "") == aid)
                        {
                            sb.AppendFormat(" - {0}：{1} ({2}点)", sName, DataHelper.Str(oDict, "cn", ""), DataHelper.IntKey(oDict, "cost", 0));
                            sb.AppendLine();
                        }
                    }
                }
            }
            sb.AppendLine("------------------------------------------");
            sb.AppendFormat(" 控制 {0:0} | 精准 {1:0} | 腰射 {2:0} | 机动 {3:0} | 初速 {4:0} m/s | 开镜 {5:0.00}s",
                cs.Control, cs.Precision ?? 0, cs.Hipfire ?? 0, cs.Mobility ?? 0, cs.V0, cs.AdsTime);
            sb.AppendLine();

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }

        // UI Helpers
        private static Border MakeInnerCard(Thickness margin)
        {
            return new Border
            {
                Background = BgDark,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = margin
            };
        }

        private static TextBlock MakeSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = TextMuted,
                Margin = new Thickness(0, 4, 0, 6)
            };
        }

        private static Border MakeBadge(string text, Brush bg, Brush fg, Thickness margin)
        {
            return new Border
            {
                Background = bg,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = margin,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 11, Foreground = fg }
            };
        }

        private static Border MakeTierBadge(string tierText)
        {
            bool isT0 = tierText.StartsWith("T0");
            bool isT1 = tierText.StartsWith("T1");
            return new Border
            {
                Background = isT0 ? AccentOrange : BgCard2,
                BorderBrush = (isT0 || isT1) ? AccentOrange : BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = tierText,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = isT0 ? Brushes.White : TextMain
                }
            };
        }

        private static Button MakeActionButton(string text, Brush bg, Brush fg, Thickness margin)
        {
            Border bd = new Border
            {
                Background = bg,
                BorderBrush = BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(11, 6, 11, 6),
                Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = fg }
            };
            Button btn = new Button
            {
                Margin = margin,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Template = MakeFlatButtonTemplate(),
                Content = bd
            };
            return btn;
        }

        private static Button MakeSmallChip(string text, bool active)
        {
            return MakeActionButton(text, active ? AccentOrange : BgDark, active ? Brushes.White : TextMuted, new Thickness(0, 0, 5, 0));
        }

        private static ControlTemplate MakeFlatButtonTemplate()
        {
            ControlTemplate tpl = new ControlTemplate(typeof(Button));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            tpl.VisualTree = presenter;
            return tpl;
        }

        private static ComboBox MakeCombo()
        {
            return new ComboBox
            {
                Padding = new Thickness(7, 4, 7, 4),
                FontSize = 12
            };
        }

        private static void AddComboItem(ComboBox cb, string tag, string text)
        {
            ComboBoxItem item = new ComboBoxItem { Tag = tag, Content = text };
            cb.Items.Add(item);
        }

        private static Border MakeStatBarCard(string label, object baseObj, object modObj, Thickness margin)
        {
            Border card = MakeInnerCard(margin);
            if (baseObj == null)
            {
                card.Child = new TextBlock { Text = label + "：—", Foreground = TextMuted, FontSize = 12 };
                return card;
            }
            double bVal = DataHelper.Dbl(baseObj, 0);
            double mVal = modObj != null ? DataHelper.Dbl(modObj, bVal) : bVal;
            int bRound = (int)Math.Round(bVal);
            int mRound = (int)Math.Round(mVal);
            int diff = mRound - bRound;

            StackPanel sp = new StackPanel();
            DockPanel row = new DockPanel();
            string diffTxt = diff > 0 ? (" (+" + diff + ")") : (diff < 0 ? (" (" + diff + ")") : "");
            TextBlock valTb = new TextBlock
            {
                Text = bRound + " → " + mRound + diffTxt,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = diff > 0 ? GoodGreen : (diff < 0 ? BadRed : TextMain)
            };
            DockPanel.SetDock(valTb, Dock.Right);
            row.Children.Add(valTb);
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = TextMuted });
            sp.Children.Add(row);

            ProgressBar bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Max(2, Math.Min(100, mVal)),
                Height = 6,
                Margin = new Thickness(0, 6, 0, 0),
                Background = BgCard2,
                Foreground = AccentOrange,
                BorderThickness = new Thickness(0)
            };
            sp.Children.Add(bar);
            card.Child = sp;
            return card;
        }
    }
}
