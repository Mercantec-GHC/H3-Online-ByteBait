// Routes controller messages over USB or the optional Sprint 1 WiFi throw probe.
#include "protocol.h"
#include "config.h"
#include "leds.h"
#include "motion.h"
#include "buzzer.h"

#if __has_include("arduino_secrets.h")
#include "arduino_secrets.h"
#endif
#ifndef WIFI_THROW_PROBE
#define WIFI_THROW_PROBE 0
#endif
#if WIFI_THROW_PROBE
#include "wifi_throw_probe.h"
#endif

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

static bool isNumber(const char* s) {
  for (; *s; s++) {
    if (*s < '0' || *s > '9') return false;
  }
  return true;
}

static void handlePing(const char* n) {
  if (!isNumber(n)) { sendErr("RANGE"); return; }   // n is a counter (docs/protocol.md)
  Serial.print("PONG:");
  Serial.println(n);
}

static void handleLed(const char* color) {
  if (!ledsSetByName(color)) { sendErr("RANGE"); return; }
  // A new timing meter (red/yellow) means the next cast is coming: the fight is over.
  if (!strcmp(color, "RED") || !strcmp(color, "YELLOW")) motionMeterStarted();
}

static void handleBuzz(const char* sound) {
  if (strcmp(sound, "LOST")) { sendErr("RANGE"); return; }
  buzzerPlayLost();
  motionStopFight();                             // the fish is gone: back to cast mode
}

// Handle one complete line from the game. Every command from the game has exactly one argument.
// Not strtok(): it skips empty and extra parts, so "LED::RED" and "LED:RED:EXTRA" would pass as LED:RED.
static void handleLine(char* line) {
  if (line[0] == 0) return;                      // an empty line is not a message

  char* arg = strchr(line, ':');
  if (arg) *arg++ = 0;                           // line is now the command, arg the rest

  bool known = !strcmp(line, "PING") || !strcmp(line, "LED") || !strcmp(line, "BUZZ");
  if (!known) { sendErr("UNKNOWN"); return; }
  if (!arg || !*arg || strchr(arg, ':')) { sendErr("ARGS"); return; }   // missing, empty or extra parts

  if      (!strcmp(line, "PING")) handlePing(arg);
  else if (!strcmp(line, "LED"))  handleLed(arg);
  else                            handleBuzz(arg);
}

void protocolPoll() {
  if (WIFI_THROW_PROBE) return;  // In probe mode Serial is only a diagnostic output.
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

void protocolBegin() {
#if WIFI_THROW_PROBE
  WifiThrowProbe::begin();
#else
  Serial.println("HELLO:USB:1");
#endif
}

void protocolSendThrow(int strength) {
  strength = constrain(strength, 0, 100);
#if WIFI_THROW_PROBE
  WifiThrowProbe::send(strength);
#else
  Serial.print("THROW:");
  Serial.println(strength);
  motionStartFight();   // after a cast the motions (hook, tilt, hold, yank) take over
#endif
}

// ---------- Motion messages from the Oplà to the game (#17) ----------
// Not sent in the WiFi probe build: there Serial is only a log for the throw test.
void protocolSendHook() {
#if !WIFI_THROW_PROBE
  Serial.println("HOOK");
#endif
}

void protocolSendTilt(char dir) {
#if !WIFI_THROW_PROBE
  Serial.print("TILT:");
  Serial.println(dir == 'L' ? "L" : "R");
#else
  (void)dir;
#endif
}

void protocolSendHold(bool still) {
#if !WIFI_THROW_PROBE
  Serial.println(still ? "HOLD:1" : "HOLD:0");
#else
  (void)still;
#endif
}

void protocolSendYank() {
#if !WIFI_THROW_PROBE
  Serial.println("YANK");
#endif
}

// TODO(#38): everything that writes to / reads from Serial here must go through link* (link.h),
//            so the same messages work over both USB and WiFi.
