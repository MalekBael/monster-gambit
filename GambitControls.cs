using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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
        private Dictionary<int, string> actionDisplayMap = new Dictionary<int, string>();

        // Add a panel to show actions for the currently selected monster
        private Panel monsterActionsPanel;
        private Button addGambitButton;
        private Button deleteDisabledButton;
        private string currentMonsterSelection;

        // Add this field to the class
        private ComboBox monsterSelector;

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

            // Load action data from CSV
            LoadActionData();

            // Create layout with header, action panel and content
            var layoutPanel = CreateLayoutPanel();

            // Create header
            layoutPanel.Controls.Add(CreateHeaderPanel(), 0, 0);

            // Create monster action panel (new)
            layoutPanel.Controls.Add(CreateMonsterActionsPanel(), 0, 1);

            // Create ListView (now in row 2 instead of 1)
            gambitListView = CreateGambitListView();
            layoutPanel.Controls.Add(gambitListView, 0, 2);

            Controls.Add(layoutPanel);
            
            // Add a panel-wide click handler to deselect rows
            this.Click += Panel_Click;
            
            // Add keyboard handler for Escape key
            gambitListView.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape)
                {
                    DeselectAllItems();
                }
            };

            // Handle panel initialization
            this.HandleCreated += (s, e) => {
                // Force refresh on load
                gambitListView.Refresh();
            };
        }

        private void LoadActionData()
        {
            try
            {
                string actionCsvPath = Path.Combine(Application.StartupPath, "resources", "ActionNames.csv");
                if (File.Exists(actionCsvPath))
                {
                    // Skip the header line and read the rest
                    var lines = File.ReadAllLines(actionCsvPath).Skip(1);

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] parts = line.Split(',');
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int actionId) && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            actionDisplayMap[actionId] = parts[1];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading action data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private TableLayoutPanel CreateLayoutPanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3, // Increased to 3 rows to accommodate action panel
                ColumnCount = 1,
                Padding = Margin = new Padding(0),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Monster actions panel
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // ListView
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

        private Panel CreateMonsterActionsPanel()
        {
            // Create the monster actions panel
            monsterActionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                BackColor = Color.FromArgb(50, 50, 60),
                Padding = new Padding(5),
            };

            // Create label for panel description
            var descLabel = new Label
            {
                Text = "Monster:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 12),
            };
            monsterActionsPanel.Controls.Add(descLabel);

            // Create dropdown for monster selection
            monsterSelector = new ComboBox
            {
                Location = new Point(80, 8),
                Width = 180,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            
            monsterSelector.SelectedIndexChanged += (s, e) => {
                if (monsterSelector.SelectedItem is MonsterItem monster)
                {
                    // Use the original name for internal operations
                    currentMonsterSelection = monster.OriginalName;
                    addGambitButton.Enabled = true;
                    deleteDisabledButton.Enabled = true;
                    
                    // Deselect any items in the list
                    foreach (ListViewItem item in gambitListView.Items)
                        item.Selected = false;
                }
            };
            monsterActionsPanel.Controls.Add(monsterSelector);

            // TableLayoutPanel for right-aligned buttons
            var buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Height = 38,
                BackColor = Color.Transparent
            };

            // Create buttons for actions (now in the button panel)
            addGambitButton = new Button
            {
                Text = "Add Gambit",
                BackColor = Color.FromArgb(80, 120, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 28),
                Margin = new Padding(3),
                Enabled = false // Disabled until a monster is selected
            };
            addGambitButton.FlatAppearance.BorderSize = 0;
            addGambitButton.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(currentMonsterSelection))
                    AddNewGambit(currentMonsterSelection);
            };
            
            deleteDisabledButton = new Button
            {
                Text = "Delete Disabled",
                BackColor = Color.FromArgb(120, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 28),
                Margin = new Padding(3),
                Enabled = false // Disabled until a monster is selected
            };
            deleteDisabledButton.FlatAppearance.BorderSize = 0;
            deleteDisabledButton.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(currentMonsterSelection))
                    DeleteDisabledGambits(currentMonsterSelection);
            };

            // Add buttons to panel in right-to-left order
            buttonPanel.Controls.Add(deleteDisabledButton, 1, 0);
            buttonPanel.Controls.Add(addGambitButton, 0, 0);
            
            monsterActionsPanel.Controls.Add(buttonPanel);

            // Add click handler to the monster action panel
            monsterActionsPanel.Click += (s, e) => {
                DeselectAllItems();
            };

            return monsterActionsPanel;
        }

        private void ListViewItemSelectionHandler(object sender, MouseEventArgs e)
        {
            // First check if we're clicking in a header area
            foreach (ListViewGroup group in gambitListView.Groups)
            {
                ListViewItem firstGroupItem = null;
                
                // Find first item in this group
                foreach (ListViewItem item in group.Items)
                {
                    firstGroupItem = item;
                    break;
                }
                
                if (firstGroupItem != null)
                {
                    // Calculate where the header would be
                    int headerTop = firstGroupItem.Bounds.Top - 28;
                    if (headerTop >= 0)
                    {
                        var headerRect = new Rectangle(
                            0, headerTop, gambitListView.Width, 28);
                        
                        if (headerRect.Contains(e.Location))
                        {
                            // If we clicked on a header, update dropdown but prevent item selection
                            // Find the corresponding monster in the dropdown
                            for (int i = 0; i < monsterSelector.Items.Count; i++)
                            {
                                if (monsterSelector.Items[i] is MonsterItem monster && 
                                    monster.OriginalName == group.Header)
                                {
                                    monsterSelector.SelectedIndex = i;
                                    break;
                                }
                            }
                            
                            // Cancel the default selection behavior
                            return;
                        }
                    }
                }
            }
            
            // If not a header click, proceed with normal selection behavior
            var hitInfo = gambitListView.HitTest(e.X, e.Y);
            
            if (hitInfo.Item != null)
            {
                // Check if we're clicking on an already selected item
                if (hitInfo.Item.Selected)
                {
                    // Deselect if already selected
                    hitInfo.Item.Selected = false;
                    return;
                }
                
                // Otherwise, select this item and deselect others
                foreach (ListViewItem item in gambitListView.Items)
                {
                    item.Selected = (item == hitInfo.Item);
                }
                
                // Update dropdown to match the group
                if (hitInfo.Item.Group != null)
                {
                    string groupName = hitInfo.Item.Group.Header;
                    
                    // Find and select the appropriate item in the dropdown
                    for (int i = 0; i < monsterSelector.Items.Count; i++)
                    {
                        if (monsterSelector.Items[i] is MonsterItem monster && 
                            monster.OriginalName == groupName)
                        {
                            monsterSelector.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Clicked in empty space - deselect all items
                foreach (ListViewItem item in gambitListView.Items)
                {
                    item.Selected = false;
                }
            }
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
                Margin = Padding = new Padding(0),
                HideSelection = false,
                LabelEdit = false
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
            listView.DrawItem += (s, e) =>
            {
                // First, check if this is a group header and draw it nicely
                bool shouldDrawHeader = false;
                if (e.ItemIndex == 0) shouldDrawHeader = true;
                else if (e.ItemIndex > 0 && e.ItemIndex < listView.Items.Count)
                {
                    var prevItem = listView.Items[e.ItemIndex - 1];
                    shouldDrawHeader = (e.Item.Group != prevItem.Group);
                }

                if (shouldDrawHeader && e.Item.Group != null)
                {
                    var headerRect = new Rectangle(
                        e.Bounds.X, e.Bounds.Y - 28, e.Bounds.Width, 26);

                    if (headerRect.Y >= 0)
                    {
                        string groupName = e.Item.Group.Header;
                        // Format the displayed name but keep original in Group.Header
                        string displayName = FormatMonsterName(groupName);

                        // Draw header background
                        e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 80)), headerRect);

                        // Draw formatted group name with larger white font
                        using var groupFont = new Font("Segoe UI", 14, FontStyle.Bold);
                        e.Graphics.DrawString(
                            displayName,
                            groupFont,
                            Brushes.White,
                            headerRect.X + 10,
                            headerRect.Y + 2
                        );
                    }
                }

                // Handle alternating row colors
                if (e.Item?.Group != null)
                {
                    // Get index in group for proper alternating colors
                    int index = 0;
                    foreach (ListViewItem item in e.Item.Group.Items)
                    {
                        if (item == e.Item) break;
                        index++;
                    }

                    // Set color based on even/odd position in group
                    e.Item.BackColor = index % 2 == 0
                        ? Color.FromArgb(45, 45, 45)
                        : Color.FromArgb(55, 55, 55);
                }

                e.DrawDefault = true;
            };
            listView.DrawColumnHeader += (s, e) => DrawColumnHeader(e);
            listView.MouseClick += GambitListView_MouseClick;
            listView.ColumnWidthChanging += (s, e) => { e.Cancel = true; e.NewWidth = listView.Columns[e.ColumnIndex].Width; };
            listView.HandleCreated += (s, e) => {
                EnableDoubleBuffering(listView);
                Application.AddMessageFilter(new ScrollMessageFilter(listView));
            };
            listView.Resize += (s, e) => { AdjustColumnWidths(); listView.Invalidate(); };

            // Add group selection tracking
            listView.MouseDown += ListViewItemSelectionHandler;

            // Handle double-click on group header by focusing on the action panel
            listView.MouseDoubleClick += (s, e) =>
            {
                var info = listView.HitTest(e.X, e.Y);
                if (info.Item != null)
                {
                    string groupName = info.Item.Group.Header;
                    currentMonsterSelection = groupName;
                    
                    // Find and select the monster in the dropdown instead
                    for (int i = 0; i < monsterSelector.Items.Count; i++)
                    {
                        if (monsterSelector.Items[i] is MonsterItem monster && 
                            monster.OriginalName == groupName)
                        {
                            monsterSelector.SelectedIndex = i;
                            break;
                        }
                    }
                }
            };

            // Add this for better selection behavior
            listView.MultiSelect = false;

            // Add a handler for the ListView itself to support deselection on empty area clicks
            listView.Click += (s, e) => {
                var hitInfo = listView.HitTest(listView.PointToClient(Cursor.Position));
                if (hitInfo.Item == null)
                {
                    DeselectAllItems();
                }
            };

            return listView;
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
            // Only handle normal list item clicks
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
            else if (hitInfo.SubItem == item.SubItems[3])
            {
                ShowActionDropdown(item, hitInfo.SubItem.Bounds);
            }
        }

        private void ShowTimingEditor(ListViewItem item, Rectangle bounds)
        {
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            // Use a regular TextBox instead of NumericUpDown to remove arrows
            var timingEditor = new TextBox
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height,
                Text = gambit.Timing.ToString(),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                TextAlign = HorizontalAlignment.Center
            };

            // Select all text for easy replacement
            timingEditor.SelectAll();

            // Validate input to ensure only numbers are entered
            timingEditor.KeyPress += (s, e) => {
                // Allow digits, backspace, and delete
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != 8 && e.KeyChar != 127)
                {
                    e.Handled = true;
                }
            };

            timingEditor.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
                {
                    SaveTimingValue();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    gambitListView.Controls.Remove(timingEditor);
                    gambitListView.Focus();
                }
            };

            timingEditor.LostFocus += (s, e) => {
                SaveTimingValue();
            };

            void SaveTimingValue()
            {
                if (int.TryParse(timingEditor.Text, out int value))
                {
                    // Enforce value constraints
                    value = Math.Max(0, Math.Min(999, value));

                    gambit.Timing = value;
                    item.SubItems[1].Text = value.ToString();
                    gambitListView.Controls.Remove(timingEditor);
                    SyncToJson();
                    
                    // Use DeselectAllItems instead of just setting focus
                    DeselectAllItems();
                }
                else
                {
                    // Revert to original value if parsing fails
                    timingEditor.Text = gambit.Timing.ToString();
                    timingEditor.SelectAll();
                }
            }

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
                DeselectAllItems();
            };

            dropdown.LostFocus += (s, e) => {
                gambitListView.Controls.Remove(dropdown);
                DeselectAllItems();
            };

            gambitListView.Controls.Add(dropdown);
            dropdown.Focus();
            dropdown.DroppedDown = true;
        }

        private void ShowActionDropdown(ListViewItem item, Rectangle bounds)
        {
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            // Create a custom ComboBox with better search handling
            var dropdown = new ComboBox
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Default // Explicitly set cursor
            };

            // Flag to prevent recursive TextChanged events
            bool updatingItems = false;

            // Use a timer to debounce text changes and help prevent cursor issues
            var filterTimer = new Timer { Interval = 150 };
            filterTimer.Tick += (s, e) => {
                filterTimer.Stop();
                if (dropdown.IsDisposed) return;

                try
                {
                    updatingItems = true;
                    string searchText = dropdown.Text.ToLower();

                    // Filter actions by ID or name
                    var filteredItems = actionDisplayMap.OrderBy(a => a.Key)
                        .Where(a => a.Key.ToString().Contains(searchText) ||
                                   a.Value.ToLower().Contains(searchText))
                        .Select(a => new ActionItem(a.Key, a.Value))
                        .ToArray();

                    // Save current state
                    int selectionStart = dropdown.SelectionStart;
                    string currentText = dropdown.Text;

                    // Suppress flicker
                    dropdown.BeginUpdate();

                    // Update items
                    dropdown.Items.Clear();
                    dropdown.Items.AddRange(filteredItems);

                    // Restore state
                    dropdown.Text = currentText;
                    dropdown.SelectionStart = selectionStart;

                    dropdown.EndUpdate();

                    // Ensure dropdown stays open
                    if (filteredItems.Length > 0)
                    {
                        dropdown.DroppedDown = true;
                        Cursor.Current = Cursors.Default; // Force cursor to remain visible
                    }
                }
                finally
                {
                    updatingItems = false;
                }
            };

            // Create a list of all actions for initial load
            var allActions = actionDisplayMap.OrderBy(a => a.Key)
                .Select(a => new ActionItem(a.Key, a.Value))
                .ToArray();

            // Initialize dropdown
            dropdown.Items.AddRange(allActions);

            // Select the current action if it exists
            if (actionDisplayMap.TryGetValue(gambit.ActionId, out string actionName))
            {
                for (int i = 0; i < dropdown.Items.Count; i++)
                {
                    if (((ActionItem)dropdown.Items[i]).Id == gambit.ActionId)
                    {
                        dropdown.SelectedIndex = i;
                        dropdown.Text = dropdown.Items[i].ToString();
                        break;
                    }
                }
            }

            // Use timer to debounce text changes
            dropdown.TextChanged += (s, e) => {
                if (updatingItems) return;
                filterTimer.Stop();
                filterTimer.Start();
            };

            dropdown.SelectionChangeCommitted += (s, e) => {
                if (dropdown.SelectedItem is ActionItem selectedAction)
                {
                    gambit.ActionId = selectedAction.Id;
                    gambit.Description = selectedAction.Name;
                    item.SubItems[3].Text = selectedAction.Name;

                    // Clean up resources
                    filterTimer.Stop();
                    filterTimer.Dispose();

                    gambitListView.Controls.Remove(dropdown);
                    SyncToJson();
                    DeselectAllItems();
                }
            };

            dropdown.LostFocus += (s, e) => {
                // Only remove if not actively selecting from dropdown
                if (!dropdown.DroppedDown)
                {
                    filterTimer.Stop();
                    filterTimer.Dispose();
                    gambitListView.Controls.Remove(dropdown);
                    DeselectAllItems();
                }
            };

            dropdown.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
                {
                    if (dropdown.SelectedItem is ActionItem selectedAction)
                    {
                        gambit.ActionId = selectedAction.Id;
                        gambit.Description = selectedAction.Name;
                        item.SubItems[3].Text = selectedAction.Name;
                    }
                    else if (dropdown.Items.Count > 0)
                    {
                        // Select first item if nothing specifically selected
                        var firstAction = (ActionItem)dropdown.Items[0];
                        gambit.ActionId = firstAction.Id;
                        gambit.Description = firstAction.Name;
                        item.SubItems[3].Text = firstAction.Name;
                    }

                    // Clean up
                    filterTimer.Stop();
                    filterTimer.Dispose();
                    gambitListView.Controls.Remove(dropdown);
                    SyncToJson();
                    DeselectAllItems();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    filterTimer.Stop();
                    filterTimer.Dispose();
                    gambitListView.Controls.Remove(dropdown);
                    DeselectAllItems();
                }
            };

            gambitListView.Controls.Add(dropdown);
            dropdown.Focus();
            dropdown.DroppedDown = true;
            Cursor.Current = Cursors.Default; // Ensure cursor is visible initially
        }

        // Helper class for dropdown items
        private class ActionItem
        {
            public int Id { get; }
            public string Name { get; }

            public ActionItem(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString() => $"{Id}: {Name}";
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
                
                // Reset UI state
                currentMonsterSelection = null;
                monsterSelector.Items.Clear();  // Clear the dropdown
                addGambitButton.Enabled = false;
                deleteDisabledButton.Enabled = false;

                var jsonDoc = JsonDocument.Parse(json);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext()) return;

                var rootType = rootEnumerator.Current.Name;
                var monstersElement = jsonDoc.RootElement.GetProperty(rootType);

                // Create a list to store monster names in the order they appear in JSON
                List<string> monsterNames = new List<string>();

                foreach (var monsterProperty in monstersElement.EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    monsterNames.Add(monsterName);  // Add to ordered list
                    JsonElement monsterElement = monsterProperty.Value;

                    var monsterGroup = new ListViewGroup(monsterName);
                    gambitListView.Groups.Add(monsterGroup);
                    monsterGroups[monsterName] = monsterGroup;

                    // Load gambits for this monster
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

                // Populate dropdown with monster names in original order
                foreach (var name in monsterNames)
                {
                    monsterSelector.Items.Add(new MonsterItem(name));
                }
                
                // Select the first monster if any exist
                if (monsterSelector.Items.Count > 0)
                {
                    monsterSelector.SelectedIndex = 0;
                }

                AdjustColumnWidths();
                gambitListView.Invalidate(); // Force redraw to position buttons
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

            // Reset UI state
            currentMonsterSelection = null;
            addGambitButton.Enabled = false;
            deleteDisabledButton.Enabled = false;
        }

        // Simplified ScrollMessageFilter that forces refresh when scrolling
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

        private void AddNewGambit(string groupName)
        {
            // Create a new default gambit
            var newGambit = new Gambit
            {
                Condition = "None",
                ActionId = 0,
                Timing = 0,
                Description = "New Action",
                ActionParam = 0,
                Enabled = true
            };

            gambits.Add(newGambit);

            // Create and add a new item to the ListView
            var item = new ListViewItem(new[] {
                "ON",
                "0",
                "No target",
                "New Action"
            });

            item.ImageIndex = 0; // Enabled icon
            item.Tag = newGambit;
            item.Group = monsterGroups[groupName];
            gambitListView.Items.Add(item);

            // Update the JSON
            SyncToJson();
        }

        private void DeleteDisabledGambits(string groupName)
        {
            // Make a separate list to avoid modifying collection during enumeration
            var itemsToRemove = new List<ListViewItem>();
            var gambitsToRemove = new List<Gambit>();

            foreach (ListViewItem item in gambitListView.Items)
            {
                if (item.Group?.Header == groupName && item.Tag is Gambit gambit && !gambit.Enabled)
                {
                    itemsToRemove.Add(item);
                    gambitsToRemove.Add(gambit);
                }
            }

            // Remove items if any were found
            if (itemsToRemove.Count > 0)
            {
                foreach (var item in itemsToRemove)
                {
                    gambitListView.Items.Remove(item);
                }

                foreach (var gambit in gambitsToRemove)
                {
                    gambits.Remove(gambit);
                }

                SyncToJson();
                gambitListView.Invalidate();
            }
        }

        // Add this class to store both original and display names for monsters
        private class MonsterItem
        {
            public string OriginalName { get; }
            public string DisplayName { get; }
            
            public MonsterItem(string original)
            {
                OriginalName = original;
                // Use the static version of FormatMonsterName
                DisplayName = FormatMonsterName(original);
            }
            
            public override string ToString() => DisplayName;
        }

        // Make this method static since it's used by MonsterItem
        private static string FormatMonsterName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName;
            
            // Insert spaces before capital letters (except the first one)
            var result = new System.Text.StringBuilder(rawName.Length * 2);
            result.Append(rawName[0]);
            
            for (int i = 1; i < rawName.Length; i++)
            {
                if (char.IsUpper(rawName[i]) && 
                    (i > 0 && !char.IsWhiteSpace(rawName[i - 1]) && char.IsLower(rawName[i - 1])))
                {
                    result.Append(' ');
                }
                result.Append(rawName[i]);
            }
            
            return result.ToString();
        }

        // Add this helper method to deselect all items - with improved implementation
        private void DeselectAllItems()
        {
            // Check if we need to do anything
            bool hasSelections = false;
            foreach (ListViewItem item in gambitListView.Items)
            {
                if (item.Selected)
                {
                    hasSelections = true;
                    break;
                }
            }
            
            if (!hasSelections)
                return;
                
            // Take focus away from ListView first
            this.Focus();
            
            // Temporarily suspend layout
            gambitListView.BeginUpdate();
            
            // Clear all selections using multiple approaches for reliability
            gambitListView.SelectedItems.Clear();
            
            foreach (ListViewItem item in gambitListView.Items)
            {
                item.Selected = false;
            }
            
            // Resume layout and force redraw
            gambitListView.EndUpdate();
            gambitListView.Invalidate();
            gambitListView.Update();
        }

        // Add this method to handle clicks on the panel
        private void Panel_Click(object sender, EventArgs e)
        {
            // Only handle clicks directly on the panel
            if (sender == this)
            {
                DeselectAllItems();
            }
        }

        // Add a public method to allow deselection from outside
        public void ClearSelection()
        {
            DeselectAllItems();
        }
    }
}