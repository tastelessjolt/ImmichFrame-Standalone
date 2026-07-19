# Modification notice

This repository is a modified version of
[immichFrame/ImmichFrame](https://github.com/immichFrame/ImmichFrame), based on upstream release
`v1.0.15.0` (upstream commit `1d01bb565d0b4882ec274c6c009e52239d8c77f4`). The modified work
remains licensed under the [GNU General Public License version 3](LICENSE.txt).

Fork-specific modifications began on **19 July 2026**. The Git history is the authoritative,
file-level record of authorship and dates. The principal changes are:

- Updated the standalone client for the current Immich API and generated API models.
- Added a distinct Android application identity,
  `io.github.tastelessjolt.immichframestandalone`, so the fork can be installed side by side.
- Added Android 6 settings migration and compatibility fixes.
- Improved low-powered-frame usability: larger touch targets, named people selection, paused
  controls, Google Sans UI typography, metadata legibility, and low-cost letterbox options.
- Reduced slideshow memory pressure and fixed asynchronous bitmap, dispatcher, and weather-refresh
  races on 32-bit Android.
- Corrected backward slideshow navigation and preserved bounded image history.
- Documented Wi-Fi ADB maintenance and tested-device reboot recovery.
- Added locally calculated midnight-to-sunrise screen power scheduling and a device-specific,
  opt-in Frameo boot hook.

Release notes provide the changes and verification specific to each distributed APK. Third-party
components and their licenses are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
