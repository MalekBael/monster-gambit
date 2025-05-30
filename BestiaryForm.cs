using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DualEditorApp
{
    public class BestiaryForm : Form
    {
        private readonly BestiaryService _bestiaryService;
        private List<MonsterInfo> _monsters;
        private List<MonsterInfo> _filteredMonsters;

        private TextBox searchBox;
        private ListView monsterListView;
        private Label statusLabel;
        private ProgressBar loadingProgress;

        // Sort tracking variables
        private int _currentSortColumn = 0;
        private SortOrder _currentSortOrder = SortOrder.Ascending;

        // Store parent form reference
        private Form _parentForm;

        public BestiaryForm(Form parentForm = null)
        {
            _bestiaryService = new BestiaryService();
            _monsters = new List<MonsterInfo>();
            _filteredMonsters = new List<MonsterInfo>();
            _parentForm = parentForm;

            InitializeComponent();
            SetSizeAndPosition();

            // Load bestiary data when form is fully shown
            this.Shown += async (s, e) =>
            {
                await LoadBestiaryAsync();
            };
        }

        private void SetSizeAndPosition()
        {
            // Set a more compact default size
            this.Size = new Size(800, 800);

            // If we have a parent form, match only position and state, not size
            if (_parentForm != null)
            {
                // Match the parent's window state (normal, maximized, etc.)
                this.WindowState = _parentForm.WindowState;

                // If the window isn't maximized, position it at the same parent location
                if (this.WindowState != FormWindowState.Maximized)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = _parentForm.Location;
                }
            }
            else
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }

        private void InitializeComponent()
        {
            // Basic form setup
            this.Text = "FFXIV Bestiary";
            this.MinimumSize = new Size(800, 600);
            this.Icon = _parentForm?.Icon;

            // Use standard system colors
            this.BackColor = SystemColors.Control;
            this.ForeColor = SystemColors.ControlText;

            // Create main layout
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = SystemColors.Control
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Search
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content/List view
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Status

            // Header
            Label headerLabel = new Label
            {
                Text = "FFXIV BESTIARY",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Search panel
            Panel searchPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 2, 5, 2)
            };

            Label searchLabel = new Label
            {
                Text = "Search:",
                AutoSize = true,
                Left = 5,
                Top = 5
            };

            searchBox = new TextBox
            {
                Left = 60,
                Top = 2,
                Width = 300,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };

            searchPanel.Controls.Add(searchLabel);
            searchPanel.Controls.Add(searchBox);

            // Create monster list view with extended columns
            monsterListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                Font = new Font("Segoe UI", 9),
                VirtualMode = false
            };

            // Add more detailed columns including the new fields
            monsterListView.Columns.Add("Name", 150);
            monsterListView.Columns.Add("BNpcNameId", 70);
            monsterListView.Columns.Add("BNpcBaseId", 70);
            monsterListView.Columns.Add("Level", 50);
            monsterListView.Columns.Add("Type", 100);
            monsterListView.Columns.Add("Family", 100);
            monsterListView.Columns.Add("Location", 150);
            monsterListView.Columns.Add("TerritoryId", 70);

            // Add column click handler for sorting
            monsterListView.ColumnClick += MonsterListView_ColumnClick;

            // Add context menu for right-click actions
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem viewWikiItem = new ToolStripMenuItem("View on Wiki");
            viewWikiItem.Click += (s, e) => OpenWikiForSelectedMonster();
            contextMenu.Items.Add(viewWikiItem);
            monsterListView.ContextMenuStrip = contextMenu;

            // Double-click to open wiki
            monsterListView.DoubleClick += (s, e) => OpenWikiForSelectedMonster();

            // Status panel
            Panel statusPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 25
            };

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0),
            };

            loadingProgress = new ProgressBar
            {
                Dock = DockStyle.Right,
                Width = 120, // Reduced from 150
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false
            };

            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(loadingProgress);

            // Add everything to the main layout
            mainLayout.Controls.Add(headerLabel, 0, 0);
            mainLayout.Controls.Add(searchPanel, 0, 1);
            mainLayout.Controls.Add(monsterListView, 0, 2);
            mainLayout.Controls.Add(statusPanel, 0, 3);

            this.Controls.Add(mainLayout);

            // Event handlers
            searchBox.TextChanged += SearchBox_TextChanged;
            this.Shown += (s, e) => searchBox.Focus();
        }

        private void MonsterListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // If the column clicked is already the sort column, toggle sort order
            if (e.Column == _currentSortColumn)
            {
                _currentSortOrder = _currentSortOrder == SortOrder.Ascending ?
                                    SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                // Set the new sort column and reset sort order to ascending
                _currentSortColumn = e.Column;
                _currentSortOrder = SortOrder.Ascending;
            }

            // Apply the sorting
            SortMonsters();
            UpdateMonsterList();

            // Update the sort indicator in the column header
            UpdateSortIndicator();
        }

        private void UpdateSortIndicator()
        {
            // Clear all column headers
            for (int i = 0; i < monsterListView.Columns.Count; i++)
            {
                string headerText = monsterListView.Columns[i].Text;

                // Remove any existing sort indicators
                headerText = headerText.Replace(" ▲", "").Replace(" ▼", "");

                // Add sort indicator to current sort column
                if (i == _currentSortColumn)
                {
                    headerText += _currentSortOrder == SortOrder.Ascending ? " ▲" : " ▼";
                }

                monsterListView.Columns[i].Text = headerText;
            }
        }

        private void SortMonsters()
        {
            // Sort based on current sort column and order
            switch (_currentSortColumn)
            {
                case 0: // Name
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.Name).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.Name).ToList();
                    break;
                case 1: // BNpcNameId
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.BNpcNameId).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.BNpcNameId).ToList();
                    break;
                case 2: // BNpcBaseId
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.BNpcBaseId).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.BNpcBaseId).ToList();
                    break;
                case 3: // Level
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => int.TryParse(m.Level, out int lvl) ? lvl : 0).ToList() :
                        _filteredMonsters.OrderByDescending(m => int.TryParse(m.Level, out int lvl) ? lvl : 0).ToList();
                    break;
                case 4: // Type
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.Type).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.Type).ToList();
                    break;
                case 5: // Family
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.Family).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.Family).ToList();
                    break;
                case 6: // Location
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.Location).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.Location).ToList();
                    break;
                case 7: // TerritoryId
                    _filteredMonsters = _currentSortOrder == SortOrder.Ascending ?
                        _filteredMonsters.OrderBy(m => m.TerritoryId).ToList() :
                        _filteredMonsters.OrderByDescending(m => m.TerritoryId).ToList();
                    break;
            }
        }

        private async Task LoadBestiaryAsync()
        {
            try
            {
                loadingProgress.Visible = true;
                statusLabel.Text = "Loading bestiary data...";

                var progress = new Progress<string>(status =>
                {
                    statusLabel.Text = status;
                });

                _monsters = await _bestiaryService.GetMonstersAsync(progress);
                _filteredMonsters = new List<MonsterInfo>(_monsters);

                // Apply initial sort (by name ascending)
                _currentSortColumn = 0;
                _currentSortOrder = SortOrder.Ascending;
                SortMonsters();

                UpdateMonsterList();
                UpdateSortIndicator();

                statusLabel.Text = $"Loaded {_monsters.Count} monsters";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading bestiary: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error loading bestiary";
            }
            finally
            {
                loadingProgress.Visible = false;
            }
        }

        private void UpdateMonsterList()
        {
            monsterListView.BeginUpdate();
            monsterListView.Items.Clear();

            var displayedMonsters = _filteredMonsters.Take(1000).ToList();

            foreach (var monster in displayedMonsters)
            {
                var item = new ListViewItem(monster.Name ?? "Unknown");
                item.SubItems.Add(monster.BNpcNameId.ToString());
                item.SubItems.Add(monster.BNpcBaseId.ToString());
                item.SubItems.Add(monster.Level ?? "");
                item.SubItems.Add(monster.Type ?? "");
                item.SubItems.Add(monster.Family ?? "");
                item.SubItems.Add(monster.Location ?? "");
                item.SubItems.Add(monster.TerritoryId ?? "");
                item.Tag = monster;

                monsterListView.Items.Add(item);
            }

            monsterListView.EndUpdate();

            // Auto-size columns for better display
            foreach (ColumnHeader column in monsterListView.Columns)
            {
                column.Width = -2; // -2 = auto-size to fit header and content
            }

            if (_filteredMonsters.Count > 1000)
                statusLabel.Text = $"Showing first 1000 of {_filteredMonsters.Count} matches";
            else
                statusLabel.Text = $"Found {_filteredMonsters.Count} monsters";
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string searchText = searchBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                _filteredMonsters = new List<MonsterInfo>(_monsters);
            }
            else
            {
                _filteredMonsters = _monsters.Where(m =>
                    (m.Name != null && m.Name.ToLower().Contains(searchText)) ||
                    (m.Type != null && m.Type.ToLower().Contains(searchText)) ||
                    (m.Family != null && m.Family.ToLower().Contains(searchText)) ||
                    (m.Location != null && m.Location.ToLower().Contains(searchText)) ||
                    m.BNpcNameId.ToString().Contains(searchText) ||
                    m.BNpcBaseId.ToString().Contains(searchText) ||
                    (m.TerritoryId != null && m.TerritoryId.ToLower().Contains(searchText))
                ).ToList();
            }

            // Apply current sorting to the filtered results
            SortMonsters();
            UpdateMonsterList();
        }

        private void OpenWikiForSelectedMonster()
        {
            if (monsterListView.SelectedItems.Count > 0)
            {
                var monster = (MonsterInfo)monsterListView.SelectedItems[0].Tag;

                // Build wiki URL
                string wikiUrl = $"https://ffxiv.gamerescape.com/wiki/{monster.Name.Replace(" ", "_")}";

                // Open in browser
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = wikiUrl,
                    UseShellExecute = true
                });
            }
        }
    }
}