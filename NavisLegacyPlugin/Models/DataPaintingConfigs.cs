using System;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Models
{
	public class MappingConfig
	{
		public Dictionary<string, string> ColumnMap { get; set; }
		public string MatchColumn { get; set; }
	}

	public class LookupConfig
	{
		public string LookupTab { get; set; }
		public string LookupProperty { get; set; }
	}

	public class WriteConfig
	{
		public bool WriteToLeafItems { get; set; }
	}

	public class ProgressConfig
	{
		public IProgress<string> ProgressText { get; set; }
		public IProgress<int> ProgressPercent { get; set; }
	}
}