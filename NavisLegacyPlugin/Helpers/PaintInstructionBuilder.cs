using System;
using System.Collections.Generic;

namespace NavisLegacyPlugin.Helpers
{
	public class PaintInstruction
	{
		public string MatchValue { get; set; }
		public Dictionary<string, Dictionary<string, string>> PropertiesByTab { get; set; }
	}

	public static class PaintInstructionBuilder
	{
		public static PaintInstruction Build(Dictionary<string, string> mapped, string matchKey)
		{
			if (!mapped.ContainsKey(matchKey))
				return null;

			var instruction = new PaintInstruction
			{
				MatchValue = mapped[matchKey],
				PropertiesByTab = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
			};

			foreach (var kvp in mapped)
			{
				if (string.Equals(kvp.Key, matchKey, StringComparison.OrdinalIgnoreCase))
					continue;

				var parts = kvp.Key.Split('.');
				if (parts.Length != 2)
					continue;

				var tab = parts[0];
				var prop = parts[1];

				if (!instruction.PropertiesByTab.ContainsKey(tab))
					instruction.PropertiesByTab[tab] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				instruction.PropertiesByTab[tab][prop] = kvp.Value;
			}

			return instruction;
		}
	}
}