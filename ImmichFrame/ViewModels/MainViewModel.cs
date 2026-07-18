using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using ImmichFrame.Helpers;
using ImmichFrame.Models;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImmichFrame.ViewModels;

public partial class MainViewModel : NavigatableViewModelBase, IDisposable
{

    public bool TimerEnabled = false;
    private AssetResponseDto? CurrentAsset;
    private PreloadedAsset? NextAsset;
    private readonly NavigationHistory<AssetResponseDto> assetHistory = new(100);
    private IImmichFrameLogic _immichLogic;
    private CultureInfo culture;
    System.Threading.Timer? timerImageSwitcher;
    System.Threading.Timer? timerLiveTime;
    System.Threading.Timer? timerWeather;
    System.Threading.Timer? timerZoom;
    private bool zoomIncreasing = true;
    private DateTime lastZoomTime = DateTime.MinValue;
    private readonly SemaphoreSlim imageChangeGate = new(1, 1);
    private readonly SemaphoreSlim preloadGate = new(1, 1);
    private UiImage? retainedImage;
    private bool disposed;
    private const int FrameWidth = 1280;
    private const int FrameHeight = 800;


    public ICommand NextImageCommand { get; set; }
    public ICommand PreviousImageCommand { get; set; }
    public ICommand PauseImageCommand { get; set; }
    public ICommand QuitCommand { get; set; }
    public ICommand NavigateSettingsPageCommand { get; set; }

    private bool isInitialized;
    public MainViewModel()
    {
        settings = Settings.CurrentSettings;
        _immichLogic = new ImmichFrameLogic(Settings.CurrentSettings);
        culture = new CultureInfo(settings.Language);
        NextImageCommand = new RelayCommand(async () => await NextImageAction());
        PreviousImageCommand = new RelayCommand(async () => await PreviousImageAction());
        PauseImageCommand = new RelayCommand(PauseImageAction);
        QuitCommand = new RelayCommand(ExitApp);
        NavigateSettingsPageCommand = new RelayCommand(NavigateSettingsPageAction);
    }

    public async Task InitializeAsync()
    {
        if (!isInitialized)
        {
            ShowSplash();
            var settings = Settings;

            if (settings == null)
                throw new SettingsNotValidException("Settings could not be parsed.");

            if (settings.UseImmichFrameAlbum)
            {
                await Task.Run(() => _immichLogic.DeleteAndCreateImmichFrameAlbum());
            }

            TimerEnabled = true;
            await Task.Run(() => ShowNextImage());
            timerImageSwitcher = new System.Threading.Timer(NextImageTick, null, settings.Interval * 1000, settings.Interval * 1000);
            if (settings.ShowClock)
            {
                timerLiveTime = new System.Threading.Timer(LiveTimeTick, null, 0, 1 * 1000); //every second
            }
            if (settings.ShowWeather)
            {
                timerWeather = new System.Threading.Timer(WeatherTick, null, 0, 10 * 60 * 1000); //every 10 minutes
            }
            if (Settings.ImageZoom)
            {
                timerZoom = new System.Threading.Timer(ZoomTick, null, 0, 100);
            }
            isInitialized = true;
        }
    }
    public void SetImage(Bitmap image)
    {
        Images = CreateUiImage(image);
    }

    public async Task SetImage(PreloadedAsset asset)
    {
        await SetImage(asset.Asset, asset.Image);
    }
    public async Task SetImage(AssetResponseDto asset, Stream? preloadedAsset = null)
    {
        using (Stream imgStream = preloadedAsset ?? await asset.ServeImage(_immichLogic))
        {
            Bitmap? thumbhashImage = null;
            if (Settings.LetterboxBackground == LetterboxBackgroundOptions.StretchedThumbhash)
            {
                using var thumbhashStream = asset.ThumbhashImage;
                if (thumbhashStream != null)
                    thumbhashImage = new Bitmap(thumbhashStream);
            }

            Images = CreateUiImage(DecodeForFrame(imgStream, asset), thumbhashImage);

            ImageDate = asset?.LocalDateTime.ToString(Settings.PhotoDateFormat, culture) ?? string.Empty;
            ImageDesc = asset?.ImageDesc ?? string.Empty;

            if (asset?.ExifInfo != null)
            {
                ImageLocation = LocationHelper.GetLocationString(asset.ExifInfo);
            }
            else
            {
                ImageLocation = string.Empty;
            }
        }
        if (Settings.UseImmichFrameAlbum)
        {
            await _immichLogic.AddAssetToAlbum(asset!);
        }
    }
    private void ZoomTick(object? state)
    {
        if (!ImagePaused)
        {
            if ((DateTime.Now - lastZoomTime).TotalMilliseconds < 2000)
            {
                return;
            }
            ImageScale += zoomIncreasing ? 0.0033 : -0.0033;

            if (ImageScale >= 1.25)
            {
                ImageScale = 1.25;
                zoomIncreasing = false;
                lastZoomTime = DateTime.Now;
            }
            else if (ImageScale <= 1.00)
            {
                ImageScale = 1.00;
                zoomIncreasing = true;
                lastZoomTime = DateTime.Now;
            }
        }
    }

    private void ShowSplash()
    {
        var uri = new Uri("avares://ImmichFrame/Assets/Immich.png");
        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);
        SetImage(bitmap);
    }

    public async void NextImageTick(object? state)
    {
        await ShowNextImage();
    }
    public void LiveTimeTick(object? state)
    {
        LiveTime = DateTime.Now.ToString(Settings.ClockFormat, culture);
    }
    public async void WeatherTick(object? state)
    {
        var weather = await _immichLogic.GetWeather();
        if (weather != null)
        {
            WeatherTemperature = $"{weather.Temperature.ToString("F1")}{weather.Unit}";
            WeatherCurrent = weather.Description;
            var iconId = weather.IconId;
            var iconUri = AssetLoader.Exists(new Uri($"avares://ImmichFrame/Assets/WeatherIcons/{iconId}.png"))
                ? new Uri($"avares://ImmichFrame/Assets/WeatherIcons/{iconId}.png")
                : new Uri("avares://ImmichFrame/Assets/WeatherIcons/default.png");
            using var iconStream = AssetLoader.Open(iconUri);
            var oldWeatherImage = WeatherImage;
            WeatherImage = new Bitmap(iconStream);
            oldWeatherImage?.Dispose();
        }
    }
    public void NavigateSettingsPageAction()
    {
        Navigate(new SettingsViewModel());
    }

    public async Task NextImageAction()
    {
        ResetTimer();
        // Needs to run on another thread, android does not allow running network stuff on the main thread
        await Task.Run(() => ShowNextImage(true));
    }

    public async Task ShowNextImage(bool force = false)
    {
        if (disposed || (!TimerEnabled && !force) || !await imageChangeGate.WaitAsync(0))
            return;

        int attempt = 0;
        AssetResponseDto? historicalAsset = null;
        var isNavigatingHistory = assetHistory.TryMoveNext(out historicalAsset);

        try
        {
            while (attempt < 3)
            {
                try
                {
                    if (isNavigatingHistory)
                    {
                        await SetImage(historicalAsset!);
                        CurrentAsset = historicalAsset;
                    }
                    else if (NextAsset?.Image == null)
                    {
                        NextAsset?.Dispose();
                        NextAsset = null;
                        // Load Image if next image was not ready
                        CurrentAsset = await _immichLogic.GetNextAsset();

                        if (CurrentAsset != null)
                        {
                            await SetImage(CurrentAsset);
                            assetHistory.Add(CurrentAsset);
                        }
                    }
                    else
                    {
                        // Use preloaded asset
                        using var preloadedAsset = NextAsset;
                        NextAsset = null;
                        await SetImage(preloadedAsset);
                        CurrentAsset = preloadedAsset.Asset;
                        assetHistory.Add(CurrentAsset);
                    }

                    if (!assetHistory.CanMoveNext)
                        _ = PreloadNextAssetAsync();

                    break;
                }
                catch (AssetNotFoundException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= 3)
                    {
                        if (Settings.UnattendedMode)
                            break;

                        Console.Error.WriteLine($"ImmichFrame image load failed: {ex}");
                        this.Navigate(new ErrorViewModel(ex));
                    }
                }
            }
        }
        finally
        {
            imageChangeGate.Release();
        }
    }

    private UiImage CreateUiImage(Bitmap image, Bitmap? thumbhashImage = null)
    {
        var mode = Settings.LetterboxBackground;
        return new UiImage
        {
            Image = image,
            ThumbhashImage = thumbhashImage,
            ImageStretch = StretchHelper.FromString(Settings.ImageStretch),
            ShowDarkGradient = mode == LetterboxBackgroundOptions.DarkGradient,
            ShowStretchedThumbhash = mode == LetterboxBackgroundOptions.StretchedThumbhash && thumbhashImage != null,
            ShowBlurredDuplicate = mode == LetterboxBackgroundOptions.BlurredDuplicate,
        };
    }

    private Bitmap DecodeForFrame(Stream stream, AssetResponseDto asset)
    {
        // The Android frame is 1280x800. Decoding phone/camera originals at
        // their full resolution makes Skia upload and blend far more pixels
        // than the display can show, particularly while two photos crossfade.
        // Keep 25% headroom only when pan/zoom is enabled.
        var targetWidth = Settings.ImageZoom ? FrameWidth * 5 / 4 : FrameWidth;
        var targetHeight = Settings.ImageZoom ? FrameHeight * 5 / 4 : FrameHeight;
        var sourceWidth = asset.ExifInfo?.ExifImageWidth;
        var sourceHeight = asset.ExifInfo?.ExifImageHeight;

        if (sourceWidth > 0 && sourceHeight > 0)
        {
            var sourceAspect = sourceWidth.Value / sourceHeight.Value;
            var frameAspect = (double)targetWidth / targetHeight;

            if (sourceAspect >= frameAspect && sourceWidth > targetWidth)
                return Bitmap.DecodeToWidth(stream, targetWidth, BitmapInterpolationMode.MediumQuality);

            if (sourceAspect < frameAspect && sourceHeight > targetHeight)
                return Bitmap.DecodeToHeight(stream, targetHeight, BitmapInterpolationMode.MediumQuality);

            return new Bitmap(stream);
        }

        // Assets without EXIF dimensions are uncommon. Height is the safer
        // bound for portrait photos and still yields about 1200px wide for a
        // typical landscape image.
        return Bitmap.DecodeToHeight(stream, targetHeight, BitmapInterpolationMode.MediumQuality);
    }

    private async Task PreloadNextAssetAsync()
    {
        if (disposed || NextAsset != null || !await preloadGate.WaitAsync(0))
            return;

        try
        {
            var asset = await _immichLogic.GetNextAsset();
            if (asset == null || disposed)
                return;

            var preloadedAsset = new PreloadedAsset(asset);
            await preloadedAsset.Preload(_immichLogic);
            if (disposed)
            {
                preloadedAsset.Dispose();
                return;
            }

            NextAsset?.Dispose();
            NextAsset = preloadedAsset;
        }
        catch
        {
            // Preloading is an optimization; the next foreground load will retry.
        }
        finally
        {
            preloadGate.Release();
        }
    }

    public async Task PreviousImageAction()
    {
        ResetTimer();
        // Needs to run on another thread, android does not allow running network stuff on the main thread
        await Task.Run(ShowPreviousImage);
    }
    public async Task ShowPreviousImage()
    {
        if (disposed || !await imageChangeGate.WaitAsync(0))
            return;

        if (!assetHistory.TryMovePrevious(out var previousAsset))
        {
            imageChangeGate.Release();
            return;
        }

        int attempt = 0;

        try
        {
            while (attempt < 3)
            {
                try
                {
                    await SetImage(previousAsset!);
                    CurrentAsset = previousAsset;

                    break;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= 3)
                    {
                        Console.Error.WriteLine($"ImmichFrame previous image load failed: {ex}");
                        this.Navigate(new ErrorViewModel(ex));
                    }
                }
            }
        }
        finally
        {
            imageChangeGate.Release();
        }
    }
    public void PauseImageAction()
    {
        PauseImage();
    }
    public void PauseImage()
    {
        ImagePaused = !ImagePaused;
        TimerEnabled = !ImagePaused;
        if (ImagePaused)
        {
            timerImageSwitcher?.Change(Timeout.Infinite, Timeout.Infinite);
            timerZoom?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        else
        {
            ResetTimer();
            timerZoom?.Change(0, 100);
        }
    }
    public void ResetTimer()
    {
        if (!disposed && !ImagePaused)
            timerImageSwitcher?.Change(Settings.Interval * 1000, Settings.Interval * 1000);
    }

    public void ExitApp()
    {
        Dispose();
        Environment.Exit(0);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        TimerEnabled = false;
        timerImageSwitcher?.Dispose();
        timerLiveTime?.Dispose();
        timerWeather?.Dispose();
        timerZoom?.Dispose();
        NextAsset?.Dispose();
        NextAsset = null;
        Images = null;
        retainedImage?.Dispose();
        retainedImage = null;
        WeatherImage?.Dispose();
        WeatherImage = null;
    }

    [ObservableProperty]
    public Settings settings;
    [ObservableProperty]
    private UiImage? images;
    [ObservableProperty]
    private string? imageDate;
    [ObservableProperty]
    private string? imageDesc;
    [ObservableProperty]
    private string? imageLocation;
    public bool IsImageLocationVisible => Settings.ShowImageLocation && !string.IsNullOrWhiteSpace(ImageLocation);
    [ObservableProperty]
    private double imageScale = 1.0;
    [ObservableProperty]
    private string? liveTime;
    [ObservableProperty]
    private string? weatherCurrent;
    [ObservableProperty]
    private string? weatherTemperature;
    [ObservableProperty]
    private Bitmap? weatherImage;
    [ObservableProperty]
    private bool imagePaused = false;

    partial void OnImagesChanged(UiImage? oldValue, UiImage? newValue)
    {
        retainedImage?.Dispose();
        retainedImage = oldValue;
    }

    partial void OnImageLocationChanged(string? value)
    {
        OnPropertyChanged(nameof(IsImageLocationVisible));
    }
}

public class PreloadedAsset : IDisposable
{
    public AssetResponseDto Asset { get; }
    private Stream? _image;
    public Stream? Image => _image;
    public PreloadedAsset(AssetResponseDto asset)
    {
        Asset = asset;
    }

    public async Task Preload(IImmichFrameLogic logic)
    {
        _image?.Dispose();
        _image = await Asset.ServeImage(logic);
    }

    public void Dispose()
    {
        _image?.Dispose();
        _image = null;
    }
}
public static class StretchHelper
{
    public static Stretch FromString(string stretch)
    {
        return Enum.TryParse(stretch, out Stretch result) ? result : Stretch.Uniform;
    }

    public static string ToString(Stretch stretch)
    {
        return stretch.ToString();
    }
}
