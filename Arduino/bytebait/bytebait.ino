// This file only wires the modules together. The logic lives in the other files:
//   config.h         all tunable constants
//   hardware.*       the only place that touches the Carrier library (sensor, LEDs, buzzer)
//   protocol.*       line format, incoming and outgoing messages (docs/protocol.md)
//   leds.*           LED colors                              (#6)
//   throw_detect.*   throw detection                         (#5)
//   motion.*         hook, tilt L/R, hold still, yank        (#17)
//   calibration.*    rest-position calibration               (#18)
//   buzzer.*         "lost fish" sound                       (#19)
//   link.*           transport: USB or WiFi                  (#38)
//   link_wifi.*      WiFi/HTTPS to Dokploy                   (#38)

#include "config.h"
#include "hardware.h"
#include "protocol.h"
#include "throw_detect.h"
#include "motion.h"
#include "calibration.h"
#include "buzzer.h"
#include "link.h"

void setup() {
  Serial.begin(SERIAL_BAUD);
  unsigned long t = millis();
  while (!Serial && millis() - t < 2000) {}   // wait at most 2 s for USB

  hwBegin();
  calibrationRun();                           // TODO(#18): empty for now
  // linkBegin();                             // TODO(#38): enable when protocol.cpp uses link* (move Serial.begin here)
  if (!PLOT_MODE) protocolBegin();
}

void loop() {
  // linkUpdate();                            // TODO(#38)
  protocolPoll();   // read messages from the game
  throwUpdate();    // detect throws and send THROW        (#5)
  motionUpdate();   // hook, tilt, hold still, yank        (#17)
  buzzerUpdate();   // play the "lost fish" sound          (#19)
}
