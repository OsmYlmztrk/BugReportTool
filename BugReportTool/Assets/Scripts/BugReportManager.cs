using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// BugReportManager is responsible for collecting player bug reports, packaging logs,
// compressing them, and uploading the resulting archive to Dropbox.
public class BugReportManager : MonoBehaviour
{
    // Input field where the user enters a short summary of the bug.
    [SerializeField] private TMP_InputField m_SummaryField;
    // Input field where the user describes the bug in more detail.
    [SerializeField] private TMP_InputField m_DetailField;
    // Button that the user presses to submit the bug report.
    [SerializeField] private Button m_SubmitButton;

    // Dropbox API access token used to authenticate upload requests.
    // This is temporary and should ideally be provided by a secure backend.
    [SerializeField] private string m_DropboxAccesToken;

    // Service responsible for creating report files and compressed archives.
    private ReportFileService m_ReportFileService;
    // Service responsible for uploading compressed reports to Dropbox.
    private DropboxUploadService m_DropboxUploadService;

    // Unity lifecycle method called when the script instance is being loaded.
    void Start()
    {
        // Initialize helper services.
        m_ReportFileService = new ReportFileService();
        m_DropboxUploadService = new DropboxUploadService();

        // Remove any existing listeners from the submit button to avoid duplicate calls.
        m_SubmitButton.onClick.RemoveAllListeners();
        // Add a listener that triggers the bug-report sending process when the button is clicked.
        m_SubmitButton.onClick.AddListener(() => { Button_SendLogFile(); });
    }
    
    // Wrapper method called when the submit button is pressed.
    // This keeps all send-logic encapsulated in a separate method.
    void Button_SendLogFile()
    {
        // Start the process of preparing log files and sending them.
        PrepareAndSendFiles();
    }

    // High-level workflow to gather files, compress them, and start the upload.
    private void PrepareAndSendFiles()
    {
        // First, write the player's text report to a file.
        m_ReportFileService.WritePlayerReport(m_SummaryField.text, m_DetailField.text);
        // Then, create a temporary folder and copy relevant files into it.
        string tempFolder = m_ReportFileService.CreateReportFolder();
        // Compress the temporary folder into a single zip archive and get its path.
        string zipPath = m_ReportFileService.CompressReportFolder(tempFolder);

        // Load the compressed file contents as a byte array.
        var _reportByteData = m_ReportFileService.GetCompressedReportBytes(zipPath);
        // If the byte array is valid, proceed with uploading.
        if (_reportByteData != null)
        {
            // Start the upload to Dropbox using the prepared byte data.
            UploadCompressedFolder(_reportByteData);
        }
    }

    #region Upload Progress

    // Starts the coroutine that uploads the compressed report using the Dropbox service.
    private void UploadCompressedFolder(byte[] fileData)
    {
        if (m_DropboxUploadService == null)
            m_DropboxUploadService = new DropboxUploadService();

        // Start the coroutine that performs the actual HTTP calls to Dropbox.
        StartCoroutine(m_DropboxUploadService.Upload(m_DropboxAccesToken, m_SummaryField.text, fileData, m_SubmitButton));
    }
    #endregion
}
