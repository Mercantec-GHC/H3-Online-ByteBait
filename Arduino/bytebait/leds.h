#pragma once
#include <Arduino.h>

// Set all LEDs by color name: RED, YELLOW, GREEN, BLUE, OFF.
// Returns false if the name is unknown.
bool ledsSetByName(const char* name);
