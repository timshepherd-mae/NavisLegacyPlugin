using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public GeometryCompareViewModel GeometryCompare { get; }

		public MainWindowViewModel(NavisworksContextService contextService)
		{
			GeometryCompare = new GeometryCompareViewModel(contextService);
		}
	}
}