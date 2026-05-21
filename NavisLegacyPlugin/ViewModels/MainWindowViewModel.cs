using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public GeometryCompareViewModel GeometryCompare { get; }

		public DataPaintingViewModel DataPainting { get; }

		public MainWindowViewModel(NavisworksContextService contextService)
		{
			GeometryCompare = new GeometryCompareViewModel(contextService);
			DataPainting = new DataPaintingViewModel(new ComPropertyWriteService());
		}

	}
}