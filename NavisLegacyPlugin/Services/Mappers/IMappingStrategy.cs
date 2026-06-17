using System.Data;
using NavisLegacyPlugin.Helpers;
using NavisLegacyPlugin.Models;

namespace NavisLegacyPlugin.Services.Mappers
{
	public interface IMappingStrategy
	{
		PaintInstruction Map(DataRow row);
	}
}