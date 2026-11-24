#include <math.h>
#include "throw_detect.h"
#include "config.h"
#include "hardware.h"
#include "protocol.h"
#include "motion.h"
#include "calibration.h"

enum State { IDLE, MEASURING, LOCKOUT };
static State state = IDLE;
static unsigned long stateStart = 0;
static float peak = 0;

// Convert peak (g) to strength 0-100
static int strengthFromPeak(float peakG, float k) {
  float s = (peakG - MIN_PEAK_G * k) / ((MAX_PEAK_G - MIN_PEAK_G) * k) * 100.0f;
  return constrain((int)(s + 0.5f), 0, 100);
}

void throwUpdate() {
  // During the fish fight the motions take over: a hook or yank must not be read as a throw.
  if (motionFightActive() || MOTION_PLOT_MODE) { state = IDLE; return; }
  if (!hwAccelAvailable()) return;

  float x, y, z;
  hwReadAccel(x, y, z);
  // The length of the acceleration vector is about 1 g at rest (gravity). The difference is the motion.
  float dyn = fabs(sqrt(x * x + y * y + z * z) - 1.0f);

  if (PLOT_MODE) {
    Serial.print("dyn:");      Serial.print(dyn);
    Serial.print(",trigger:"); Serial.print(TRIGGER_G);
    Serial.print(",x:");       Serial.print(x);
    Serial.print(",y:");       Serial.print(y);
    Serial.print(",z:");       Serial.println(z);
    return;
  }

  unsigned long now = millis();
  const float k = 1.0f / calibrationSensitivity();   // from calibration; 1.0 = values in config.h

  switch (state) {
    case IDLE:
      if (dyn > TRIGGER_G * k) {
        state = MEASURING;
        stateStart = now;
        peak = dyn;
      }
      break;

    case MEASURING:
      if (dyn > peak) peak = dyn;
      if (now - stateStart >= WINDOW_MS) {
        protocolSendThrow(strengthFromPeak(peak, k));
        state = LOCKOUT;
        stateStart = millis();  // Start the quiet period after any network wait.
      }
      break;

    case LOCKOUT: {
      unsigned long elapsed = now - stateStart;
      bool quiet = dyn < QUIET_G;
      if ((elapsed >= LOCKOUT_MS && quiet) || elapsed >= MAX_LOCKOUT_MS) {
        state = IDLE;
      }
      break;
    }
  }
}
