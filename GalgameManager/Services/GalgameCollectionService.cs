﻿using System.Collections.ObjectModel;

using Windows.Storage;

using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;


namespace GalgameManager.Services;

// ReSharper disable EnforceForeachStatementBraces
// ReSharper disable EnforceIfStatementBraces
public class GalgameCollectionService : IDataCollectionService<Galgame>
{
    private ObservableCollection<Galgame> _galgames = new();
    private ILocalSettingsService LocalSettingsService { get; }
    public delegate void GalgameAddedEventHandler(Galgame galgame);
    public event GalgameAddedEventHandler? GalgameAddedEvent;
    public delegate void GalgameLoadedEventHandler();
    public event GalgameLoadedEventHandler? GalgameLoadedEvent;

    private IGalInfoPhraser[] PhraserList
    {
        get;
    } = new IGalInfoPhraser[5];

    public GalgameCollectionService(ILocalSettingsService localSettingsService)
    {
        LocalSettingsService = localSettingsService;
        PhraserList[(int)RssType.Bangumi] = new BgmPhraser(localSettingsService);
        PhraserList[(int)RssType.Vndb] = new VndbPhraser();

        GetGalgames();

        App.MainWindow.AppWindow.Closing += async (_, _) =>
        {
            await SaveGalgamesAsync();
        };
    }

    private async void GetGalgames()
    {
        _galgames = await LocalSettingsService.ReadSettingAsync<ObservableCollection<Galgame>>(KeyValues.Galgames, true) ?? new ObservableCollection<Galgame>();
        var toRemove = _galgames.Where(galgame => !galgame.CheckExist()).ToList();
        foreach (var galgame in toRemove)
            _galgames.Remove(galgame);
        GalgameLoadedEvent?.Invoke();
    }

    public enum AddGalgameResult
    {
        Success,
        AlreadyExists,
        NotFoundInRss
    }

    /// <summary>
    /// 试图添加一个galgame，若已存在则不添加
    /// </summary>
    /// <param name="path">galgame路径</param>
    /// <param name="isForce">是否强制添加（若RSS源中找不到相关游戏信息）</param>
    public async Task<AddGalgameResult> TryAddGalgameAsync(string path, bool isForce = false)
    {
        if (_galgames.Any(gal => gal.Path == path))
            return AddGalgameResult.AlreadyExists;

        var galgame = new Galgame(path);
        galgame = await PhraseGalInfoAsync(galgame);
        if(!isForce && galgame.RssType == RssType.None)
            return AddGalgameResult.NotFoundInRss;
        _galgames.Add(galgame);
        GalgameAddedEvent?.Invoke(galgame);
        await SaveGalgamesAsync();
        //为了防止在Home添加游戏的时候galgameFolderService还没有初始化，把要加的库暂存起来
        var libToCheck = await LocalSettingsService.ReadSettingAsync<List<string>>(KeyValues.LibToCheck) ?? new List<string>();
        var libPath = galgame.Path[..galgame.Path.LastIndexOf('\\')];
        if (!libToCheck.Contains(libPath))
        {
            libToCheck.Add(libPath);
            await LocalSettingsService.SaveSettingAsync(KeyValues.LibToCheck, libToCheck, true);
        }
        
        return galgame.RssType == RssType.None ? AddGalgameResult.NotFoundInRss : AddGalgameResult.Success;
    }


    /// <summary>
    /// 从下载源获取这个galgame的信息
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="rssType">信息源，若设置为None则使用设置中的默认信息源</param>
    /// <returns>获取信息后的galgame，如果信息源不可达则galgame保持不变</returns>
    public async Task<Galgame> PhraseGalInfoAsync(Galgame galgame, RssType rssType = RssType.None)
    {
        var selectedRss = rssType==RssType.None ? await LocalSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType) : rssType;
        var result =  await PhraserAsync(galgame, PhraserList[(int)selectedRss]);
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames, true);
        return result;
    }
    
    private static async Task<Galgame> PhraserAsync(Galgame galgame, IGalInfoPhraser phraser)
    {
        var tmp = await phraser.GetGalgameInfo(galgame);
        if (tmp == null) return galgame;

        galgame.Id = tmp.Id;
        galgame.Description = tmp.Description;
        if(tmp.Developer != Galgame.DefaultString) galgame.Developer = tmp.Developer;
        if (tmp.ExpectedPlayTime != Galgame.DefaultString) galgame.ExpectedPlayTime = tmp.ExpectedPlayTime;
        galgame.Name = tmp.Name;
        galgame.ImageUrl = tmp.ImageUrl;
        galgame.Rating = tmp.Rating;
        galgame.RssType = phraser.GetPhraseType();
        galgame.ImagePath = await DownloadAndSaveImageAsync(galgame.ImageUrl) ?? Galgame.DefaultImagePath;
        galgame.CheckSavePosition();
        return galgame;
    }
    
    private static async Task<string?> DownloadAndSaveImageAsync(string? imageUrl)
    {
        if (imageUrl == null) return null;
        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(imageUrl);
        response.EnsureSuccessStatusCode();

        var imageBytes = await response.Content.ReadAsByteArrayAsync();

        var localFolder = ApplicationData.Current.LocalFolder;
        var fileName = imageUrl[(imageUrl.LastIndexOf('/') + 1)..];
        var storageFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

        await using (var fileStream = await storageFile.OpenStreamForWriteAsync())
        {
            using var memoryStream = new MemoryStream(imageBytes);
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(fileStream);
        }

        // 返回本地文件的路径
        return storageFile.Path;
    }

    public async Task<ObservableCollection<Galgame>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _galgames;
    }
    
    /// <summary>
    /// 保存galgame列表（以及其内部的galgame）
    /// </summary>
    private async Task SaveGalgamesAsync()
    {
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames, true);
    }
}
