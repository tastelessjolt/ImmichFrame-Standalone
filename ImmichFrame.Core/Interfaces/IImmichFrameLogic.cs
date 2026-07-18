using ImmichFrame.Core.Api;
using ImmichFrame.Core.Models;
using OpenWeatherMap;
using OpenWeatherMap.Models;

namespace ImmichFrame.Core.Interfaces
{
    public interface IImmichFrameLogic
    {
        public Task<AssetResponseDto> GetNextAsset();
        public Task<FileResponse> GetImage(Guid id);
        public Task AddAssetToAlbum(AssetResponseDto assetToAdd);
        public Task DeleteAndCreateImmichFrameAlbum();
        public Task<IReadOnlyList<PersonInfo>> GetPeopleAsync(CancellationToken cancellationToken = default);
        public Task<IWeather?> GetWeather();
        public Task<IWeather?> GetWeather(double latitude, double longitude, OpenWeatherMapOptions Options);
    }
}
