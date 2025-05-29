using System;
using System.Windows.Forms;

namespace DualEditorApp
{
    public class SearchForm : Form
    {
        public event Action<string, bool> SearchRequested; // bool: true=next, false=prev

        private TextBox searchBox;
        private Button nextButton;
        private Button prevButton;

        public SearchForm()
        {
            this.Text = "Search";
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.CenterParent; // Center over parent
            this.Width = 300;
            this.Height = 100;

            searchBox = new TextBox { Left = 10, Top = 10, Width = 200 };
            nextButton = new Button { Text = "Next", Left = 215, Top = 8, Width = 60 };
            prevButton = new Button { Text = "Previous", Left = 215, Top = 38, Width = 60 };

            nextButton.Click += (s, e) => SearchRequested?.Invoke(searchBox.Text, true);
            prevButton.Click += (s, e) => SearchRequested?.Invoke(searchBox.Text, false);

            this.Controls.Add(searchBox);
            this.Controls.Add(nextButton);
            this.Controls.Add(prevButton);
        }

        public void SetSearchText(string text)
        {
            searchBox.Text = text;
        }

        public string GetSearchText() => searchBox.Text;
    }
}