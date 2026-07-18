#!/system/bin/sh

# This device-specific hook is called synchronously by
# /system/bin/install-recovery.sh. Keeping it in the same init service process
# group prevents Android init from killing it before boot has completed.
attempt=0
while [ "$(getprop sys.boot_completed)" != "1" ] && [ "$attempt" -lt 150 ]; do
  attempt=$((attempt + 1))
  sleep 2
done

if [ "$(getprop sys.boot_completed)" != "1" ]; then
  log -t ImmichFramePower "Boot hook timed out waiting for Android"
  exit 1
fi

# This board starts at 1970 when its RTC is unavailable and corrects the clock
# from the network later. Wait up to 15 minutes for a plausible year so alarms
# are never calculated against the temporary epoch time.
attempt=0
while [ "$(date +%Y)" -lt 2024 ] && [ "$attempt" -lt 450 ]; do
  attempt=$((attempt + 1))
  sleep 2
done

if [ "$(date +%Y)" -lt 2024 ]; then
  log -t ImmichFramePower "Boot hook timed out waiting for the system clock"
  exit 1
fi

# Give PackageManager and the home activity a final moment to settle. The
# activity has Theme.NoDisplay and immediately exits.
sleep 5

package_name="io.github.tastelessjolt.immichframestandalone"
component="$package_name/.ScreenScheduleBootstrapActivity"
if ! pm path "$package_name" >/dev/null 2>&1; then
  log -t ImmichFramePower "Boot hook skipped: $package_name is not installed"
  exit 0
fi

if am start -n "$component" >/dev/null 2>&1; then
  log -t ImmichFramePower "Boot hook started the native schedule bootstrap"
else
  log -t ImmichFramePower "Boot hook could not start the native schedule bootstrap"
  exit 1
fi
