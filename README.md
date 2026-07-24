# ImmichFrame Standalone

This fork of [immichFrame/ImmichFrame](https://github.com/immichFrame/ImmichFrame) preserves the standalone .NET/Avalonia Android client: it connects directly to an Immich server and does not require the separate ImmichFrame Docker service.

The Android app is based on upstream `v1.0.15.0` and includes compatibility fixes for the current Immich API, Android 6 settings migration, and side-by-side installation under `io.github.tastelessjolt.immichframestandalone`.

> **Modification notice:** This repository contains a modified version of
> [immichFrame/ImmichFrame](https://github.com/immichFrame/ImmichFrame), based on upstream
> `v1.0.15.0`. Fork-specific changes began on **19 July 2026** and include current Immich API
> compatibility, the standalone Android package identity, Android 6 support, low-power frame UI
> and stability work, remote device maintenance, and sunrise screen scheduling. See
> [MODIFICATIONS.md](MODIFICATIONS.md) and the Git history for the dated change record.

The legacy client deletes and recreates the configured `ImmichFrameAlbumName` album when it starts. This behavior is inherited from the upstream version.

[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![GPLv3 License][license-shield]][license-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/tastelessjolt/ImmichFrame-Standalone">
    <img src="ImmichFrame/Assets/AppIcon.png" alt="Logo" width="200" height="200">
  </a>

  <h3 align="center">ImmichFrame Standalone</h3>

  <p align="center">
    An awesome way to display your photos as an digital photo frame
    <br />
    <a href="https://immich.app/"><strong>Explore immich »</strong></a>
    <br />
    <br />
    <a href="https://github.com/tastelessjolt/ImmichFrame-Standalone/issues">Report Bug</a>
    ·
    <a href="https://github.com/tastelessjolt/ImmichFrame-Standalone/issues">Request Feature</a>
  </p>
</div>

## ⚠️ Disclaimer

**This project is not affiliated with [immich][immich-github-url]!**

<!-- ABOUT THE PROJECT -->

## 🛈 About The Project

This project is a digital photo frame application that interfaces with your [immich][immich-github-url] server. It is a cross-platform C# .NET 8 project that currently supports Android, Linux, macOS, and Windows.

## ✨ Demo
### Web Demo
<img src="/design/demo/web_demo.png" alt="Web Demo" height="300">

### Client Demo
<img src="/design/demo/client_demo.png" alt="Client Demo" height="300">

<!-- GETTING STARTED -->

## 🚀 Getting Started

ImmichFrame is easy to run on your desired plattform. Get the latest stable release from the [release page][releases-url] and unzip to desired folder (Linux, macOS, Windows), or install APK (Android).

### 📋 Prerequisites

- A set up and functioning immich server that is accessible by the network of the ImmichFrame device.

### 🧪 Maintainer real-device testing

The Android 6 Frameo test device is reserved at `192.168.0.7` and accepts authorized ADB connections at `192.168.0.7:5555`. See the [real-device Wi-Fi ADB testing guide](docs/real-device-testing.md) for the verified build, update, launch, log, screenshot, and slideshow-navigation procedure.

<!-- USAGE EXAMPLES -->

## 🔧 Usage / Installation

### 🌐 Browser
- [ImmichFrame Web](/Install_Web.md#-installation)

### 💻 Windows, Linux, MacOS, Android
- [ImmichFrame Client](/Install_Client.md#-installation)


## ⚙️ Configuration

| **Section**             | **Config-Key**             | **Value**          | **Default**          | **Description**                                                                                      |
| ----------------------- | -------------------------- | ------------------ | -------------------- | ---------------------------------------------------------------------------------------------------- |
| **Required**            | **ImmichServerUrl**        | **string**         |                      | **The URL of your Immich server e.g. `http://photos.yourdomain.com` / `http://192.168.0.100:2283`.** |
| **Required**            | **ApiKey**                 | **string**         |                      | **Read more about how to obtain an [immich API key][immich-api-url].**                               |
| [Filtering](#filtering) | Albums                     | string[]           | []                   | UUID of album(s)                                                                                     |
| [Filtering](#filtering) | ExcludedAlbums             | string[]           | []                   | UUID of excluded album(s)                                                                            |
| [Filtering](#filtering) | People                     | string[]           | []                   | UUID of person(s)                                                                                    |
| [Filtering](#filtering) | ShowMemories               | boolean            | false                | If this is set, memories are displayed.                                                              |
| [Caching](#caching)     | RenewImagesDuration        | int                | 30                   | Interval in hours.                                                                                   |
| [Caching](#caching)     | DownloadImages             | boolean            | false                | \*Client only.                                                                                       |
| [Caching](#caching)     | RefreshAlbumPeopleInterval | int                | 12                   | Interval in hours. Determines how often images are pulled from a person in immich.                   |
| [Image](#image)         | ImageZoom                  | boolean            | true                 | Zooms into or out of an image and gives it a touch of life.                                          |
| [Image](#image)         | Interval                   | int                | 45                   | Image interval in seconds. How long a image is displayed in the frame.                               |
| [Image](#image)         | TransitionAnimation        | string             | Crossfade            | Client transition: Crossfade, Clean push, Edge wipe, Soft iris, Quiet cover, or Exposure dip.        |
| [Image](#image)         | TransitionDuration         | double             | 2                    | Duration in seconds; decimal values such as 0.52 are supported.                                      |
| [Image](#image)         | ImageStretch               | int                | Uniform              | \*Client only.                                                                                       |
| [Weather](#weather)     | WeatherApiKey              | string             |                      | Get api-key: [OpenWeatherMap][openweathermap-url].                                                   |
| [Weather](#weather)     | UnitSystem                 | imperial \| metric | imperial             | Imperial or metric system. (Fahrenheit or degrees)                                                   |
| [Weather](#weather)     | Language                   | string             | en                   | 2 digit ISO code, sets the language of the weather description.                                      |
| [Weather](#weather)     | ShowWeatherDescription     | boolean            | true                 | Displays the description of the current weather.                                                     |
| [Weather](#weather)     | WeatherFontSize            | int                | 36                   | \*Client only.                                                                                       |
| [Weather](#weather)     | WeatherLatLong             | string             | 40.730610,-73.935242 | Set the weather and sunrise location with latitude/longitude.                                        |
| [Weather](#weather)     | SunriseScreenScheduleEnabled | boolean          | true                 | Android: turn the display off at midnight and wake it at locally calculated sunrise.                 |
| [Clock](#clock)         | ShowClock                  | boolean            | true                 | Displays the current time.                                                                           |
| [Clock](#clock)         | ClockFontSize              | int                | 48                   | \*Client only.                                                                                       |
| [Clock](#clock)         | ClockFormat                | string             | hh:mm                | Time format.                                                                                         |
| [Metadata](#metadata)   | ShowImageDesc              | boolean            | true                 | Displays the description of the current image.                                                       |
| [Metadata](#metadata)   | ImageDescFontSize          | int                | 3                    | \*Client only.                                                                                       |
| [Metadata](#metadata)   | ShowImageLocation          | boolean            | true                 | Displays the location of the current image.                                                          |
| [Metadata](#metadata)   | ImageLocationFormat        | string             | City,State,Country   | \*Client only.                                                                                       |
| [Metadata](#metadata)   | ImageLocationFontSize      | int                | 36                   | \*Client only.                                                                                       |
| [Metadata](#metadata)   | ShowPhotoDate              | boolean            | true                 | Displays the date of the current image.                                                              |
| [Metadata](#metadata)   | PhotoDateFontSize          | int                | 36                   | \*Client only.                                                                                       |
| [Metadata](#metadata)   | PhotoDateFormat            | string             | yyyy-MM-dd           | Date format.                                                                                         |
| [UI](#ui)               | FontColor                  | string             | #FFFFFF              | \*Client only.                                                                                       |
| [Misc](#misc)           | ImmichFrameAlbumName       | string             |                      | Creates album and stores last 100 photos displayed.                                                  |
| [Misc](#misc)           | Margin                     | string             | 0,0,0,0              | \*Client only. Optionally fine tune margins to adjust for under/over scan.                           |
| [Misc](#misc)           | UnattendedMode             | boolean            | false                | \*Client only. Don't show error messages, silently keep trying.                                      |

### Filtering
You can get the UUIDs from the URL of the album/person. For this URL: `https://demo.immich.app/albums/85c85b29-c95d-4a8b-90f7-c87da1d518ba` this is the UUID: `85c85b29-c95d-4a8b-90f7-c87da1d518ba`

### Caching
Needs documentation

### Image
Needs documentation

### Weather
Weather is enabled by entering an API key. Get yours free from [OpenWeatherMap][openweathermap-url]

On Android, the optional sunrise screen schedule uses `WeatherLatLong` and the device time zone. Sunrise and sunset are calculated locally without another weather request. When enabled, the display stays on from sunrise until midnight and sleeps from midnight until sunrise. Opening the app or saving settings creates or replaces both alarms. Android removes application alarms during a reboot, so standard Android restores them through its boot receiver. This Frameo ROM instead needs the documented [device-specific boot hook](docs/real-device-testing.md#frameo-boot-hook) for unattended reboot or power-loss recovery because it rejects third-party background receivers. The hook is unnecessary if ImmichFrame is always opened manually after reboot. Screen power commands require root access on this Frameo device.

### Clock
Needs documentation

### Metadata
Needs documentation

### UI
Needs documentation

### Misc
Needs documentation

## 🛣️ Roadmap

- [x] Display random assets
- [x] Display Albums
- [x] Display Memories
- [x] Android build
- [x] Add License
- [x] Web app
- [ ] Add Additional Templates w/ Examples

See the [open issues](https://github.com/tastelessjolt/ImmichFrame-Standalone/issues) for a full list of proposed features (and known issues).

## ✍ Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📜 License

This modified fork remains licensed under the [GNU General Public License v3.0](LICENSE.txt).
Release pages identify the exact corresponding-source tag next to each APK. Components retained
under their own compatible licenses, including the bundled font, are documented in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## 🆘 Help

[Discord Channel][support-url]

## 🙏 Acknowledgments

- BIG thanks to the [immich team][immich-github-url] for creating an awesome tool

## 🌟 Star History

[![Star History Chart](https://api.star-history.com/svg?repos=tastelessjolt/ImmichFrame-Standalone&type=Date)](https://star-history.com/#tastelessjolt/ImmichFrame-Standalone&Date)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->

[contributors-shield]: https://img.shields.io/github/contributors/tastelessjolt/ImmichFrame-Standalone.svg?style=for-the-badge
[contributors-url]: https://github.com/tastelessjolt/ImmichFrame-Standalone/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/tastelessjolt/ImmichFrame-Standalone.svg?style=for-the-badge
[forks-url]: https://github.com/tastelessjolt/ImmichFrame-Standalone/network/members
[stars-shield]: https://img.shields.io/github/stars/tastelessjolt/ImmichFrame-Standalone.svg?style=for-the-badge
[stars-url]: https://github.com/tastelessjolt/ImmichFrame-Standalone/stargazers
[issues-shield]: https://img.shields.io/github/issues/tastelessjolt/ImmichFrame-Standalone.svg?style=for-the-badge
[issues-url]: https://github.com/tastelessjolt/ImmichFrame-Standalone/issues
[license-shield]: https://img.shields.io/github/license/tastelessjolt/ImmichFrame-Standalone.svg?style=for-the-badge
[license-url]: https://github.com/tastelessjolt/ImmichFrame-Standalone/blob/standalone-android/LICENSE.txt
[releases-url]: https://github.com/tastelessjolt/ImmichFrame-Standalone/releases/latest
[support-url]: https://discord.com/channels/979116623879368755/1217843270244372480
[openweathermap-url]: https://openweathermap.org/
[immich-github-url]: https://github.com/immich-app/immich
[immich-api-url]: https://immich.app/docs/features/command-line-interface#obtain-the-api-key
