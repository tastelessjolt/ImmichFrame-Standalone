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

The TCP/IP install and smoke-test procedure below was last verified on 2026-07-19 with `v1.0.15.0-standalone.6`.

## Security boundary

Port 5555 must be reachable only from the trusted local network or through a private VPN/jump host. Never forward this port from the router to the public internet. Wi-Fi ADB provides shell access to the frame, and this particular device also has locally available root access.

## Enable Wi-Fi ADB

Android 6 uses legacy ADB-over-TCP rather than the pairing-code workflow introduced in newer Android releases. If TCP mode is no longer active after a reboot, connect the authorized USB host once and run:

```bash
adb devices -l
adb -s 7E5C1001 tcpip 5555
adb connect 192.168.0.7:5555
```

Both USB and TCP transports may appear until the cable is disconnected. Use the full TCP serial in every test command to ensure the test is actually running over the network:

```bash
adb -s 192.168.0.7:5555 get-state
adb -s 192.168.0.7:5555 shell 'getprop ro.build.version.release; getprop ro.product.cpu.abi; wm size; id'
```

Expected values include Android `6.0.1`, ABI `armeabi-v7a`, physical size `1280x800`, and a normal ADB shell identity.

If another computer will maintain the frame, authorize that computer's ADB public key while physical or root access is still available. Authorization is per ADB host key.

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

If the endpoint is unreachable after a frame reboot, first confirm that the frame still owns `192.168.0.7`. If the IP is correct but port 5555 is closed, re-enable TCP mode over USB. A persistent root boot configuration should only be added after confirming which boot-script mechanism this firmware supports.
