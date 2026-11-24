#pragma once
#include <Arduino.h>

// S2.2 (#18): calibration, so the motions feel the same no matter who holds the Oplà.
//  1. At start: wait until the Oplà is still, average the rest position (which way is "up").
//  2. Sensitivity: touch pads make all motion/throw limits lower (more sensitive) or higher.
//     Level 3 of 5 is the default and gives exactly the values in config.h.

void calibrationRun();                                          // call in setup() after hwBegin(); can take a few seconds
void calibrationUpdate();                                       // call in loop(): touch pads (sensitivity, recalibrate)
bool calibrationDone();                                         // true if the last calibration succeeded
uint8_t calibrationVersion();                                   // increases every time a calibration succeeds
void calibrationGetRest(float &x, float &y, float &z);          // the rest position (g)
float calibrationSensitivity();                                 // 0.6 ... 1.6, 1.0 = config.h values; limits are divided by this
