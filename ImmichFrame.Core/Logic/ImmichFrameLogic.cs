using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Models;
using System.Data;
using OpenWeatherMap.Models;
using OpenWeatherMap;

namespace ImmichFrame.Core.Logic
{
    public class ImmichFrameLogic : IImmichFrameLogic
    {
        private IServerSettings _settings;
        public ImmichFrameLogic(IServerSettings settings)
        {
            _settings = settings;
        }

        private Task<Dictionary<Guid, AssetResponseDto>?>? _filteredAssetInfos;
        private DateTime lastFilteredAssetRefesh;
        private List<Guid> ImmichFrameAlbumAssets = new List<Guid>();
        private static AlbumResponseDto immichFrameAlbum = new AlbumResponseDto();


        private Task<HashSet<Guid>>? _excludedAssets;
        private DateTime lastExcludedAssetRefresh;
        private Task<HashSet<Guid>>? _excludedAlbumAssets;
        private DateTime lastExcludedAlbumAssetRefresh;
        private Task<HashSet<Guid>> ExcludedAssets
        {
            get
            {
                TimeSpan timeSinceRefresh = DateTime.Now - lastExcludedAssetRefresh;
                if (_excludedAssets == null || timeSinceRefresh.TotalHours > _settings.RefreshAlbumPeopleInterval)
                {
                    lastExcludedAssetRefresh = DateTime.Now;
                    _excludedAssets = GetExcludedAssets();
                }

                return _excludedAssets;
            }
        }

        private Task<HashSet<Guid>> ExcludedAlbumAssets
        {
            get
            {
                TimeSpan timeSinceRefresh = DateTime.Now - lastExcludedAlbumAssetRefresh;
                if (_excludedAlbumAssets == null || timeSinceRefresh.TotalHours > _settings.RefreshAlbumPeopleInterval)
                {
                    lastExcludedAlbumAssetRefresh = DateTime.Now;
                    _excludedAlbumAssets = GetExcludedAlbumAssets();
                }

                return _excludedAlbumAssets;
            }
        }

        private Task<Dictionary<Guid, AssetResponseDto>?> FilteredAssetInfos
        {
            get
            {
                TimeSpan timeSinceRefresh = DateTime.Now - lastFilteredAssetRefesh;
                if (_filteredAssetInfos == null || timeSinceRefresh.TotalHours > _settings.RefreshAlbumPeopleInterval)
                {
                    lastFilteredAssetRefesh = DateTime.Now;
                    _filteredAssetInfos = GetFilteredAssetIds();
                }

                return _filteredAssetInfos;
            }
        }

        public async Task<AssetResponseDto> GetNextAsset()
        {
            // People-only selection is best handled from a random batch. Building a
            // complete server-side asset list for every selected person is slow and
            // memory intensive on small photo frames.
            if ((_settings.People?.Any() ?? false) && !(_settings.Albums?.Any() ?? false) && !_settings.ShowMemories)
            {
                return await GetRandomAsset() ?? throw new AssetNotFoundException();
            }

            if ((await FilteredAssetInfos) != null)
            {
                return await GetRandomFilteredAsset() ?? throw new AssetNotFoundException();
            }

            return await GetRandomAsset() ?? throw new AssetNotFoundException();
        }

        public async Task<FileResponse> GetImage(Guid id)
        {
            using (var client = new HttpClient())
            {
                client.UseApiKey(_settings.ApiKey);

                var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);

                return await immichApi.ViewAssetAsync(id, string.Empty, AssetMediaSize.Preview);
            }
        }

        public async Task<IReadOnlyList<PersonInfo>> GetPeopleAsync(CancellationToken cancellationToken = default)
        {
            using var client = new HttpClient();
            client.UseApiKey(_settings.ApiKey);
            var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);

            const int pageSize = 250;
            var page = 1;
            var people = new List<PersonInfo>();
            bool hasNextPage;

            do
            {
                var response = await immichApi.GetAllPeopleAsync(page, pageSize, true, cancellationToken);
                people.AddRange(response.People
                    .Where(person => Guid.TryParse(person.Id, out _))
                    .Select(person => new PersonInfo(Guid.Parse(person.Id), person.Name?.Trim() ?? string.Empty)));

                hasNextPage = response.HasNextPage ?? response.People.Count == pageSize;
                page++;
            }
            while (hasNextPage);

            return people
                .DistinctBy(person => person.Id)
                .ToList();
        }

        public async Task<byte[]?> GetPersonThumbnailAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            using var client = new HttpClient();
            client.UseApiKey(_settings.ApiKey);
            var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);
            using var response = await immichApi.GetPersonThumbnailAsync(id, cancellationToken);
            using var thumbnail = new MemoryStream();
            await response.Stream.CopyToAsync(thumbnail, cancellationToken);
            return thumbnail.Length == 0 ? null : thumbnail.ToArray();
        }

        public async Task AddAssetToAlbum(AssetResponseDto assetToAdd)
        {
            using (var client = new HttpClient())
            {
                client.UseApiKey(_settings.ApiKey);
                var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);
                var itemsToAdd = new BulkIdsDto();
                itemsToAdd.Ids.Add(new Guid(assetToAdd.Id));
                await immichApi.AddAssetsToAlbumAsync(new Guid(immichFrameAlbum.Id), null, itemsToAdd);
                ImmichFrameAlbumAssets.Add(new Guid(assetToAdd.Id));
                //only keep 100 most recent assets in album
                var albumInfo = await immichApi.GetAlbumInfoAsync(new Guid(immichFrameAlbum.Id), null, null);
                if (albumInfo.AssetCount > 100)
                {
                    var itemToRemove = new BulkIdsDto();
                    itemToRemove.Ids.Add(ImmichFrameAlbumAssets[0]);
                    await immichApi.RemoveAssetFromAlbumAsync(new Guid(immichFrameAlbum.Id), itemToRemove);
                    ImmichFrameAlbumAssets.RemoveAt(0);
                }
            }
        }
        public async Task DeleteAndCreateImmichFrameAlbum()
        {
            using (var client = new HttpClient())
            {
                client.UseApiKey(_settings.ApiKey);
                var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);
                var immichAlbums = await immichApi.GetAllAlbumsAsync(null, null);
                immichFrameAlbum = immichAlbums.FirstOrDefault(album => album.AlbumName == _settings.ImmichFrameAlbumName)!;
                if (immichFrameAlbum != null)
                {
                    await immichApi.DeleteAlbumAsync(new Guid(immichFrameAlbum.Id));
                }
                var albumDto = new CreateAlbumDto
                {
                    AlbumName = _settings.ImmichFrameAlbumName,
                    Description = "Recent ImmichFrame Photos"
                };
                var result = await immichApi.CreateAlbumAsync(albumDto);
                immichFrameAlbum = new AlbumResponseDto { Id = result.Id };
            }
        }

        private async Task<Dictionary<Guid, AssetResponseDto>?> GetFilteredAssetIds()
        {
            bool assetsAdded = false;
            IEnumerable<AssetResponseDto> list = new List<AssetResponseDto>();
            if (_settings.ShowMemories)
            {
                assetsAdded = true;
                list = list.Union(await GetMemoryAssets());
            }

            if (_settings.Albums?.Any() ?? false)
            {
                assetsAdded = true;
                list = list.Union(await GetAlbumAssets());
            }

            if (_settings.People?.Any() ?? false)
            {
                assetsAdded = true;
                list = list.Union(await GetPeopleAssets());
            }

            if (assetsAdded)
            {
                // Exclude videos
                list = list.Where(x => x.Type != AssetTypeEnum.VIDEO);

                var excludedList = await ExcludedAssets;

                // Exclude assets if configured
                if (excludedList.Any())
                    list = list.Where(x => !excludedList.Contains(Guid.Parse(x.Id)));

                // return only unique assets, no duplicates, only with Thumbnail
                return list.Where(x => x.Thumbhash != null).DistinctBy(x => x.Id).ToDictionary(x => Guid.Parse(x.Id));
            }

            return null;
        }
        private async Task<IEnumerable<AssetResponseDto>> GetMemoryAssets()
        {
            using (var client = new HttpClient())
            {
                client.UseApiKey(_settings.ApiKey);

                var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);

                var allAssets = new List<AssetResponseDto>();

                var date = DateTime.Today;
                ICollection<MemoryLaneResponseDto> memoryLane;
                try
                {
                    memoryLane = await immichApi.GetMemoryLaneAsync(date.Day, date.Month);
                }
                catch (ApiException ex)
                {
                    throw new AlbumNotFoundException($"Memories were not found, check your settings file!{Environment.NewLine}{Environment.NewLine}{ex.Message}", ex);
                }

                foreach (var lane in memoryLane)
                {
                    var assets = lane.Assets.ToList();
                    assets.ForEach(asset => asset.ImageDesc = $"{lane.YearsAgo} {(lane.YearsAgo == 1 ? "year" : "years")} ago");

                    allAssets.AddRange(assets);
                }

                return allAssets;
            }
        }
        private async Task<IEnumerable<AssetResponseDto>> GetAlbumAssets(Guid albumId, ImmichApi immichApi)
        {
            try
            {
                var albumInfo = await immichApi.GetAlbumInfoAsync(albumId, null, null);

                return albumInfo.Assets;
            }
            catch (ApiException ex)
            {
                throw new AlbumNotFoundException($"Album '{albumId}' was not found, check your settings file!{Environment.NewLine}{Environment.NewLine}{ex.Message}", ex);
            }
        }
        private async Task<IEnumerable<AssetResponseDto>> GetAlbumAssets()
        {
            using var client = new HttpClient();

            var allAssets = new List<AssetResponseDto>();

            var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);

            client.UseApiKey(_settings.ApiKey);
            foreach (var albumId in _settings.Albums!)
            {
                allAssets.AddRange(await GetAlbumAssets(albumId, immichApi));
            }

            return allAssets;
        }
        private async Task<HashSet<Guid>> GetExcludedAssets()
        {
            var excludedAssets = new HashSet<Guid>(await ExcludedAlbumAssets);

            if (_settings.ExcludedPeople?.Any() ?? false)
            {
                excludedAssets.UnionWith((await GetPeopleAssets(_settings.ExcludedPeople)).Select(x => Guid.Parse(x.Id)));
            }

            return excludedAssets;
        }

        private async Task<HashSet<Guid>> GetExcludedAlbumAssets()
        {
            using var client = new HttpClient();

            var allAssets = new List<AssetResponseDto>();

            var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);

            client.UseApiKey(_settings.ApiKey);
            foreach (var albumId in _settings.ExcludedAlbums!)
            {
                allAssets.AddRange(await GetAlbumAssets(albumId, immichApi));
            }

            return allAssets.Select(x => Guid.Parse(x.Id)).ToHashSet();
        }
        private async Task<IEnumerable<AssetResponseDto>> GetPeopleAssets()
        {
            return await GetPeopleAssets(_settings.People!);
        }

        private async Task<IEnumerable<AssetResponseDto>> GetPeopleAssets(IEnumerable<Guid> personIds)
        {
            using (var client = new HttpClient())
            {
                var allAssets = new List<AssetResponseDto>();

                var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);

                client.UseApiKey(_settings.ApiKey);
                foreach (var personId in personIds)
                {
                    try
                    {
                        int page = 1;
                        int batchSize = 1000;
                        int itemsInPage;
                        do
                        {
                            var metadataBody = new MetadataSearchDto
                            {
                                Page = page,
                                Size = batchSize,
                                PersonIds = new[] { personId },
                                Type = AssetTypeEnum.IMAGE,
                                WithExif = true,
                                WithPeople = true
                            };
                            var personInfo = await immichApi.SearchMetadataAsync(metadataBody);

                            allAssets.AddRange(personInfo.Assets.Items);
                            itemsInPage = personInfo.Assets.Items.Count;
                            page++;
                        }
                        while (itemsInPage == batchSize);
                    }
                    catch (ApiException ex)
                    {
                        throw new PersonNotFoundException($"Person '{personId}' was not found, check your settings file!{Environment.NewLine}{Environment.NewLine}{ex.Message}", ex);
                    }
                }

                // Remove duplicates
                var uniqueAssets = allAssets.DistinctBy(x => x.Id);

                return uniqueAssets;
            }
        }
        private Random _random = new Random();
        private async Task<AssetResponseDto?> GetRandomFilteredAsset()
        {
            var filteredAssetInfos = await FilteredAssetInfos;
            if (filteredAssetInfos == null || !filteredAssetInfos.Any())
                throw new AssetNotFoundException();

            var rnd = _random.Next(filteredAssetInfos.Count);

            return filteredAssetInfos.ElementAt(rnd).Value;
        }

        List<AssetResponseDto> RandomAssetList = new List<AssetResponseDto>();
        private async Task<AssetResponseDto?> GetRandomAsset()
        {
            if (RandomAssetList.Any())
            {
                var randomAsset = RandomAssetList.First();
                RandomAssetList.Remove(randomAsset);

                // Skip this asset
                if (randomAsset.Thumbhash == null)
                    return await GetRandomAsset();

                return randomAsset;
            }

            using (var client = new HttpClient())
            {
                client.UseApiKey(_settings.ApiKey);

                var immichApi = new ImmichApi(_settings.ImmichServerUrl, client);
                try
                {
                    var searchBody = new RandomSearchDto
                    {
                        Size = 250,
                        Type = AssetTypeEnum.IMAGE,
                        WithExif = true,
                        WithPeople = true
                    };
                    var searchResponse = await immichApi.SearchRandomAsync(searchBody);

                    var randomAssets = searchResponse;

                    if (randomAssets.Any())
                    {
                        var excludedList = await ExcludedAlbumAssets;

                        randomAssets = randomAssets.Where(x => !excludedList.Contains(Guid.Parse(x.Id))).ToList();

                        if (_settings.People?.Any() ?? false)
                        {
                            var includedPeople = _settings.People.ToHashSet();
                            randomAssets = randomAssets.Where(asset => HasAnyPerson(asset, includedPeople)).ToList();
                        }

                        if (_settings.ExcludedPeople?.Any() ?? false)
                        {
                            var excludedPeople = _settings.ExcludedPeople.ToHashSet();
                            randomAssets = randomAssets.Where(asset => !HasAnyPerson(asset, excludedPeople)).ToList();
                        }

                        RandomAssetList.AddRange(randomAssets);

                        return await GetRandomAsset();
                    }
                }
                catch (ApiException ex)
                {
                    throw new PersonNotFoundException($"Asset was not found, check your settings file!{Environment.NewLine}{Environment.NewLine}{ex.Message}", ex);
                }
            }

            return null;
        }

        private static bool HasAnyPerson(AssetResponseDto asset, HashSet<Guid> people)
        {
            return asset.People?.Any(person => Guid.TryParse(person.Id, out var personId) && people.Contains(personId)) ?? false;
        }

        public Task<IWeather?> GetWeather()
        {
            OpenWeatherMapOptions options = new OpenWeatherMapOptions
            {
                ApiKey = _settings.WeatherApiKey,
                UnitSystem = _settings.UnitSystem,
                Language = _settings.Language,
            };

            var weatherLatLong = _settings.WeatherLatLong;

            var weatherLat = !string.IsNullOrWhiteSpace(weatherLatLong) ? float.Parse(weatherLatLong!.Split(',')[0]) : 0f;
            var weatherLong = !string.IsNullOrWhiteSpace(weatherLatLong) ? float.Parse(weatherLatLong!.Split(',')[1]) : 0f;

            return GetWeather(weatherLat, weatherLong, options);
        }

        public async Task<IWeather?> GetWeather(double latitude, double longitude, OpenWeatherMapOptions Options)
        {
            try
            {
                IOpenWeatherMapService openWeatherMapService = new OpenWeatherMapService(Options);
                var weatherInfo = await openWeatherMapService.GetCurrentWeatherAsync(latitude, longitude);

                return weatherInfo.ToWeather();
            }
            catch
            {
                //do nothing and return null
            }

            return null;
        }
    }
}
