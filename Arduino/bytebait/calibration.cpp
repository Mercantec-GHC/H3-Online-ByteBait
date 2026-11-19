#include "calibration.h"
#include "config.h"
#include "hardware.h"

// Default rest position until calibration has run (TODO(#18): adjust)
static float restX = 0.0f, restY = 0.0f, restZ = 1.0f;
static bool  done  = false;

void calibrationRun() {
  // TODO(#18): wait until the Oplà is at rest (dyn < QUIET_G) for e.g. 2 s
  // TODO(#18): average x, y, z over that period -> restX/Y/Z
  // TODO(#18): give feedback (e.g. LEDs) while calibrating and when done
  // TODO(#18): optional: recalibrate on a touch button press
}

bool calibrationDone() {
  return done;
}

void calibrationGetRest(float &x, float &y, float &z) {
  x = restX;
  y = restY;
  z = restZ;
}
