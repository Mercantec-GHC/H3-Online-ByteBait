#include "protocol.h"
#include "config.h"
#include "leds.h"

static char    buf[MAX_LINE + 1];
static uint8_t len = 0;
static bool    tooLong = false;

static void sendErr(const char* code) {
  static unsigned long last = 0;
  if (millis() - last < 1000) return;          // max 1 ERR per second
  last = millis();
  Serial.print("ERR:");
  Serial.println(code);
}

// Handle one complete line from the game
static void handleLine(char* line) {
  char* cmd = strtok(line, ":");
  char* a1  = strtok(nullptr, ":");
  if (!cmd) return;

  if (!strcmp(cmd, "PING") && a1) {
    Serial.print("PONG:");
    Serial.println(a1);
  } else if (!strcmp(cmd, "LED") && a1) {
    if (!ledsSetByName(a1)) sendErr("RANGE");
  // TODO(#19): } else if (!strcmp(cmd, "BUZZ") && a1) { BUZZ:LOST -> buzzerPlayLost()
  } else {
    sendErr("UNKNOWN");
  }
}

void protocolPoll() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (tooLong) sendErr("TOOLONG");
      else { buf[len] = 0; handleLine(buf); }
      len = 0; tooLong = false;
      continue;
    }
    if (len < MAX_LINE) buf[len++] = c;
    else tooLong = true;
  }
}

void protocolSendHello() {
  Serial.println("HELLO:USB:1");
}

void protocolSendThrow(int strength) {
  Serial.print("THROW:");
  Serial.println(constrain(strength, 0, 100));
}

// ---------- New messages from the Oplà to the game (#17) ----------
void protocolSendHook() {
  // TODO(#17): send "HOOK"
}

void protocolSendTilt(char dir) {
  (void)dir;
  // TODO(#17): send "TILT:L" or "TILT:R"
}

void protocolSendHold(bool still) {
  (void)still;
  // TODO(#17): send "HOLD:1" (still) or "HOLD:0" (moving)
}

void protocolSendYank() {
  // TODO(#17): send "YANK"
}

// TODO(#38): everything that writes to / reads from Serial here must go through link* (link.h),
//            so the same messages work over both USB and WiFi.
