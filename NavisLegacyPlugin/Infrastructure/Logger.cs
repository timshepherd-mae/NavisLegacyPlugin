using System;
using System.IO;

namespace NavisLegacyPlugin
{
	internal static class Logger
	{
		private static readonly string LogFile =
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"NavisLegacyPlugin.log");

		public static void Info(string message)
		{
			Write("INFO", message);
		}

		public static void Error(string message, Exception ex)
		{
			Write("ERROR", $"{message} | {ex}");
		}

		private static void Write(string level, string message)
		{
			try
			{
				File.AppendAllText(
					LogFile,
					$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
			}
			catch
			{
				// Never throw from logging in an add-in
			}
		}
	}
}
