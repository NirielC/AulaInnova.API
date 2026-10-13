# Estación NFC AulaInnova

ESP32 con lector NFC, teclado, pantalla 16x2, reloj, microSD y batería.
Va en la puerta de cada aula y registra en `AulaInnova.API`:

| Quién pasa la tarjeta | Modo ASISTENCIA | Modo SALIDAS |
|---|---|---|
| Estudiante | Su asistencia del día (puntual / tardía) | Si está dentro: elige motivo (1-5) y sale. Si estaba fuera: confirma su regreso |
| Profesor, auxiliar, administrativo | Llegada al aula (1.ª vez) y salida (2.ª vez), ligadas a su clase de esa hora | Igual |
| Administrador con **D** antes | Menú: cambiar modo, info, enviar cola, sincronizar | Igual |

Sin red, todo se guarda en la microSD y se envía solo cuando vuelve la conexión.
Cómo viaja cada dato hasta la base: [`../FLUJO_DE_DATOS.md`](../FLUJO_DE_DATOS.md).

```
estacion_nfc/
├─ estacion_nfc.ino     firmware
├─ config.ejemplo.h     copiar como config.h y llenar (config.h no se sube a git)
├─ MEDIDAS_CAJA.md      caja de madera
└─ README.md            este archivo
```

---

## 1. Subir el firmware

1. Arduino IDE → placa **ESP32 Dev Module** (paquete "esp32" de Espressif).
2. Librerías (Gestor de librerías): **Adafruit PN532**, **RTClib** (Adafruit),
   **LiquidCrystal I2C** (Frank de Brabander), **Keypad** (Mark Stanley).
   WiFi, HTTPClient, SD y Preferences ya vienen con el ESP32.
3. Copiar `config.ejemplo.h` como **`config.h`** y llenarlo (sección 4).
4. Subir y abrir el Monitor Serie a **115200**: cada llamada a la API se ve así
   ```
   POST /api/estacion/lectura {"referenciaNfc":"53C22C21430001","modo":"ASISTENCIA",...} -> 200 {...}
   ```

Compilado con el core ESP32 3.0.7: usa ~88 % de la memoria del programa.

---

## 2. Cómo funciona

### Al encender
1. Pantalla, reloj, NFC y microSD (si falta la SD, avisa pero sigue).
2. WiFi (10 s; si no conecta, sigue intentando sola).
3. **Hora UTC por NTP**. Si la red del colegio bloquea NTP, toma la hora de la
   API (`servidorUtc`) y la guarda en el reloj DS3231 (que tiene pila).
4. **Pide su configuración a la API** (`GET /api/estacion/configuracion`): aula,
   etiqueta, modos habilitados y segundos para elegir motivo. Lo guarda en la
   memoria del ESP32: si mañana arranca sin red, ya sabe quién es.

### Al pasar una tarjeta
La estación lee el **UUID que la app escribió en el sticker** (NDEF) o, si no
tiene, el **UID** del chip, y lo manda en hexadecimal en mayúsculas sin guiones
(lo único que acepta `ai.credencial_nfc`).

```
Tarjeta → POST /api/estacion/lectura { referenciaNfc, modo, seleccionId }
            │
            ├─ ASISTENCIA_REGISTRADA / YA_REGISTRADA ──► "Elias Lopez / Asistencia OK"
            ├─ LLEGADA_PERSONAL / SALIDA_PERSONAL ──────► "Llegada Maria P / Matematica 1 A"
            ├─ ELEGIR_MOTIVO ──► tecla 1-5 ──► # ──► POST /api/Movement/exit
            ├─ CONFIRMAR_REGRESO ──────────► # ──► POST /api/Movement/return
            └─ 400 { codigo } ──► mensaje del error (sección 3)
```

- **`seleccionId`**: un UUID nuevo por cada operación. Si la red se corta justo
  después de enviar y la estación lo reenvía, la API reconoce el mismo
  `seleccionId` y **no lo duplica**.
- **`seleccionEdadMs`**: milisegundos entre pasar la tarjeta y confirmar con `#`.

### Sin red (o API apagada)
| Situación | Qué hace |
|---|---|
| Modo ASISTENCIA | Guarda la lectura en `cola.csv` con la hora del reloj. Al reenviar, la API decide: asistencia (estudiante) o llegada/salida (personal) |
| Modo SALIDAS, tarjeta de personal conocida | Igual que arriba |
| Modo SALIDAS, estudiante | No sabe si está dentro o fuera: pregunta **"1-5 salida / A regreso"** y guarda eso |
| Menú admin | Vale lo último que dijo la API de esa tarjeta (`personal.csv`) o las tarjetas de emergencia `ADMIN_REFS` |

Cada 30 s intenta reenviar `cola.csv`:
- **200** → se borra de la cola.
- **400** → la API no lo acepta (ej. tarjeta no registrada): pasa a
  `rechazados.csv` con su código, para revisarlo; **no** se reintenta.
- Sin red, 401/403 o 5xx → se queda y se reintenta después.

### En segundo plano (solo en espera)
- Cada **5 min**: `POST /api/estacion/heartbeat` con el modo y cuántos
  pendientes tiene → la API guarda `ultima_comunicacion_utc` y `modo_reportado`.
- Cada **30 min**: vuelve a pedir su configuración.
- Cada **6 h**: re-sincroniza la hora (cada hora si NTP está bloqueado).
- La luz de la pantalla se apaga a los **60 s** sin uso; la primera tecla solo
  la enciende.

### Menú de administrador (tecla D + credencial)
```
1Asist 2Salidas      1 = modo ASISTENCIA      2 = modo SALIDAS
3Inf 4Cola 5Sinc     3 = IP, cola, aula, hora 4 = enviar cola ya
                     5 = sincronizar hora y configuración    * = salir
```
La API dice si la tarjeta es de un **Administrador** (rol del usuario ligado a
esa persona). `ADMIN_REFS` en `config.h` son tarjetas de emergencia que entran
siempre (para la primera configuración o sin red).

---

## 3. Mensajes en pantalla

| Pantalla | Qué significa | Qué hacer |
|---|---|---|
| `Elias Lopez` / `Asistencia OK` · `Llegada tardia` | Asistencia registrada | — |
| `Ya registrado` | Ya tenía asistencia hoy | — |
| `Llegada Maria P` / clase | Docente marcó llegada a su clase | — |
| `... / Sin clase aqui` | Personal sin clase en esta aula a esta hora: se guardó **por revisar** | Revisar horario/asignación |
| `Tarjeta no / registrada` | La referencia no está en `ai.credencial_nfc` (o no está activa) | Registrar la credencial en la app/web |
| `No es tarjeta / de estudiante` | Tarjeta de personal en una operación de estudiante | — |
| `Sin jornada / escolar ahora` | No hay jornada para esta hora | Crear jornadas (`POST /api/admin/jornadas/rango`) |
| `Sin matricula / Avise direccion` | El estudiante no tiene matrícula vigente | Matricularlo |
| `Ya esta fuera / Marque regreso` | Tiene una salida abierta | Registrar el regreso |
| `No tiene salida / abierta` | Regreso sin salida | — |
| `Modo no activo / en el servidor` | La estación no tiene ese modo en la API | `POST /api/admin/estaciones/{id}/mode?modo=...` |
| `Estacion sin / configurar` | No tiene configuración vigente | `POST /api/admin/estaciones/{id}/configuration` |
| `Estacion sin / permiso: clave?` | 401/403: id o clave mal en `config.h` | Emitir clave nueva (`/secret`) |
| `Error de hora / Sincronice reloj` | El reloj de la estación está adelantado | Menú admin → 5 |
| `Sin conexion / Guardado OK` | Sin red: quedó en la microSD | Se envía solo |
| `Sin conexion / ...no se guardo!` | Sin red y sin SD (o sin hora) | Revisar microSD / WiFi |
| Línea de espera termina en `!` | No hay WiFi | — |

Sonidos: **OK** = dos pitidos agudos + LED verde · **aviso** (ya registrado,
guardado sin red) = un pitido + LED verde · **error** = pitido grave + LED rojo.

---

## 4. `config.h`

| Campo | Qué poner |
|---|---|
| `WIFI_SSID` / `WIFI_PASSWORD` | Red de **2.4 GHz** (el ESP32 no ve 5 GHz) |
| `API_URL` | `http://IP_DE_LA_COMPU:5077` (sin `/` al final) |
| `ESTACION_ID` | id de `ai.estacion` |
| `ESTACION_CLAVE` | valor **completo** de `POST /api/admin/estaciones/{id}/secret` (`identificador.secreto`) |
| `ADMIN_REFS` | tarjetas de administrador de emergencia |

Cómo registrar la estación en la API: [`../README.md`](../README.md) (puesta en marcha).

---

## 5. Pruebas antes de montar

| Prueba | Resultado esperado |
|---|---|
| Encender con WiFi | `Pidiendo config.` y luego pitido + verde/rojo un instante |
| Estudiante (ASISTENCIA) | `Nombre` / `Asistencia OK` o `Llegada tardia` |
| Mismo estudiante otra vez (>5 s) | `Ya registrado` |
| Profesor | `Llegada Nombre` / su clase; otra vez al rato: `Salida ...` |
| Tarjeta sin registrar | `Tarjeta no / registrada` + rojo |
| D → admin → 2 | `Modo cambiado: SALIDAS` |
| Estudiante → 1 → # | `Salida OK / Bano` |
| Mismo estudiante → # | `Regreso OK` |
| Apagar la API y pasar tarjeta | `Sin conexion / Guardado OK` |
| Prender la API | En ≤30 s se envía solo (Monitor Serie: `Cola: 1 enviados`) |
| Clave mala en `config.h` | `Clave estacion / invalida (401)` al arrancar |

---

## 6. Hardware

### Componentes
| Parte | Detalle |
|---|---|
| ESP32 DevKit 30 pines | ESP-WROOM-32, USB-C, CP2102 → **ESP32 Dev Module** |
| LCD 1602 con módulo I2C | dirección `0x27` (contraste con el potenciómetro azul) |
| Reloj DS3231 | `0x68` (EEPROM `0x57`), pila CR2032, guarda **UTC** |
| Lector NFC PN532 (Elechouse V3) | modo **I2C**: switch 1 = ON, 2 = OFF |
| Teclado membrana 4x4 | 8 hilos |
| Buzzer, LED verde, LED rojo | LEDs con resistencia de 220 Ω |
| Módulo microSD (con regulador AMS1117) | a **5 V**, FAT32 |
| Batería 18650 + TP4056 + switch + MT3608 (5.1 V) + capacitor 1000 µF | respaldo sin corriente |

### Pines del ESP32
| Función | GPIO | Rótulo |
|---|---|---|
| I2C SDA (LCD, DS3231, PN532) | 21 | D21 |
| I2C SCL | 22 | D22 |
| Teclado filas 1-4 | 13, 14, 27, 26 | D13, D14, D27, D26 |
| Teclado columnas 1-4 | 25, 33, 32, 4 | D25, D33, D32, D4 |
| Buzzer | 17 | TX2 |
| LED verde | 16 | RX2 |
| LED rojo | 2 | D2 |
| microSD CS / SCK / MISO / MOSI | 5 / 18 / 19 / 23 | D5 / D18 / D19 / D23 |

Alimentación: **5 V (VIN)** → LCD y microSD · **3.3 V** → DS3231 y PN532 ·
todos los GND unidos.

Conector del teclado (teclas hacia ti, de izquierda a derecha):
`1→D13  2→D14  3→D27  4→D26  5→D25  6→D33  7→D32  8→D4`

### Placa perforada (vista desde el COBRE, USB del ESP32 abajo)
ESP32 en headers hembra: **columna 7** (D23…3V3) y **columna 17** (EN…VIN),
filas 17 a 31. Cada jumper va en el agujero de al lado, unido con estaño a su
pin en la **misma fila**.

| Fila | Pin col 7 | Jumpers | Pin col 17 | Jumpers |
|---|---|---|---|---|
| 17 | D23 | SD MOSI | EN | — |
| 18 | D22 (SCL) | LCD, reloj, NFC | VP | — |
| 21 | D21 (SDA) | LCD, reloj, NFC | D35 | — |
| 22 | D19 | SD MISO | D32 | teclado 7 |
| 23 | D18 | SD SCK | D33 | teclado 6 |
| 24 | D5 | SD CS | D25 | teclado 5 |
| 25 | TX2 | buzzer + | D26 | teclado 4 |
| 26 | RX2 | LED verde (220 Ω) | D27 | teclado 3 |
| 27 | D4 | teclado 8 | D14 | teclado 2 |
| 28 | D2 | LED rojo (220 Ω) | D12 | — |
| 29 | D15 | — | D13 | teclado 1 |
| 30 | GND | reloj/NFC, buzzer −, LEDs − | GND | LCD, SD, MT3608 −, capacitor − |
| 31 | 3V3 | reloj/NFC VCC | VIN (5 V) | LCD, SD, MT3608 +, capacitor + |

### Energía
```
Cargador USB-C ──► [TP4056] ◄──► Batería 18650
                       │ OUT+ / OUT−
                       ▼
                 Switch ON/OFF (en el +)
                       │
                       ▼
                   [MT3608]  ← ajustar a 5.1 V ANTES de conectar el ESP32
                       │ VOUT+ / VOUT−
                       ├──► Capacitor 1000 µF (+ a 5 V, − a GND)
                       ▼
                 ESP32 VIN y GND
```

### Stickers del teclado
```
 ┌──────┬──────┬──────┬──────┐
 │  1   │  2   │  3   │  A   │
 │ Baño │Clínic│Emerg.│Regre-│   A = regreso (solo se usa sin red)
 │      │  a   │      │ so   │
 ├──────┼──────┼──────┼──────┤
 │  4   │  5   │      │      │
 │Admin.│ Otro │  ▓▓  │  ▓▓  │
 ├──────┼──────┼──────┼──────┤
 │  ▓▓  │  ▓▓  │  ▓▓  │  ▓▓  │
 ├──────┼──────┼──────┼──────┤
 │  ✖   │  ▓▓  │  ✔   │  🔒  │
 │Cancel│      │  OK  │Config│
 └──────┴──────┴──────┴──────┘
   ▓▓ = tapada, no hace nada
```
