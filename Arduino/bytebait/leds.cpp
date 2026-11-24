#include "leds.h"
#include "hardware.h"

bool ledsSetByName(const char* name) {
  if      (!strcmp(name, "RED"))    hwSetLeds(255, 0, 0);
  else if (!strcmp(name, "YELLOW")) hwSetLeds(255, 180, 0);
  else if (!strcmp(name, "GREEN"))  hwSetLeds(0, 255, 0);
  else if (!strcmp(name, "BLUE"))   hwSetLeds(0, 0, 255);
  else if (!strcmp(name, "OFF"))    hwSetLeds(0, 0, 0);
  else return false;
  return true;
}
