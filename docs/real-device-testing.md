# Real-device testing over Wi-Fi ADB

This guide documents the maintainer's Android 6 Frameo test device and the exact network workflow used to install and validate ImmichFrame Standalone without a USB data connection.

## Test device

| Property | Value |
| --- | --- |
| Model | `ZN-DP1002` |
| Android | `6.0.1` |
| CPU ABI | `armeabi-v7a` (32-bit ARM) |
| Display | `1280x800` |
| Reserved LAN IP | `192.168.0.7` |
| Wi-Fi ADB endpoint | `192.168.0.7:5555` |
| App package | `io.github.tastelessjolt.immichframestandalone` |
| Activity | `io.github.tastelessjolt.immichframestandalone/.MainActivity` |

The TCP/IP install, boot scheduling, and smoke-test procedure below was last verified on 2026-07-19 with `v1.0.15.0-standalone.9`.

## Security boundary

Port 5555 must be reachable only from the trusted local network or through a private VPN/jump host. Never forward this port from the router to the public internet. Wi-Fi ADB provides shell access to the frame, and this particular device also has locally available root access.

## Connect to the frame

Wi-Fi ADB is configured to start automatically after a frame reboot. For normal maintenance from an already authorized computer, connect directly:

```bash
adb connect 192.168.0.7:5555
adb -s 192.168.0.7:5555 get-state
```

The expected state is `device`. Always use the full TCP serial in subsequent commands so that a connected USB cable cannot accidentally hide a network-connection failure.

## Persistent Wi-Fi ADB configuration

Android 6 uses legacy RSA-authorized ADB-over-TCP rather than the pairing-code workflow introduced in newer Android releases. The initial TCP session was enabled from the authorized USB host with:

```bash
adb devices -l
adb -s 7E5C1001 tcpip 5555
adb connect 192.168.0.7:5555
```

The one-session command above sets the volatile property `service.adb.tcp.port`. This rooted frame persists the listener without modifying the read-only `/system` partition by setting Android's persistent counterpart:

```bash
adb -s 192.168.0.7:5555 shell \
  su -c 'setprop persist.adb.tcp.port 5555'
adb -s 192.168.0.7:5555 shell \
  getprop persist.adb.tcp.port
```

The expected value is `5555`. This setting is stored by Android under `/data/property`; do not edit the property file directly.

Persistence was verified with a real reboot on 2026-07-19. After boot, `service.adb.tcp.port` was empty, `persist.adb.tcp.port` remained `5555`, the frame reacquired `192.168.0.7`, and a new TCP ADB connection succeeded automatically. USB debugging is already configured to start `adbd` during boot.

Both USB and TCP transports may appear while the cable is connected. Verify the TCP transport and device characteristics with:

```bash
adb -s 192.168.0.7:5555 get-state
adb -s 192.168.0.7:5555 shell 'getprop ro.build.version.release; getprop ro.product.cpu.abi; wm size; id'
```

Expected values include Android `6.0.1`, ABI `armeabi-v7a`, physical size `1280x800`, and a normal ADB shell identity.

## Automatic sunrise screen schedule

ImmichFrame Standalone can keep the display on from local sunrise until midnight, turn it off at `00:00`, and wake it at the next sunrise. `SunriseScreenScheduleEnabled` is enabled by default and can be changed under **Weather** in the app settings.

The schedule uses `WeatherLatLong` and the Android device time zone. Sunrise and sunset are calculated locally with no additional weather API request or polling. Sunset is recorded for diagnostics; the configured power transition is midnight-to-sunrise. Exact `RTC_WAKEUP` alarms are refreshed whenever the app starts or settings are saved. Standard Android devices also refresh them through the registered boot, clock, timezone, date, and package-replacement receiver; this Frameo uses the root boot hook below because its ROM rejects those background deliveries.

The alarm PendingIntents open a zero-display, native Java activity rather than a broadcast receiver. This is intentional: the Frameo ROM labels background process forks as bad, but permits an activity PendingIntent to start. The native scheduler runs in the lightweight `:screen_schedule` process without loading Mono or Avalonia. At sunrise it wakes the display and starts the slideshow; at midnight it turns the display off.

### Frameo boot hook

This ROM also rejects third-party `BOOT_COMPLETED` receivers, and its clock starts at 1970 before network time synchronization. A device-specific root hook is therefore installed at `/system/bin/immichframe-schedule-boot.sh`. The existing `/system/bin/install-recovery.sh` invokes it synchronously as part of Android's `flash_recovery` init service. The hook waits for `sys.boot_completed=1`, waits for the year to be at least 2024, and then starts `.ScreenScheduleBootstrapActivity` without showing UI.

The tracked copy of the hook is [device/frameo/immichframe-schedule-boot.sh](../device/frameo/immichframe-schedule-boot.sh). The installed files last verified on 2026-07-19 have these MD5 checksums:

```text
f94ebaf5c91c3f89c427b19fa038820d  /system/bin/install-recovery.sh
8c2871ebfd65954358cb5a4b89bc8476  /system/bin/immichframe-schedule-boot.sh
```

The untouched original recovery script has MD5 `70cbf93d526ecd134752a8af15c95f40` and is backed up in both locations below:

```text
/data/local/immichframe-backups/install-recovery.sh.pre-immichframe-schedule
Desktop/immichframe-app-data-backup/frameo-system-pre-schedule-20260719/install-recovery.sh
```

To remove the hook, restore only that exact backup on this exact device, delete the helper, restore ownership/permissions/SELinux context, and return `/system` to read-only:

```bash
adb -s 192.168.0.7:5555 shell "su -c '
  mount -o remount,rw /system &&
  cp /data/local/immichframe-backups/install-recovery.sh.pre-immichframe-schedule /system/bin/install-recovery.sh &&
  rm -f /system/bin/immichframe-schedule-boot.sh &&
  chown 0:0 /system/bin/install-recovery.sh &&
  chmod 0750 /system/bin/install-recovery.sh &&
  restorecon /system/bin/install-recovery.sh &&
  sync &&
  mount -o remount,ro /system
'"
```

Do not restore this device-specific system script to another Android build. A firmware update may replace it; after an update, verify the stock script before reapplying the one-line hook.

This Frameo's root access is required for Android `KEYCODE_SLEEP` and `KEYCODE_WAKEUP`. Confirm the calculated alarms and recent power operations with:

```bash
adb -s 192.168.0.7:5555 shell \
  "dumpsys alarm | grep -A 5 -E 'immichframestandalone.action.(SLEEP|WAKE)'"
adb -s 192.168.0.7:5555 shell \
  "logcat -d | grep ImmichFramePower | tail -n 30"
```

For an intentional end-to-end test, these activity starts briefly turn the physical display off and back on:

```bash
adb -s 192.168.0.7:5555 shell \
  "su -c 'am start -n io.github.tastelessjolt.immichframestandalone/.ScreenScheduleBootstrapActivity -a io.github.tastelessjolt.immichframestandalone.action.SLEEP'"
adb -s 192.168.0.7:5555 shell \
  "su -c 'am start -n io.github.tastelessjolt.immichframestandalone/.ScreenScheduleBootstrapActivity -a io.github.tastelessjolt.immichframestandalone.action.WAKE'"
```

## Restore access on another computer

ADB authorization belongs to the host RSA key, not to a user account or GitHub checkout. Back up these two files from the currently authorized Windows host:

```text
%USERPROFILE%\.android\adbkey
%USERPROFILE%\.android\adbkey.pub
```

`adbkey` is the unencrypted private key and must be treated as a secret. Never commit either file to this repository, paste the private key into an issue or log, or place it in an unencrypted shared folder.

The encrypted backups are stored in Vaultwarden under folder ID `d00e0595-8e2a-4861-a19b-b8d798a6f75b`. Use these item references when recovering the authorized host key:

| File | Vaultwarden item ID |
| --- | --- |
| `adbkey` | `ee3c592f-88da-4120-bba9-f6cc118938a2` |
| `adbkey.pub` | `674bac8b-b90e-44df-bc6f-e2ffbc754709` |

These identifiers locate the encrypted Vaultwarden records; they are not substitutes for the key files and do not grant access without the Vaultwarden account.

On a replacement Windows computer:

1. Install Android Platform Tools but do not start `adb` yet.
2. Restore the files with the exact names `adbkey` and `adbkey.pub` under `%USERPROFILE%\.android\`.
3. Run `adb kill-server`, followed by `adb start-server`.
4. Run `adb connect 192.168.0.7:5555` and verify the TCP serial reports `device`.

On Linux or macOS, restore them under `~/.android/` and restrict the private key:

```bash
mkdir -p ~/.android
chmod 700 ~/.android
chmod 600 ~/.android/adbkey
chmod 644 ~/.android/adbkey.pub
adb kill-server
adb start-server
adb connect 192.168.0.7:5555
```

When Windows `adb.exe` is invoked from WSL, it uses the Windows profile's `.android` directory; a different key under the WSL home directory does not replace it.

If the private key backup is unavailable, the new host key must be authorized again using physical USB access or root file access on the frame. Authorization is per ADB host key, and possession of the backed-up private key grants the same shell access as the original computer.

## Preserve the separate APK-signing key

The ADB host key above authorizes remote shell access. It is not the key that signs ImmichFrame APK updates. The current .NET Android build uses a separate keystore whose certificate SHA-256 fingerprint is:

```text
628267ffd63fc93fc6d893f1aa2bf4a84f3a04feca43e212fc7aa8e494c9cf8d
```

On the current Linux/WSL build host, the matching .NET Android default keystore is located at:

```text
~/.local/share/Xamarin/Mono for Android/debug.keystore
```

Back up this keystore as a second encrypted attachment, together with its alias and passwords. Never commit it to Git. Losing it prevents future APKs from updating the installed package in place, even if ADB access still works. Restoring only the ADB RSA key is therefore insufficient for a complete new-PC recovery.

## Build the tested APK

Run the tests first:

```bash
dotnet test ImmichFrame.Core.Tests/ImmichFrame.Core.Tests.csproj -c Release
```

Build the 32-bit ARM release, supplying the local Android SDK and JDK paths:

```bash
dotnet build ImmichFrame.Android/ImmichFrame.Android.csproj \
  -c Release \
  -p:AndroidSdkDirectory=/path/to/android-sdk \
  -p:JavaSdkDirectory=/path/to/jdk
```

The signed APK is produced at:

```text
ImmichFrame.Android/bin/Release/net8.0-android/android-arm/io.github.tastelessjolt.immichframestandalone-Signed.apk
```

Future updates must keep the package ID and signing certificate unchanged. Increment both `android:versionCode` and `android:versionName` in `ImmichFrame.Android/Properties/AndroidManifest.xml` before publishing a new build.

## Install over TCP/IP

Clear the old log, update the existing installation without deleting its settings, and launch it:

```bash
adb -s 192.168.0.7:5555 logcat -c
adb -s 192.168.0.7:5555 install -r \
  ImmichFrame.Android/bin/Release/net8.0-android/android-arm/io.github.tastelessjolt.immichframestandalone-Signed.apk
adb -s 192.168.0.7:5555 shell am start \
  -n io.github.tastelessjolt.immichframestandalone/.MainActivity
```

The approximately 18.5 MiB APK took about 34 seconds to transfer during the recorded Wi-Fi test. Do not interrupt `adb install` merely because it is temporarily quiet.

Verify the installed version and foreground activity:

```bash
adb -s 192.168.0.7:5555 shell dumpsys package \
  io.github.tastelessjolt.immichframestandalone
adb -s 192.168.0.7:5555 shell dumpsys activity activities
```

Confirm that `versionCode` and `versionName` match the build and that `.MainActivity` is resumed.

## Smoke-test the slideshow

Allow the first photo to load, then exercise the same key paths used by the on-screen controls:

```bash
# Pause/unpause
adb -s 192.168.0.7:5555 shell input keyevent 62

# Previous photo
adb -s 192.168.0.7:5555 shell input keyevent 21

# Next photo
adb -s 192.168.0.7:5555 shell input keyevent 22
```

For navigation-history validation:

1. Pause the slideshow.
2. Capture photo A.
3. Press Right three times, waiting for each image to settle, and capture B, C, and D.
4. Press Left three times and verify that the frame returns to C, B, and A.
5. Press Right once and verify that it returns to B.
6. Press Space to resume the slideshow before finishing.

Capture evidence without using a USB connection:

```bash
adb -s 192.168.0.7:5555 shell screencap -p /sdcard/immichframe-test.png
adb -s 192.168.0.7:5555 pull /sdcard/immichframe-test.png .
adb -s 192.168.0.7:5555 shell rm /sdcard/immichframe-test.png
```

## Inspect failures

Read logs through the TCP transport:

```bash
adb -s 192.168.0.7:5555 logcat -d
```

Pay particular attention to:

- `FATAL EXCEPTION`
- `NetworkOnMainThreadException`
- `ImmichFrame image load failed`
- `ImmichFrame previous image load failed`
- API deserialization errors

Useful recovery commands are:

```bash
adb disconnect 192.168.0.7:5555
adb connect 192.168.0.7:5555
adb -s 192.168.0.7:5555 shell am force-stop \
  io.github.tastelessjolt.immichframestandalone
adb -s 192.168.0.7:5555 shell am start \
  -n io.github.tastelessjolt.immichframestandalone/.MainActivity
```

If the endpoint is unreachable after a frame reboot, first confirm that the frame still owns `192.168.0.7`. If the IP is correct but port 5555 is closed, use the authorized USB transport to verify that `persist.adb.tcp.port` is still `5555`, then restart TCP mode with `adb tcpip 5555`.
