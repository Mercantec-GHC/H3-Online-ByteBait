#pragma once
#include <Arduino.h>

// ---------- Serial ----------
const unsigned long SERIAL_BAUD = 115200;
const uint8_t       MAX_LINE    = 32;      // max characters per line (docs/protocol.md)

// ---------- Throw (S1.5) ----------
const bool  PLOT_MODE      = false;   // true = output data for Serial Plotter (sends NO messages)
const float TRIGGER_G      = 0.8f;    // dynamic acceleration (g) that starts a throw
const float MIN_PEAK_G     = 0.8f;    // peak that maps to strength 0   (soft throw)
const float MAX_PEAK_G     = 3.5f;    // peak that maps to strength 100 (hard throw)
const float QUIET_G        = 0.25f;   // below this the Oplà counts as at rest
const unsigned long WINDOW_MS      = 250;    // how long the peak is measured after the trigger
const unsigned long LOCKOUT_MS     = 1000;   // minimum pause after a throw
const unsigned long MAX_LOCKOUT_MS = 3000;   // the pause ends at this time at the latest, even without rest

// ---------- LEDs (S1.6) ----------
const uint8_t LED_COUNT      = 5;
const uint8_t LED_BRIGHTNESS = 40;    // 0-255, kept low so it does not dazzle

// ---------- Motions (S2.1, #17) ----------
// TODO(#17): thresholds for hook, tilt, hold still and yank (g, ms)

// ---------- Calibration (S2.2, #18) ----------
// TODO(#18): time at rest before calibration is done, default rest position

// ---------- Buzzer (S2.3, #19) ----------
// TODO(#19): tone frequencies and durations for "EEEEEEEUUHH"

// ---------- WiFi (S3.9, #38) ----------
// TODO(#38): timeouts, pause between reconnect attempts (WiFi credentials live in arduino_secrets.h)

// ---------- Fine-tuning (S4.4, #46) ----------
// TODO(#46): adjust the values above after the user test (write before/after in the PR)
