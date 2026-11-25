#include <math.h>
#include "calibration.h"
#include "config.h"
#include "hardware.h"
#include "leds.h"
#include "motion.h"

static const float DEFAULT_X = 0.0f, DEFAULT_Y = 0.0f, DEFAULT_Z = 1.0f;   // lying flat, face up

static float restX = DEFAULT_X, restY = DEFAULT_Y, restZ = DEFAULT_Z;
static bool  done = false;
static uint8_t version = 0;
static uint8_t level = SENS_DEFAULT_LEVEL;

// ---------- Rest-position calibration (a small state machine, so it never blocks the loop) ----------
enum CalState { CAL_IDLE, CAL_RUNNING, CAL_FEEDBACK };
static CalState calState = CAL_IDLE;
static unsigned long calStart = 0;      // when the calibration began (for the timeout)
static unsigned long calEnd = 0;        // when the result LEDs were shown
static unsigned long stillSince = 0;    // 0 = not collecting
static float sx = 0, sy = 0, sz = 0;    // sum of the samples in the current still period
static uint16_t n = 0;
static float px = 0, py = 0, pz = 0;
static bool havePrev = false;

static void calBegin() {
  calState = CAL_RUNNING;
  calStart = millis();
  stillSince = 0; sx = sy = sz = 0; n = 0; havePrev = false;
  hwSetLeds(255, 180, 0);               // yellow = hold still, calibrating
}

static void calFinish(bool ok) {
  if (ok) {
    restX = sx / n; restY = sy / n; restZ = sz / n;
    version++;
  } else {                              // timed out: the default rest position is used, as the guide says
    restX = DEFAULT_X; restY = DEFAULT_Y; restZ = DEFAULT_Z;
  }
  done = ok;
  hwSetLeds(ok ? 0 : 255, ok ? 255 : 0, 0);   // green = done, red = gave up
  calState = CAL_FEEDBACK;
  calEnd = millis();
}

static void calStep() {
  unsigned long now = millis();

  if (calState == CAL_FEEDBACK) {
    if (now - calEnd >= CAL_FEEDBACK_MS) {
      ledsRestoreLast();                // the game's last color (OFF if the game has not sent any)
      calState = CAL_IDLE;
    }
    return;
  }
  if (calState != CAL_RUNNING) return;

  if (hwAccelAvailable()) {
    float x, y, z;
    hwReadAccel(x, y, z);
    float mag = sqrt(x * x + y * y + z * z);
    float delta = havePrev ? sqrt((x - px) * (x - px) + (y - py) * (y - py) + (z - pz) * (z - pz)) : 1.0f;
    px = x; py = y; pz = z; havePrev = true;

    bool still = fabs(mag - 1.0f) < QUIET_G && delta < CAL_STILL_DELTA_G;

    // A slow turn changes the sample very little from one sample to the next, so also check that the
    // orientation stays close to the average of this period. If not, start the period again.
    if (still && n > 0) {
      float ax = sx / n, ay = sy / n, az = sz / n;
      float drift = sqrt((x - ax) * (x - ax) + (y - ay) * (y - ay) + (z - az) * (z - az));
      if (drift > CAL_DRIFT_G) still = false;
    }

    if (!still) {
      stillSince = 0; sx = sy = sz = 0; n = 0;
    } else {
      if (stillSince == 0) stillSince = now;
      sx += x; sy += y; sz += z; n++;
      if (now - stillSince >= CAL_REST_MS) { calFinish(true); return; }
    }
  }

  if (now - calStart >= CAL_TIMEOUT_MS) calFinish(false);
}

// At start: wait here until the calibration is completely finished (including the result LEDs).
void calibrationRun() {
  calBegin();
  while (calState != CAL_IDLE) {
    hwAccelPoll();
    calStep();
  }
}

bool calibrationBusy() {
  return calState == CAL_RUNNING;
}

bool calibrationDone() {
  return done;
}

uint8_t calibrationVersion() {
  return version;
}

void calibrationGetRest(float &x, float &y, float &z) {
  x = restX;
  y = restY;
  z = restZ;
}

// ---------- Sensitivity and touch pads ----------
float calibrationSensitivity() {
  return SENS_LEVELS[level];
}

void calibrationUpdate() {
  calStep();                                    // a background calibration (pad 3) moves on a little each loop
  if (!CAL_BUTTONS) return;

  static unsigned long lastCheck = 0;
  static unsigned long shownAt = 0;             // when the level display was shown (0 = not shown)
  static bool prevLess = false, prevMore = false, prevRecal = false;
  unsigned long now = millis();

  // Hide the level display again after a short time. The game's last color comes back.
  if (shownAt != 0 && now - shownAt >= CAL_LEVEL_SHOW_MS) {
    ledsRestoreLast();
    shownAt = 0;
  }

  if (now - lastCheck < 50) return;             // reading the touch pads every loop would be wasteful
  lastCheck = now;
  hwButtonsUpdate();

  bool less  = hwTouch(CAL_BTN_LESS);
  bool more  = hwTouch(CAL_BTN_MORE);
  bool recal = hwTouch(CAL_BTN_RECAL);
  bool downLess = less && !prevLess, downMore = more && !prevMore, downRecal = recal && !prevRecal;
  prevLess = less; prevMore = more; prevRecal = recal;

  if (motionFightActive()) return;              // never change settings during a fight (a hand can touch a pad)
  if (calState != CAL_IDLE) return;             // wait until a running calibration has finished

  if (downLess && level > 0) {
    level--;
    hwShowLevel(level + 1, 150, 0, 255);        // purple, one LED per level
    shownAt = now;
  } else if (downMore && level + 1 < SENS_LEVEL_COUNT) {
    level++;
    hwShowLevel(level + 1, 150, 0, 255);
    shownAt = now;
  } else if (downRecal) {
    calBegin();                                 // runs in the background: the loop keeps handling the game
    shownAt = 0;
  }
}
