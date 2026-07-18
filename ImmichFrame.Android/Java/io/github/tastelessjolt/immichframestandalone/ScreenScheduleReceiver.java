package io.github.tastelessjolt.immichframestandalone;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.os.PowerManager;
import android.util.Log;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.nio.charset.Charset;
import java.text.SimpleDateFormat;
import java.util.Calendar;
import java.util.Date;
import java.util.Locale;
import java.util.TimeZone;

/**
 * A native, isolated receiver keeps the power schedule independent of the
 * Avalonia/.NET process. Android can therefore restore and execute alarms
 * after boot without loading the slideshow runtime.
 */
public final class ScreenScheduleReceiver extends BroadcastReceiver {
    private static final String TAG = "ImmichFramePower";
    private static final String PACKAGE_NAME = "io.github.tastelessjolt.immichframestandalone";
    private static final String SLEEP_ACTION = PACKAGE_NAME + ".action.SLEEP";
    private static final String WAKE_ACTION = PACKAGE_NAME + ".action.WAKE";
    private static final int SLEEP_REQUEST_CODE = 4101;
    private static final int WAKE_REQUEST_CODE = 4102;
    private static final int SLEEP_KEY_CODE = 223;
    private static final int WAKE_KEY_CODE = 224;
    private static final double OFFICIAL_ZENITH_DEGREES = 90.833d;
    private static final long DAY_MILLISECONDS = 24L * 60L * 60L * 1000L;

    @Override
    public void onReceive(Context context, Intent intent) {
        String action = intent == null ? null : intent.getAction();
        try {
            handleAction(context, action);
        } catch (Exception exception) {
            Log.e(TAG, "Native screen schedule receiver failed", exception);
        }
    }

    static void handleAction(Context context, String action) throws Exception {
        if (SLEEP_ACTION.equals(action)) {
            schedule(context, false);
            sleep(context);
        } else if (WAKE_ACTION.equals(action)) {
            wake(context);
            schedule(context, false);
        } else {
            schedule(context, true);
        }
    }

    static void schedule(Context context, boolean applyCurrentState) throws Exception {
        File settingsFile = new File(context.getFilesDir(), "Settings.json");
        if (!settingsFile.isFile()) {
            cancel(context);
            return;
        }

        JSONObject settings = new JSONObject(readFile(settingsFile));
        if (!settings.optBoolean("SunriseScreenScheduleEnabled", true)) {
            cancel(context);
            Log.i(TAG, "Sunrise screen schedule is disabled");
            return;
        }

        String[] coordinates = settings.optString("WeatherLatLong", "").split(",");
        if (coordinates.length != 2)
            throw new IllegalArgumentException("Weather coordinates are unavailable or invalid");

        double latitude = Double.parseDouble(coordinates[0].trim());
        double longitude = Double.parseDouble(coordinates[1].trim());
        if (latitude < -90d || latitude > 90d || longitude < -180d || longitude > 180d)
            throw new IllegalArgumentException("Weather coordinates are out of range");

        SchedulePlan plan = createPlan(System.currentTimeMillis(), latitude, longitude, TimeZone.getDefault());
        if (plan == null)
            throw new IllegalStateException("No sunrise could be calculated for the configured location");

        cancelLegacyBroadcastAlarms(context);
        setAlarm(context, SLEEP_ACTION, SLEEP_REQUEST_CODE, plan.nextSleep);
        setAlarm(context, WAKE_ACTION, WAKE_REQUEST_CODE, plan.nextWake);
        Log.i(
            TAG,
            "Native schedule: sleep " + format(plan.nextSleep)
                + ", wake " + format(plan.nextWake)
                + ", sunset " + format(plan.todaySunset));

        if (applyCurrentState && plan.shouldBeAsleep)
            sleep(context);
    }

    private static SchedulePlan createPlan(long now, double latitude, double longitude, TimeZone timeZone) {
        Calendar localNow = Calendar.getInstance(timeZone);
        localNow.setTimeInMillis(now);
        int year = localNow.get(Calendar.YEAR);
        int month = localNow.get(Calendar.MONTH);
        int day = localNow.get(Calendar.DAY_OF_MONTH);

        Long todaySunrise = calculateSolarEvent(year, month, day, latitude, longitude, timeZone, true);
        Long todaySunset = calculateSolarEvent(year, month, day, latitude, longitude, timeZone, false);
        Long nextWake = null;

        Calendar candidateDate = Calendar.getInstance(timeZone);
        candidateDate.clear();
        candidateDate.set(year, month, day, 12, 0, 0);
        for (int dayOffset = 0; dayOffset <= 370; dayOffset++) {
            Long candidate = calculateSolarEvent(
                candidateDate.get(Calendar.YEAR),
                candidateDate.get(Calendar.MONTH),
                candidateDate.get(Calendar.DAY_OF_MONTH),
                latitude,
                longitude,
                timeZone,
                true);
            if (candidate != null && candidate > now + 60_000L) {
                nextWake = candidate;
                break;
            }
            candidateDate.add(Calendar.DAY_OF_MONTH, 1);
        }

        if (nextWake == null)
            return null;

        Calendar nextMidnight = Calendar.getInstance(timeZone);
        nextMidnight.setTimeInMillis(now);
        nextMidnight.add(Calendar.DAY_OF_MONTH, 1);
        nextMidnight.set(Calendar.HOUR_OF_DAY, 0);
        nextMidnight.set(Calendar.MINUTE, 0);
        nextMidnight.set(Calendar.SECOND, 0);
        nextMidnight.set(Calendar.MILLISECOND, 0);

        return new SchedulePlan(
            nextMidnight.getTimeInMillis(),
            nextWake,
            todaySunrise != null && now < todaySunrise,
            todaySunset);
    }

    private static Long calculateSolarEvent(
        int year,
        int month,
        int day,
        double latitude,
        double longitude,
        TimeZone timeZone,
        boolean sunrise) {
        Calendar localDate = Calendar.getInstance(timeZone);
        localDate.clear();
        localDate.set(year, month, day, 12, 0, 0);
        int dayOfYear = localDate.get(Calendar.DAY_OF_YEAR);

        double longitudeHour = longitude / 15d;
        double approximateTime = dayOfYear + ((sunrise ? 6d : 18d) - longitudeHour) / 24d;
        double meanAnomaly = 0.9856d * approximateTime - 3.289d;
        double trueLongitude = normalizeDegrees(
            meanAnomaly
                + 1.916d * sinDegrees(meanAnomaly)
                + 0.020d * sinDegrees(2d * meanAnomaly)
                + 282.634d);

        double rightAscension = normalizeDegrees(toDegrees(Math.atan(0.91764d * Math.tan(toRadians(trueLongitude)))));
        double longitudeQuadrant = Math.floor(trueLongitude / 90d) * 90d;
        double rightAscensionQuadrant = Math.floor(rightAscension / 90d) * 90d;
        rightAscension = (rightAscension + longitudeQuadrant - rightAscensionQuadrant) / 15d;

        double sinDeclination = 0.39782d * sinDegrees(trueLongitude);
        double cosDeclination = Math.cos(Math.asin(sinDeclination));
        double cosHourAngle =
            (cosDegrees(OFFICIAL_ZENITH_DEGREES) - sinDeclination * sinDegrees(latitude))
                / (cosDeclination * cosDegrees(latitude));
        if (cosHourAngle > 1d || cosHourAngle < -1d)
            return null;

        double hourAngle = sunrise
            ? 360d - toDegrees(Math.acos(cosHourAngle))
            : toDegrees(Math.acos(cosHourAngle));
        hourAngle /= 15d;

        double localMeanTime = hourAngle + rightAscension - 0.06571d * approximateTime - 6.622d;
        double utcHour = normalizeHours(localMeanTime - longitudeHour);

        Calendar utc = Calendar.getInstance(TimeZone.getTimeZone("UTC"));
        utc.clear();
        utc.set(year, month, day, 0, 0, 0);
        long result = utc.getTimeInMillis() + Math.round(utcHour * 60d * 60d * 1000d);

        Calendar localResult = Calendar.getInstance(timeZone);
        localResult.setTimeInMillis(result);
        int targetDate = dateKey(year, month, day);
        while (dateKey(localResult) < targetDate) {
            result += DAY_MILLISECONDS;
            localResult.setTimeInMillis(result);
        }
        while (dateKey(localResult) > targetDate) {
            result -= DAY_MILLISECONDS;
            localResult.setTimeInMillis(result);
        }

        return result;
    }

    private static void setAlarm(Context context, String action, int requestCode, long triggerAtMillis) {
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null)
            throw new IllegalStateException("Android AlarmManager is unavailable");

        PendingIntent pendingIntent = createPendingIntent(context, action, requestCode);
        if (Build.VERSION.SDK_INT >= 31 && !alarmManager.canScheduleExactAlarms()) {
            alarmManager.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, triggerAtMillis, pendingIntent);
        } else if (Build.VERSION.SDK_INT >= 23) {
            alarmManager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, triggerAtMillis, pendingIntent);
        } else {
            alarmManager.setExact(AlarmManager.RTC_WAKEUP, triggerAtMillis, pendingIntent);
        }
    }

    private static PendingIntent createPendingIntent(Context context, String action, int requestCode) {
        Intent intent = new Intent(context, ScreenScheduleBootstrapActivity.class)
            .setAction(action)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_NO_ANIMATION | Intent.FLAG_ACTIVITY_EXCLUDE_FROM_RECENTS);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= 23)
            flags |= PendingIntent.FLAG_IMMUTABLE;
        return PendingIntent.getActivity(context, requestCode, intent, flags);
    }

    private static PendingIntent createLegacyBroadcastPendingIntent(Context context, String action, int requestCode) {
        Intent intent = new Intent(context, ScreenScheduleReceiver.class).setAction(action);
        int flags = PendingIntent.FLAG_UPDATE_CURRENT;
        if (Build.VERSION.SDK_INT >= 23)
            flags |= PendingIntent.FLAG_IMMUTABLE;
        return PendingIntent.getBroadcast(context, requestCode, intent, flags);
    }

    private static void cancelLegacyBroadcastAlarms(Context context) {
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null)
            return;
        alarmManager.cancel(createLegacyBroadcastPendingIntent(context, SLEEP_ACTION, SLEEP_REQUEST_CODE));
        alarmManager.cancel(createLegacyBroadcastPendingIntent(context, WAKE_ACTION, WAKE_REQUEST_CODE));
    }

    private static void cancel(Context context) {
        AlarmManager alarmManager = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
        if (alarmManager == null)
            return;
        alarmManager.cancel(createPendingIntent(context, SLEEP_ACTION, SLEEP_REQUEST_CODE));
        alarmManager.cancel(createPendingIntent(context, WAKE_ACTION, WAKE_REQUEST_CODE));
        cancelLegacyBroadcastAlarms(context);
    }

    private static void sleep(Context context) {
        PowerManager powerManager = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
        if (powerManager != null && !powerManager.isInteractive())
            return;
        executeRootKeyEvent(SLEEP_KEY_CODE, "sleep");
    }

    private static void wake(Context context) {
        PowerManager powerManager = (PowerManager) context.getSystemService(Context.POWER_SERVICE);
        if (powerManager != null && !powerManager.isInteractive()) {
            PowerManager.WakeLock wakeLock = powerManager.newWakeLock(
                PowerManager.SCREEN_BRIGHT_WAKE_LOCK
                    | PowerManager.ACQUIRE_CAUSES_WAKEUP
                    | PowerManager.ON_AFTER_RELEASE,
                "ImmichFrame::NativeSunriseWakeLock");
            wakeLock.acquire(30_000L);
            executeRootKeyEvent(WAKE_KEY_CODE, "wake");
        }

        Intent activityIntent = new Intent();
        activityIntent.setClassName(PACKAGE_NAME, PACKAGE_NAME + ".MainActivity");
        activityIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_REORDER_TO_FRONT | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        context.startActivity(activityIntent);
    }

    private static void executeRootKeyEvent(int keyCode, String operation) {
        Process process = null;
        try {
            process = new ProcessBuilder("su", "-c", "input keyevent " + keyCode)
                .redirectErrorStream(true)
                .start();
            int exitCode = process.waitFor();
            if (exitCode == 0)
                Log.i(TAG, "Native screen " + operation + " command completed");
            else
                Log.e(TAG, "Native screen " + operation + " command failed with exit code " + exitCode);
        } catch (Exception exception) {
            Log.e(TAG, "Native screen " + operation + " command failed", exception);
        } finally {
            if (process != null)
                process.destroy();
        }
    }

    private static String readFile(File file) throws Exception {
        FileInputStream input = new FileInputStream(file);
        ByteArrayOutputStream output = new ByteArrayOutputStream();
        try {
            byte[] buffer = new byte[4096];
            int count;
            while ((count = input.read(buffer)) != -1)
                output.write(buffer, 0, count);
            return new String(output.toByteArray(), Charset.forName("UTF-8"));
        } finally {
            input.close();
            output.close();
        }
    }

    private static String format(Long value) {
        if (value == null)
            return "unavailable";
        return new SimpleDateFormat("yyyy-MM-dd HH:mm:ss Z", Locale.US).format(new Date(value));
    }

    private static int dateKey(Calendar calendar) {
        return dateKey(
            calendar.get(Calendar.YEAR),
            calendar.get(Calendar.MONTH),
            calendar.get(Calendar.DAY_OF_MONTH));
    }

    private static int dateKey(int year, int month, int day) {
        return year * 10_000 + (month + 1) * 100 + day;
    }

    private static double normalizeDegrees(double value) {
        return ((value % 360d) + 360d) % 360d;
    }

    private static double normalizeHours(double value) {
        return ((value % 24d) + 24d) % 24d;
    }

    private static double toRadians(double degrees) {
        return degrees * Math.PI / 180d;
    }

    private static double toDegrees(double radians) {
        return radians * 180d / Math.PI;
    }

    private static double sinDegrees(double degrees) {
        return Math.sin(toRadians(degrees));
    }

    private static double cosDegrees(double degrees) {
        return Math.cos(toRadians(degrees));
    }

    private static final class SchedulePlan {
        final long nextSleep;
        final long nextWake;
        final boolean shouldBeAsleep;
        final Long todaySunset;

        SchedulePlan(long nextSleep, long nextWake, boolean shouldBeAsleep, Long todaySunset) {
            this.nextSleep = nextSleep;
            this.nextWake = nextWake;
            this.shouldBeAsleep = shouldBeAsleep;
            this.todaySunset = todaySunset;
        }
    }
}
