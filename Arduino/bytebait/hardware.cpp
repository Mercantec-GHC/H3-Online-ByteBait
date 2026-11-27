#include <Arduino_MKRIoTCarrier.h>
#include "hardware.h"
#include "config.h"

// The Carrier library is included ONLY here, so it is not defined multiple times.
static MKRIoTCarrier carrier;

void hwBegin() {
  CARRIER_CASE = false;                       // true if the Oplà sits in its case
  carrier.begin();
  carrier.leds.setBrightness(LED_BRIGHTNESS);
}

// The sensor hands out each sample only once: reading it clears the "new data" flag.
// Several modules (throw and motion) need the same sample, so it is read here once per loop
// pass and cached.
static float lastX = 0, lastY = 0, lastZ = 1;
static bool  fresh = false;

void hwAccelPoll() {
  fresh = carrier.IMUmodule.accelerationAvailable();
  if (fresh) carrier.IMUmodule.readAcceleration(lastX, lastY, lastZ);
}

bool hwAccelAvailable() {
  return fresh;
}

void hwReadAccel(float &x, float &y, float &z) {
  x = lastX; y = lastY; z = lastZ;
}

void hwSetLeds(uint8_t r, uint8_t g, uint8_t b) {
  uint32_t color = carrier.leds.Color(r, g, b);
  for (uint8_t i = 0; i < LED_COUNT; i++) carrier.leds.setPixelColor(i, color);
  carrier.leds.show();
}

void hwShowLevel(uint8_t n, uint8_t r, uint8_t g, uint8_t b) {
  uint32_t on  = carrier.leds.Color(r, g, b);
  uint32_t off = carrier.leds.Color(0, 0, 0);
  for (uint8_t i = 0; i < LED_COUNT; i++) carrier.leds.setPixelColor(i, i < n ? on : off);
  carrier.leds.show();
}

void hwButtonsUpdate() {
  carrier.Buttons.update();
}

bool hwTouch(uint8_t n) {
  switch (n) {
    case 0: return carrier.Buttons.getTouch(TOUCH0);
    case 1: return carrier.Buttons.getTouch(TOUCH1);
    case 2: return carrier.Buttons.getTouch(TOUCH2);
    case 3: return carrier.Buttons.getTouch(TOUCH3);
    case 4: return carrier.Buttons.getTouch(TOUCH4);
  }
  return false;
}

void hwBuzzerOn(uint16_t freq) {
  carrier.Buzzer.sound(freq);
}

void hwBuzzerOff() {
  carrier.Buzzer.noSound();
}
