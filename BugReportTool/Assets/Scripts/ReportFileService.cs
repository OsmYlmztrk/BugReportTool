using System.IO;
using Unity.SharpZipLib.Utils;
using UnityEngine;

/// <summary>
/// Handles creation of the player report text file, temporary report folder,
/// compression of that folder, and loading the compressed data as bytes.
/// </summary>
public class ReportFileService
{
    // Name of the text file that stores the player's written report
    private const string ReportFileName = "PlayerReport.txt";
    // Name of the compressed zip file that will contain all report data
    private const string ZipFileName = "BugReportFile.zip";
    // Name of the temporary folder used before compression
    private const string TempFolderName = "BugReportTemp";

    public void WritePlayerReport(string summary, string detail)
    {
        // Create full path for the player report text file
        string path = Path.Combine(Application.persistentDataPath, ReportFileName);
        // Combine summary and detailed description with a blank line between
        string content = summary + "\n\n" + detail;
        // Overwrite or create the report file with the given content
        File.WriteAllText(path, content);
    }

    public string CreateReportFolder()
    {
        // Create a temp folder path inside the application's temporary cache
        string tempFolder = Path.Combine(Application.temporaryCachePath, TempFolderName);

        // If a previous temp folder exists, remove it to start fresh
        if (Directory.Exists(tempFolder))
            Directory.Delete(tempFolder, true);

        // Create a new empty temp folder
        Directory.CreateDirectory(tempFolder);

        // Copy current player log file if it exists
        string playerLogPath = Path.Combine(Application.persistentDataPath, "Player.log");
        if (File.Exists(playerLogPath))
            File.Copy(playerLogPath, Path.Combine(tempFolder, "Player.log"), true);

        // Copy the player report file if it exists
        string reportPath = Path.Combine(Application.persistentDataPath, ReportFileName);
        if (File.Exists(reportPath))
            File.Copy(reportPath, Path.Combine(tempFolder, ReportFileName), true);

        // Copy the previous player log file, if any, and store it with a fixed name
        string playerPrevLogPath = Path.Combine(Application.persistentDataPath, "Player-prev.log");
        if (File.Exists(playerPrevLogPath))
            File.Copy(playerPrevLogPath, Path.Combine(tempFolder, "PlayerReport.txt"), true);

        return tempFolder;
    }

    public string CompressReportFolder(string tempFolder)
    {
        // Path where the resulting zip file will be stored
        string zipPath = Path.Combine(Application.persistentDataPath, ZipFileName);

        if (Directory.Exists(tempFolder))
        {
            // Compress the entire temp folder into a single zip file
            ZipUtility.CompressFolderToZip(zipPath, null, tempFolder);
            Debug.Log($"{tempFolder} folder compressed to: " + zipPath);
            // Remove the temporary folder after successful compression
            Directory.Delete(tempFolder, true);
        }
        else
        {
            Debug.LogWarning("Temp folder not found for compression: " + tempFolder);
        }

        return zipPath;
    }

    public byte[] GetCompressedReportBytes(string zipPath)
    {
        // Ensure the zip file exists before attempting to read it
        if (File.Exists(zipPath))
        {
            // Read entire zip file into a byte array for uploading or sending
            byte[] zipBytes = File.ReadAllBytes(zipPath);
            Debug.Log("Zip file converted to byte array, size: " + zipBytes.Length);
            return zipBytes;
        }
        else
        {
            Debug.LogWarning("Zip file not found: " + zipPath);
            return null;
        }
    }
}
