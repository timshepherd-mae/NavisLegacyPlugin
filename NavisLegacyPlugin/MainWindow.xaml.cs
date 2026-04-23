using System.Linq;
using System.Windows;
using Autodesk.Navisworks.Api;

namespace NavisLegacyPlugin
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			LoadNavisworksContext();
		}

		private void LoadNavisworksContext()
		{
			Document doc = Autodesk.Navisworks.Api.Application.ActiveDocument;

			if (doc == null)
			{
				StatusText.Text = "No active document.";
				return;
			}

			int selectionCount = doc.CurrentSelection.SelectedItems.Count;
			StatusText.Text = $"Active Document: {doc.Title}\nSelected Items: {selectionCount}";
		}

		private void Close_Click(object sender, RoutedEventArgs e) 
		{
			Close();
		}	
	}
}