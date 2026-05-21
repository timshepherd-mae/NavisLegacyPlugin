using System;
using System.Linq;
using System.Windows.Input;
using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.ViewModels
{
	public class DataPaintingViewModel : ViewModelBase
	{
		private readonly ComPropertyWriteService _writer;

		public ICommand WriteTestCommand { get; }

		private string _status = "Ready.";
		public string Status
		{
			get { return _status; }
			private set { _status = value; OnPropertyChanged(); }
		}

		// HARD-CODED TEST VALUES (replace these)
		private const string TestGuidString = "68bdb303-1e93-582a-b49e-938165be3a61";
		private const string TestTabName = "MAE";
		private const string TestPropName = "PaintTest";
		private const string TestPropValue = "Hello from Data Painting";

		public DataPaintingViewModel(ComPropertyWriteService writer)
		{
			_writer = writer;
			WriteTestCommand = new RelayCommand(WriteTest);
		}

		private void WriteTest()
		{
			try
			{
				Status = "Writing...";
				var guid = new Guid(TestGuidString);


				var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
				if (doc == null)
					throw new InvalidOperationException("No active Navisworks document.");

				var target = doc.CurrentSelection.SelectedItems
					.Cast<Autodesk.Navisworks.Api.ModelItem>()
					.FirstOrDefault(i => i.InstanceGuid == guid);

				if (target == null)
					throw new InvalidOperationException("Target GUID not found in current selection.");

				_writer.WriteUserDefinedProperty(target, TestTabName, TestPropName, TestPropValue);
				Status = "Write complete (check properties).";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}
	}
}