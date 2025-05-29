using FastColoredTextBoxNS;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace DualEditorApp
{
    public partial class MainForm : Form
    {
        private ToolStrip toolStrip;
        private ToolStripDropDownButton fileDropDown;
        private ToolStripMenuItem openMenuItem;
        private ToolStripMenuItem saveMenuItem;
        private ToolStripMenuItem exportMenuItem;
        private ToolStripButton searchButton;

        private SplitContainer splitContainer;
        private GambitPanel gambitPanel; // Left panel for settings
        private FastColoredTextBox editorRight; // Right panel for file display

        private OpenFileDialog openFileDialog;
        private SaveFileDialog saveFileDialog;

        private SearchForm searchForm;
        private int lastSearchIndex = -1;
        private string lastSearchText = "";

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
            this.Load += MainForm_Load;
        }

        private void SetupUI()
        {
            this.Text = "Monster Gambit Editor";
            this.Width = 1400;
            this.Height = 1000;
            this.StartPosition = FormStartPosition.CenterScreen;

            // ToolStrip
            toolStrip = new ToolStrip();
            fileDropDown = new ToolStripDropDownButton("File");

            openMenuItem = new ToolStripMenuItem("Open", null, OpenFile);
            saveMenuItem = new ToolStripMenuItem("Save", null, SaveFile);
            exportMenuItem = new ToolStripMenuItem("Export", null, ExportFile);

            fileDropDown.DropDownItems.Add(openMenuItem);
            fileDropDown.DropDownItems.Add(saveMenuItem);
            fileDropDown.DropDownItems.Add(exportMenuItem);
            toolStrip.Items.Add(fileDropDown);

            searchButton = new ToolStripButton("Search", null, SearchButton_Click);
            toolStrip.Items.Add(searchButton);

            this.Controls.Add(toolStrip);

            // Split view
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = this.Width / 2,
                IsSplitterFixed = true
            };

            splitContainer.SplitterMoving += (s, e) => splitContainer.SplitterDistance = splitContainer.Width / 2;
            splitContainer.SplitterMoved += (s, e) => splitContainer.SplitterDistance = splitContainer.Width / 2;

            // Left panel for settings (empty for now)
            gambitPanel = new GambitPanel(this)
            {
                Dock = DockStyle.Fill  // Ensure it fills the container
            };

            // Right editor: FastColoredTextBox
            editorRight = new FastColoredTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                BackColor = Color.White,
                ForeColor = Color.Black,
                WordWrap = false,
                ReadOnly = false,
                AutoIndent = true,
                Language = Language.Custom,
                CurrentLineColor = Color.Yellow,
                SelectionColor = Color.Blue,
                BracketsHighlightStrategy = BracketsHighlightStrategy.Strategy2
            };

            splitContainer.Panel1.Controls.Add(gambitPanel);
            splitContainer.Panel2.Controls.Add(editorRight);
            this.Controls.Add(splitContainer);

            // File dialogs
            openFileDialog = new OpenFileDialog
            {
                Filter = ".cpp /.json Files (*.cpp;*.json)|*.cpp;*.json|C++ Files (*.cpp)|*.cpp|JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };

            saveFileDialog = new SaveFileDialog
            {
                Filter = openFileDialog.Filter
            };
        }

        private void OpenFile(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // "Close" the previous file: reset the control
                    editorRight.Clear();
                    editorRight.ClearUndo();
                    editorRight.Language = Language.Custom;
                    editorRight.DescriptionFile = null;
                    editorRight.Selection.Start = new Place(0, 0);

                    // Now "open" the new file
                    string content = File.ReadAllText(openFileDialog.FileName);
                    editorRight.Text = content;

                    // Set syntax highlighting based on file extension
                    string ext = Path.GetExtension(openFileDialog.FileName).ToLowerInvariant();
                    if (ext == ".json")
                    {
                        editorRight.Language = Language.JSON;
                        gambitPanel.LoadGambits(content);
                    }
                    else
                    {
                        // For non-JSON files, clear the gambit panel
                        gambitPanel.ClearGambits();
                        
                        if (ext == ".cpp")
                            editorRight.Language = Language.Custom; // No built-in C++ highlighting
                        else
                            editorRight.Language = Language.Custom;
                    }

                    editorRight.Selection.Start = new Place(0, 0); // Move caret to start
                    editorRight.Refresh();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to open file: " + ex.Message);
                }
            }
        }

        private void SaveFile(object sender, EventArgs e)
        {
            // Ensure gambits are synced to JSON before saving
            gambitPanel.SyncToJson();

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, editorRight.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save file: " + ex.Message);
                }
            }
        }

        private void ExportFile(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, editorRight.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to export file: " + ex.Message);
                }
            }
        }

        private void CloseFile(object sender, EventArgs e)
        {
            editorRight.Clear();
            editorRight.ClearUndo();
            editorRight.Language = Language.Custom;
            editorRight.DescriptionFile = null;
            editorRight.Selection.Start = new Place(0, 0);
            editorRight.Refresh();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            splitContainer.SplitterDistance = splitContainer.Width / 2;
        }

        private void MainForm_Load_1(object sender, EventArgs e)
        {

        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            if (searchForm == null || searchForm.IsDisposed)
            {
                searchForm = new SearchForm();
                searchForm.SearchRequested += SearchInEditor;
            }
            searchForm.Owner = this;

            // Manually position the search form to the left of center
            var mainRect = this.Bounds;
            int offset = mainRect.Width / 10;
            searchForm.StartPosition = FormStartPosition.Manual;
            searchForm.Location = new Point(
                mainRect.Left + (mainRect.Width - searchForm.Width) / 2 - offset,
                mainRect.Top + (mainRect.Height - searchForm.Height) / 2);

            searchForm.Show();
            searchForm.BringToFront();
            searchForm.Focus();
        }

        private void SearchInEditor(string searchText, bool searchNext)
        {
            if (string.IsNullOrEmpty(searchText))
                return;

            string editorText = editorRight.Text;
            int startIndex = 0;

            if (lastSearchText == searchText && lastSearchIndex >= 0)
            {
                startIndex = searchNext
                    ? lastSearchIndex + 1
                    : Math.Max(0, lastSearchIndex - 1);
            }

            int foundIndex = -1;
            if (searchNext)
            {
                foundIndex = editorText.IndexOf(searchText, startIndex, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Search backwards
                if (startIndex > 0)
                    foundIndex = editorText.LastIndexOf(searchText, startIndex - 1, StringComparison.OrdinalIgnoreCase);
            }

            if (foundIndex >= 0)
            {
                editorRight.Selection.Start = editorRight.PositionToPlace(foundIndex);
                editorRight.Selection.End = editorRight.PositionToPlace(foundIndex + searchText.Length);
                editorRight.DoSelectionVisible();
                editorRight.Focus();
                lastSearchIndex = foundIndex;
                lastSearchText = searchText;
            }
            else
            {
                MessageBox.Show("Text not found.", "Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lastSearchIndex = -1;
            }
        }

        // Add these methods to your MainForm class
        public string GetEditorText()
        {
            return editorRight.Text;
        }

        public void SetEditorText(string text)
        {
            editorRight.Text = text;
        }

        // Connect gambits changes to the editor
        private void GambitControl_Changed(object sender, EventArgs e)
        {
            gambitPanel.SyncToJson();
        }
    }

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
        private List<GambitRowControl> gambitControls = new List<GambitRowControl>();
        private MainForm parentForm;

        public GambitPanel(MainForm parent)
        {
            parentForm = parent;
            this.AutoScroll = true;
            this.Padding = new Padding(10);
            this.BackColor = Color.FromArgb(40, 40, 40);
        }

        public void LoadGambits(string json)
        {
            try
            {
                // Clear existing controls
                this.Controls.Clear();
                gambitControls.Clear();
                gambits.Clear();

                // Create a top panel for the fixed header (non-scrolling)
                Panel headerPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 60, // Increased height for better spacing
                    BackColor = Color.FromArgb(40, 40, 40)
                };
                this.Controls.Add(headerPanel);

                // Add title with more padding
                Label titleLabel = new Label
                {
                    Text = "GAMBITS",
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Padding = new Padding(0, 10, 0, 0) // Add top padding
                };
                headerPanel.Controls.Add(titleLabel);

                // Create a panel that will contain all content and handle scrolling
                Panel contentPanel = new Panel
                {
                    AutoScroll = true,
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(40, 40, 40),
                    Padding = new Padding(0, 5, 0, 5)
                };
                this.Controls.Add(contentPanel);

                // Parse JSON
                var jsonDoc = JsonDocument.Parse(json);

                // Get the root element (sprites, golem)
                var rootEnumerator = jsonDoc.RootElement.EnumerateObject();
                if (!rootEnumerator.MoveNext())
                    return;

                var rootType = rootEnumerator.Current.Name; // "sprites" or "golem"
                var monstersElement = jsonDoc.RootElement.GetProperty(rootType);

                // Position tracker for adding controls sequentially
                int yPosition = 70; // Start a bit lower

                // Process each monster/sprite in the JSON
                foreach (var monsterProperty in monstersElement.EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    JsonElement monsterElement = monsterProperty.Value;

                    // Add monster header
                    Label monsterLabel = new Label
                    {
                        Text = monsterName,
                        Font = new Font("Segoe UI", 12, FontStyle.Bold),
                        ForeColor = Color.Yellow,
                        BackColor = Color.FromArgb(60, 60, 60),
                        Width = contentPanel.ClientSize.Width - 40, // Use ClientSize for accurate width
                        Height = 30,
                        Location = new Point(10, yPosition),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    contentPanel.Controls.Add(monsterLabel);
                    yPosition += monsterLabel.Height + 5;

                    // Get the gambit timelines for this monster
                    var timeLines = monsterElement.GetProperty("gambitPack").GetProperty("timeLines");

                    // Process each gambit for this monster
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

                        // Create and position gambit row control
                        var gambitRow = new GambitRowControl(gambit, this);
                        gambitRow.Location = new Point(20, yPosition);
                        gambitRow.Width = contentPanel.ClientSize.Width - 50;
                        contentPanel.Controls.Add(gambitRow);
                        gambitControls.Add(gambitRow);

                        // Inside the LoadGambits method, after creating each gambitRow:
                        gambitRow.GambitChanged += (s, e) => SyncToJson();

                        yPosition += gambitRow.Height + 3;
                    }

                    // Add space between monsters
                    yPosition += 15;
                }

                // Make sure the panel refreshes
                contentPanel.PerformLayout();
                contentPanel.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing gambits: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                // Update each monster's gambits
                int gambitIndex = 0;
                foreach (var monsterProperty in jsonDoc.RootElement.GetProperty(rootType).EnumerateObject())
                {
                    string monsterName = monsterProperty.Name;
                    var timeLinesCount = monsterProperty.Value.GetProperty("gambitPack").GetProperty("timeLines").GetArrayLength();

                    // Get the JsonArray for the current monster's timelines
                    var timeLines = jsonNode[rootType][monsterName]["gambitPack"]["timeLines"].AsArray();

                    // Update the gambits for this monster
                    for (int i = 0; i < timeLinesCount; i++)
                    {
                        if (gambitIndex < gambits.Count && gambitIndex < gambitControls.Count)
                        {
                            var gambit = gambits[gambitIndex];

                            if (!gambit.Enabled)
                            {
                                // Store original values for re-enabling later
                                if (!timeLines[i].AsObject().ContainsKey("originalCondition"))
                                {
                                    // Save original values
                                    timeLines[i]["originalCondition"] = timeLines[i]["condition"].GetValue<string>();
                                    timeLines[i]["originalActionId"] = timeLines[i]["actionId"].GetValue<int>();
                                    
                                    // Set actionId to 0 - this is likely to be an invalid action ID 
                                    // that the action manager won't be able to handle
                                    timeLines[i]["actionId"] = 0;
                                    
                                    // Also modify condition to ensure the target can't be found
                                    timeLines[i]["condition"] = "None";  // "None" is a valid condition but unlikely to have targets
                                }
                            }
                            else if (timeLines[i].AsObject().ContainsKey("originalCondition"))
                            {
                                // Restore the original values
                                timeLines[i]["condition"] = timeLines[i]["originalCondition"].GetValue<string>();
                                timeLines[i]["actionId"] = timeLines[i]["originalActionId"].GetValue<int>();
                                
                                // Remove our temp fields
                                timeLines[i].AsObject().Remove("originalCondition");
                                timeLines[i].AsObject().Remove("originalActionId");
                            }

                            gambitIndex++;
                        }
                    }
                }

                // Format the JSON with indentation
                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = jsonNode.ToJsonString(options);

                // Update the editor text with the new JSON
                parentForm.SetEditorText(updatedJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating JSON: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ClearGambits()
        {
            this.Controls.Clear();
            gambitControls.Clear();
            gambits.Clear();
        }
    }

    public class GambitRowControl : Panel
    {
        private Gambit gambit;
        private GambitPanel parentPanel;
        private CheckBox enabledCheck;
        private Label timingLabel;
        private Label conditionLabel;
        private Label actionLabel;

        public event EventHandler GambitChanged;

        public GambitRowControl(Gambit gambit, GambitPanel parent)
        {
            this.gambit = gambit;
            this.parentPanel = parent;
            this.Height = 35;
            this.BackColor = Color.FromArgb(50, 50, 50);  // Darker background
            this.BorderStyle = BorderStyle.FixedSingle;

            // Create layout
            enabledCheck = new CheckBox
            {
                Text = "ON",
                Checked = gambit.Enabled,
                Location = new Point(5, 8),
                Width = 50,
                ForeColor = Color.Yellow,
                FlatStyle = FlatStyle.Flat
            };
            enabledCheck.CheckedChanged += (s, e) =>
            {
                gambit.Enabled = enabledCheck.Checked;
                enabledCheck.Text = gambit.Enabled ? "ON" : "OFF";
                GambitChanged?.Invoke(this, EventArgs.Empty);
            };

            timingLabel = new Label
            {
                Text = gambit.Timing.ToString(),
                Location = new Point(60, 10),
                Width = 30,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };

            string conditionText = MapConditionToUI(gambit.Condition);
            conditionLabel = new Label
            {
                Text = conditionText,
                Location = new Point(100, 10),
                Width = 135,  // Slightly narrower to fit in panel
                ForeColor = Color.White,
                AutoEllipsis = true
            };

            actionLabel = new Label
            {
                Text = gambit.Description,
                Location = new Point(240, 10),  // Moved left slightly
                Width = 135,  // Adjusted width
                ForeColor = Color.LightGreen,
                AutoEllipsis = true
            };

            this.Controls.Add(enabledCheck);
            this.Controls.Add(timingLabel);
            this.Controls.Add(conditionLabel);
            this.Controls.Add(actionLabel);
        }

        private string MapConditionToUI(string jsonCondition)
        {
            // Map JSON conditions to UI-friendly text
            switch (jsonCondition)
            {
                case "None": return "No target";
                case "TopHateTarget": return "Enemy: Top Aggro";
                case "Self": return "Self";
                // Add more mappings as needed
                default: return jsonCondition;
            }
        }

        // Add methods to get/set gambit properties
        public bool IsEnabled => gambit.Enabled;
        public int ActionId => gambit.ActionId;
        public string Condition => gambit.Condition;
        public int Timing => gambit.Timing;
        public double? Radius => gambit.Radius;
        public int ActionParam => gambit.ActionParam;
        public string Description => gambit.Description;
    }
}