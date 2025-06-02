using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace DualEditorApp
{
    public class Gambit
    {
        public string Condition { get; set; }
        public int ActionId { get; set; }
        public int ActionParam { get; set; }
        public int Timing { get; set; }
        public string Description { get; set; }
        public double? Radius { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class GambitPanel : Panel
    {
        private List<Gambit> gambits = new List<Gambit>();
        private ListView gambitListView;
        private MainForm parentForm;
        private Dictionary<string, ListViewGroup> monsterGroups = new Dictionary<string, ListViewGroup>();
        private ImageList statusIcons;

        // Map from condition values to display text
        private readonly Dictionary<string, string> conditionDisplayMap = new Dictionary<string, string>
        {
            { "None", "No target" },
            { "Self", "Self" },
            { "Player", "Player" },
            { "PlayerAndAlly", "Player & Ally" },
            { "Ally", "Ally" },
            { "BNpc", "BNpc" },
            { "TopHateTarget", "Enemy: Top Aggro" },
            { "HPSelfPctLessThanTarget", "HP < X%" }
        };

        public GambitPanel(MainForm parent)
        {
            parentForm = parent;
            Padding = Margin = new Padding(0);
            BackColor = Color.FromArgb(40, 40, 40);
            Dock = DockStyle.Fill;

            // Create layout with header and content
            var layoutPanel = CreateLayoutPanel();

            // Create header
            layoutPanel.Controls.Add(CreateHeaderPanel(), 0, 0);

            // Create ListView
            gambitListView = CreateGambitListView();
            layoutPanel.Controls.Add(gambitListView, 0, 1);

            Controls.Add(layoutPanel);
        }

        private TableLayoutPanel CreateLayoutPanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = Margin = new Padding(0),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return layout;
        }

        private Panel CreateHeaderPanel()
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0)
            };

            // Center the title
            var centerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = Margin = new Padding(0)
            };
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var titleLabel = new Label
            {
                Text = "GAMBITS",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 15, 0, 15)
            };

            centerPanel.Controls.Add(new Panel(), 0, 0);
            centerPanel.Controls.Add(titleLabel, 1, 0);
            centerPanel.Controls.Add(new Panel(), 2, 0);
            headerPanel.Controls.Add(centerPanel);

            return headerPanel;
        }

        private ListView CreateGambitListView()
        {
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                ShowGroups = true,
                Margin = Padding = new Padding(0)
            };

            // Setup icons
            statusIcons = new ImageList();
            statusIcons.Images.Add("enabled", CreateStatusIcon(true));
            statusIcons.Images.Add("disabled", CreateStatusIcon(false));
            listView.SmallImageList = statusIcons;

            // Configure columns
            listView.Columns.Add("Status", 60);
            listView.Columns.Add("Timing", 60);
            listView.Columns.Add("Condition", 140);
            listView.Columns.Add("Action", 200);

            // Set up event handlers
            listView.DrawItem += (s, e) => DrawGroupHeader(e);
            listView.DrawColumnHeader += (s, e) => DrawColumnHeader(e);
            listView.MouseClick += GambitListView_MouseClick;
            listView.ColumnWidthChanging += (s, e) => { e.Cancel = true; e.NewWidth = listView.Columns[e.ColumnIndex].Width; };
            listView.HandleCreated += (s, e) => {
                EnableDoubleBuffering(listView);
                Application.AddMessageFilter(new ScrollMessageFilter(listView));
            };
            listView.Resize += (s, e) => { AdjustColumnWidths(); listView.Invalidate(); };

            return listView;
        }

        private void DrawGroupHeader(DrawListViewItemEventArgs e)
        {
            try
            {
                bool shouldDrawHeader = false;
                if (e.ItemIndex == 0) shouldDrawHeader = true;
                else if (e.ItemIndex > 0 && e.ItemIndex < gambitListView.Items.Count)
                {
                    var prevItem = gambitListView.Items[e.ItemIndex - 1];
                    shouldDrawHeader = (e.Item.Group != prevItem.Group);
                }

                if (shouldDrawHeader && e.Item.Group != null)
                {
                    var headerRect = new Rectangle(
                        e.Bounds.X, e.Bounds.Y - 28, e.Bounds.Width, 26);

                    if (headerRect.Y >= 0)
                    {
                        e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 80)), headerRect);
                        using var groupFont = new Font("Segoe UI", 14, FontStyle.Bold);
                        e.Graphics.DrawString(
                            e.Item.Group.Header,
                            groupFont,
                            Brushes.White,
                            headerRect.X + 10,
                            headerRect.Y + 2
                        );
                    }
                }
            }
            catch { }
            e.DrawDefault = true;
        }

        private void DrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(55, 55, 55)))
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

            using (var borderPen = new Pen(Color.FromArgb(70, 70, 70)))
                e.Graphics.DrawLine(borderPen, e.Bounds.X, e.Bounds.Bottom - 1,
                    e.Bounds.Right, e.Bounds.Bottom - 1);

            var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            var textBounds = new Rectangle(e.Bounds.X + 5, e.Bounds.Y,
                e.Bounds.Width - 10, e.Bounds.Height);

            using (var headerFont = new Font("Segoe UI", 9, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
                e.Graphics.DrawString(e.Header.Text, headerFont, textBrush, textBounds, format);

            e.DrawDefault = false;
        }

        private void EnableDoubleBuffering(ListView listView)
        {
            typeof(ListView).GetMethod("SetStyle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(
                listView,
                new object[] { ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true });
        }

        private Image CreateStatusIcon(bool enabled)
        {
            var img = new Bitmap(16, 16);
            using var g = Graphics.FromImage(img);
            var color = enabled ? Color.LightGreen : Color.LightCoral;
            g.FillRectangle(new SolidBrush(color), 0, 0, 16, 16);
            g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80)), 0, 0, 15, 15);
            return img;
        }

        public void AdjustColumnWidths()
        {
            if (gambitListView.Width <= 0) return;
            int totalWidth = gambitListView.ClientSize.Width;
            gambitListView.Columns[0].Width = (int)(totalWidth * 0.15);
            gambitListView.Columns[1].Width = (int)(totalWidth * 0.10);
            gambitListView.Columns[2].Width = (int)(totalWidth * 0.35);
            gambitListView.Columns[3].Width = totalWidth -
                gambitListView.Columns[0].Width -
                gambitListView.Columns[1].Width -
                gambitListView.Columns[2].Width - 5;
        }

        private void GambitListView_MouseClick(object sender, MouseEventArgs e)
        {
            var hitInfo = gambitListView.HitTest(e.X, e.Y);
            if (hitInfo.Item == null || e.Button != MouseButtons.Left) return;

            var item = hitInfo.Item;
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            if (hitInfo.SubItem == item.SubItems[0])
            {
                // Toggle status
                gambit.Enabled = !gambit.Enabled;
                item.ImageIndex = gambit.Enabled ? 0 : 1;
                item.SubItems[0].Text = gambit.Enabled ? "ON" : "OFF";
                SyncToJson();
            }
            else if (hitInfo.SubItem == item.SubItems[2])
            {
                ShowConditionDropdown(item, hitInfo.SubItem.Bounds);
            }
            else if (hitInfo.SubItem == item.SubItems[1])
            {
                ShowTimingEditor(item, hitInfo.SubItem.Bounds);
            }
        }

        private void ShowTimingEditor(ListViewItem item, Rectangle bounds)
        {
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            var timingEditor = new NumericUpDown
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height,
                Minimum = 0,
                Maximum = 999,
                Value = gambit.Timing,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            timingEditor.ValueChanged += (s, e) => {
                gambit.Timing = (int)timingEditor.Value;
                item.SubItems[1].Text = timingEditor.Value.ToString();
            };

            timingEditor.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
                {
                    gambitListView.Controls.Remove(timingEditor);
                    SyncToJson();
                }
            };

            timingEditor.LostFocus += (s, e) => {
                gambitListView.Controls.Remove(timingEditor);
                SyncToJson();
            };

            gambitListView.Controls.Add(timingEditor);
            timingEditor.Focus();
        }

        private void ShowConditionDropdown(ListViewItem item, Rectangle bounds)
        {
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            var dropdown = new ComboBox
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            string[] conditions = { "None", "Self", "Player", "PlayerAndAlly", "Ally",
                "BNpc", "TopHateTarget", "HPSelfPctLessThanTarget" };

            foreach (var condition in conditions)
                dropdown.Items.Add(MapConditionToDisplay(condition));

            dropdown.SelectedItem = MapConditionToDisplay(gambit.Condition);
            dropdown.SelectedIndexChanged += (s, e) => {
                string displayText = dropdown.SelectedItem.ToString();
                gambit.Condition = MapDisplayToCondition(displayText);
                item.SubItems[2].Text = displayText;
                gambitListView.Controls.Remove(dropdown);
                SyncToJson();
            };

            dropdown.LostFocus += (s, e) => gambitListView.Controls.Remove(dropdown);
            gambitListView.Controls.Add(dropdown);
            dropdown.Focus();
            dropdown.DroppedDown = true;
        }

        private string MapConditionToDisplay(string condition) =>
            conditionDisplayMap.ContainsKey(condition) ? conditionDisplayMap[condition] : condition;

        private string MapDisplayToCondition(string displayText)
        {
            foreach (var pair in conditionDisplayMap)
                if (pair.Value == displayText) return pair.Key;
            return displayText;
        }

        public void LoadGambits(string json)
        {
            try
            {
                gambitListView.Items.Clear();
                gambitListView.Groups.Clear();
                monsterGroups.Clear();
                gambits.Clear();

                var jsonDoc = JsonDocument.Parse(json);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext()) return;

                var rootType = rootEnumerator.Current.Name;
                var monstersElement = jsonDoc.RootElement.GetProperty(rootType);

                foreach (var monsterProperty in monstersElement.EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    JsonElement monsterElement = monsterProperty.Value;

                    var monsterGroup = new ListViewGroup(monsterName);
                    gambitListView.Groups.Add(monsterGroup);
                    monsterGroups[monsterName] = monsterGroup;

                    var timeLines = monsterElement.GetProperty("gambitPack").GetProperty("timeLines");
                    foreach (var timeline in timeLines.EnumerateArray())
                    {
                        bool isDisabled = timeline.TryGetProperty("originalCondition", out var _);
                        string condition = isDisabled ?
                            timeline.GetProperty("originalCondition").GetString() :
                            timeline.GetProperty("condition").GetString();

                        var gambit = new Gambit
                        {
                            Condition = condition,
                            ActionId = timeline.GetProperty("actionId").GetInt32(),
                            Timing = timeline.GetProperty("timing").GetInt32(),
                            Description = timeline.GetProperty("description").GetString(),
                            ActionParam = timeline.GetProperty("actionParam").GetInt32(),
                            Enabled = !isDisabled
                        };

                        if (timeline.TryGetProperty("radius", out var radius))
                            gambit.Radius = radius.GetDouble();

                        gambits.Add(gambit);

                        var item = new ListViewItem(new[] {
                            gambit.Enabled ? "ON" : "OFF",
                            gambit.Timing.ToString(),
                            MapConditionToDisplay(gambit.Condition),
                            gambit.Description
                        });

                        item.ImageIndex = gambit.Enabled ? 0 : 1;
                        item.Tag = gambit;
                        item.Group = monsterGroup;
                        gambitListView.Items.Add(item);
                    }
                }

                AdjustColumnWidths();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading gambits: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SyncToJson()
        {
            try
            {
                if (gambits.Count == 0 || parentForm == null) return;

                string currentJson = parentForm.GetEditorText();
                var jsonDoc = JsonDocument.Parse(currentJson);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext()) return;

                var rootType = rootEnumerator.Current.Name;
                var jsonNode = JsonNode.Parse(currentJson);

                var gambitsByMonster = new Dictionary<string, List<Gambit>>();
                foreach (ListViewItem item in gambitListView.Items)
                {
                    if (item.Tag is Gambit gambit && item.Group != null)
                    {
                        string monsterName = item.Group.Header;
                        if (!gambitsByMonster.ContainsKey(monsterName))
                            gambitsByMonster[monsterName] = new List<Gambit>();
                        gambitsByMonster[monsterName].Add(gambit);
                    }
                }

                foreach (var monsterProperty in jsonDoc.RootElement.GetProperty(rootType).EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    if (!gambitsByMonster.ContainsKey(monsterName)) continue;

                    var timeLines = new JsonArray();
                    foreach (var gambit in gambitsByMonster[monsterName])
                    {
                        var timeLineNode = new JsonObject();

                        if (gambit.Enabled)
                        {
                            timeLineNode.Add("condition", gambit.Condition);
                            timeLineNode.Add("actionId", gambit.ActionId);
                        }
                        else
                        {
                            timeLineNode.Add("condition", "None");
                            timeLineNode.Add("actionId", 0);
                            timeLineNode.Add("originalCondition", gambit.Condition);
                            timeLineNode.Add("originalActionId", gambit.ActionId);
                        }

                        timeLineNode.Add("timing", gambit.Timing);
                        timeLineNode.Add("description", gambit.Description);
                        timeLineNode.Add("actionParam", gambit.ActionParam);

                        if (gambit.Radius.HasValue)
                            timeLineNode.Add("radius", gambit.Radius.Value);

                        timeLines.Add(timeLineNode);
                    }

                    jsonNode[rootType][monsterName]["gambitPack"]["timeLines"] = timeLines;
                }

                string updatedJson = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                parentForm.SetEditorText(updatedJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating JSON: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ClearGambits()
        {
            gambitListView.Items.Clear();
            gambitListView.Groups.Clear();
            monsterGroups.Clear();
            gambits.Clear();
        }

        // Simplified ScrollMessageFilter that handles both functions
        private class ScrollMessageFilter : IMessageFilter
        {
            private readonly ListView listView;
            private const int WM_VSCROLL = 0x115;
            private const int WM_MOUSEWHEEL = 0x20A;

            public ScrollMessageFilter(ListView listView) => this.listView = listView;

            public bool PreFilterMessage(ref Message m)
            {
                if ((m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL) && m.HWnd == listView.Handle)
                    listView.BeginInvoke(new Action(() => listView.Refresh()));
                return false;
            }
        }
    }
}