using System.Windows;

namespace NavisLegacyPlugin.UI
{
	public partial class InputDialog : Window
	{
		public string Result { get; private set; }

		public InputDialog(string initialValue, string fieldLabel)
		{
			InitializeComponent();

			Title = "Edit " + fieldLabel;

			InputBox.Text = initialValue ?? "";
			InputBox.Focus();
			InputBox.SelectAll();
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			Result = InputBox.Text;
			DialogResult = true;
			Close();
		}
	}
}