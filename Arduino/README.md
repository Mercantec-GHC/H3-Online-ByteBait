# Arduino

Kode til Arduino-hardwaren.

WiFi-koder og nøgler lægges i `arduino_secrets.h`, som er ignoreret af Git.

## WiFi-prøve til opgave #4

[Testsketchen](spike_wifi_https/spike_wifi_https.ino) måler HTTPS fra Oplà'en til
Test-ByteBait. USB bruges til Serial Monitor. Hver nulstilling starter én runde.

1. Åbn sketchen i Arduino IDE 2. Vælg **Arduino MKR WiFi 1010** og boardets port.
   Sketchen bruger boardpakken **Arduino SAMD Boards** og biblioteket **WiFiNINA**.
2. Kopiér `arduino_secrets.example.h` til `arduino_secrets.h` i sketchens mappe,
   hvis den lokale fil mangler. Udfyld `SECRET_SSID` og `SECRET_PASS` lokalt.
   Testmålet er `bytebait-test.mercantec.tech` på port `443`.
3. Vælg **Verify** for at kompilere. Eksempelkonfigurationen med tomme WiFi-felter
   kan også bygges. Hvis den uploades, viser Serial Monitor en konfigurationsfejl.
4. **Ved hardwareprøven (4.3)** lukkes Serial Monitor. Åbn
   **Tools → Upload Root Certificates**, tilføj `https://bytebait-test.mercantec.tech`,
   vælg certifikaterne, som boardet skal bruge, og vælg det rigtige board.
   Upload og afvent **Certificates uploaded**. [Arduinos certifikatvejledning](https://support.arduino.cc/hc/en-us/articles/360016119219-Upload-SSL-root-certificates)
5. Upload testsketchen, åbn Serial Monitor ved **115200 baud**, og tryk én gang
   på Reset. Gem hele runden. Upload af testsketchen erstatter programmet på
   boardet. Spillets sketch ligger i [bytebait](bytebait/).

Prøven sender fem `HEAD /game` med ét sekunds pause mellem svar og næste kald.
Efter femte svar venter den 30 sekunder og prøver igen. `connect_ms` måler åbning
af forbindelsen med DNS, TCP og TLS. `response_ms` måler fra HTTP-afsendelse til
modtaget statuslinje. Ved en genforbindelse får du en særskilt TLS-linje og
`connection=reconnected`. Forvent HTTP `200` ved hvert gennemført kald.

Uddrag af eksempeloutput med illustrative tider.

```text
WiFi join_ms=1400
Starting request 1
TLS request=1 connect_ms=3100
HTTP request=1 connection=new status=200 response_ms=81
Starting request 2
HTTP request=2 connection=reused status=200 response_ms=76
Starting request 6
HTTP request=6 connection=reused status=200 response_ms=85
Round ended. Reset to repeat.
```

## Ét fysisk kast over WiFi til browseren (4.4b)

1. Åbn [WiFi throw test på Test-ByteBait](https://bytebait-test.mercantec.tech/api/test/wifi-throw).
   Vent på **Ready**. Klik **Send simulated throw**. Siden skal vise
   **Simulated · THROW:72** og **Physical throws: 0**.
2. Hold fanen åben. Kopiér dens **Test ID for the Oplà**. En genindlæsning giver et nyt ID.
3. Åbn [spillets sketch](bytebait/bytebait.ino). Kopiér dens
   [eksempelkonfiguration](bytebait/arduino_secrets.example.h) til `arduino_secrets.h`
   i samme mappe, hvis filen mangler. Sæt `WIFI_THROW_PROBE` til `1`, udfyld de lokale
   WiFi-felter og indsæt ID'et i `SECRET_PROBE_ID`. Brug TLS-certifikatet fra 4.3.
4. Upload til **Arduino MKR WiFi 1010** med bibliotekerne Arduino_MKRIoTCarrier,
   WiFiNINA og ArduinoHttpClient. Åbn Serial Monitor ved **115200 baud**. Tryk Reset
   og vent på **WiFi connected**. Hold boardet i ro, indtil browseren viser **Ready**.
5. Lav ét kast. Serial Monitor skal vise `THROW:<styrke>` og `HTTP 204`. Browseren
   skal vise samme linje og tælle fra **0 til 1**. USB-kablet bruges her til strøm og log.

HTTP `409` betyder, at ID'et mangler en åben modtager. Kontrollér fanen og dens ID.
Ved manglende svar tjekkes browseren, før man kaster igen. Sketchen sender kun én
forespørgsel pr. kast og kan genbruge TLS-forbindelsen ved næste kast.
Sæt `WIFI_THROW_PROBE` til `0` og upload igen for at bruge USB-protokollen.
