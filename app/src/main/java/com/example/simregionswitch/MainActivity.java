package com.example.simregionswitch;

import android.Manifest;
import android.app.Activity;
import android.content.ComponentName;
import android.content.Intent;
import android.content.ServiceConnection;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.os.IBinder;
import android.provider.Settings;
import android.telephony.SubscriptionInfo;
import android.telephony.SubscriptionManager;
import android.telephony.TelephonyManager;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

import rikka.shizuku.Shizuku;

public class MainActivity extends Activity {

    private static final int REQ_PHONE = 2001;
    private static final int REQ_SHIZUKU = 2002;

    private TextView setupState;
    private TextView currentState;
    private TextView resultState;
    private Spinner simSpinner;
    private Spinner countrySpinner;
    private EditText customCountry;
    private Button phonePermissionButton;
    private Button shizukuPermissionButton;
    private Button openShizukuButton;
    private Button applyButton;
    private Button restoreButton;

    private final List<SubscriptionInfo> subscriptions = new ArrayList<>();
    private IShellService shellService;
    private boolean serviceBinding;

    private final Shizuku.UserServiceArgs userServiceArgs =
            new Shizuku.UserServiceArgs(
                    new ComponentName("com.example.simregionswitch", ShellUserService.class.getName())
            )
                    .processNameSuffix("region_shell")
                    .debuggable(false)
                    .version(1);

    private final ServiceConnection userServiceConnection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName name, IBinder service) {
            shellService = IShellService.Stub.asInterface(service);
            serviceBinding = false;
            refreshSetupState();
        }

        @Override
        public void onServiceDisconnected(ComponentName name) {
            shellService = null;
            serviceBinding = false;
            refreshSetupState();
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        bindViews();
        setupCountries();
        wireActions();
        handlePendingResult();
    }

    @Override
    protected void onResume() {
        super.onResume();
        refreshSetupState();
        loadSubscriptions();
        tryBindUserService();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        // Do not remove the UserService; keeping it makes subsequent one-tap use faster.
    }

    private void bindViews() {
        setupState = findViewById(R.id.setupState);
        currentState = findViewById(R.id.currentState);
        resultState = findViewById(R.id.resultState);
        simSpinner = findViewById(R.id.simSpinner);
        countrySpinner = findViewById(R.id.countrySpinner);
        customCountry = findViewById(R.id.customCountry);
        phonePermissionButton = findViewById(R.id.phonePermissionButton);
        shizukuPermissionButton = findViewById(R.id.shizukuPermissionButton);
        openShizukuButton = findViewById(R.id.openShizukuButton);
        applyButton = findViewById(R.id.applyButton);
        restoreButton = findViewById(R.id.restoreButton);
    }

    private void setupCountries() {
        String[] items = new String[]{
                "美国 · US",
                "日本 · JP",
                "新加坡 · SG",
                "香港 · HK",
                "英国 · GB",
                "加拿大 · CA",
                "自定义"
        };
        countrySpinner.setAdapter(new ArrayAdapter<>(
                this,
                android.R.layout.simple_spinner_dropdown_item,
                items
        ));
        countrySpinner.addOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                customCountry.setVisibility(position == items.length - 1 ? View.VISIBLE : View.GONE);
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
            }
        });
    }

    private void wireActions() {
        phonePermissionButton.setOnClickListener(v -> requestPermissions(
                new String[]{Manifest.permission.READ_PHONE_STATE},
                REQ_PHONE
        ));

        shizukuPermissionButton.setOnClickListener(v -> {
            try {
                if (!Shizuku.pingBinder()) {
                    Toast.makeText(this, "请先在 Shizuku 中启动服务", Toast.LENGTH_LONG).show();
                    return;
                }
                Shizuku.requestPermission(REQ_SHIZUKU);
            } catch (Throwable t) {
                showError("Shizuku 不可用：" + t.getMessage());
            }
        });

        openShizukuButton.setOnClickListener(v -> {
            Intent intent = getPackageManager().getLaunchIntentForPackage("moe.shizuku.privileged.api");
            if (intent != null) {
                startActivity(intent);
            } else {
                Intent details = new Intent(Settings.ACTION_APPLICATION_SETTINGS);
                startActivity(details);
            }
        });

        applyButton.setOnClickListener(v -> runOverride(false));
        restoreButton.setOnClickListener(v -> runOverride(true));

        simSpinner.addOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                refreshCurrentState();
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
            }
        });
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == REQ_PHONE) {
            refreshSetupState();
            loadSubscriptions();
        }
    }

    private void refreshSetupState() {
        boolean phoneGranted = checkSelfPermission(Manifest.permission.READ_PHONE_STATE)
                == PackageManager.PERMISSION_GRANTED;
        boolean shizukuRunning = false;
        boolean shizukuGranted = false;
        try {
            shizukuRunning = Shizuku.pingBinder();
            shizukuGranted = shizukuRunning
                    && Shizuku.checkSelfPermission() == PackageManager.PERMISSION_GRANTED;
        } catch (Throwable ignored) {
        }

        String service = shellService != null ? "已连接" : (serviceBinding ? "连接中" : "未连接");
        setupState.setText(
                "读取 SIM 权限：" + mark(phoneGranted) + "\n"
                        + "Shizuku 服务：" + mark(shizukuRunning) + "\n"
                        + "Shizuku 授权：" + mark(shizukuGranted) + "\n"
                        + "执行服务：" + service
        );

        phonePermissionButton.setVisibility(phoneGranted ? View.GONE : View.VISIBLE);
        shizukuPermissionButton.setVisibility(
                shizukuRunning && !shizukuGranted ? View.VISIBLE : View.GONE
        );
        openShizukuButton.setVisibility(shizukuRunning ? View.GONE : View.VISIBLE);

        boolean ready = phoneGranted && shizukuRunning && shizukuGranted && shellService != null;
        applyButton.setEnabled(ready);
        restoreButton.setEnabled(ready);
    }

    private String mark(boolean ok) {
        return ok ? "✓" : "×";
    }

    private void tryBindUserService() {
        if (shellService != null || serviceBinding) return;
        try {
            if (!Shizuku.pingBinder()) return;
            if (Shizuku.checkSelfPermission() != PackageManager.PERMISSION_GRANTED) return;
            serviceBinding = true;
            Shizuku.bindUserService(userServiceArgs, userServiceConnection);
        } catch (Throwable t) {
            serviceBinding = false;
            showError("连接执行服务失败：" + t.getMessage());
        }
    }

    private void loadSubscriptions() {
        subscriptions.clear();
        if (checkSelfPermission(Manifest.permission.READ_PHONE_STATE)
                != PackageManager.PERMISSION_GRANTED) {
            simSpinner.setAdapter(new ArrayAdapter<>(
                    this,
                    android.R.layout.simple_spinner_dropdown_item,
                    new String[]{"请先授予读取 SIM 权限"}
            ));
            currentState.setText("当前地区：—");
            return;
        }

        try {
            SubscriptionManager manager = getSystemService(SubscriptionManager.class);
            List<SubscriptionInfo> active = manager != null
                    ? manager.getActiveSubscriptionInfoList()
                    : null;
            if (active != null) subscriptions.addAll(active);
        } catch (Throwable t) {
            showError("读取 SIM 失败：" + t.getMessage());
        }

        if (subscriptions.isEmpty()) {
            simSpinner.setAdapter(new ArrayAdapter<>(
                    this,
                    android.R.layout.simple_spinner_dropdown_item,
                    new String[]{"未检测到活动 SIM"}
            ));
            currentState.setText("当前地区：—");
            return;
        }

        List<String> labels = new ArrayList<>();
        for (SubscriptionInfo info : subscriptions) {
            String carrier = info.getCarrierName() == null ? "未知运营商" : info.getCarrierName().toString();
            labels.add("SIM" + (info.getSimSlotIndex() + 1) + " · " + carrier);
        }
        simSpinner.setAdapter(new ArrayAdapter<>(
                this,
                android.R.layout.simple_spinner_dropdown_item,
                labels
        ));
        refreshCurrentState();
    }

    private void refreshCurrentState() {
        SubscriptionInfo info = getSelectedSubscription();
        if (info == null) {
            currentState.setText("当前地区：—");
            return;
        }
        try {
            TelephonyManager tm = getSystemService(TelephonyManager.class)
                    .createForSubscriptionId(info.getSubscriptionId());
            String iso = tm.getSimCountryIso();
            String operator = tm.getSimOperatorName();
            if (iso == null || iso.isEmpty()) iso = "未知";
            if (operator == null || operator.isEmpty()) operator = "未知运营商";
            currentState.setText(
                    "当前系统识别：" + iso.toUpperCase(Locale.ROOT)
                            + "\n运营商：" + operator
                            + "\nsubId：" + info.getSubscriptionId()
            );
        } catch (Throwable t) {
            currentState.setText("当前地区读取失败：" + t.getMessage());
        }
    }

    private SubscriptionInfo getSelectedSubscription() {
        int position = simSpinner.getSelectedItemPosition();
        if (position < 0 || position >= subscriptions.size()) return null;
        return subscriptions.get(position);
    }

    private String selectedCountry() {
        int p = countrySpinner.getSelectedItemPosition();
        switch (p) {
            case 0: return "US";
            case 1: return "JP";
            case 2: return "SG";
            case 3: return "HK";
            case 4: return "GB";
            case 5: return "CA";
            default:
                return customCountry.getText().toString().trim().toUpperCase(Locale.ROOT);
        }
    }

    private void runOverride(boolean restore) {
        SubscriptionInfo info = getSelectedSubscription();
        if (info == null) {
            showError("没有可用 SIM");
            return;
        }
        if (shellService == null) {
            showError("Shizuku 执行服务尚未连接");
            tryBindUserService();
            return;
        }

        String country = selectedCountry();
        if (!restore && !country.matches("[A-Z]{2}")) {
            showError("自定义国家码必须是 2 个英文字母，例如 US / JP / SG");
            return;
        }

        int subId = info.getSubscriptionId();
        String pkg = getPackageName();
        String component = pkg + "/.CarrierOverrideInstrumentation";
        String activity = pkg + "/.MainActivity";

        StringBuilder command = new StringBuilder();
        command.append("am instrument -w -r ")
                .append("-e action ").append(restore ? "restore" : "apply").append(' ')
                .append("-e subId ").append(subId).append(' ');
        if (!restore) {
            command.append("-e country ").append(country).append(' ');
        }
        command.append(component)
                .append(" >/data/local/tmp/sim_region_switch.log 2>&1; ")
                .append("sleep 1; am start -n ").append(activity)
                .append(" >/dev/null 2>&1");

        getPreferences(MODE_PRIVATE).edit()
                .putBoolean("pending", true)
                .putBoolean("pending_restore", restore)
                .putString("pending_country", country)
                .putInt("pending_sub_id", subId)
                .apply();

        resultState.setText(restore
                ? "正在恢复原始配置，应用会自动重启…"
                : "正在应用 " + country + "，应用会自动重启…");

        try {
            shellService.execDetached(command.toString());
        } catch (Throwable t) {
            getPreferences(MODE_PRIVATE).edit().clear().apply();
            showError("执行失败：" + t.getMessage());
        }
    }

    private void handlePendingResult() {
        boolean pending = getPreferences(MODE_PRIVATE).getBoolean("pending", false);
        if (!pending) return;
        boolean restore = getPreferences(MODE_PRIVATE).getBoolean("pending_restore", false);
        String country = getPreferences(MODE_PRIVATE).getString("pending_country", "");
        getPreferences(MODE_PRIVATE).edit().putBoolean("pending", false).apply();

        resultState.setText(restore
                ? "恢复命令已执行，正在重新读取 SIM 状态…"
                : "配置命令已执行，正在验证 " + country + "…");
        resultState.postDelayed(() -> {
            loadSubscriptions();
            verifyLastAction(restore, country);
        }, 1500);
    }

    private void verifyLastAction(boolean restore, String country) {
        SubscriptionInfo selected = getSelectedSubscription();
        if (selected == null) return;
        try {
            TelephonyManager tm = getSystemService(TelephonyManager.class)
                    .createForSubscriptionId(selected.getSubscriptionId());
            String actual = tm.getSimCountryIso();
            if (actual == null) actual = "";
            if (!restore && actual.equalsIgnoreCase(country)) {
                resultState.setText("✓ 已生效：系统当前识别为 " + country.toUpperCase(Locale.ROOT));
            } else if (restore) {
                resultState.setText("✓ 已执行恢复。当前系统识别："
                        + (actual.isEmpty() ? "未知" : actual.toUpperCase(Locale.ROOT)));
            } else {
                resultState.setText("命令已执行，但当前读取到："
                        + (actual.isEmpty() ? "未知" : actual.toUpperCase(Locale.ROOT))
                        + "。部分机型可能需要等待数秒或重开目标 App。"
                );
            }
        } catch (Throwable t) {
            resultState.setText("已执行命令，但验证失败：" + t.getMessage());
        }
    }

    private void showError(String text) {
        resultState.setText(text);
        Toast.makeText(this, text, Toast.LENGTH_LONG).show();
    }
}
