// Optional HTTPS transport for the physical throw probe. Included only in probe builds.
#pragma once
#include <WiFiNINA.h>
#include <ArduinoHttpClient.h>

namespace WifiThrowProbe {
  WiFiSSLClient tls;
  HttpClient http(tls, SECRET_HOST, SECRET_PORT);
  const char path[] = "/api/test/wifi-throw/" SECRET_PROBE_ID;
  bool configured = false;

  void begin() {
    if (!strlen(SECRET_SSID) || !strlen(SECRET_HOST) || strlen(SECRET_PROBE_ID) != 36) {
      Serial.println("WiFi probe needs an SSID, host and the test ID from the open page.");
      return;
    }
    if (WiFi.status() == WL_NO_MODULE) {
      Serial.println("WiFi module unavailable.");
      return;
    }
    tls.setConnectionTimeout(10000);
    WiFi.setTimeout(20000);
    http.connectionKeepAlive();
    if (WiFi.begin(SECRET_SSID, SECRET_PASS) != WL_CONNECTED) {
      Serial.println("WiFi join failed. Check settings and reset to reconnect.");
      return;
    }
    configured = true;
    Serial.println("WiFi connected. Wait for Ready in the test tab, then make one cast.");
  }

  void send(int strength) {
    char line[11];  // THROW:100, LF and NUL
    snprintf(line, sizeof(line), "THROW:%d\n", strength);
    Serial.print(line);
    if (!configured || WiFi.status() != WL_CONNECTED) {
      Serial.println("Throw not sent. Check WiFi and reset to reconnect.");
      return;
    }

    // Make one POST. A lost response can still mean the browser received this cast.
    int status = http.post(path, "text/plain", line);
    if (status == HTTP_SUCCESS) {
      http.setHttpResponseTimeout(10000);
      status = http.responseStatusCode();
    }
    if (status == 204 && http.skipResponseHeaders() == HTTP_SUCCESS) {
      Serial.println("HTTP 204");
      return;  // The empty response is consumed. Reuse this TLS connection on the next cast.
    }
    if (status > 0 && status != 204) {
      Serial.print("HTTP ");
      Serial.println(status);
    } else {
      Serial.println("No complete HTTP response. Check the browser before casting again.");
    }
    http.stop();  // Drop unread bytes. Only a new physical gesture can send another POST.
  }
}
