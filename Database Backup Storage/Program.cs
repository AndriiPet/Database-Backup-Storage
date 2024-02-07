using System;
using System.IO;
using System.Net;
using File = System.IO.File;
using System.Buffers;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Threading.Tasks;


class Program
{
    static void Main()
    {
        //Connection and reading of a JSON file
        string jsonContent = "";
        string jsonFilePath = "./Config.json";
        try
        {
             jsonContent = File.ReadAllText(jsonFilePath);
        }
        catch(FileNotFoundException)
        {
            Console.WriteLine($"Помилка: Файл '{jsonFilePath}' не знайдено.");
            Environment.Exit(0);
        }
        serverConfig configData = JsonConvert.DeserializeObject<serverConfig>(jsonContent);


        
        

        //Аrray of all files on disk
        string[] allFiles = GetAllFiles(configData.filePath);
        DateTime currentDateAndTime = DateTime.Now;

        List<DateTime> creationDates = new List<DateTime>();

        foreach (string file in allFiles)
        {
            FileInfo fileInfo = new FileInfo(file);
            creationDates.Add(fileInfo.CreationTime);
        }

        List<int> timeDifference = new List<int>();

        foreach (DateTime date in creationDates)
        {
            timeDifference.Add((currentDateAndTime - date).Days);

        }


        Dictionary<string, int> fileTimeDictionary = new Dictionary<string, int>();
        for (int i = 0; i < allFiles.Length; i++)
        {
            fileTimeDictionary.Add(allFiles[i], timeDifference[i]);
        }

        //Calling functions to upload and delete files from an FTP server
        foreach (var pair in fileTimeDictionary)
        {
            //Checking files age
            if (pair.Value < configData.daysForUpdate)
            {
                // Checking for the existence of a folder
                if (!FtpDirectoryExists(configData.ftpServer, pair.Key.Replace(configData.filePath, ""), configData.userName, configData.password, configData.serverFolder))
                {
                    //Creating directory
                    FtpCreateDirectory(configData.ftpServer, pair.Key.Replace(configData.filePath, ""), configData.userName, configData.password, configData.serverFolder);
                }
                // Upload file
                UploadFileToFtp(configData.ftpServer, configData.userName, configData.password, (pair.Key).Replace(configData.filePath, ""), pair.Key, configData.serverFolder);
            }
        }
        //Deleting file
        DeleteFiles(configData.ftpServer, configData.serverFolder, configData.userName, configData.password, configData.fileAge);
    }

    //Finds all nested files in the specified folder.
    static string[] GetAllFiles(string folderPath)
    {
        try
        {
            string[] currentFolderFiles = Directory.GetFiles(folderPath);



            string[] subdirectories = Directory.GetDirectories(folderPath);
            foreach (string subdirectory in subdirectories)
            {

                string[] subdirectoryFiles = GetAllFiles(subdirectory);
                currentFolderFiles = currentFolderFiles.Concat(subdirectoryFiles).ToArray();
            }

            return currentFolderFiles;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return null;
        }
    }

    //Checking for the existence of a folder on the FTP server
    static bool FtpDirectoryExists(string ftpServer, string filePath, string username, string password, string serverFolder)
    {
        // Connecting to an FTP server
        string folderPath = filePath.Replace(Path.GetFileName(filePath), "");
        string url = $"{ftpServer}{serverFolder}/{folderPath.Replace("\\", "/")}/?dummy={Guid.NewGuid()}";//Disabling response caching
        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(url));
        request.Credentials = new NetworkCredential(username, password);
        request.Method = WebRequestMethods.Ftp.ListDirectory;

        try
        {

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                return true;
            }
        }
        catch (WebException ex)
        {
            FtpWebResponse response = (FtpWebResponse)ex.Response;
            if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
            {
                return false;
            }
            else
            {
                throw;
            }
        }
    }

    //Ceating folders on ftp server
    static void FtpCreateDirectory(string ftpServer, string filePath, string username, string password, string serverFolder)
    {
        try
        {
            // Connecting to an FTP server
            string folderPath = filePath.Replace(Path.GetFileName(filePath), "").Replace("\\", "/");
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri($"{ftpServer}{serverFolder}{folderPath}"));
            request.Credentials = new NetworkCredential(username, password);
            //Creating FTP directory
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                Console.WriteLine($"Папка створена.  {response.StatusCode}");
            }
        }
        catch(WebException ex)
        {
            Console.WriteLine($"FTP Error {ex.Message}");
            // Handle specific FTP errors here if needed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error {ex.Message}");
            // Handle other exceptions here if needed
        }
    }

    //Uploading a file to an FTP server
    static void UploadFileToFtp(string ftpServer, string userName, string password, string fileName, string localFilePath, string serverFolder)
    {
        try
        {
            // Generate a unique file name for the copy
            string uniqueFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            string tempFilePath = Path.Combine(Path.GetTempPath(), uniqueFileName);

            // Copy the original file to the temporary location
            File.Copy(localFilePath, tempFilePath, true);

            // Connecting to an FTP server
            string path = $"{ftpServer}{serverFolder}{uniqueFileName.Replace("\\", "/")}";
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(path);

            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.Credentials = new NetworkCredential(userName, password);

            // Read file content into byte array
            byte[] fileContents = File.ReadAllBytes(tempFilePath);

            using (Stream ftpStream = request.GetRequestStream())
            {
                ftpStream.Write(fileContents, 0, fileContents.Length);
            }

            Console.WriteLine("\nFile uploaded successfully.");

            // Delete temporary file
            File.Delete(tempFilePath);
        }
        catch (WebException ex)
        {
            Console.WriteLine($"FTP Error: {ex.Message}");
            // Handle specific FTP errors here if needed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            // Handle other exceptions here if needed
        }
    }







    static void DeleteFiles(string ftpServer, string ftpFolder, string ftpUsername, string ftpPassword, int fileAge)
    {
        try
        {
            // Connecting to an FTP server
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"{ftpServer}{ftpFolder}");
            request.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            request.Method = WebRequestMethods.Ftp.ListDirectory;

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                //Getting a list of files and folders.
                string[] filesAndFolders = reader.ReadToEnd().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                foreach (string fileOrFolder in filesAndFolders)
                {
                    string fullPath = $"{ftpFolder}/{fileOrFolder}";

                    if (fileOrFolder.Contains("."))
                    {
                        // File processing
                        DeleteFile(ftpServer, fullPath, ftpUsername, ftpPassword, fileAge);
                    }
                    else
                    {
                        // Folder processing (recursively
                        DeleteFiles(ftpServer, fullPath, ftpUsername, ftpPassword, fileAge);
                    }
                }
            }
        }
        catch(WebException ex)
        {
            Console.WriteLine($"FTP Error {ex.Message}");
            // Handle specific FTP errors here if needed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            // Handle other exceptions here if needed
        }
    }

    static void DeleteFile(string ftpServer, string filePath, string ftpUsername, string ftpPassword, int fileAge)
    {
        try
        {
            // Getting information about a file
            FtpWebRequest fileInfoRequest = (FtpWebRequest)WebRequest.Create($"{ftpServer}{filePath}");
            fileInfoRequest.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
            fileInfoRequest.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            FtpWebResponse fileInfoResponse = (FtpWebResponse)fileInfoRequest.GetResponse();
            DateTime lastModified = fileInfoResponse.LastModified;

            // Checking if the file is older than 10 days
            if (DateTime.Now.Subtract(lastModified).TotalDays > 10)
            {
                // Deleting file
                FtpWebRequest deleteRequest = (FtpWebRequest)WebRequest.Create($"{ftpServer}{filePath}");
                deleteRequest.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
                deleteRequest.Method = WebRequestMethods.Ftp.DeleteFile;
                deleteRequest.GetResponse();
                Console.WriteLine($"File {filePath} deleted.");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Deleting error: {ex.Message}");
        }
    }

}

public interface IServerConfig
{
    public string ftpServer { get; set; }
    public string userName { get; set; }
    public string password { get; set; }
    public string filePath { get; set; }
    public string serverFolder { get; set; }
    public int fileAge { get; set; }
    public int daysForUpdate { get; set; }

}
public class serverConfig : IServerConfig
{
    public string ftpServer { get; set; }
    public string userName { get; set; }
    public string password { get; set; }
    public string filePath { get; set; }
    public string serverFolder { get; set; }
    public int fileAge { get; set; }
    public int daysForUpdate { get; set; }
}