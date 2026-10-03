package com.example.simregionswitch;

import android.Manifest;
import android.app.Activity;
import android.app.Instrumentation;
import android.app.UiAutomation;
import android.content.Context;
import android.os.Bundle;
import android.os.PersistableBundle;
import android.telephony.CarrierConfigManager;

import org.lsposed.hiddenapibypass.HiddenApiBypass;

import java.util.Locale;

/**
 * Self-targeting instrumentation used on newer Android releases.
 * It adopts shell-granted MODIFY_PHONE_STATE while keeping an app UID,
 * which avoids the Android 16/17 "shell UID" rejection path.
 */
public class CarrierOverrideInstrumentation extends Instrumentation {

    private Bundle instrumentationArguments;

    @Override
    public void onCreate(Bundle arguments) {
        instrumentationArguments = arguments == null ? Bundle.EMPTY : new Bundle(arguments);
        super.onCreate(arguments);
        start();
    }

    @Override
    public void onStart() {
        Bundle out = new Bundle();
        UiAutomation ui = null;
        try {
            Bundle args = instrumentationArguments;
            String action = args != null ? args.getString("action", "") : "";
            int subId = Integer.parseInt(args != null ? args.getString("subId", "-1") : "-1");

            if (subId < 0) {
                throw new IllegalArgumentException("Invalid subscription id");
            }

            ui = getUiAutomation();
            ui.adoptShellPermissionIdentity(Manifest.permission.MODIFY_PHONE_STATE);

            Context context = getTargetContext();
            CarrierConfigManager manager =
                    (CarrierConfigManager) context.getSystemService(Context.CARRIER_CONFIG_SERVICE);
            if (manager == null) {
                throw new IllegalStateException("CarrierConfigManager unavailable");
            }

            if ("restore".equals(action)) {
                invokeOverride(manager, subId, null);
                out.putString("result", "restored");
            } else if ("apply".equals(action)) {
                String country = args != null ? args.getString("country", "") : "";
                country = country.trim().toLowerCase(Locale.ROOT);
                if (!country.matches("[a-z]{2}")) {
                    throw new IllegalArgumentException("Country must be ISO alpha-2");
                }

                PersistableBundle override = new PersistableBundle();
                override.putString("sim_country_iso_override_string", country);
                invokeOverride(manager, subId, override);
                out.putString("result", "applied");
                out.putString("country", country);
            } else {
                throw new IllegalArgumentException("Unknown action: " + action);
            }

            finish(Activity.RESULT_OK, out);
        } catch (Throwable t) {
            out.putString("result", "error");
            out.putString("error", t.getClass().getSimpleName() + ": " + t.getMessage());
            finish(Activity.RESULT_CANCELED, out);
        } finally {
            if (ui != null) {
                try {
                    ui.dropShellPermissionIdentity();
                } catch (Throwable ignored) {
                }
            }
        }
    }

    private static void invokeOverride(
            CarrierConfigManager manager,
            int subId,
            PersistableBundle values
    ) throws Throwable {
        Throwable first;
        try {
            HiddenApiBypass.invoke(
                    CarrierConfigManager.class,
                    manager,
                    "overrideConfig",
                    subId,
                    values
            );
            return;
        } catch (Throwable t) {
            first = t;
        }

        try {
            HiddenApiBypass.invoke(
                    CarrierConfigManager.class,
                    manager,
                    "overrideConfig",
                    subId,
                    values,
                    false
            );
        } catch (Throwable second) {
            second.addSuppressed(first);
            throw second;
        }
    }
}
