#include <math.h>
#include "calibration.h"
#include "config.h"
#include "hardware.h"
#include "motion.h"

static float restX = 0.0f, restY = 0.0f, restZ = 1.0f;   // default: lying flat, face up
static bool  done = false;
static uint8_t version = 0;
static uint8_t level = SENS_DEFAULT_LEVEL;

// ---------- Rest-position calibration ----------
void calibrationRun() {
  hwSetLeds(255, 180, 0);                       // yellow = hold still, calibrating

  unsigned long start = millis();
  unsigned long stillSince = 0;
  float sx = 0, sy = 0, sz = 0;
  uint16_t n = 0;
  float px = 0, py = 0, pz = 0;
  bool havePrev = false;
  bool ok = false;

  while (millis() - start < CAL_TIMEOUT_MS) {
    hwAccelPoll();
    if (!hwAccelAvailable()) continue;

    float x, y, z;
    hwReadAccel(x, y, z);
    float mag = sqrt(x * x + y * y + z * z);
    float delta = havePrev ? sqrt((x - px) * (x - px) + (y - py) * (y - py) + (z - pz) * (z - pz)) : 1.0f;
    px = x; py = y; pz = z; havePrev = true;

    bool still = fabs(mag - 1.0f) < QUIET_G && delta < CAL_STILL_DELTA_G;
    if (!still) {                                // moved: start counting again
      stillSince = 0; sx = sy = sz = 0; n = 0;
      continue;
    }
    if (stillSince == 0) stillSince = millis();
    sx += x; sy += y; sz += z; n++;
    if (millis() - stillSince >= CAL_REST_MS && n > 0) {
      restX = sx / n; restY = sy / n; restZ = sz / n;
      ok = true;
      break;
    }
  }

  done = ok;
  if (ok) version++;
  hwSetLeds(ok ? 0 : 255, ok ? 255 : 0, 0);     // green = done, red = gave up (default rest position is used)
  delay(CAL_FEEDBACK_MS);
  hwSetLeds(0, 0, 0);
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
  if (!CAL_BUTTONS) return;

  static unsigned long lastCheck = 0;
  static unsigned long shownAt = 0;             // when the level display was shown (0 = not shown)
  static bool prevLess = false, prevMore = false, prevRecal = false;
  unsigned long now = millis();

  // Hide the level display again after a short time.
  if (shownAt != 0 && now - shownAt >= CAL_LEVEL_SHOW_MS) {
    hwSetLeds(0, 0, 0);
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

  if (downLess && level > 0) {
    level--;
    hwShowLevel(level + 1, 150, 0, 255);        // purple, one LED per level
    shownAt = now;
  } else if (downMore && level + 1 < SENS_LEVEL_COUNT) {
    level++;
    hwShowLevel(level + 1, 150, 0, 255);
    shownAt = now;
  } else if (downRecal) {
    calibrationRun();
    shownAt = 0;
  }
}
