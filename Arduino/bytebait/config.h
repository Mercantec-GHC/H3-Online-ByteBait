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

// The switch between cast and fight waits for the arm to stop, so one movement never gives two messages.
// After THROW: the rest of the throw must not become HOOK/TILT/YANK. The Oplà waits until it has been close to
// 1 g and calm for FIGHT_SETTLE_MS (then "up" is read again, the rod may be tilted now), at most FIGHT_SETTLE_MAX_MS.
// After the fight: the rest of a pull must not become a new THROW. Throw detection starts in its normal lockout.
const unsigned long FIGHT_SETTLE_MS     = 150;
const unsigned long FIGHT_SETTLE_MAX_MS = 1500;

// The timing meter sends LED:RED / LED:YELLOW on every zone change. One that was already on its way when THROW
// was sent must not end the fight that THROW just started. The next zone change ends it instead.
const unsigned long FIGHT_GRACE_MS = 300;

// ---------- Calibration (S2.2, #18) ----------
const unsigned long CAL_REST_MS      = 1500;   // the Oplà must be still this long before the rest position is saved
const unsigned long CAL_TIMEOUT_MS   = 6000;   // give up after this long (default rest position is used)
const float         CAL_STILL_DELTA_G = 0.06f; // sample-to-sample change (g) that still counts as still
const float         CAL_DRIFT_G      = 0.10f;  // the orientation must stay this close to its average (g, ~6 degrees), or the still period starts again
const unsigned long CAL_FEEDBACK_MS  = 600;    // how long the green (done) / red (gave up) LEDs are shown

// Sensitivity: 5 levels. The limits that start a throw or a motion (TRIGGER_G, MIN/MAX_PEAK_G, MOTION_TRIGGER_G,
// HOOK_G, YANK_G, TILT_G) are divided by the value, so a higher value = more sensitive. The limits for being
// still (QUIET_G, STILL_DELTA_G, MOVE_DELTA_G) are fixed: holding still is the same for everyone.
// Level 3 (index 2, value 1.0) gives exactly the limits above.
const uint8_t SENS_LEVEL_COUNT = 5;
const float   SENS_LEVELS[SENS_LEVEL_COUNT] = { 0.6f, 0.8f, 1.0f, 1.25f, 1.6f };
const uint8_t SENS_DEFAULT_LEVEL = 2;
const unsigned long CAL_LEVEL_SHOW_MS = 800;   // how long the purple level LEDs are shown

// Touch pads on the Carrier (0-4). Pad 0 is kept free (planned: hold at start to force WiFi, #38).
const bool    CAL_BUTTONS  = true;             // false = ignore the touch pads
const uint8_t CAL_BTN_LESS  = 1;               // less sensitive
const uint8_t CAL_BTN_MORE  = 2;               // more sensitive
const uint8_t CAL_BTN_RECAL = 3;               // calibrate the rest position again

// ---------- Buzzer (S2.3, #19) ----------
// "EEEEEEEUUHH": a held high "EEE" with a little wobble, then "UUHH" sliding down to a low tone.
const uint16_t BUZZ_HIGH_HZ      = 900;    // pitch of the "EEE" part
const uint16_t BUZZ_LOW_HZ       = 200;    // pitch where "UUHH" ends
const uint16_t BUZZ_WOBBLE_HZ    = 40;     // +/- pitch change of the wobble in the "EEE" part
const unsigned long BUZZ_WOBBLE_MS = 70;   // time for one half wobble
const unsigned long BUZZ_HOLD_MS = 700;    // length of the "EEE" part
const unsigned long BUZZ_SLIDE_MS = 600;   // length of the "UUHH" part (pitch falls from HIGH to LOW)
const unsigned long BUZZ_SLIDE_STEP_MS = 30; // the falling pitch changes this often (tone() restarts the timer on every call)

// ---------- WiFi (S3.9, #38) ----------
// TODO(#38): timeouts, pause between reconnect attempts (WiFi credentials live in arduino_secrets.h)

// ---------- Fine-tuning (S4.4, #46) ----------
// TODO(#46): adjust the values above after the user test (write before/after in the PR)
