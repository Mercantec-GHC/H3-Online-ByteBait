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
