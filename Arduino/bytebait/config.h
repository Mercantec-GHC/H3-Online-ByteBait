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
// All values are starting points. Tune them with MOTION_PLOT_MODE (Serial Plotter), 10 tries per motion.
const bool  MOTION_TEST_MODE = false;  // true = motions always on and throw detection off (for testing #17)
const bool  MOTION_PLOT_MODE = false;  // true = print up/lat/delta to Serial Plotter, send NO motion messages

// Which sensor axis is "left/right" for the player, and which sign means "right".
// If TILT:L and TILT:R come out swapped, flip MOTION_LAT_SIGN. If the axis is wrong, try 1 or 2.
const uint8_t MOTION_LAT_AXIS = 0;     // 0 = x, 1 = y, 2 = z
const float   MOTION_LAT_SIGN = 1.0f;  // +1 = positive reading is "right", -1 = negative is "right"

const float MOTION_TRIGGER_G = 0.5f;   // linear acceleration (g, gravity removed) that starts a motion
const unsigned long MOTION_WINDOW_MS      = 150;   // peak is measured this long, then the motion is classified
const unsigned long MOTION_LOCKOUT_MS     = 400;   // minimum pause after a motion (protocol: >= 100 ms between messages)
const unsigned long MOTION_MAX_LOCKOUT_MS = 1000;  // the pause ends at this time at the latest

// Upward pull: HOOK_G <= peak < YANK_G is a hook, peak >= YANK_G is a yank (a harder, faster pull).
const float HOOK_G = 0.9f;
const float YANK_G = 2.0f;

// Sideways pull: the sideways peak must reach TILT_G and be at least TILT_RATIO times the upward peak.
const float TILT_G     = 0.7f;
const float TILT_RATIO = 0.8f;

// Hold still: smoothed sample-to-sample change (g). Two limits (hysteresis) so it does not flicker.
const float STILL_DELTA_G = 0.06f;     // below this the Oplà counts as still
const float MOVE_DELTA_G  = 0.12f;     // above this it counts as moving again
const unsigned long STILL_MS = 400;    // must be still this long before HOLD:1 is sent

// The motions are only active during the fish fight. The fight starts when THROW is sent and ends
// when the game starts a new timing meter (LED:RED or LED:YELLOW), sends BUZZ:LOST, or after this timeout.
// Why: a throw, a hook and a yank are all accelerations, and the Oplà cannot tell them apart without knowing the phase.
const unsigned long FIGHT_TIMEOUT_MS = 60000;

// ---------- Calibration (S2.2, #18) ----------
// TODO(#18): time at rest before calibration is done, default rest position

// ---------- Buzzer (S2.3, #19) ----------
// TODO(#19): tone frequencies and durations for "EEEEEEEUUHH"

// ---------- WiFi (S3.9, #38) ----------
// TODO(#38): timeouts, pause between reconnect attempts (WiFi credentials live in arduino_secrets.h)

// ---------- Fine-tuning (S4.4, #46) ----------
// TODO(#46): adjust the values above after the user test (write before/after in the PR)
