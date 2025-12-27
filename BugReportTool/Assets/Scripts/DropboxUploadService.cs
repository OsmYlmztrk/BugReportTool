using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Handles building Dropbox paths, sanitizing filenames, and performing
/// the HTTP requests to upload the compressed report.
/// </summary>
public class DropboxUploadService
{
    /// <summary>
    /// Uploads the given zipped report to Dropbox.
    /// </summary>
    /// <param name="accessToken">OAuth 2 access token generated for the target Dropbox account/app.</param>
    /// <param name="summary">
    /// A short user summary or title for the report. This is converted into a
    /// safe filename (for example "Player fell through floor" → "Player_fell_through_floor.zip").
    /// </param>
    /// <param name="fileData">The raw bytes of the .zip file that will be uploaded.</param>
    /// <param name="submitButton">
    /// Optional reference to the UI submit button. It is temporarily disabled
    /// while the upload is in progress so the user cannot submit multiple times.
    /// </param>
    public IEnumerator Upload(string accessToken, string summary, byte[] fileData, Button submitButton)
    {
        // Abort early if there is nothing to upload.
        if (fileData == null || fileData.Length == 0)
        {
            Debug.LogWarning("DropboxUploadService.Upload called with empty file data.");
            yield break;
        }

        // Temporarily disable the submit button so the user cannot
        // trigger multiple uploads at the same time.
        if (submitButton != null)
            submitButton.enabled = false;

        // Build a folder name based on the current date (e.g. 2025-12-27)
        // and a file name based on the report summary.
        DateTime now = DateTime.Now;
        string safeSubject = SanitizeForFilename(summary); // Turn user input into a safe filename base.
        string fileName = $"{safeSubject}.zip";          // Add .zip extension to indicate a compressed report.
        string folderName = now.ToString("yyyy-MM-dd");  // One folder per day on Dropbox.
        string dropboxFolderPath = "/" + folderName;     // Dropbox API expects paths that start with "/".
        string dropboxFilePath = dropboxFolderPath + "/" + fileName; // Full path to the file.

        // 1) Make sure the date folder exists on Dropbox.
        //    If the folder already exists, Dropbox returns 409 (conflict) which is not a real error here.
        using (var createReq = new UnityWebRequest("https://api.dropboxapi.com/2/files/create_folder_v2", "POST"))
        {
            // Dropbox create_folder_v2 endpoint expects a JSON body.
            // Example: {"path":"/2025-12-27","autorename":false}
            byte[] body = Encoding.UTF8.GetBytes("{\"path\":\"" + dropboxFolderPath + "\",\"autorename\":false}");
            createReq.uploadHandler = new UploadHandlerRaw(body);      // Sends the JSON body in the POST request.
            createReq.downloadHandler = new DownloadHandlerBuffer();   // Buffer to read the response text.

            // Authentication and content-type headers.
            createReq.SetRequestHeader("Authorization", "Bearer " + accessToken);
            createReq.SetRequestHeader("Content-Type", "application/json");

            // Send the web request and wait until it finishes.
            yield return createReq.SendWebRequest();

            // If the request failed and the status code is NOT 409 (folder already exists), log a warning.
            if (createReq.result != UnityWebRequest.Result.Success && createReq.responseCode != 409)
            {
                Debug.LogWarning(
                    "Create folder warning: " + createReq.error +
                    " | code: " + createReq.responseCode +
                    " | body: " + createReq.downloadHandler.text);
            }
        }

        // 2) Upload the file itself.
        // Dropbox-API-Arg header carries a JSON string that defines
        // the target path and other upload parameters.
        string EscapeForJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string argJson = "{\"path\":\"" + EscapeForJson(dropboxFilePath) +
                         "\",\"mode\":\"add\",\"autorename\":true,\"mute\":false}";

        using (var req = new UnityWebRequest("https://content.dropboxapi.com/2/files/upload", "POST"))
        {
            // The payload is raw bytes (zip file), so we use UploadHandlerRaw.
            req.uploadHandler = new UploadHandlerRaw(fileData);
            req.uploadHandler.contentType = "application/octet-stream"; // Binary content type.
            req.downloadHandler = new DownloadHandlerBuffer();           // To read the response.

            // For this content upload endpoint, Dropbox expects authentication
            // and upload parameters (path, mode, etc.) via headers.
            req.SetRequestHeader("Authorization", "Bearer " + accessToken);
            req.SetRequestHeader("Dropbox-API-Arg", argJson);
            req.SetRequestHeader("Content-Type", "application/octet-stream");

            // Start the upload and wait for completion.
            yield return req.SendWebRequest();

            // If the request failed, log a detailed error message,
            // otherwise log the response body for debugging/verification.
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "Dropbox upload error: " + req.error +
                    " | HTTP code: " + req.responseCode +
                    " | body: " + req.downloadHandler.text);
            }
            else
            {
                Debug.Log("Dropbox response: " + req.downloadHandler.text);
            }
        }

        // Re-enable the button once the upload is complete.
        if (submitButton != null)
            submitButton.enabled = true;
    }

    /// <summary>
    /// Converts a user-provided report title/summary into a safe file-name-friendly string.
    /// </summary>
    /// <param name="input">Raw user text (for example the report summary).</param>
    /// <returns>
    /// A non-empty file-name-safe string that contains only ASCII
    /// letters/digits and the characters . _ - and is at most 50
    /// characters long. If the cleaned value would be empty,
    /// returns "report" instead.
    /// </returns>
    public string SanitizeForFilename(string input)
    {
        // If the input is entirely empty/whitespace, immediately return a default.
        if (string.IsNullOrWhiteSpace(input)) return "report";

        // Trim leading and trailing whitespace.
        input = input.Trim();

        // Unicode normalization:
        // FormD splits characters into base letters + combining marks
        // (e.g. "é" → "e" + accent mark).
        string normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        // Skip characters whose Unicode category is NonSpacingMark (accents, etc.)
        // so we keep only the base letters.
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        // Normalize back to FormC (standard composed form).
        input = sb.ToString().Normalize(NormalizationForm.FormC);

        // Replace any kind of whitespace (space, tab, newline, etc.) with underscores.
        input = Regex.Replace(input, @"\s+", "_");

        // Remove everything except letters, digits, and . _ - so the
        // result is safe for most file systems and cloud providers.
        input = Regex.Replace(input, @"[^A-Za-z0-9._-]", "");

        // If everything got stripped out, fall back to a safe default name.
        if (string.IsNullOrEmpty(input)) input = "report";

        // Limit the result to 50 characters to avoid overly long filenames
        // that might cause issues on some platforms/UI.
        if (input.Length > 50) input = input.Substring(0, 50);

        return input;
    }
}
