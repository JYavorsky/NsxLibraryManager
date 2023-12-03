﻿using LibHac.Ncm;
using Microsoft.Extensions.Logging;
using NsxLibraryManager.Core.Enums;
using NsxLibraryManager.Core.Exceptions;
using NsxLibraryManager.Core.FileLoading;
using NsxLibraryManager.Core.FileLoading.Interface;
using NsxLibraryManager.Core.Models;
using NsxLibraryManager.Core.Services.Interface;

namespace NsxLibraryManager.Core.Services;

public class FileInfoService : IFileInfoService
{
    private readonly IPackageInfoLoader _packageInfoLoader;
    private readonly ITitleDbService _titleDbService;
    private readonly ILogger<FileInfoService> _logger;
    private IEnumerable<string> _directoryFiles = new List<string>();
   
    public FileInfoService(
            IPackageInfoLoader packageInfoLoader, 
            ITitleDbService titleDbService,
            ILogger<FileInfoService> logger)
    {
        _packageInfoLoader = packageInfoLoader ?? throw new ArgumentNullException(nameof(packageInfoLoader));
        _titleDbService = titleDbService ?? throw new ArgumentNullException(nameof(titleDbService));
        _logger = logger;
    }
    
    public  async Task<IEnumerable<string>> GetFileNames(
            string filePath, 
            bool recursive = false)
    {
        var fileList = new List<string>();
        _directoryFiles = fileList;
        if (File.Exists(filePath))
        {
            fileList.Add(Path.GetFullPath(filePath));
        }
        else if (Directory.Exists(filePath) && !recursive)
        {
            var directoryFiles = GetDirectoryFiles(filePath);
            fileList.AddRange(directoryFiles);
        }
        else if (Directory.Exists(filePath) && recursive)
        {
            var recursiveFiles = await GetRecursiveFiles(filePath);
            fileList.AddRange(recursiveFiles);
        }
        else
        {
            throw new InvalidPathException(filePath);
        }
        return fileList;
    }
    
    public IEnumerable<string> GetDirectoryFiles(string filePath)
    {
        var files = Directory.GetFiles(filePath);
        return files.ToList();
    }
    
    public async Task<IEnumerable<string>> GetRecursiveFiles(string filePath)
    {
        var files = Directory.GetFiles(filePath);
        _directoryFiles = _directoryFiles.Concat(files.ToList());
        
        var subdirectoryEntries = Directory.GetDirectories(filePath);
        foreach (var subdirectory in subdirectoryEntries)
        {
            await GetRecursiveFiles(subdirectory);
        }

        return _directoryFiles.ToList();
    }
    
    public async Task<LibraryTitle> GetFileInfo(string filePath)
    {
        var packageInfo = _packageInfoLoader.GetPackageInfo(filePath);

        if (packageInfo.Contents is null)
            throw new Exception("No contents found in the package");
        
        var title = new LibraryTitle
        {
            ApplicationTitleId = packageInfo.Contents.ApplicationTitleId,
            PatchTitleId = packageInfo.Contents.PatchTitleId,
            PatchNumber = packageInfo.Contents.PatchNumber,
            TitleId = packageInfo.Contents.TitleId,
            TitleName = packageInfo.Contents.Name,
            Publisher = packageInfo.Contents.Publisher,
            TitleVersion = packageInfo.Contents.Version.Version,
            PackageType = packageInfo.AccuratePackageType,
            FileName = Path.GetFullPath(filePath),
            LastWriteTime = File.GetLastWriteTime(filePath)
        };

        var availableVersion = await _titleDbService.GetAvailableVersion(title.TitleId);
        title.AvailableVersion = availableVersion >> 16;
        title.Type = packageInfo.Contents.Type switch
        {
                ContentMetaType.Application => TitleLibraryType.Base,
                ContentMetaType.Patch => TitleLibraryType.Update,
                ContentMetaType.AddOnContent => TitleLibraryType.DLC,
                _ => title.Type
        };

        return title;
    }
}