// ByteBait - Oplà som fiskestang (S1.5: kastgenkendelse over USB)
//
// Et kast giver præcis én besked: THROW:<styrke>  (0-100, se docs/protocol.md)
// Hardware: Arduino MKR WiFi 1010 + MKR IoT Carrier Rev2
//
// Tuning: sæt PLOT_MODE til true, åbn Tools -> Serial Plotter (115200 baud),
// lav nogle bløde og hårde kast, og justér konstanterne herunder efter kurven.
#include <Arduino_MKRIoTCarrier.h>

MKRIoTCarrier carrier;

// ---------- Indstillinger ----------
const bool  PLOT_MODE      = false;   // true = vis data til Serial Plotter (sender INGEN beskeder)
const float TRIGGER_G      = 0.8f;    // dynamisk acceleration (g) der starter et kast
const float MIN_PEAK_G     = 0.8f;    // peak der giver styrke 0   (blødt kast)
const float MAX_PEAK_G     = 3.5f;    // peak der giver styrke 100 (hårdt kast)
const float QUIET_G        = 0.25f;   // under dette regnes Oplà'en som i ro
const unsigned long WINDOW_MS      = 250;    // hvor længe toppen måles efter udløsning
const unsigned long LOCKOUT_MS     = 1000;   // min. pause efter et kast
const unsigned long MAX_LOCKOUT_MS = 3000;   // pausen slutter senest her, selv uden ro

enum State { IDLE, MEASURING, LOCKOUT };
State state = IDLE;
unsigned long stateStart = 0;
float peak = 0;

// ---------- Seriel indlæsning (protokol) ----------
static char buf[33];
static uint8_t len = 0;
static bool tooLong = false;

void setup() {
  Serial.begin(115200);
  unsigned long t = millis();
  while (!Serial && millis() - t < 2000) {}   // vent højst 2 sek. på USB

  CARRIER_CASE = false;                        // true hvis Oplà'en sidder i sit etui
  carrier.begin();

  if (!PLOT_MODE) Serial.println("HELLO:USB:1");
}

void loop() {
  readSerial();

  if (!carrier.IMUmodule.accelerationAvailable()) return;

  float x, y, z;
  carrier.IMUmodule.readAcceleration(x, y, z);
  // Længden af accelerationen er ca. 1 g i ro (tyngdekraften). Forskellen er bevægelsen.
  float dyn = fabs(sqrt(x * x + y * y + z * z) - 1.0f);

  if (PLOT_MODE) {
    Serial.print("dyn:");     Serial.print(dyn);
    Serial.print(",trigger:"); Serial.print(TRIGGER_G);
    Serial.print(",x:");       Serial.print(x);
    Serial.print(",y:");       Serial.print(y);
    Serial.print(",z:");       Serial.println(z);
    return;
  }

  unsigned long now = millis();

  switch (state) {
    case IDLE:
      if (dyn > TRIGGER_G) {
        state = MEASURING;
        stateStart = now;
        peak = dyn;
      }
      break;

    case MEASURING:
      if (dyn > peak) peak = dyn;
      if (now - stateStart >= WINDOW_MS) {
        sendThrow(peak);
        state = LOCKOUT;
        stateStart = now;
      }
      break;

    case LOCKOUT: {
      unsigned long elapsed = now - stateStart;
      bool quiet = dyn < QUIET_G;
      if ((elapsed >= LOCKOUT_MS && quiet) || elapsed >= MAX_LOCKOUT_MS) {
        state = IDLE;
      }
      break;
    }
  }
}

// Omregn peak (g) til styrke 0-100 og send beskeden
void sendThrow(float peakG) {
  float s = (peakG - MIN_PEAK_G) / (MAX_PEAK_G - MIN_PEAK_G) * 100.0f;
  int strength = constrain((int)(s + 0.5f), 0, 100);
  Serial.print("THROW:");
  Serial.println(strength);
}

// ---------- Beskeder fra spillet ----------
void readSerial() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      if (tooLong) sendErr("TOOLONG");
      else { buf[len] = 0; handleLine(buf); }
      len = 0; tooLong = false;
      continue;
    }
    if (len < 32) buf[len++] = c;
    else tooLong = true;
  }
}

void handleLine(char* line) {
  char* cmd = strtok(line, ":");
  char* a1  = strtok(nullptr, ":");
  if (!cmd) return;

  if (!strcmp(cmd, "PING") && a1) {
    Serial.print("PONG:");
    Serial.println(a1);
  } else if (!strcmp(cmd, "LED") && a1) {
    if      (!strcmp(a1, "RED"))    setLeds(255, 0, 0);
    else if (!strcmp(a1, "YELLOW")) setLeds(255, 180, 0);
    else if (!strcmp(a1, "GREEN"))  setLeds(0, 255, 0);
    else if (!strcmp(a1, "BLUE"))   setLeds(0, 0, 255);
    else if (!strcmp(a1, "OFF"))    setLeds(0, 0, 0);
    else sendErr("RANGE");
  } else {
    sendErr("UNKNOWN");
  }
}

void setLeds(uint8_t r, uint8_t g, uint8_t b) {
  uint32_t color = carrier.leds.Color(r, g, b);
  for (int i = 0; i < 5; i++) carrier.leds.setPixelColor(i, color);
  carrier.leds.show();
}

void sendErr(const char* code) {
  static unsigned long last = 0;
  if (millis() - last < 1000) return;          // max 1 ERR i sekundet
  last = millis();
  Serial.print("ERR:");
  Serial.println(code);
}
