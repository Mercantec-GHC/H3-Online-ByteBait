#pragma once
#include <Arduino.h>

// The only interface to the Oplà hardware. The rest of the code does not touch the Carrier library.
void hwBegin();
bool hwAccelAvailable();
void hwReadAccel(float &x, float &y, float &z);    // unit: g
void hwSetLeds(uint8_t r, uint8_t g, uint8_t b);   // all LEDs the same color

// TODO(#19): void hwBuzzerOn(uint16_t freq);  void hwBuzzerOff();  (the Carrier buzzer)
