using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Navisworks.Api;

using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.ViewModels
{
	public class GetGeometryPositionsViewModel : ViewModelBase, IDisposable
	{
		public GetGeometryPositionsViewModel(GeometryPositionService service)
		{
			_service = service;
			RunCommand = new RelayCommand(Run);

			SubscribeToSelection();
		}

		private void SubscribeToSelection()
		{
			var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
			if (doc != null)
				doc.CurrentSelection.Changed += OnNavisSelectionChanged;
		}

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

		private GeometryPositionRow _selectedRow;
		public GeometryPositionRow SelectedRow
		{
			get => _selectedRow;
			set
			{
				if (_selectedRow == value)
					return;

				_selectedRow = value;
				OnPropertyChanged();

				if (_isInternalSelectionChange)
					return;

				if (_selectedRow?.ModelItem != null)
				{
					_isInternalSelectionChange = true;

					var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
					if (doc != null)
					{
						doc.CurrentSelection.Clear();
						doc.CurrentSelection.Add(_selectedRow.ModelItem);
					}

					_isInternalSelectionChange = false;
				}
			}
		}
		private bool _isInternalSelectionChange;

		private void OnNavisSelectionChanged(object sender, EventArgs e)
		{
			if (_isInternalSelectionChange)
				return;

			var selection = Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems;

			if (selection.Count == 0)
				return;

			var selectedItem = selection[0];

			var matchingRow = Results.FirstOrDefault(r => r.ModelItem == selectedItem);

			if (matchingRow != null)
			{
				_isInternalSelectionChange = true;
				SelectedRow = matchingRow;
				_isInternalSelectionChange = false;
			}
		}

		public void Dispose()
		{
			if (Autodesk.Navisworks.Api.Application.ActiveDocument != null)
			{
				Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.Changed -= OnNavisSelectionChanged;
			}
		}

		public ICommand RunCommand { get; }


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
