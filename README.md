# BugReportTool

BugReportTool is a small Unity utility that helps you collect in-game bug reports from players and automatically upload them to Dropbox.

A simple Unity error reporting tool that collects player reports and Unity log files, compresses them into a ZIP, and uploads the result to Dropbox. This is not a production-ready tool; it is published only as a reference and a starting point for developers building their own reporting workflow.

It:

- Saves a short summary and a detailed description written by the player  
- Gathers Unity log files into a temporary folder  
- Compresses that folder into a `.zip` archive  
- Uploads the resulting zip file to your Dropbox account  

---

## Features

- In-game bug report UI (summary + detail text fields)
- Automatic inclusion of Unity log and report files:
  - `Player.log`
  - `Player-prev.log` (if present)
  - `PlayerReport.txt` (player-written report)
- Creates a `BugReportFile.zip` archive with all collected files
- Uploads the archive to Dropbox
  - Date-based folder structure in Dropbox: `/YYYY-MM-DD/`
  - File name based on the sanitized summary text
- Disables the submit button while the upload is in progress (to prevent double submits)

---

## Requirements

- Unity 2022.3.62f2 (or a compatible 2022.3 LTS version)
- The following Unity packages / external libraries:
  - TextMeshPro (official Unity package)
  - Unity.SharpZipLib (used to compress the folder into a zip)
- A Dropbox API Access Token (created for your Dropbox account)

---

## Setup

1. **Clone or download the project**
   - Clone this repository from GitHub.
   - Open it with Unity Hub.

2. **Open the scene**
   - Open the scene: `Assets/Scenes/BugReportPanel.unity`.

3. **Check the BugReportManager component**
   - In the `BugReportPanel` scene, locate the GameObject that hosts the bug report UI.  
     Make sure the `BugReportManager` script is attached.

4. **Assign UI references**
   - In the `BugReportManager` component:
     - `m_SummaryField` → `TMP_InputField` used for the short summary
     - `m_DetailField` → `TMP_InputField` used for the detailed description
     - `m_SubmitButton` → `Button` that sends the bug report
   - Drag and drop the corresponding UI elements from the hierarchy into these fields in the Inspector.

5. **Set the Dropbox Access Token**
   - In the `BugReportManager` component, set:
     - `m_DropboxAccesToken` to your Dropbox Access Token.
   - For production use:
     - Avoid hard-coding or exposing this token directly in the client.  
       Prefer a secure backend, configuration service, or encrypted storage.

---

## How It Works

1. **User fills out the form**
   - The player types a short summary and a detailed description.
   - The player clicks the submit button.

2. **Creating the text report**
   - `GetPlayerReportText()` writes `PlayerReport.txt` under `Application.persistentDataPath`.
   - The file content is:
     - First lines: summary  
     - Then: detailed description

3. **Preparing the report folder**
   - `CreateReportFolder()`:
     - Creates a temporary folder `BugReportTemp` under `Application.temporaryCachePath`.
     - If present, copies:
       - `Player.log` → `BugReportTemp/Player.log`
       - `Player-prev.log` → `BugReportTemp/PlayerReport.txt` (note: name collision, see below)
       - `PlayerReport.txt` → `BugReportTemp/PlayerReport.txt`

4. **Compressing the folder**
   - `CompressReportFolder(tempFolder)`:
     - Compresses `BugReportTemp` into  
       `Application.persistentDataPath/BugReportFile.zip`.
     - Deletes the temporary folder afterward.

5. **Uploading to Dropbox**
   - `UploadCompressedFolder(byte[])`:
     - Creates a date-based folder name: `yyyy-MM-dd`.
     - Sanitizes the summary text to build a safe file name:
       - If empty: falls back to `report`
       - Removes/normalizes special characters and whitespace
       - Truncates to max 50 characters
     - Final path format: `/YYYY-MM-DD/sanitized_summary.zip`
   - The `UploadToDropbox(...)` coroutine:
     1. Calls Dropbox `files/create_folder_v2` for `/YYYY-MM-DD`.  
        - If it returns 409, the folder already exists (this is fine).
     2. Uploads the contents of `BugReportFile.zip` via `files/upload`.
     3. Re-enables the submit button when finished.

---

## Notes

- **Dropbox Access Token security**
  - The sample stores the token directly in a serialized field on the MonoBehaviour.
  - For real-world projects, you should:
    - Never ship permanent tokens inside a public client build.
    - Prefer a secure backend that issues short-lived tokens or proxies the upload.

- **Log file locations**
  - On different platforms, Unity log files may live in different folders.
  - This project assumes `Player.log` and `Player-prev.log` are under `Application.persistentDataPath`.
  - Adjust `CreateReportFolder()` to match your actual log file locations if needed.

- **Potential improvements**
  - `Player-prev.log` is currently copied as `PlayerReport.txt`, which can overwrite the real report file.  
    It is safer to copy it as `Player-prev.log` instead.
  - Add a progress indicator or UI feedback for upload success/failure.
  - Show user-facing error messages when the upload fails (e.g., via a popup).

---

## Usage Scenario

- You add a "Report a Bug" button to your game.
- When the player clicks it:
  - You show or load the `BugReportPanel` scene.
- The player:
  - Writes a short summary (like a title).
  - Describes in detail what happened, expected vs actual behavior, steps to reproduce, etc.
  - Clicks the submit button.
- As a developer, you:
  - Open the date folder in Dropbox.
  - Download the zip file.
  - Inspect the player report, log files, and any other artifacts you decide to include in the future.

---

## Contributing

- Feel free to open GitHub Issues for bugs, feature requests, or questions.
- Before submitting a Pull Request:
  - Make sure the project compiles and the bug report flow works.
  - Add a short description and, if possible, screenshots or GIFs of UI changes.

---
