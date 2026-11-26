#include "leds.h"
#include "hardware.h"

// The last color the game asked for (starts as OFF).
static uint8_t lastR = 0, lastG = 0, lastB = 0;

static void setGameColor(uint8_t r, uint8_t g, uint8_t b) {
  lastR = r; lastG = g; lastB = b;
  hwSetLeds(r, g, b);
}

bool ledsSetByName(const char* name) {
  if      (!strcmp(name, "RED"))    setGameColor(255, 0, 0);
  else if (!strcmp(name, "YELLOW")) setGameColor(255, 180, 0);
  else if (!strcmp(name, "GREEN"))  setGameColor(0, 255, 0);
  else if (!strcmp(name, "BLUE"))   setGameColor(0, 0, 255);
  else if (!strcmp(name, "OFF"))    setGameColor(0, 0, 0);
  else return false;
  return true;
}

void ledsRestoreLast() {
  hwSetLeds(lastR, lastG, lastB);
}
