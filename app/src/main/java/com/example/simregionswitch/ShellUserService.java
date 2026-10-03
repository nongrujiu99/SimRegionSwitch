package com.example.simregionswitch;

import android.content.Context;

/** Runs in a Shizuku UserService process with shell/ADB identity. */
public class ShellUserService extends IShellService.Stub {

    public ShellUserService() {
    }

    public ShellUserService(Context context) {
    }

    @Override
    public void execDetached(String command) {
        // Inputs are validated by MainActivity before reaching here.
        try {
            new ProcessBuilder("sh", "-c", command)
                    .redirectErrorStream(true)
                    .start();
        } catch (Exception e) {
            throw new IllegalStateException("Unable to start shell command", e);
        }
    }
}
