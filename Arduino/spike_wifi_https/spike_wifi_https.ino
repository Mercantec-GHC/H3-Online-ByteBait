// Standalone MKR WiFi 1010 probe for issue #4. Serial reports WiFi, TLS and
// HTTP timings separately so the idle connection can be compared with a warm one.
#include <WiFiNINA.h>

#if __has_include("arduino_secrets.h")
#include "arduino_secrets.h"
#else
#include "arduino_secrets.example.h"
#endif

constexpr unsigned long responseTimeoutMs = 10000;
constexpr unsigned int requestCount = 6;
WiFiSSLClient client;
unsigned int requestNumber = 0;  // Zero while idle, including after the last sample.
unsigned long previousRequestEndedAt;
bool hadTlsConnection = false;

// Return the full line length, retaining only the prefix needed to identify
// the status. Long headers are consumed without storing the whole response.
int readResponseLine(char* prefix, size_t capacity, unsigned long requestStartedAt) {
  int length = 0;
  while (millis() - requestStartedAt < responseTimeoutMs) {
    int ch = client.read();
    if (ch < 0) {
      if (!client.connected()) {
        Serial.println("HTTP error: connection closed before headers were complete.");
        return -1;
      }
      delay(1);
      continue;
    }
    if (ch == '\n') {
      prefix[min(static_cast<size_t>(length), capacity - 1)] = '\0';
      return length;
    }
    if (ch != '\r') {
      if (static_cast<size_t>(length) < capacity - 1) {
        prefix[length] = static_cast<char>(ch);
      }
      ++length;
    }
  }
  Serial.println("HTTP error: response timed out.");
  return -1;
}

// Send HEAD over the already established socket. Stop the measurement at
// the status line, then drain the remaining headers before reusing the socket.
bool measureHead(const char* connection) {
  char request[sizeof(SECRET_HOST) + 80];
  int size = snprintf(request, sizeof(request),
                      "HEAD /game HTTP/1.1\r\nHost: %s:%u\r\nConnection: keep-alive\r\n\r\n",
                      SECRET_HOST, static_cast<unsigned int>(SECRET_PORT));
  const unsigned long startedAt = millis();
  if (client.print(request) != static_cast<size_t>(size)) {
    Serial.println("HTTP error: request write failed.");
    return false;
  }

  char prefix[16];
  int length = readResponseLine(prefix, sizeof(prefix), startedAt);
  const unsigned long responseMs = millis() - startedAt;
  if (length < 0) {
    return false;
  }
  if (length < 12 || (strncmp(prefix, "HTTP/1.1 ", 9) && strncmp(prefix, "HTTP/1.0 ", 9)) ||
      !isdigit(static_cast<unsigned char>(prefix[9])) ||
      !isdigit(static_cast<unsigned char>(prefix[10])) ||
      !isdigit(static_cast<unsigned char>(prefix[11])) ||
      (prefix[12] != ' ' && prefix[12] != '\0')) {
    Serial.println("HTTP error: invalid status line.");
    return false;
  }
  int status = atoi(prefix + 9);
  do {
    length = readResponseLine(prefix, sizeof(prefix), startedAt);
  } while (length > 0);
  if (length < 0) {
    return false;
  }

  Serial.print("HTTP request=");
  Serial.print(requestNumber);
  Serial.print(" connection=");
  Serial.print(connection);
  Serial.print(" status=");
  Serial.print(status);
  Serial.print(" response_ms=");
  Serial.println(responseMs);
  if (status != 200) {
    Serial.println("HTTP error: expected status 200.");
  }
  return true;  // A complete HTTP response, including a server-side error.
}

// Time DNS/TCP/TLS setup together. The HTTP timer starts after connect returns.
bool openTls() {
  const unsigned long startedAt = millis();
  bool connected = client.connect(SECRET_HOST, SECRET_PORT);
  const unsigned long connectMs = millis() - startedAt;
  Serial.print("TLS request=");
  Serial.print(requestNumber);
  Serial.print(" connect_ms=");
  Serial.println(connectMs);
  if (!connected) {
    Serial.println("TLS error: connect failed; check host, network and root certificate.");
  }
  return connected;
}

// Join WiFi once per reset. USB is only used for the measurement log.
void setup() {
  Serial.begin(115200);
  const unsigned long serialStartedAt = millis();
  while (!Serial && millis() - serialStartedAt < 5000) {
    delay(1);
  }
  if (!SECRET_SSID[0] || !SECRET_PASS[0] || !SECRET_HOST[0] || !SECRET_PORT) {
    Serial.println("Configuration error: fill in arduino_secrets.h and upload again.");
    return;
  }
  if (WiFi.status() == WL_NO_MODULE) {
    Serial.println("WiFi error: NINA module not found.");
    return;
  }
  client.setConnectionTimeout(10000);
  WiFi.setTimeout(20000);
  const unsigned long startedAt = millis();
  int status = WiFi.begin(SECRET_SSID, SECRET_PASS);
  const unsigned long joinMs = millis() - startedAt;
  if (status != WL_CONNECTED) {
    Serial.print("WiFi error: join failed, status=");
    Serial.println(status);
    return;
  }
  Serial.print("WiFi join_ms=");
  Serial.println(joinMs);
  requestNumber = 1;
}

// Schedule five warm-up/measurement requests and one after a longer idle period.
// A stale socket can look connected until the first write or read after the pause.
void loop() {
  if (requestNumber == 0) {
    return;
  }
  const unsigned long pauseMs = requestNumber == requestCount ? 30000 : 1000;
  if (requestNumber > 1 && millis() - previousRequestEndedAt < pauseMs) {
    return;
  }
  Serial.print("Starting request ");
  Serial.println(requestNumber);
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi error: connection lost; reset to repeat the round.");
    client.stop();
    requestNumber = 0;
    return;
  }

  bool complete = false;
  if (client.connected()) {
    complete = measureHead("reused");
    if (!complete) {
      Serial.println("Reuse failed; retrying this request once on a fresh connection.");
      client.stop();
    }
  }
  if (!complete && openTls()) {
    complete = measureHead(hadTlsConnection ? "reconnected" : "new");
    hadTlsConnection = true;
  }
  if (!complete || requestNumber == requestCount) {
    client.stop();
  }
  previousRequestEndedAt = millis();
  if (requestNumber == requestCount) {
    Serial.println("Round ended. Reset to repeat.");
    requestNumber = 0;
  } else {
    ++requestNumber;
  }
}
