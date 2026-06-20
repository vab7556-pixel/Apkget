using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace TcpServerApp.Services.Elite;

public static class ToolsRefinery
{
	public static async Task<bool> EnsureAndroid36JarExistsAsync(Action<string>? onLog = null)
	{
		try
		{
			string root = ToolLocator.Android36Root;
			string targetJar = ToolLocator.Android36JarPath;
			if (File.Exists(targetJar))
			{
				if (new FileInfo(targetJar).Length > 1048576)
				{
					onLog?.Invoke("Android 36 Jar already exists.");
					return true;
				}
				onLog?.Invoke("Found corrupted Jar. Re-smelting...");
				File.Delete(targetJar);
			}
			onLog?.Invoke("Smelting Android 36 Resources... This is a one-time process.");
			string[] foldersToInclude = new string[7] { "android", "com", "java", "javax", "jdk", "sun", "dalvik" };
			await Task.Run(delegate
			{
				using ZipArchive archive = ZipFile.Open(targetJar, ZipArchiveMode.Create);
				string[] array = foldersToInclude;
				foreach (string text in array)
				{
					string text2 = Path.Combine(root, text);
					if (Directory.Exists(text2))
					{
						onLog?.Invoke("Processing: " + text + "...");
						AddDirectoryToZip(archive, text2, root);
					}
				}
			});
			onLog?.Invoke("Smelting Complete! Android 36 is Ready.");
			return true;
		}
		catch (Exception ex)
		{
			onLog?.Invoke("Refinery Error: " + ex.Message);
			return false;
		}
	}

	private static void AddDirectoryToZip(ZipArchive archive, string sourceDir, string rootDir)
	{
		string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string entryName = Path.GetRelativePath(rootDir, text).Replace('\\', '/');
			archive.CreateEntryFromFile(text, entryName);
		}
	}
}
