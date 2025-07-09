using FastColoredTextBoxNS;
using System;
using System.Drawing;
using System.IO;
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
        private ToolStripButton bestiaryButton;

        private SplitContainer splitContainer;
        private GambitPanel gambitPanel; 
        private FastColoredTextBox editorRight;

        private OpenFileDialog openFileDialog;
        private SaveFileDialog saveFileDialog;

        private SearchForm searchForm;
        private int lastSearchIndex = -1;
        private string lastSearchText = "";

        // Fields to track current highlighting
        private TextStyle highlightStyle;
        private FastColoredTextBoxNS.Range highlightedRange; // Fully qualify Range
        private bool highlightingSuspended = false;

        // Add these field declarations for line-based highlighting
        private int highlightedStartLine = -1;
        private int highlightedEndLine = -1;

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
            this.Load += MainForm_Load;

            // Initialize highlight style - light blue background
            highlightStyle = new TextStyle(null, new SolidBrush(Color.FromArgb(30, 100, 180, 255)), FontStyle.Regular);
            
            // Add handler for line painting to create a more visible highlight
            editorRight.PaintLine += EditorRight_PaintLine;
        }

        private void SetupUI()
        {
            this.Text = "Monster Gambit Editor";
            this.Width = 1400;
            this.Height = 1000;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create a container for all content with proper layout
            TableLayoutPanel mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                RowStyles = {
            new RowStyle(SizeType.AutoSize), // Row 0: ToolStrip row
            new RowStyle(SizeType.Percent, 100) // Row 1: Content row (100%)
        },
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // ToolStrip - same as before
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

            bestiaryButton = new ToolStripButton("Bestiary", null, BestiaryButton_Click);
            toolStrip.Items.Add(bestiaryButton);

            // Add ToolStrip to the first row
            Panel toolStripContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Height = toolStrip.Height
            };
            toolStripContainer.Controls.Add(toolStrip);
            mainContainer.Controls.Add(toolStripContainer, 0, 0);

            // Split view in second row
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5,
                IsSplitterFixed = true,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            splitContainer.SplitterMoving += (s, e) => splitContainer.SplitterDistance = splitContainer.Width / 2;
            splitContainer.SplitterMoved += (s, e) => splitContainer.SplitterDistance = splitContainer.Width / 2;

            // Add panels to the splitcontainer
            gambitPanel = new GambitPanel(this)
            {
                Dock = DockStyle.Fill
            };

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

            // Add splitcontainer to second row of the main container
            mainContainer.Controls.Add(splitContainer, 0, 1);

            // Add the main container to the form
            this.Controls.Add(mainContainer);

            // File dialogs (unchanged)
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
            // Keep this simple line that works
            splitContainer.SplitterDistance = splitContainer.Width / 2;

            // Safer approach: use BeginInvoke to defer the column adjustment 
            // until after the main form's layout is complete
            this.BeginInvoke(new Action(() => {
                if (gambitPanel != null && gambitPanel.IsHandleCreated)
                {
                    try
                    {
                        gambitPanel.AdjustColumnWidths();
                    }
                    catch
                    {
                        // Silently handle any exceptions during layout
                    }
                }
            }));
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

        // Methods for the GambitPanel to interact with the editor
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

        private void BestiaryButton_Click(object sender, EventArgs e)
        {
            // Pass a reference to the main form
            var bestiaryForm = new BestiaryForm(this);
            bestiaryForm.Show();
        }

        public void SuspendHighlighting()
        {
            highlightingSuspended = true;
            ClearHighlighting();
        }

        public void ResumeHighlighting()
        {
            highlightingSuspended = false;
        }

        private void ClearHighlighting()
        {
            if (highlightedRange != null)
            {
                highlightedRange.ClearStyle(highlightStyle);
                highlightedStartLine = -1;
                highlightedEndLine = -1;
                editorRight.Invalidate();
                highlightedRange = null;
            }
        }

        public void HighlightJsonSection(int startPos, int endPos)
        {
            if (highlightingSuspended) return;
            
            try
            {
                // Clear previous highlighting
                ClearHighlighting();
                
                // Convert positions to Place objects
                var startPlace = editorRight.PositionToPlace(startPos);
                var endPlace = editorRight.PositionToPlace(endPos);
                
                // Store line range for the PaintLine handler
                highlightedStartLine = startPlace.iLine;
                highlightedEndLine = endPlace.iLine;
                
                // Create a range for the section
                highlightedRange = new FastColoredTextBoxNS.Range(editorRight, startPlace, endPlace);
                
                // Apply subtle text highlighting (optional)
                highlightedRange.SetStyle(highlightStyle);
                
                // Scroll to make the section visible
                editorRight.DoRangeVisible(highlightedRange, true);
                
                // Refresh the editor
                editorRight.Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error highlighting JSON section: {ex.Message}");
            }
        }

        // Add this method to draw line markers
        private void EditorRight_PaintLine(object sender, PaintLineEventArgs e)
        {
            // If we have valid highlight lines and this line is within that range
            if (highlightedStartLine >= 0 && highlightedEndLine >= 0 && 
                e.LineIndex >= highlightedStartLine && e.LineIndex <= highlightedEndLine)
            {
                // Draw a colored background for the whole line
                e.Graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(40, 100, 180, 255)), 
                    e.LineRect
                );
                
                // Add a distinctive marker in the left margin for better visibility
                e.Graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(200, 100, 150, 250)),
                    new Rectangle(0, e.LineRect.Top, 5, e.LineRect.Height)
                );
            }
        }
    }
}