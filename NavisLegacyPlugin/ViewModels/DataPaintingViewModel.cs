using System;
using System.Collections.Generic;
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


				var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "RID", DateTime.Now.ToString("HHmmss") },
					{ "RID_2", "SECOND_" + DateTime.Now.ToString("HHmmss") },
					{ "RID_3", "THIRD_" + DateTime.Now.ToString("HHmmss") }	
				};

				_writer.WriteToCurrentSelection("Synchro", props);

				Status = "Write complete.";
			}
			catch (Exception ex)
			{
				Status = "Failed: " + ex.Message;
			}
		}
	}
}