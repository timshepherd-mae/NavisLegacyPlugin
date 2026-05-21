using NavisLegacyPlugin.Services;
using System.Windows.Input;

namespace NavisLegacyPlugin.ViewModels
{
	public class GeometryCompareViewModel : ViewModelBase
	{
		private readonly NavisworksContextService _contextService;

		public GetGeometryPositionsViewModel GeometryA { get; }
		public GetGeometryPositionsViewModel GeometryB { get; }

		public ICommand ActionOneCommand { get; }
		public ICommand ActionTwoCommand { get; }

		public GeometryCompareViewModel(NavisworksContextService contextService)
		{
			_contextService = contextService;

			var geometryService = new GeometryPositionService();
			GeometryA = new GetGeometryPositionsViewModel(geometryService);
			GeometryB = new GetGeometryPositionsViewModel(geometryService);

			ActionOneCommand = new RelayCommand(OnActionOne);
			ActionTwoCommand = new RelayCommand(OnActionTwo);
		}

		private void OnActionOne()
		{
			System.Diagnostics.Debug.WriteLine("Geometry Compare – Action One");
		}

		private void OnActionTwo()
		{
			System.Diagnostics.Debug.WriteLine("Geometry Compare – Action Two");
		}
	}
}