#include <math.h>
#include "motion.h"
#include "config.h"
#include "hardware.h"
#include "protocol.h"
#include "calibration.h"

// ---------- Fight mode ----------
// Motions are only looked for during the fight. See FIGHT_TIMEOUT_MS in config.h.
static bool fight = MOTION_TEST_MODE;
static unsigned long fightStart = 0;

// ---------- Sample state ----------
static float gx = 0, gy = 0, gz = 1;   // slow estimate of gravity (the direction "up" while holding still)
static bool  gravityKnown = false;
static float px = 0, py = 0, pz = 0;   // previous sample, for the hold-still check
static bool  prevKnown = false;
static float deltaAvg = 0;             // smoothed change between samples

// ---------- Motion state (hook / tilt / yank) ----------
// M_SETTLING: right after THROW, waiting for the rest of the throw to stop (see FIGHT_SETTLE_MS in config.h)
enum MotionState { M_SETTLING, M_IDLE, M_MEASURING, M_LOCKOUT };
static MotionState mState = M_IDLE;
static unsigned long mStart = 0;
static unsigned long calmSince = 0;    // while settling: when the Oplà became calm (0 = not calm)
static float peakUp = 0;               // largest upward acceleration in the window
static float peakLatPos = 0;           // largest sideways acceleration, positive direction
static float peakLatNeg = 0;           // largest sideways acceleration, negative direction (stored as positive)
static int   latSign = 0;              // direction of the first sideways push above TILT_G (+1 / -1)

// ---------- Hold-still state ----------
static bool holding = false;           // last HOLD value sent (false = HOLD:0)
static bool stillTiming = false;
static unsigned long stillSince = 0;
static unsigned long lastHoldSent = 0;

static void resetMotionState() {
  mState = M_IDLE;
  holding = false;
  stillTiming = false;
  prevKnown = false;
  deltaAvg = 0;
}

void motionStartFight() {
  fight = true;
  fightStart = millis();
  resetMotionState();
  mState = M_SETTLING;                 // the arm is often still moving after the throw
  mStart = fightStart;
  calmSince = 0;
}

void motionStopFight() {
  if (MOTION_TEST_MODE) return;        // in test mode the motions stay on
  fight = false;
  resetMotionState();
}

void motionMeterStarted() {
  // A zone color sent just before the game got THROW belongs to the old meter
  if (fight && millis() - fightStart < FIGHT_GRACE_MS) return;
  motionStopFight();
}

bool motionFightActive() {
  return fight;
}

// One decision per motion: sideways pull, yank or hook (in that order of priority).
static void classify(float k) {   // k = 1 / sensitivity: scales every limit
  float dirPeak = (latSign > 0) ? peakLatPos : peakLatNeg;
  if (latSign != 0 && dirPeak >= TILT_G * k && dirPeak >= peakUp * TILT_RATIO) {
    // The sensor axis is positive = "right" when MOTION_LAT_SIGN = +1.
    bool right = (latSign * MOTION_LAT_SIGN) > 0;
    protocolSendTilt(right ? 'R' : 'L');
  } else if (peakUp >= YANK_G * k) {
    protocolSendYank();
  } else if (peakUp >= HOOK_G * k) {
    protocolSendHook();
  }
  // Otherwise the movement was too weak: send nothing.
}

void motionUpdate() {
  unsigned long now = millis();

  // The fight ends by itself if the game never says so.
  if (fight && !MOTION_TEST_MODE && now - fightStart >= FIGHT_TIMEOUT_MS) motionStopFight();

  if (!hwAccelAvailable()) return;

  float x, y, z;
  hwReadAccel(x, y, z);

  // --- Sensitivity from calibration: every limit is divided by it (higher sensitivity = lower limits).
  const float k = 1.0f / calibrationSensitivity();

  // --- A finished calibration (also a timeout, which gives the default position) gives a clean starting value for "up".
  static uint8_t seenVersion = 0;
  if (seenVersion != calibrationVersion()) {
    calibrationGetRest(gx, gy, gz);
    gravityKnown = true;
    seenVersion = calibrationVersion();
  }

  // --- Gravity: follow it slowly, but only while the total acceleration is close to 1 g (at rest).
  float mag = sqrt(x * x + y * y + z * z);
  if (!gravityKnown) {
    gx = x; gy = y; gz = z;
    gravityKnown = true;
  } else if (fabs(mag - 1.0f) < QUIET_G) {
    const float ALPHA = 0.02f;         // about 0.5 s time constant at ~100 samples per second
    gx += ALPHA * (x - gx);
    gy += ALPHA * (y - gy);
    gz += ALPHA * (z - gz);
  }

  // --- Linear acceleration = sample minus gravity. "Up" is along the gravity reading.
  float gl = sqrt(gx * gx + gy * gy + gz * gz);
  if (gl < 0.1f) return;               // should not happen, but never divide by ~0
  float ux = gx / gl, uy = gy / gl, uz = gz / gl;
  float lx = x - gx, ly = y - gy, lz = z - gz;
  float up = lx * ux + ly * uy + lz * uz;
  float linMag = sqrt(lx * lx + ly * ly + lz * lz);

  // --- Sideways: the chosen sensor axis with the "up" part removed (so a tilted rod still works).
  float lin[3] = { lx, ly, lz };
  float u[3]   = { ux, uy, uz };
  float lat = (lin[MOTION_LAT_AXIS] - up * u[MOTION_LAT_AXIS]);   // positive = along the chosen axis

  // --- Change since the previous sample (also catches rotation, which a magnitude check would miss).
  float delta = 0;
  if (prevKnown) {
    float dx = x - px, dy = y - py, dz = z - pz;
    delta = sqrt(dx * dx + dy * dy + dz * dz);
  }
  px = x; py = y; pz = z;
  prevKnown = true;
  deltaAvg += 0.2f * (delta - deltaAvg);

  if (MOTION_PLOT_MODE) {              // for Serial Plotter: tune HOOK_G, YANK_G, TILT_G, STILL/MOVE
    Serial.print("up:");      Serial.print(up);
    Serial.print(",lat:");    Serial.print(lat);
    Serial.print(",delta:");  Serial.print(deltaAvg);
    Serial.print(",hook:");   Serial.print(HOOK_G * k);
    Serial.print(",yank:");   Serial.print(YANK_G * k);
    Serial.print(",tilt:");   Serial.print(TILT_G * k);
    Serial.print(",still:");  Serial.print(STILL_DELTA_G);
    Serial.print(",move:");   Serial.println(MOVE_DELTA_G);
    return;
  }

  if (PLOT_MODE || !fight) return;     // PLOT_MODE (throw) sends no messages; motions only during the fight

  // =====================  Hook / tilt / yank  =====================
  switch (mState) {
    case M_SETTLING: {
      // Close to 1 g and hardly changing. Not linMag: "up" is still from before the throw.
      bool calm = fabs(mag - 1.0f) < QUIET_G && deltaAvg < MOVE_DELTA_G;
      if (!calm) calmSince = 0;
      else if (calmSince == 0) calmSince = now;

      bool settled = calmSince != 0 && now - calmSince >= FIGHT_SETTLE_MS;
      if (settled || now - mStart >= FIGHT_SETTLE_MAX_MS) {
        if (calm) { gx = x; gy = y; gz = z; }   // the rod may be held at a new angle after the throw
        mState = M_IDLE;
      }
      break;
    }

    case M_IDLE:
      if (linMag > MOTION_TRIGGER_G * k) {
        mState = M_MEASURING;
        mStart = now;
        peakUp = 0; peakLatPos = 0; peakLatNeg = 0; latSign = 0;
      } else {
        break;
      }
      // fall through: the trigger sample counts for the peaks as well

    case M_MEASURING:
      if (up > peakUp) peakUp = up;
      if (lat > peakLatPos) peakLatPos = lat;
      if (-lat > peakLatNeg) peakLatNeg = -lat;
      if (latSign == 0 && fabs(lat) >= TILT_G * k) latSign = (lat > 0) ? 1 : -1;   // same limit as in classify()
      if (now - mStart >= MOTION_WINDOW_MS) {
        classify(k);
        mState = M_LOCKOUT;
        mStart = millis();
      }
      break;

    case M_LOCKOUT: {
      unsigned long elapsed = now - mStart;
      bool quiet = linMag < QUIET_G;
      if ((elapsed >= MOTION_LOCKOUT_MS && quiet) || elapsed >= MOTION_MAX_LOCKOUT_MS) {
        mState = M_IDLE;
      }
      break;
    }
  }

  // =====================  Hold still (give line)  =====================
  // HOLD is only sent on a change: HOLD:1 after being still for STILL_MS, HOLD:0 when moving again.
  bool canSend = (now - lastHoldSent >= 100);          // protocol: not more often than every 100 ms
  if (!holding) {
    if (deltaAvg < STILL_DELTA_G) {
      if (!stillTiming) { stillTiming = true; stillSince = now; }
      else if (now - stillSince >= STILL_MS && canSend) {
        holding = true;
        lastHoldSent = now;
        protocolSendHold(true);
      }
    } else {
      stillTiming = false;
    }
  } else if (deltaAvg > MOVE_DELTA_G && canSend) {
    holding = false;
    stillTiming = false;
    lastHoldSent = now;
    protocolSendHold(false);
  }
}
