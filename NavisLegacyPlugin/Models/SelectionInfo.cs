namespace NavisLegacyPlugin
{
	public class SelectionInfo
	{
		public int SelectedItemCount { get; set; }

		public static SelectionInfo Empty =>
			new SelectionInfo { SelectedItemCount = 0 };
	}
}