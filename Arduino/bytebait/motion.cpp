#include "motion.h"
#include "config.h"
#include "hardware.h"
#include "protocol.h"

// TODO(#17): state per motion (static)

// TODO(#17): helper functions, e.g.
//   static bool detectHook(float x, float y, float z);
//   static bool detectTilt(float x, float y, float z, char &dir);  // 'L' or 'R'
//   static bool detectStill(float dyn);
//   static bool detectYank(float x, float y, float z);

void motionUpdate() {
  // TODO(#17): read hwReadAccel(), detect the motion, send a message via protocolSendXxx()
  // TODO(#17): send one message per motion (pause between repeats, as in throw_detect.cpp)
  // TODO(#17): HOLD is only sent on change (1 = still, 0 = moving)
}
