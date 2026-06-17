using System.Collections.Generic;
using System.Data;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Models;
using NavisLegacyPlugin.Services;

namespace NavisLegacyPlugin.Services.Mappers
{
	public class MappingConfigStrategy : IMappingStrategy
	{
		private readonly Dictionary<string, string> _columnMap;
		private readonly string _matchColumn;

		public MappingConfigStrategy(MappingConfig config)
		{
			_columnMap = config.ColumnMap;
			_matchColumn = config.MatchColumn;
		}

		public PaintInstruction Map(DataRow row)
		{
			var mapped = PropertyMappingHelper.MapRow(row, _columnMap);
			var instruction = PaintInstructionBuilder.Build(mapped, _matchColumn);

			return instruction;
		}
	}
}