using System;
using System.IO;
using Verse;

namespace KryptonianGene
{
	public static class KryptonianDebug
	{
		private static readonly string LogFile = Path.Combine(GenFilePaths.SaveDataFolderPath, "KryptonianDebug.log");

		public static void Log(string message)
		{
			try
			{
				string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
				File.AppendAllText(LogFile, line + Environment.NewLine);
			}
			catch (Exception ex)
			{
				Verse.Log.Warning($"[KryptonianGene] Failed to write debug log: {ex}");
			}
		}
	}
}
