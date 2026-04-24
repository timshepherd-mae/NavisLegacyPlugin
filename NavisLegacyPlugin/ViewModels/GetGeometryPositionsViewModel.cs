using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.ViewModels
{
	public class GetGeometryPositionsViewModel : ViewModelBase
	{
		private readonly GeometryPositionService _service;

		public ObservableCollection<GeometryPositionRow> Results { get; }
			= new ObservableCollection<GeometryPositionRow>();

		private bool _includeSubObjects;
		public bool IncludeSubObjects
		{
			get => _includeSubObjects;
			set
			{
				_includeSubObjects = value;
				OnPropertyChanged();
			}
		}

		public ICommand RunCommand { get; }

		public GetGeometryPositionsViewModel(
			GeometryPositionService service)
		{
			_service = service;
			RunCommand = new RelayCommand(Run);
		}

		private void Run()
		{

			Results.Clear();

			var rows = _service.GetGeometryPositions(IncludeSubObjects);

			System.Diagnostics.Debug.WriteLine($"Geometry rows returned: {rows.Count}");

			foreach (var row in rows)
				Results.Add(row);
		}
	}
}
