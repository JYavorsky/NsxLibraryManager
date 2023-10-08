﻿using AutoMapper;
using LiteDB;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NsxLibraryManager.Models;
using NsxLibraryManager.Repository;
using NsxLibraryManager.Settings;

namespace NsxLibraryManager.Services;

public class DataService : IDataService
{
    private readonly string _connStr;
    private ILiteDatabase? _db;
    private bool _disposed;
    private ITitleRepository? _titleRepository;
    private Dictionary<string, IRegionRepository>? _regionRepository;
    private ITitleLibraryRepository? _titleLibraryRepository;
    private readonly AppSettings _configuration;
    private readonly IMapper _mapper;
    private readonly ILogger<DataService> _logger;
    
    private ILiteDatabase Db
    {
        get { return _db ??= new LiteDatabase(_connStr); }
    }

    public DataService(IOptions<AppSettings> configuration, IMapper mapper, ILogger<DataService> logger)
    {
        _logger = logger;
        _mapper = mapper;
        _configuration = configuration.Value;
        var configTitleDb = _configuration.TitleDatabase;
        var titleDbLocation = configTitleDb ?? throw new InvalidOperationException();
        _connStr = Path.Combine(titleDbLocation);
    }
    
    public IRegionRepository RegionRepository(string region)
    {
        if (_regionRepository is not null)
        {
            if (_regionRepository.TryGetValue(region, out var repository))
                return repository;
        }
        
        _regionRepository = new Dictionary<string, IRegionRepository>()
        {
                [region] = new RegionRepository(Db, region)
        };
        return _regionRepository[region];


    }
    
    public ITitleLibraryRepository TitleLibraryRepository()
    {
        return _titleLibraryRepository ??= new TitleLibraryRepository(Db);
    }

    public async Task<IEnumerable<RegionTitle>> GetRegionTitlesAsync(string region)
    {
        var regionTitleRepository = RegionRepository(region);
        return await Task.Run(() => regionTitleRepository.All());
    }
    
    public async Task<IEnumerable<LibraryTitle>> GetLibraryTitlesAsync()
    {
        var libraryRepository = TitleLibraryRepository();
        return await Task.Run(() => libraryRepository.All());
    }

    public async Task AddLibraryTitleAsync(LibraryTitle libraryTitle)
    {
        var titleLibraryRepository = TitleLibraryRepository();
        await Task.Run(() => titleLibraryRepository.Create(libraryTitle));
    }

    public bool DropDbCollection(string collectionName)
    {
        return Db.DropCollection(collectionName);
    }

    public DateTime? GetRegionLastUpdate(string region, CancellationToken cancellationToken = default)
    {
        var regionTitleRepository = RegionRepository(region);
        var firstTitle = regionTitleRepository.FindOne(o => true);
        return firstTitle?.CreatedTime;
    }

    public ITitleRepository TitleRepository
    {
        get { return _titleRepository ??= new TitleRepository(Db); }
    }

    public void ImportTitleDbRegionTitles(JObject titles, string region)
    {
        var i = 0;
        var regionTitleRepository = RegionRepository(region);
        foreach (var title in titles)
        {
            var tt = title.Value;
            if (title.Value != null && title.Value.ToString() == "{}") continue;
            var titleDbTitle = JsonConvert.DeserializeObject<TitleDbTitle>(title.Value.ToString());

            var regionTitle = _mapper.Map<RegionTitle>(titleDbTitle);

            _logger.LogDebug($"{regionTitle.Name}");
            regionTitleRepository.Create(regionTitle);
            i++;
        }

    }

    public async Task Import()
    {
        var naranjas = "nada aun";
        /*
        var downloadConfig = _configuration.GetSection("download").Get<DownloadSettings>();

        if (downloadConfig is not null)
        {
            foreach (var region in downloadConfig.Regions)
            {
                Db.DropCollection(region);

                var destFilePath = await _downloadService.GetRegionFile(region, CancellationToken.None);
                var titles = JObject.Parse(await File.ReadAllTextAsync(destFilePath));
                LoadTitlesInDb(titles, region);
            }
        }
        */
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _db?.Dispose();
        }

        _disposed = true;
    }

    ~DataService()
    {
        Dispose(false);
    }
}