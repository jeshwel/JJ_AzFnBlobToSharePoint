using JJ_AzFnBlobToSharePoint.Core.Contracts;
using JJ_AzFnBlobToSharePoint.Core.DataAccess;
using JJ_AzFnBlobToSharePoint.Core.Extensions;
using JJ_AzFnBlobToSharePoint.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;
using System;
using System.IO;
using System.Security;

namespace JJ_AzFnBlobToSharePoint.Core
{
    public class SharePointManager : ISharePointManager
    {
        private readonly ILogger _logger;
        private readonly string siteURL;
        private readonly string spUserName;
        private readonly string spPassWord;
        private readonly string libraryName;
        const int guidPrefixLength = 33;
        DEPActivityRepository depActivityRepository;

        public SharePointManager(ILogger logger)
        {
            _logger = logger;
            siteURL = Environment.GetEnvironmentVariable("CN_SharePointSiteUrl");
            spUserName = Environment.GetEnvironmentVariable("CN_SharePointAdminUser");
            spPassWord = Environment.GetEnvironmentVariable("CN_SharePointAdminPwd");
            libraryName = Environment.GetEnvironmentVariable("CN_SharePointLibraryName");
            depActivityRepository = new DEPActivityRepository();
        }

        public void CreateFolderIfNotExists(string FolderPath)
        {
            try
            {
                using (ClientContext spContext = new ClientContext(siteURL.Trim()))
                {
                    SetSPCredentials(spContext);

                    //Important!! Note: API has issues when trying to find existing Folder names with #, so as per discussion with
                    //business team it was decided to remove # from folder names.
                    FolderPath = FolderPath.Replace("#", "");
                    var spFolderEntity = GetSharePointFolderByServerRelativeUrl(spContext, FolderPath);

                    //Strip out the child directory path from parent path.
                    var newSubDirPath = spFolderEntity.CreateFolderInLibRoot ? FolderPath : FolderPath.Substring(spFolderEntity.ServerRelativeFolderUrl.Length);
                    if (!string.IsNullOrWhiteSpace(newSubDirPath))
                        CreateFolderInternal(spContext.Web, spFolderEntity.SPFolder, newSubDirPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"SharePoint Folder creation failed : " + ex.Message);
            }

        }

        private SharePointFolderEntity GetSharePointFolderByServerRelativeUrl(ClientContext spContext, string serverRelativeFolderUrl)
        {
            bool folderExists = spContext.TryGetFolderByServerRelativeUrl(libraryName, serverRelativeFolderUrl, out Folder result);
            if (!folderExists)
            {
                //Get parent dir path and check if it exists
                serverRelativeFolderUrl = serverRelativeFolderUrl.GetParentDirectoryPath();
                if (string.IsNullOrEmpty(serverRelativeFolderUrl))
                {
                    //Note: Library folder should always exists, we are not creating lib folder dynamically through code.
                    spContext.TryGetFolderByServerRelativeUrl(libraryName, serverRelativeFolderUrl, out Folder libFolder);
                    return new SharePointFolderEntity { SPFolder = libFolder, ServerRelativeFolderUrl = serverRelativeFolderUrl, CreateFolderInLibRoot = true };

                }
                return GetSharePointFolderByServerRelativeUrl(spContext, serverRelativeFolderUrl);
            }
            else
                return new SharePointFolderEntity { SPFolder = result, ServerRelativeFolderUrl = serverRelativeFolderUrl };
        }

        private static Folder CreateFolderInternal(Web web, Folder parentFolder, string fullFolderPath)
        {
            var folderUrls = fullFolderPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string folderUrl = folderUrls[0];
            var curFolder = parentFolder.Folders.Add(folderUrl);
            web.Context.Load(curFolder);
            web.Context.ExecuteQuery();

            if (folderUrls.Length > 1)
            {
                var folderPath = string.Join("/", folderUrls, 1, folderUrls.Length - 1);
                return CreateFolderInternal(web, curFolder, folderPath);
            }
            return curFolder;
        }

        public Microsoft.SharePoint.Client.File UploadBlobFile(string SharePointFolderUrl, string TempFilePath)
        {
            try
            {
                using (ClientContext clientContext = new ClientContext(siteURL.Trim()))
                {
                    SetSPCredentials(clientContext);
                    return UploadFileSlicePerSlice(clientContext, SharePointFolderUrl, TempFilePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"SharePoint Upload failed : " + ex.Message);
            }
        }

        private void SetSPCredentials(ClientContext clientContext)
        {
            //creates secure password from raw password
            SecureString securePassWord = new SecureString();
            foreach (char c in spPassWord.Trim().ToCharArray())
            {
                securePassWord.AppendChar(c);
            }

            //authenticates with SharePoint
            SharePointOnlineCredentials _myCredentials = new SharePointOnlineCredentials(spUserName.Trim(), securePassWord);
            clientContext.Credentials = _myCredentials;
        }

        private Microsoft.SharePoint.Client.File UploadFileSlicePerSlice(ClientContext ctx, string sharePointFolderUrl, string tempFilePath, int fileChunkSizeInMB = 10)
        {
            // Each sliced upload requires a unique ID.
            Guid uploadId = Guid.NewGuid();

            string originalFileName = Path.GetFileName(tempFilePath).RemovePrefix(guidPrefixLength);
            //Important!! Note: API has issues when trying to find existing Folder names with #, so as per discussion with
            //business team it was decided to remove # from folder names.
            sharePointFolderUrl = sharePointFolderUrl.Replace("#", "");
            // Get the folder to upload into.
            var serverRelativeFolderUrl = $"{libraryName}/{sharePointFolderUrl}";
            Folder uploadFolder = ctx.Web.GetFolderByServerRelativeUrl(serverRelativeFolderUrl);

            // Get the information about the folder that will hold the file.
            ctx.Load(uploadFolder);
            ctx.ExecuteQuery();


            // File object.
            Microsoft.SharePoint.Client.File uploadFile = null;

            // Calculate block size in bytes.
            int blockSize = fileChunkSizeInMB * 1024 * 1024;
            ctx.Load(uploadFolder, f => f.ServerRelativeUrl);
            ctx.ExecuteQuery();

            // Get the size of the file.
            long fileSize = new FileInfo(tempFilePath).Length;

            if (fileSize <= blockSize)
            {
                // Use regular approach.
                using (FileStream fs = new FileStream(tempFilePath, FileMode.Open))
                {
                    FileCreationInformation fileInfo = new FileCreationInformation();
                    fileInfo.ContentStream = fs;
                    fileInfo.Url = originalFileName;
                    fileInfo.Overwrite = true;
                    //uploadFile = docs.RootFolder.Files.Add(fileInfo);
                    uploadFile = uploadFolder.Files.Add(fileInfo);

                    ctx.Load(uploadFile);
                    ctx.ExecuteQuery();

                    depActivityRepository.UpdateActivity(DateTime.UtcNow, ActivityStage.SharePointCopy);
                    // Return the file object for the uploaded file.
                    return uploadFile;
                }
            }
            else
            {
                // Use large file upload approach.
                ClientResult<long> bytesUploaded = null;
                FileStream fs = null;
                try
                {
                    fs = System.IO.File.Open(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        byte[] buffer = new byte[blockSize];
                        byte[] lastBuffer = null;
                        long fileoffset = 0;
                        long totalBytesRead = 0;
                        int bytesRead;
                        bool first = true;
                        bool last = false;

                        // Read data from file system in blocks.
                        while ((bytesRead = br.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            totalBytesRead = totalBytesRead + bytesRead;

                            // You've reached the end of the file.
                            if (totalBytesRead == fileSize)
                            {
                                last = true;
                                // Copy to a new buffer that has the correct size.
                                lastBuffer = new byte[bytesRead];
                                Array.Copy(buffer, 0, lastBuffer, 0, bytesRead);
                            }

                            if (first)
                            {
                                _logger.LogInformation($"SharePoint upload started {originalFileName}");
                                using (MemoryStream contentStream = new MemoryStream())
                                {
                                    // Add an empty file.
                                    FileCreationInformation fileInfo = new FileCreationInformation();
                                    fileInfo.ContentStream = contentStream;
                                    fileInfo.Url = originalFileName;
                                    fileInfo.Overwrite = true;
                                    uploadFile = uploadFolder.Files.Add(fileInfo);

                                    // Start upload by uploading the first slice.
                                    using (MemoryStream s = new MemoryStream(buffer))
                                    {
                                        // Call the start upload method on the first slice.
                                        bytesUploaded = uploadFile.StartUpload(uploadId, s);
                                        ctx.ExecuteQuery();
                                        // fileoffset is the pointer where the next slice will be added.
                                        fileoffset = bytesUploaded.Value;
                                        depActivityRepository.UpdateActivity(DateTime.UtcNow, ActivityStage.SharePointCopy);
                                    }

                                    // You can only start the upload once.
                                    first = false;
                                }
                            }
                            else
                            {
                                if (last)
                                {
                                    _logger.LogInformation($"SharePoint upload completed {originalFileName}");
                                    // Is this the last slice of data?
                                    using (MemoryStream s = new MemoryStream(lastBuffer))
                                    {
                                        // End sliced upload by calling FinishUpload.
                                        uploadFile = uploadFile.FinishUpload(uploadId, fileoffset, s);
                                        ctx.ExecuteQuery();
                                        //TODO check if this can be made async
                                        depActivityRepository.UpdateActivity(DateTime.UtcNow, ActivityStage.SharePointCopy);
                                        // Return the file object for the uploaded file.
                                        return uploadFile;
                                    }
                                }
                                else
                                {
                                    using (MemoryStream s = new MemoryStream(buffer))
                                    {
                                        // Continue sliced upload.
                                        bytesUploaded = uploadFile.ContinueUpload(uploadId, fileoffset, s);
                                        ctx.ExecuteQuery();
                                        depActivityRepository.UpdateActivity(DateTime.UtcNow, ActivityStage.SharePointCopy);
                                        // Update fileoffset for the next slice.
                                        fileoffset = bytesUploaded.Value;
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                    }
                }
            }

            return null;
        }

    }
}
