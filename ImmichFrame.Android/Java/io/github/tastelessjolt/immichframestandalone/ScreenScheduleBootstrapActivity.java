package io.github.tastelessjolt.immichframestandalone;

import android.app.Activity;
import android.os.Bundle;
import android.util.Log;

/**
 * Zero-display entry point for Frameo ROMs that reject background process
 * starts. A root boot hook launches this as a foreground component; it creates
 * the screen schedule and exits without loading the .NET/Avalonia runtime.
 */
public final class ScreenScheduleBootstrapActivity extends Activity {
    private static final String TAG = "ImmichFramePower";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        try {
            ScreenScheduleReceiver.handleAction(this, getIntent() == null ? null : getIntent().getAction());
        } catch (Exception exception) {
            Log.e(TAG, "Native screen schedule bootstrap failed", exception);
        } finally {
            finish();
        }
    }
}
