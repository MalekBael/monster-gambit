using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace DualEditorApp
{
    // Gambit classes to represent the data structure
    public class Gambit
    {
        public string Condition { get; set; }
        public int ActionId { get; set; }
        public int ActionParam { get; set; }
        public int Timing { get; set; }
        public string Description { get; set; }
        public double? Radius { get; set; }
        public bool Enabled { get; set; } = true; // Not in JSON but needed for UI
    }

    public class GambitPanel : Panel
    {
        private List<Gambit> gambits = new List<Gambit>();
        private ListView gambitListView;
        private MainForm parentForm;
        private Dictionary<string, ListViewGroup> monsterGroups;
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
            this.Padding = new Padding(0);
            this.Margin = new Padding(0);
            this.BackColor = Color.FromArgb(40, 40, 40);
            this.Dock = DockStyle.Fill;
            monsterGroups = new Dictionary<string, ListViewGroup>();

            // Create a TableLayoutPanel to ensure proper vertical layout
            TableLayoutPanel layoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // Configure rows - first row is auto-sized for header, second row fills remaining space
            layoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Create header panel in the first row
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 60,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0)
            };

            // Center the label in the header
            TableLayoutPanel centerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // Configure columns for centering - left buffer, center content, right buffer
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            centerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Label titleLabel = new Label
            {
                Text = "GAMBITS",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 15, 0, 15) // Add vertical padding
            };

            // Add label to the center column
            centerPanel.Controls.Add(new Panel(), 0, 0); // Left spacer
            centerPanel.Controls.Add(titleLabel, 1, 0);  // Centered label
            centerPanel.Controls.Add(new Panel(), 2, 0); // Right spacer

            headerPanel.Controls.Add(centerPanel);
            layoutPanel.Controls.Add(headerPanel, 0, 0);

            // Create ListView in the second row
            gambitListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            // Rest of your ListView setup
            statusIcons = new ImageList();
            statusIcons.Images.Add("enabled", CreateStatusIcon(true));
            statusIcons.Images.Add("disabled", CreateStatusIcon(false));
            gambitListView.SmallImageList = statusIcons;

            // Configure columns
            gambitListView.Columns.Add("Status", 60);
            gambitListView.Columns.Add("Timing", 60);
            gambitListView.Columns.Add("Condition", 140);
            gambitListView.Columns.Add("Action", 200);
            gambitListView.ShowGroups = true;

            // Set up event handlers
            gambitListView.MouseDoubleClick += GambitListView_MouseDoubleClick;
            gambitListView.MouseClick += GambitListView_MouseClick;
            this.Resize += (s, e) => AdjustColumnWidths();

            // Add ListView to the second row of the layout panel
            layoutPanel.Controls.Add(gambitListView, 0, 1);

            // Add the layout panel to the GambitPanel
            this.Controls.Add(layoutPanel);
        }

        // Add this method to adjust column widths
        public void AdjustColumnWidths()
        {
            if (gambitListView.Width <= 0) return;

            int totalWidth = gambitListView.ClientSize.Width;

            // Proportional sizing
            gambitListView.Columns[0].Width = (int)(totalWidth * 0.15); // Status
            gambitListView.Columns[1].Width = (int)(totalWidth * 0.10); // Timing
            gambitListView.Columns[2].Width = (int)(totalWidth * 0.35); // Condition

            // Action column gets remaining space
            int usedWidth = gambitListView.Columns[0].Width +
                            gambitListView.Columns[1].Width +
                            gambitListView.Columns[2].Width;
            gambitListView.Columns[3].Width = totalWidth - usedWidth - 5; // Action
        }

        private Image CreateStatusIcon(bool enabled)
        {
            Bitmap img = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(img))
            {
                Color color = enabled ? Color.LightGreen : Color.LightCoral;
                g.FillRectangle(new SolidBrush(color), 0, 0, 16, 16);
                g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80)), 0, 0, 15, 15);
            }
            return img;
        }

        private void GambitListView_MouseClick(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo hitInfo = gambitListView.HitTest(e.X, e.Y);
            if (hitInfo.Item != null && e.Button == MouseButtons.Left)
            {
                // Get the clicked item and its tag (which contains the Gambit)
                ListViewItem item = hitInfo.Item;
                Gambit gambit = item.Tag as Gambit;

                if (gambit != null)
                {
                    // Check if the Status column was clicked
                    if (hitInfo.SubItem == item.SubItems[0])
                    {
                        // Toggle the enabled state
                        gambit.Enabled = !gambit.Enabled;
                        item.ImageIndex = gambit.Enabled ? 0 : 1;
                        item.SubItems[0].Text = gambit.Enabled ? "ON" : "OFF";
                        SyncToJson();
                    }
                    // Check if the Condition column was clicked
                    else if (hitInfo.SubItem == item.SubItems[2])
                    {
                        ShowConditionDropdown(item, hitInfo.SubItem.Bounds);
                    }
                    // Check if the Timing column was clicked
                    else if (hitInfo.SubItem == item.SubItems[1])
                    {
                        ShowTimingEditor(item, hitInfo.SubItem.Bounds);
                    }
                }
            }
        }

        private void ShowTimingEditor(ListViewItem item, Rectangle bounds)
        {
            Gambit gambit = item.Tag as Gambit;
            if (gambit == null) return;

            // Create numeric up-down control for timing
            NumericUpDown timingEditor = new NumericUpDown
            {
                Location = new Point(bounds.X, bounds.Y),
                Width = bounds.Width,
                Height = bounds.Height,
                Minimum = 0,
                Maximum = 999,
                Value = gambit.Timing,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            // Handle value change
            timingEditor.ValueChanged += (s, e) => {
                gambit.Timing = (int)timingEditor.Value;
                item.SubItems[1].Text = timingEditor.Value.ToString();
            };

            // Handle key press to commit on Enter
            timingEditor.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
                {
                    gambitListView.Controls.Remove(timingEditor);
                    SyncToJson();
                }
            };

            // Handle focus loss
            timingEditor.LostFocus += (s, e) => {
                gambitListView.Controls.Remove(timingEditor);
                SyncToJson();
            };

            // Show the editor
            gambitListView.Controls.Add(timingEditor);
            timingEditor.Focus();
        }

        private void ShowConditionDropdown(ListViewItem item, Rectangle bounds)
        {
            Gambit gambit = item.Tag as Gambit;
            if (gambit == null) return;

            // Create dropdown
            ComboBox dropdown = new ComboBox
            {
                Location = new Point(bounds.X, bounds.Y),
                Width = bounds.Width,
                Height = bounds.Height,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            // Add condition options
            string[] conditions = new[] {
                "None", "Self", "Player", "PlayerAndAlly", "Ally",
                "BNpc", "TopHateTarget", "HPSelfPctLessThanTarget"
            };

            foreach (var condition in conditions)
            {
                dropdown.Items.Add(MapConditionToDisplay(condition));
            }

            // Set current selection
            dropdown.SelectedItem = MapConditionToDisplay(gambit.Condition);

            // Handle selection changes
            dropdown.SelectedIndexChanged += (s, e) => {
                string displayText = dropdown.SelectedItem.ToString();
                gambit.Condition = MapDisplayToCondition(displayText);
                item.SubItems[2].Text = displayText;

                gambitListView.Controls.Remove(dropdown);
                SyncToJson();
            };

            dropdown.LostFocus += (s, e) => {
                gambitListView.Controls.Remove(dropdown);
            };

            // Show the dropdown
            gambitListView.Controls.Add(dropdown);
            dropdown.Focus();
            dropdown.DroppedDown = true;
        }

        private string MapConditionToDisplay(string condition)
        {
            return conditionDisplayMap.ContainsKey(condition) ?
                conditionDisplayMap[condition] : condition;
        }

        private string MapDisplayToCondition(string displayText)
        {
            foreach (var pair in conditionDisplayMap)
            {
                if (pair.Value == displayText)
                    return pair.Key;
            }
            return displayText;
        }

        private void GambitListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Double-click handling - could be used for editing
        }

        public void LoadGambits(string json)
        {
            try
            {
                // Clear existing data
                gambitListView.Items.Clear();
                gambitListView.Groups.Clear();
                monsterGroups.Clear();
                gambits.Clear();

                // Parse JSON
                var jsonDoc = JsonDocument.Parse(json);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext())
                    return;

                var rootType = rootEnumerator.Current.Name; // "sprites" or "golem"
                var monstersElement = jsonDoc.RootElement.GetProperty(rootType);

                // Process each monster/sprite
                foreach (var monsterProperty in monstersElement.EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    JsonElement monsterElement = monsterProperty.Value;

                    // Create a group for this monster
                    ListViewGroup monsterGroup = new ListViewGroup(monsterName);
                    gambitListView.Groups.Add(monsterGroup);
                    monsterGroups[monsterName] = monsterGroup;

                    // Process gambits
                    var timeLines = monsterElement.GetProperty("gambitPack").GetProperty("timeLines");

                    foreach (var timeline in timeLines.EnumerateArray())
                    {
                        // Create a Gambit object
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

                        // Create a ListView item
                        ListViewItem item = new ListViewItem(new[] {
                            gambit.Enabled ? "ON" : "OFF",
                            gambit.Timing.ToString(),
                            MapConditionToDisplay(gambit.Condition),
                            gambit.Description
                        });

                        // Set the item's state
                        item.ImageIndex = gambit.Enabled ? 0 : 1;
                        item.Tag = gambit;
                        item.Group = monsterGroup;

                        gambitListView.Items.Add(item);
                    }
                }

                // At the end, add:
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
                if (gambits.Count == 0 || parentForm == null)
                    return;

                // Get the current JSON text
                string currentJson = parentForm.GetEditorText();

                // Parse the current JSON
                var jsonDoc = JsonDocument.Parse(currentJson);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext())
                    return;

                var rootType = rootEnumerator.Current.Name; // "sprites" or "golem"

                // Create a JsonNode from the current JSON to make modifications
                var jsonNode = JsonNode.Parse(currentJson);

                // Group gambits by monster
                Dictionary<string, List<Gambit>> gambitsByMonster = new Dictionary<string, List<Gambit>>();

                foreach (ListViewItem item in gambitListView.Items)
                {
                    if (item.Tag is Gambit gambit && item.Group != null)
                    {
                        string monsterName = item.Group.Header;
                        if (!gambitsByMonster.ContainsKey(monsterName))
                        {
                            gambitsByMonster[monsterName] = new List<Gambit>();
                        }
                        gambitsByMonster[monsterName].Add(gambit);
                    }
                }

                // Update each monster's gambits
                foreach (var monsterProperty in jsonDoc.RootElement.GetProperty(rootType).EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    if (!gambitsByMonster.ContainsKey(monsterName))
                        continue;

                    var monsterGambits = gambitsByMonster[monsterName];

                    // Create a new timeline array
                    var timeLines = new JsonArray();
                    foreach (var gambit in monsterGambits)
                    {
                        var timeLineNode = new JsonObject();

                        // Set condition and actionId based on enabled status
                        if (gambit.Enabled)
                        {
                            // Normal, enabled gambit
                            timeLineNode.Add("condition", gambit.Condition);
                            timeLineNode.Add("actionId", gambit.ActionId);
                        }
                        else
                        {
                            // Disabled gambit - store originals and set disabled values
                            timeLineNode.Add("condition", "None");
                            timeLineNode.Add("actionId", 0);
                            timeLineNode.Add("originalCondition", gambit.Condition);
                            timeLineNode.Add("originalActionId", gambit.ActionId);
                        }

                        // Add common properties
                        timeLineNode.Add("timing", gambit.Timing);
                        timeLineNode.Add("description", gambit.Description);
                        timeLineNode.Add("actionParam", gambit.ActionParam);

                        if (gambit.Radius.HasValue)
                            timeLineNode.Add("radius", gambit.Radius.Value);

                        timeLines.Add(timeLineNode);
                    }

                    // Replace the timeline array
                    jsonNode[rootType][monsterName]["gambitPack"]["timeLines"] = timeLines;
                }

                // Format the JSON with indentation
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = jsonNode.ToJsonString(options);

                // Update the editor text with the new JSON
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
    }
}