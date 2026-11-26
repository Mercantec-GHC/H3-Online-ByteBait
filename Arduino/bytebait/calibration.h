#pragma once
#include <Arduino.h>

// S2.2 (#18): calibration, so the motions feel the same no matter who holds the Oplà.
//  1. Rest position: wait until the Oplà is still AND held in the same orientation, then average it.
//     At start this blocks in setup(). With touch pad 3 it runs in the background: loop() keeps
//     handling the game's messages while it runs.
//  2. Sensitivity: touch pads make all motion/throw limits lower (more sensitive) or higher.
//     Level 3 of 5 is the default and gives exactly the values in config.h.

void calibrationRun();                                          // call in setup() after hwBegin(): blocks until done or timed out
void calibrationUpdate();                                       // call in loop(): runs a background calibration and the touch pads
bool calibrationBusy();                                         // true while a calibration is waiting for the Oplà to be still
bool calibrationDone();                                         // true if the last calibration succeeded (false after a timeout)
uint8_t calibrationVersion();                                   // increases every time a calibration finishes (also after a timeout)
void calibrationGetRest(float &x, float &y, float &z);          // the rest position (g); the default (lying flat) if not calibrated
float calibrationSensitivity();                                 // 0.6 ... 1.6, 1.0 = config.h values; limits are divided by this
