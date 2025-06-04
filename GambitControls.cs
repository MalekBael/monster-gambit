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
    // Define pack types matching the C++ implementation
    public enum GambitPackType
    {
        TimeLine,
        RuleSet
    }

    public class Gambit
    {
        public string Condition { get; set; }
        public int ActionId { get; set; }
        public int ActionParam { get; set; }
        public int Timing { get; set; } // Used for TimeLine packs
        public uint CoolDown { get; set; } = 0; // Used for RuleSet packs (milliseconds)
        public string Description { get; set; }
        public double? Radius { get; set; }
        public bool Enabled { get; set; } = true;
        public uint HPThreshold { get; set; } = 0; // Used for HP conditions
        public double AttackRange { get; set; } = 0; // New property
        public bool IsRanged { get; set; } = false; // New property
    }

    public class GambitPanel : Panel
    {
        private List<Gambit> gambits = new List<Gambit>();
        private ListView gambitListView;
        private MainForm parentForm;
        private Dictionary<string, ListViewGroup> monsterGroups = new Dictionary<string, ListViewGroup>();
        private ImageList statusIcons;
        private Dictionary<int, string> actionDisplayMap = new Dictionary<int, string>();

        // GambitPack configuration
        private GambitPackType currentPackType = GambitPackType.TimeLine;
        private int loopCount = 1; // Default: run once
        private bool infiniteLoop = false;

        // UI controls
        private Panel monsterActionsPanel;
        private Button addGambitButton;
        private Button deleteDisabledButton;
        private string currentMonsterSelection;
        private ComboBox monsterSelector;
        private NumericUpDown loopCountEditor;
        private CheckBox infiniteLoopCheckbox;
        private Panel extraParamsPanel;
        private NumericUpDown attackRangeEditor; // New control
        private CheckBox isRangedCheckbox; // New control

        // Add these field declarations to the GambitPanel class, not the Gambit class
        private TextBox baseIdEditor;
        private TextBox nameIdEditor;

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

            LoadActionData();

            var layoutPanel = CreateLayoutPanel();
            layoutPanel.Controls.Add(CreateHeaderPanel(), 0, 0);
            layoutPanel.Controls.Add(CreateMonsterActionsPanel(), 0, 1);
            layoutPanel.Controls.Add(CreateMonsterPropertiesPanel(), 0, 2);
            layoutPanel.Controls.Add(CreateGambitPackSettingsPanel(), 0, 3); // New panel for pack settings
            gambitListView = CreateGambitListView();
            layoutPanel.Controls.Add(gambitListView, 0, 4);
            Controls.Add(layoutPanel);

            this.Click += Panel_Click;
            gambitListView.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape)
                {
                    DeselectAllItems();
                }
            };

            this.HandleCreated += (s, e) => {
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
                        
                    // Update the attack range and isRanged controls for the selected monster
                    UpdateMonsterPropertyControls(monster.OriginalName);
                    
                    // Highlight the monster section in the JSON editor
                    HighlightMonsterInJson(monster.OriginalName);
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

            // Create buttons for actions
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

            // Add buttons to panel
            buttonPanel.Controls.Add(deleteDisabledButton, 1, 0);
            buttonPanel.Controls.Add(addGambitButton, 0, 0);

            monsterActionsPanel.Controls.Add(buttonPanel);

            // Add click handler
            monsterActionsPanel.Click += (s, e) => {
                DeselectAllItems();
            };

            return monsterActionsPanel;
        }

        // Create a panel for pack type and settings
        private Panel CreateGambitPackSettingsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 55),
                Padding = new Padding(5),
            };

            // Attack Range Label
            var rangeLabel = new Label
            {
                Text = "Attack Range:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 12),
            };
            panel.Controls.Add(rangeLabel);

            // Attack Range Editor
            var rangeEditor = new NumericUpDown
            {
                Location = new Point(95, 10),
                Width = 60,
                Minimum = 0,
                Maximum = 50,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Value = 0,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            rangeEditor.ValueChanged += (s, e) => {
                if (currentMonsterSelection != null)
                {
                    // Update all gambits for the current monster
                    foreach (ListViewItem item in gambitListView.Items)
                    {
                        if (item.Group?.Header == currentMonsterSelection && item.Tag is Gambit gambit)
                        {
                            gambit.AttackRange = (double)rangeEditor.Value;
                        }
                    }
                    SyncToJson();
                }
            };
            panel.Controls.Add(rangeEditor);

            // Is Ranged Checkbox
            var rangedCheckbox = new CheckBox  // Renamed from isRangedCheckbox to rangedCheckbox
            {
                Text = "Is Ranged",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(170, 11),
                AutoSize = true
            };
            rangedCheckbox.CheckedChanged += (s, e) => {
                if (currentMonsterSelection != null)
                {
                    // Update all gambits for the current monster
                    foreach (ListViewItem item in gambitListView.Items)
                    {
                        if (item.Group?.Header == currentMonsterSelection && item.Tag is Gambit gambit)
                        {
                            gambit.IsRanged = rangedCheckbox.Checked;  // Use renamed variable
                        }
                    }
                    SyncToJson();
                }
            };
            panel.Controls.Add(rangedCheckbox);

            // Store these controls as class members to access them elsewhere
            attackRangeEditor = rangeEditor;
            isRangedCheckbox = rangedCheckbox;  // Now correctly assigns local variable to class member

            // Loop count elements for the TimeLine functionality
            var loopPanel = new Panel
            {
                Location = new Point(290, 0),
                Width = 250,
                Height = panel.Height,
                BackColor = Color.Transparent
            };

            var loopLabel = new Label
            {
                Text = "Loop Count:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(5, 12)
            };
            loopPanel.Controls.Add(loopLabel);

            loopCountEditor = new NumericUpDown
            {
                Location = new Point(85, 10),
                Width = 60,
                Minimum = 1,
                Maximum = 100,
                Value = 1,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            loopCountEditor.ValueChanged += (s, e) => {
                loopCount = (int)loopCountEditor.Value;
                SyncToJson();
            };
            loopPanel.Controls.Add(loopCountEditor);

            infiniteLoopCheckbox = new CheckBox
            {
                Text = "Infinite Loop",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(155, 11),
                AutoSize = true
            };
            infiniteLoopCheckbox.CheckedChanged += (s, e) => {
                infiniteLoop = infiniteLoopCheckbox.Checked;
                loopCountEditor.Enabled = !infiniteLoop;
                SyncToJson();
            };
            loopPanel.Controls.Add(infiniteLoopCheckbox);

            panel.Controls.Add(loopPanel);
            extraParamsPanel = loopPanel;

            return panel;
        }

        // Create a new method in the GambitPanel class for the monster properties panel
        private Panel CreateMonsterPropertiesPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                BackColor = Color.FromArgb(45, 45, 55),
                Padding = new Padding(5),
            };

            // Base ID Label
            var baseIdLabel = new Label
            {
                Text = "Base ID:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 12),
            };
            panel.Controls.Add(baseIdLabel);

            // Base ID Editor
            baseIdEditor = new TextBox
            {
                Location = new Point(95, 10),
                Width = 60,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                TextAlign = HorizontalAlignment.Center
            };
            baseIdEditor.TextChanged += (s, e) => {
                if (currentMonsterSelection != null && int.TryParse(baseIdEditor.Text, out int baseId))
                {
                    SyncToJson();
                }
            };
            baseIdEditor.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                {
                    // Move focus to the panel on Enter or Escape
                    panel.Focus();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            panel.Controls.Add(baseIdEditor);

            // Name ID Label
            var nameIdLabel = new Label
            {
                Text = "Name ID:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(180, 12),
            };
            panel.Controls.Add(nameIdLabel);

            // Name ID Editor
            nameIdEditor = new TextBox
            {
                Location = new Point(265, 10),
                Width = 60,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                TextAlign = HorizontalAlignment.Center
            };
            nameIdEditor.TextChanged += (s, e) => {
                if (currentMonsterSelection != null && int.TryParse(nameIdEditor.Text, out int nameId))
                {
                    SyncToJson();
                }
            };
            nameIdEditor.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                {
                    // Move focus to the panel on Enter or Escape
                    panel.Focus();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            panel.Controls.Add(nameIdEditor);

            // Add click handler to panel to deselect text fields
            panel.Click += (s, e) => {
                panel.Focus();
            };

            return panel;
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
                // Draw group header
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
                        string displayName = FormatMonsterName(groupName);

                        // Draw header background
                        e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 80)), headerRect);

                        // Draw formatted group name
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

                // Use consistent grey color for all rows
                if (e.Item?.Group != null)
                {
                    e.Item.BackColor = Color.FromArgb(55, 55, 55);
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

            // Handle double-click
            listView.MouseDoubleClick += (s, e) =>
            {
                var info = listView.HitTest(e.X, e.Y);
                if (info.Item != null)
                {
                    string groupName = info.Item.Group.Header;
                    currentMonsterSelection = groupName;

                    // Find and select monster in dropdown
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

            listView.MultiSelect = false;

            // Add deselection for empty area clicks
            listView.Click += (s, e) => {
                var hitInfo = listView.HitTest(listView.PointToClient(Cursor.Position));
                if (hitInfo.Item == null)
                {
                    DeselectAllItems();
                }
            };

            return listView;
        }

        private void ListViewItemSelectionHandler(object sender, MouseEventArgs e)
        {
            // Check if clicking in a header area
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
                    // Calculate where header would be
                    int headerTop = firstGroupItem.Bounds.Top - 28;
                    if (headerTop >= 0)
                    {
                        var headerRect = new Rectangle(
                            0, headerTop, gambitListView.Width, 28);

                        if (headerRect.Contains(e.Location))
                        {
                            // Update dropdown but prevent item selection
                            for (int i = 0; i < monsterSelector.Items.Count; i++)
                            {
                                if (monsterSelector.Items[i] is MonsterItem monster &&
                                    monster.OriginalName == group.Header)
                                {
                                    monsterSelector.SelectedIndex = i;
                                    break;
                                }
                            }
                            return;
                        }
                    }
                }
            }

            // Normal selection behavior
            var hitInfo = gambitListView.HitTest(e.X, e.Y);

            if (hitInfo.Item != null)
            {
                // Deselect if already selected
                if (hitInfo.Item.Selected)
                {
                    hitInfo.Item.Selected = false;
                    return;
                }

                // Otherwise select this item and deselect others
                foreach (ListViewItem item in gambitListView.Items)
                {
                    item.Selected = (item == hitInfo.Item);
                }

                // Update dropdown to match group
                if (hitInfo.Item.Group != null)
                {
                    string groupName = hitInfo.Item.Group.Header;

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
                // Deselect all items
                foreach (ListViewItem item in gambitListView.Items)
                {
                    item.Selected = false;
                }
            }
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

        private TableLayoutPanel CreateLayoutPanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5, // Updated to 5 rows for the new panel
                ColumnCount = 1,
                Padding = Margin = new Padding(0),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Monster actions panel
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Monster properties panel
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Pack settings panel
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // ListView
            return layout;
        }

        // Update columns based on pack type
        private void UpdateListViewColumns(GambitPackType packType)
        {
            gambitListView.BeginUpdate();

            // Preserve existing columns
            string[] existingColumnTexts = new string[gambitListView.Columns.Count];
            int[] existingColumnWidths = new int[gambitListView.Columns.Count];

            for (int i = 0; i < gambitListView.Columns.Count; i++)
            {
                existingColumnTexts[i] = gambitListView.Columns[i].Text;
                existingColumnWidths[i] = gambitListView.Columns[i].Width;
            }

            gambitListView.Columns.Clear();

            // Re-add Status, Condition, Action columns
            gambitListView.Columns.Add("Status", existingColumnWidths[0]);

            // Add type-specific columns
            if (packType == GambitPackType.TimeLine)
            {
                gambitListView.Columns.Add("Timing", 60);
            }
            else // RuleSet
            {
                gambitListView.Columns.Add("Cooldown", 80);
            }

            gambitListView.Columns.Add("Condition", existingColumnWidths.Length > 2 ? existingColumnWidths[2] : 140);
            gambitListView.Columns.Add("Action", existingColumnWidths.Length > 3 ? existingColumnWidths[3] : 200);

            AdjustColumnWidths();
            gambitListView.EndUpdate();
            gambitListView.Invalidate();
        }

        // Adjust column widths based on listview width
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

        // Handle clicks on gambit rows
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
                if (currentPackType == GambitPackType.TimeLine)
                    ShowTimingEditor(item, hitInfo.SubItem.Bounds);
                else
                    ShowCooldownEditor(item, hitInfo.SubItem.Bounds);
            }
            else if (hitInfo.SubItem == item.SubItems[3])
            {
                ShowActionDropdown(item, hitInfo.SubItem.Bounds);
            }
        }

        // Modified to handle HP thresholds
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

                // If HP condition selected, show threshold editor
                if (gambit.Condition == "HPSelfPctLessThanTarget")
                {
                    gambitListView.Controls.Remove(dropdown);
                    ShowHPThresholdEditor(item, bounds, gambit);
                }
                else
                {
                    gambitListView.Controls.Remove(dropdown);
                    SyncToJson();
                    DeselectAllItems();
                }
            };

            dropdown.LostFocus += (s, e) => {
                if (!dropdown.DroppedDown)
                {
                    gambitListView.Controls.Remove(dropdown);
                    DeselectAllItems();
                }
            };

            gambitListView.Controls.Add(dropdown);
            dropdown.Focus();
            dropdown.DroppedDown = true;
        }

        // Show timing editor for TimeLine pack items
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

        // Show cooldown editor for RuleSet pack items
        private void ShowCooldownEditor(ListViewItem item, Rectangle bounds)
        {
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            var cooldownEditor = new TextBox
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height,
                Text = gambit.CoolDown.ToString(),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                TextAlign = HorizontalAlignment.Center
            };

            cooldownEditor.SelectAll();

            cooldownEditor.KeyPress += (s, e) => {
                // Allow digits, backspace, and delete
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != 8 && e.KeyChar != 127)
                {
                    e.Handled = true;
                }
            };

            cooldownEditor.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
                {
                    SaveCooldownValue();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    gambitListView.Controls.Remove(cooldownEditor);
                    gambitListView.Focus();
                }
            };

            cooldownEditor.LostFocus += (s, e) => {
                SaveCooldownValue();
            };

            void SaveCooldownValue()
            {
                if (uint.TryParse(cooldownEditor.Text, out uint value))
                {
                    gambit.CoolDown = value;
                    item.SubItems[1].Text = value.ToString();
                    gambitListView.Controls.Remove(cooldownEditor);
                    SyncToJson();
                    DeselectAllItems();
                }
                else
                {
                    cooldownEditor.Text = gambit.CoolDown.ToString();
                    cooldownEditor.SelectAll();
                }
            }

            gambitListView.Controls.Add(cooldownEditor);
            cooldownEditor.Focus();
        }

        // New method to show HP threshold editor
        private void ShowHPThresholdEditor(ListViewItem item, Rectangle bounds, Gambit gambit)
        {
            var thresholdPanel = new Panel
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height + 30, // Taller panel
                BackColor = Color.FromArgb(50, 50, 60),
                BorderStyle = BorderStyle.FixedSingle
            };

            var label = new Label
            {
                Text = "HP Threshold %:",
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(5, 5)
            };

            var editor = new NumericUpDown
            {
                Location = new Point(5, 25),
                Width = bounds.Width - 10,
                Minimum = 1,
                Maximum = 100,
                Value = gambit.HPThreshold > 0 ? gambit.HPThreshold : 50, // Default to 50%
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            var okButton = new Button
            {
                Text = "OK",
                BackColor = Color.FromArgb(80, 120, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(60, 25),
                Location = new Point(bounds.Width - 70, bounds.Height)
            };
            okButton.FlatAppearance.BorderSize = 0;
            okButton.Click += (s, e) => {
                gambit.HPThreshold = (uint)editor.Value;
                item.SubItems[2].Text = $"HP < {gambit.HPThreshold}%";
                gambitListView.Controls.Remove(thresholdPanel);
                SyncToJson();
                DeselectAllItems();
            };

            thresholdPanel.Controls.Add(label);
            thresholdPanel.Controls.Add(editor);
            thresholdPanel.Controls.Add(okButton);

            gambitListView.Controls.Add(thresholdPanel);
            editor.Focus();
        }

        private void ShowActionDropdown(ListViewItem item, Rectangle bounds)
        {
            var gambit = item.Tag as Gambit;
            if (gambit == null) return;

            var dropdown = new ComboBox
            {
                Location = bounds.Location,
                Width = bounds.Width,
                Height = bounds.Height,
                DropDownStyle = ComboBoxStyle.DropDown,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Default
            };

            bool updatingItems = false;
            var filterTimer = new Timer { Interval = 150 };
            filterTimer.Tick += (s, e) => {
                filterTimer.Stop();
                if (dropdown.IsDisposed) return;

                try
                {
                    updatingItems = true;
                    string searchText = dropdown.Text.ToLower();

                    var filteredItems = actionDisplayMap.OrderBy(a => a.Key)
                        .Where(a => a.Key.ToString().Contains(searchText) ||
                                   a.Value.ToLower().Contains(searchText))
                        .Select(a => new ActionItem(a.Key, a.Value))
                        .ToArray();

                    int selectionStart = dropdown.SelectionStart;
                    string currentText = dropdown.Text;

                    dropdown.BeginUpdate();
                    dropdown.Items.Clear();
                    dropdown.Items.AddRange(filteredItems);
                    dropdown.Text = currentText;
                    dropdown.SelectionStart = selectionStart;
                    dropdown.EndUpdate();

                    if (filteredItems.Length > 0)
                    {
                        dropdown.DroppedDown = true;
                        Cursor.Current = Cursors.Default;
                    }
                }
                finally
                {
                    updatingItems = false;
                }
            };

            var allActions = actionDisplayMap.OrderBy(a => a.Key)
                .Select(a => new ActionItem(a.Key, a.Value))
                .ToArray();
            dropdown.Items.AddRange(allActions);

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
                    filterTimer.Stop();
                    filterTimer.Dispose();
                    gambitListView.Controls.Remove(dropdown);
                    SyncToJson();
                    DeselectAllItems();
                }
            };

            dropdown.LostFocus += (s, e) => {
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
                        var firstAction = (ActionItem)dropdown.Items[0];
                        gambit.ActionId = firstAction.Id;
                        gambit.Description = firstAction.Name;
                        item.SubItems[3].Text = firstAction.Name;
                    }

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
            Cursor.Current = Cursors.Default;
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

        // Add this class to store both original and display names for monsters
        private class MonsterItem
        {
            public string OriginalName { get; }
            public string DisplayName { get; }

            public MonsterItem(string original)
            {
                OriginalName = original;
                DisplayName = FormatMonsterName(original);
            }

            public override string ToString() => DisplayName;
        }

        // Format monster names by adding spaces between camel case words
        private static string FormatMonsterName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName;

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

                currentMonsterSelection = null;
                monsterSelector.Items.Clear();
                addGambitButton.Enabled = false;
                deleteDisabledButton.Enabled = false;

                if (string.IsNullOrWhiteSpace(json))
                {
                    MessageBox.Show("Cannot load empty JSON", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var jsonDoc = JsonDocument.Parse(json);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext()) 
                {
                    MessageBox.Show("JSON has no root object", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var rootType = rootEnumerator.Current.Name;
                
                // Check if monsters element exists
                if (!jsonDoc.RootElement.TryGetProperty(rootType, out var monstersElement))
                {
                    MessageBox.Show($"JSON missing '{rootType}' element", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                List<string> monsterNames = new List<string>();

                foreach (var monsterProperty in monstersElement.EnumerateObject())
                {
                    try
                    {
                        string monsterName = monsterProperty.Name;
                        monsterNames.Add(monsterName);
                        JsonElement monsterElement = monsterProperty.Value;

                        var monsterGroup = new ListViewGroup(monsterName);
                        gambitListView.Groups.Add(monsterGroup);
                        monsterGroups[monsterName] = monsterGroup;

                        // Get monster-level properties
                        double attackRange = 0;
                        bool isRanged = false;

                        // Read attackRange from monster level
                        if (monsterElement.TryGetProperty("attackRange", out var attackRangeElement) &&
                            attackRangeElement.ValueKind == JsonValueKind.Number)
                        {
                            attackRange = attackRangeElement.GetDouble();
                        }

                        // Read isRanged from monster level
                        if (monsterElement.TryGetProperty("isRanged", out var isRangedElement) &&
                            (isRangedElement.ValueKind == JsonValueKind.True || isRangedElement.ValueKind == JsonValueKind.False))
                        {
                            isRanged = isRangedElement.GetBoolean();
                        }

                        // Check if gambitPack exists
                        if (!monsterElement.TryGetProperty("gambitPack", out var gambitPackElement))
                        {
                            MessageBox.Show($"Monster '{monsterName}' missing 'gambitPack' element", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }

                        // Check for loop count with safer approach
                        if (gambitPackElement.TryGetProperty("loopCount", out var loopElement) &&
                            loopElement.ValueKind == JsonValueKind.Number)
                        {
                            int loadedLoopCount = loopElement.GetInt32();
                            if (loadedLoopCount == -1)
                            {
                                infiniteLoop = true;
                                infiniteLoopCheckbox.Checked = true;
                            }
                            else
                            {
                                loopCount = loadedLoopCount;
                                loopCountEditor.Value = Math.Max(loopCountEditor.Minimum, 
                                                      Math.Min(loopCountEditor.Maximum, loopCount));
                            }
                        }

                        // Check if timeLines exists
                        if (!gambitPackElement.TryGetProperty("timeLines", out var timeLines) ||
                            timeLines.ValueKind != JsonValueKind.Array)
                        {
                            MessageBox.Show($"Monster '{monsterName}' missing 'timeLines' array", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }

                        // Load gambits for this monster
                        foreach (var timeline in timeLines.EnumerateArray())
                        {
                            try
                            {
                                bool isDisabled = timeline.TryGetProperty("originalCondition", out var _);
                                
                                // Get condition safely
                                string condition;
                                if (isDisabled)
                                {
                                    condition = timeline.TryGetProperty("originalCondition", out var conditionElement) && 
                                               conditionElement.ValueKind == JsonValueKind.String ?
                                               conditionElement.GetString() : "None";
                                }
                                else
                                {
                                    condition = timeline.TryGetProperty("condition", out var conditionElement) && 
                                               conditionElement.ValueKind == JsonValueKind.String ?
                                               conditionElement.GetString() : "None";
                                }

                                // Create gambit with safe property access
                                var gambit = new Gambit
                                {
                                    Condition = condition,
                                    ActionId = timeline.TryGetProperty("actionId", out var actionId) ? actionId.GetInt32() : 0,
                                    Timing = timeline.TryGetProperty("timing", out var timing) ? timing.GetInt32() : 0,
                                    Description = timeline.TryGetProperty("description", out var desc) && 
                                                 desc.ValueKind == JsonValueKind.String ? 
                                                 desc.GetString() : "Unknown Action",
                                    ActionParam = timeline.TryGetProperty("actionParam", out var actionParam) ? 
                                                 actionParam.GetInt32() : 0,
                                    Enabled = !isDisabled,
                                    
                                    // Use monster-level properties
                                    AttackRange = attackRange,
                                    IsRanged = isRanged
                                };

                                // Load optional properties
                                if (timeline.TryGetProperty("radius", out var radius) && 
                                    radius.ValueKind == JsonValueKind.Number)
                                {
                                    gambit.Radius = radius.GetDouble();
                                }

                                if (timeline.TryGetProperty("hpThreshold", out var hpThreshold) &&
                                    hpThreshold.ValueKind == JsonValueKind.Number)
                                {
                                    gambit.HPThreshold = (uint)hpThreshold.GetInt32();
                                }

                                if (timeline.TryGetProperty("coolDown", out var coolDown) &&
                                    coolDown.ValueKind == JsonValueKind.Number)
                                {
                                    gambit.CoolDown = (uint)coolDown.GetInt32();
                                }

                                gambits.Add(gambit);

                                // Create display text that may include HP threshold
                                string conditionDisplayText = MapConditionToDisplay(gambit.Condition);
                                if (gambit.Condition == "HPSelfPctLessThanTarget" && gambit.HPThreshold > 0)
                                {
                                    conditionDisplayText = $"HP < {gambit.HPThreshold}%";
                                }

                                var item = new ListViewItem(new[] {
                                    gambit.Enabled ? "ON" : "OFF",
                                    gambit.Timing.ToString(),
                                    conditionDisplayText,
                                    gambit.Description
                                });

                                item.ImageIndex = gambit.Enabled ? 0 : 1;
                                item.Tag = gambit;
                                item.Group = monsterGroup;
                                gambitListView.Items.Add(item);
                            }
                            catch (Exception ex)
                            {
                                // Log the error but continue processing other timelines
                                Console.WriteLine($"Error processing timeline for {monsterName}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue processing other monsters
                        Console.WriteLine($"Error processing monster {monsterProperty.Name}: {ex.Message}");
                    }
                }

                // Populate dropdown with monster names
                foreach (var name in monsterNames)
                {
                    monsterSelector.Items.Add(new MonsterItem(name));
                }

                AdjustColumnWidths();
                gambitListView.Invalidate();
            }
            catch (JsonException jsonEx)
            {
                MessageBox.Show($"Error parsing JSON: {jsonEx.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading gambits: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SyncToJson()
        {
            try
            {
                if (parentForm == null) return;

                // Store the current selection to reapply highlighting later
                string monsterToHighlight = currentMonsterSelection;

                // Suspend highlighting during update to prevent flicker
                parentForm.SuspendHighlighting();

                string currentJson = parentForm.GetEditorText();
                var jsonDoc = JsonDocument.Parse(currentJson);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext()) return;

                var rootType = rootEnumerator.Current.Name;
                var jsonNode = JsonNode.Parse(currentJson);

                // Gather gambits by monster
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

                foreach (var monsterEntry in gambitsByMonster)
                {
                    string monsterName = monsterEntry.Key;
                    List<Gambit> monsterGambits = monsterEntry.Value;

                    // Skip if the monster doesn't exist in the JSON
                    if (jsonNode[rootType][monsterName] == null)
                        continue;

                    // Update baseId and nameId if this is the selected monster
                    if (monsterName == currentMonsterSelection &&
                        int.TryParse(baseIdEditor.Text, out int baseId) &&
                        int.TryParse(nameIdEditor.Text, out int nameId))
                    {
                        jsonNode[rootType][monsterName]["baseId"] = baseId;
                        jsonNode[rootType][monsterName]["nameId"] = nameId;
                    }

                    // Update attackRange and isRanged from the first gambit
                    if (monsterGambits.Count > 0)
                    {
                        var firstGambit = monsterGambits[0];
                        jsonNode[rootType][monsterName]["attackRange"] = firstGambit.AttackRange;
                        jsonNode[rootType][monsterName]["isRanged"] = firstGambit.IsRanged;
                    }

                    // Set loop count
                    jsonNode[rootType][monsterName]["gambitPack"]["loopCount"] = infiniteLoop ? -1 : loopCount;

                    // Remove type if it exists
                    if (jsonNode[rootType][monsterName]["gambitPack"]["type"] != null)
                    {
                        ((JsonObject)jsonNode[rootType][monsterName]["gambitPack"]).Remove("type");
                    }

                    // Create a new timeLines array
                    var timeLines = new JsonArray();

                    // Add all gambits for this monster
                    foreach (var gambit in monsterGambits)
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

                        // Add common properties
                        timeLineNode.Add("timing", gambit.Timing);
                        timeLineNode.Add("description", gambit.Description);
                        timeLineNode.Add("actionParam", gambit.ActionParam);

                        // Add pack-specific properties
                        if (currentPackType == GambitPackType.RuleSet)
                        {
                            timeLineNode.Add("coolDown", (int)gambit.CoolDown);
                        }

                        // Add condition-specific properties
                        if (gambit.Condition == "HPSelfPctLessThanTarget" && gambit.HPThreshold > 0)
                        {
                            timeLineNode.Add("hpThreshold", (int)gambit.HPThreshold);
                        }

                        if (gambit.Radius.HasValue)
                            timeLineNode.Add("radius", gambit.Radius.Value);

                        timeLines.Add(timeLineNode);
                    }

                    // Update the timeLines array in the JSON
                    jsonNode[rootType][monsterName]["gambitPack"]["timeLines"] = timeLines;
                }

                // Update the text in the editor
                string updatedJson = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                parentForm.SetEditorText(updatedJson);

                // After updating the JSON, reapply the highlighting
                if (!string.IsNullOrEmpty(monsterToHighlight))
                {
                    // Short delay to ensure the editor has updated
                    this.BeginInvoke(new Action(() => HighlightMonsterInJson(monsterToHighlight)));
                }

                // Resume highlighting
                parentForm.ResumeHighlighting();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating JSON: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddNewGambit(string groupName)
        {
            var newGambit = new Gambit
            {
                Condition = "None",
                ActionId = 0,
                Timing = 0,
                CoolDown = 0,
                Description = "New Action",
                ActionParam = 0,
                Enabled = true
            };

            gambits.Add(newGambit);

            var item = new ListViewItem(new[] {
                "ON",
                "0", // Timing or cooldown
                "No target",
                "New Action"
            });

            item.ImageIndex = 0;
            item.Tag = newGambit;
            item.Group = monsterGroups[groupName];
            gambitListView.Items.Add(item);

            SyncToJson();
        }

        private void DeleteDisabledGambits(string groupName)
        {
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

        private void DeselectAllItems()
        {
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

            this.Focus();
            gambitListView.BeginUpdate();
            gambitListView.SelectedItems.Clear();

            foreach (ListViewItem item in gambitListView.Items)
            {
                item.Selected = false;
            }

            gambitListView.EndUpdate();
            gambitListView.Invalidate();
            gambitListView.Update();
        }

        // Add this method to handle clicks on the panel
        private void Panel_Click(object sender, EventArgs e)
        {
            if (sender == this)
            {
                DeselectAllItems();
            }
        }

        // Scroll message filter for smooth scrolling
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

        public void ClearSelection()
        {
            DeselectAllItems();
        }

        // Add this method to the GambitPanel class
        public void ClearGambits()
        {
            gambitListView.Items.Clear();
            gambitListView.Groups.Clear();
            monsterGroups.Clear();
            gambits.Clear();

            // Reset UI state
            currentMonsterSelection = null;
            monsterSelector.Items.Clear();
            addGambitButton.Enabled = false;
            deleteDisabledButton.Enabled = false;

            // Reset pack type to default
            currentPackType = GambitPackType.TimeLine;
            //packTypeSelector.SelectedIndex = 0;

            // Reset loop values
            loopCount = 1;
            loopCountEditor.Value = 1;
            infiniteLoop = false;
            infiniteLoopCheckbox.Checked = false;

            // Make sure timeline settings are visible
            extraParamsPanel.Visible = true;

            // Update UI
            gambitListView.Invalidate();
        }

        // In the UpdateMonsterPropertyControls method:
        private void UpdateMonsterPropertyControls(string monsterName)
        {
            try
            {
                // Get the current JSON to read the IDs
                string json = parentForm.GetEditorText();
                var jsonDoc = JsonDocument.Parse(json);
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext()) return;

                var rootType = rootEnumerator.Current.Name;

                // Find the selected monster
                if (jsonDoc.RootElement.TryGetProperty(rootType, out var monstersElement) &&
                    monstersElement.TryGetProperty(monsterName, out var monsterElement))
                {
                    // Read baseId and nameId
                    int baseId = monsterElement.TryGetProperty("baseId", out var baseIdElement) ?
                        baseIdElement.GetInt32() : 0;
                    int nameId = monsterElement.TryGetProperty("nameId", out var nameIdElement) ?
                        nameIdElement.GetInt32() : 0;

                    // Update the text fields
                    baseIdEditor.Text = baseId.ToString();
                    nameIdEditor.Text = nameId.ToString();
                }

                // Find first gambit for this monster to get its properties
                var firstGambit = gambits.FirstOrDefault(g =>
                    gambitListView.Items.Cast<ListViewItem>()
                        .Any(item => item.Group?.Header == monsterName && item.Tag == g));

                if (firstGambit != null)
                {
                    // Update attack range and isRanged controls
                    decimal minValue = attackRangeEditor.Minimum;
                    decimal maxValue = attackRangeEditor.Maximum;
                    decimal attackRange = (decimal)firstGambit.AttackRange;

                    // Clamp value between min and max
                    decimal clampedValue = Math.Min(maxValue, Math.Max(minValue, attackRange));
                    attackRangeEditor.Value = clampedValue;

                    // Update checkbox
                    isRangedCheckbox.Checked = firstGambit.IsRanged;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating monster properties: {ex.Message}");
            }
        }

        // Add this new method to the GambitPanel class
        private void HighlightMonsterInJson(string monsterName)
        {
            if (parentForm == null) return;
            
            try
            {
                // Get the current JSON
                string json = parentForm.GetEditorText();
                
                // Find the monster section in the JSON
                string pattern = $"\"{monsterName}\"\\s*:\\s*\\{{";
                int startPos = -1;
                
                // Find the position of the monster's entry
                System.Text.RegularExpressions.Match match = 
                    System.Text.RegularExpressions.Regex.Match(json, pattern);
                
                if (match.Success)
                {
                    startPos = match.Index;
                    
                    // Find the end of the monster section (matching closing brace)
                    int braceCount = 1;
                    int endPos = match.Index + match.Length;
                    
                    while (braceCount > 0 && endPos < json.Length)
                    {
                        if (json[endPos] == '{') braceCount++;
                        else if (json[endPos] == '}') braceCount--;
                        endPos++;
                    }
                    
                    if (braceCount == 0)
                    {
                        // Request the main form to highlight this section
                        parentForm.HighlightJsonSection(startPos, endPos);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error highlighting monster in JSON: {ex.Message}");
            }
        }
    }
}