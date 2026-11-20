#pragma once
#include <Arduino.h>

// S2.2 (#18): calibration at start (rest position), so motions
// feel the same no matter how the Oplà is held.

void calibrationRun();                                          // call in setup() after hwBegin()
bool calibrationDone();
void calibrationGetRest(float &x, float &y, float &z);          // the rest position (g)
