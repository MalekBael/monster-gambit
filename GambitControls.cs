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
                        ForeColor = Color.White,
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
                            var control = gambitControls[gambitIndex];

                            // Update the condition based on dropdown selection
                            if (timeLines[i].AsObject().ContainsKey("originalCondition"))
                            {
                                // For disabled gambits, just update the stored original condition
                                timeLines[i]["originalCondition"] = gambit.Condition;
                            }
                            else
                            {
                                // For enabled gambits, update the condition directly
                                timeLines[i]["condition"] = gambit.Condition;
                            }

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
        private ComboBox conditionDropdown; // Changed from Label to ComboBox
        private Label actionLabel;

        public event EventHandler GambitChanged;

        // Condition options for the dropdown
        private readonly string[] conditionOptions = new[]
        {
            "Self",
            "Player",
            "PlayerAndAlly",
            "Ally",
            "BNpc",
            "TopHateTarget",
            "HPSelfPctLessThanTarget",
            "None"
        };

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
                ForeColor = Color.White,
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

            // Replace the condition label with a dropdown
            conditionDropdown = new ComboBox
            {
                Location = new Point(100, 6),
                Width = 135,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Add the condition options to the dropdown
            foreach (var option in conditionOptions)
            {
                conditionDropdown.Items.Add(conditionDisplayMap.ContainsKey(option) ? 
                    conditionDisplayMap[option] : option);
            }

            // Set the initial selected value
            string displayText = MapConditionToUI(gambit.Condition);
            int index = conditionDropdown.Items.IndexOf(displayText);
            if (index >= 0)
                conditionDropdown.SelectedIndex = index;

            // Add event handler for dropdown changes
            conditionDropdown.SelectedIndexChanged += (s, e) =>
            {
                // Map the selected display text back to the actual condition value
                string selectedDisplay = conditionDropdown.SelectedItem.ToString();
                string actualCondition = MapUIToCondition(selectedDisplay);
                gambit.Condition = actualCondition;
                GambitChanged?.Invoke(this, EventArgs.Empty);
            };

            actionLabel = new Label
            {
                Text = gambit.Description,
                Location = new Point(240, 10),  // Moved left slightly
                Width = 135,  // Adjusted width
                ForeColor = Color.White,
                AutoEllipsis = true
            };

            this.Controls.Add(enabledCheck);
            this.Controls.Add(timingLabel);
            this.Controls.Add(conditionDropdown);
            this.Controls.Add(actionLabel);
        }

        private string MapConditionToUI(string jsonCondition)
        {
            // Map JSON conditions to UI-friendly text
            return conditionDisplayMap.ContainsKey(jsonCondition) ? 
                conditionDisplayMap[jsonCondition] : jsonCondition;
        }

        private string MapUIToCondition(string uiText)
        {
            // Map UI-friendly text back to JSON conditions
            foreach (var pair in conditionDisplayMap)
            {
                if (pair.Value == uiText)
                    return pair.Key;
            }
            return uiText;
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