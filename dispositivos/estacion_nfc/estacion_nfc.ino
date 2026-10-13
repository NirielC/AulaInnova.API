// ============================================================
//  Estación NFC AulaInnova - v2.0
//
//  - Modo ASISTENCIA: el estudiante pasa su tarjeta y queda su asistencia.
//  - Modo SALIDAS: el estudiante pasa su tarjeta; si está en el aula elige
//    el motivo (1-5) y confirma; si estaba fuera, confirma su regreso.
//  - Profesores, auxiliares y administrativos: su tarjeta marca LLEGADA y
//    luego SALIDA del aula (en cualquier modo), ligada a su clase.
//  - Tecla D + tarjeta de administrador: menú (modo, info, cola, sincronizar).
//  - Sin red: guarda en la microSD y reenvía solo cuando vuelve la conexión.
//  - Hora UTC por WiFi (NTP; si la red lo bloquea, la toma de la API).
//
//  Antes de compilar: copia config.ejemplo.h como config.h y llénalo.
//  Placa: "ESP32 Dev Module". Cómo llegan los datos a la API y a la base:
//  ver dispositivos/FLUJO_DE_DATOS.md
// ============================================================

#include <Wire.h>
#include <SPI.h>
#include <SD.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <Preferences.h>
#include <esp_random.h>
#include <time.h>
#include <vector>
#include <LiquidCrystal_I2C.h>
#include <RTClib.h>
#include <Adafruit_PN532.h>
#include <Keypad.h>
#include "config.h"

// ---------- Pines (los de la placa perforada) ----------
const int PIN_BUZZER    = 17;  // TX2
const int PIN_LED_VERDE = 16;  // RX2
const int PIN_LED_ROJO  = 2;
const int PIN_SD_CS     = 5;
const int PIN_SD_SCK    = 18;
const int PIN_SD_MISO   = 19;
const int PIN_SD_MOSI   = 23;

// ---------- Archivos en la microSD ----------
const char* ARCHIVO_COLA         = "/cola.csv";        // lo que falta enviar
const char* ARCHIVO_COLA_TMP     = "/cola.tmp";
const char* ARCHIVO_RECHAZADOS   = "/rechazados.csv";  // lo que la API rechazó (400)
const char* ARCHIVO_PERSONAL     = "/personal.csv";    // tarjetas de personal conocidas
const char* ARCHIVO_PERSONAL_TMP = "/personal.tmp";

// ---------- Rutas de la API ----------
const char* RUTA_LECTURA       = "/api/estacion/lectura";
const char* RUTA_CREDENCIAL    = "/api/estacion/credencial/";
const char* RUTA_CONFIGURACION = "/api/estacion/configuracion";
const char* RUTA_HEARTBEAT     = "/api/estacion/heartbeat";
const char* RUTA_SALIDA        = "/api/Movement/exit";
const char* RUTA_REGRESO       = "/api/Movement/return";

// ---------- Módulos ----------
LiquidCrystal_I2C lcd(0x27, 16, 2);
RTC_DS3231 rtc;
Adafruit_PN532 nfc(-1, -1);  // modo I2C (switch 1 ON, 2 OFF)
Preferences prefs;

const byte FILAS = 4;
const byte COLUMNAS = 4;
char teclas[FILAS][COLUMNAS] = {
  {'1', '2', '3', 'A'},
  {'4', '5', '6', 'B'},
  {'7', '8', '9', 'C'},
  {'*', '0', '#', 'D'}
};
byte pinesFilas[FILAS] = {13, 14, 27, 26};
byte pinesColumnas[COLUMNAS] = {25, 33, 32, 4};
Keypad teclado = Keypad(makeKeymap(teclas), pinesFilas, pinesColumnas, FILAS, COLUMNAS);

// ---------- Motivos de salida (tabla ai.motivo_salida) ----------
struct Motivo {
  char tecla;
  const char* codigo;
  const char* nombre;  // máx. 16 caracteres, sin tildes (la LCD no las tiene)
};
const Motivo MOTIVOS[] = {
  {'1', "BANO",           "Bano"},
  {'2', "CLINICA",        "Clinica"},
  {'3', "EMERGENCIA",     "Emergencia"},
  {'4', "ADMINISTRATIVA", "Administrativa"},
  {'5', "OTRO",           "Otro"},
};
const int TOTAL_MOTIVOS = sizeof(MOTIVOS) / sizeof(MOTIVOS[0]);
const char TECLA_REGRESO = 'A';

// ---------- Mensajes para cada código de error de la API ----------
// (RechazoEstacionException en AulaInnova.API)
struct MensajeError {
  const char* codigo;
  const char* linea1;
  const char* linea2;
};
const MensajeError ERRORES[] = {
  {"CREDENCIAL_NO_REGISTRADA",   "Tarjeta no",      "registrada"},
  {"PERSONA_DESHABILITADA",      "Persona",         "deshabilitada"},
  {"NO_ES_ESTUDIANTE",           "No es tarjeta",   "de estudiante"},
  {"SIN_JORNADA",                "Sin jornada",     "escolar ahora"},
  {"SIN_MATRICULA",              "Sin matricula",   "Avise direccion"},
  {"OPERACION_NO_HABILITADA",    "Modo no activo",  "en el servidor"},
  {"ESTACION_SIN_CONFIGURACION", "Estacion sin",    "configurar"},
  {"ESTACION_SIN_AULA",          "Estacion sin",    "aula asignada"},
  {"SALIDA_YA_ABIERTA",          "Ya esta fuera",   "Marque regreso"},
  {"SIN_SALIDA_ABIERTA",         "No tiene salida", "abierta"},
  {"MOTIVO_INVALIDO",            "Motivo no",       "valido"},
  {"REGRESO_ANTES_DE_SALIDA",    "Hora de regreso", "invalida"},
  {"HORA_FUTURA",                "Error de hora",   "Sincronice reloj"},
  {"HORA_NO_UTC",                "Error de hora",   "Sincronice reloj"},
  {"MODO_INVALIDO",              "Modo invalido",   ""},
  {"SELECCION_REPETIDA",         "Operacion",       "repetida"},
  {"REFERENCIA_INVALIDA",        "Tarjeta",         "ilegible"},
};

// ---------- Estado ----------
enum Estado { ESPERA, MOV_ELEGIR, MOV_CONFIRMAR, ADMIN_TARJETA, ADMIN_MENU };

enum TipoRespuesta { RES_OK, RES_RECHAZADO, RES_NO_AUTORIZADO, RES_CONFLICTO, RES_ERROR_SERVIDOR, RES_SIN_RED };
struct Respuesta {
  TipoRespuesta tipo;
  int http;
  String cuerpo;
};

Estado estado = ESPERA;
String modo = "ASISTENCIA";     // "ASISTENCIA" o "MOVIMIENTOS"

// Lo que la estación sabe de sí misma (se guarda en memoria flash)
String aulaCodigo;              // "3C"
String etiquetaPunto;           // "Puerta 3C"
String modosServidor;           // "ASISTENCIA,MOVIMIENTOS"
int segundosSelector = 15;      // ai.estacion_configuracion.expiracion_selector_s

// Flujo de salida/regreso en curso
String refPendiente;
String nombrePendiente;
String seleccionPendiente;      // UUID: si se reenvía, la API no duplica
int motivoPendiente = -1;       // índice en MOTIVOS, o -1 = regreso
bool selectorSinRed = false;    // sin red no se sabe si está dentro o fuera
unsigned long lecturaMs = 0;    // cuándo pasó la tarjeta (para seleccionEdadMs)

unsigned long limiteSelector = 0;
unsigned long mensajeHasta = 0;
unsigned long ultimaActividad = 0;
unsigned long proximoIntentoCola = 0;
unsigned long proximaSyncHora = 0;
unsigned long proximoHeartbeat = 0;
unsigned long proximaConfiguracion = 0;
bool pantallaEncendida = true;
bool redibujar = true;
bool sdLista = false;
bool horaValida = false;
bool horaPorNtp = false;

String ultimaRef;
unsigned long ultimaRefMs = 0;

// ============================================================
//  Pantalla, sonidos y luces
// ============================================================
void pantalla(const String& linea1, const String& linea2, unsigned long ms = 0) {
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print(linea1.substring(0, 16));
  lcd.setCursor(0, 1);
  lcd.print(linea2.substring(0, 16));
  mensajeHasta = ms > 0 ? millis() + ms : 0;
}

void despertar() {
  ultimaActividad = millis();
  if (!pantallaEncendida) {
    lcd.backlight();
    pantallaEncendida = true;
  }
}

void apagarLeds() {
  digitalWrite(PIN_LED_VERDE, LOW);
  digitalWrite(PIN_LED_ROJO, LOW);
}

void sonidoTecla() {
  tone(PIN_BUZZER, 2500, 30);
}

void sonidoInvalido() {
  tone(PIN_BUZZER, 600, 120);
}

void respuestaOk() {
  digitalWrite(PIN_LED_VERDE, HIGH);
  tone(PIN_BUZZER, 2000, 100);
  delay(150);
  tone(PIN_BUZZER, 2600, 100);
  delay(150);
}

void respuestaAviso() {  // ya registrado / guardado sin red: verde + un pitido
  digitalWrite(PIN_LED_VERDE, HIGH);
  tone(PIN_BUZZER, 1500, 200);
  delay(250);
}

void respuestaError() {
  digitalWrite(PIN_LED_ROJO, HIGH);
  tone(PIN_BUZZER, 400, 600);
  delay(650);
}

void detener(const char* mensaje) {
  Serial.println(mensaje);
  pantalla(mensaje, "Revise conexion");
  respuestaError();
  while (true) delay(1000);
}

// ============================================================
//  Hora (el DS3231 guarda UTC)
// ============================================================
DateTime horaLocal() {
  return rtc.now() + TimeSpan((int32_t)ZONA_HORARIA_HORAS * 3600);
}

String isoUtc(const DateTime& t) {
  char texto[25];
  snprintf(texto, sizeof(texto), "%04d-%02d-%02dT%02d:%02d:%02dZ",
           t.year(), t.month(), t.day(), t.hour(), t.minute(), t.second());
  return String(texto);
}

bool sincronizarNtp() {
  if (WiFi.status() != WL_CONNECTED) return false;
  configTime(0, 0, "pool.ntp.org", "time.google.com");
  struct tm info;
  if (!getLocalTime(&info, 5000)) {
    Serial.println("NTP: sin respuesta (la red puede bloquearlo)");
    return false;
  }
  time_t ahora;
  time(&ahora);
  rtc.adjust(DateTime((uint32_t)ahora));
  horaValida = true;
  horaPorNtp = true;
  Serial.println("NTP: reloj ajustado (UTC)");
  return true;
}

// Cada cuánto volver a intentar NTP (cada intento fallido congela ~5 s):
// con NTP, cada 6 h; si la hora vino de la API, cada hora; sin hora, cada minuto.
unsigned long esperaSyncHora() {
  if (horaPorNtp) return 6UL * 3600000UL;
  return horaValida ? 3600000UL : 60000UL;
}

// Respaldo si la red bloquea NTP: la API manda su hora UTC
// ("servidorUtc":"2026-10-13T16:04:58.29Z") en configuracion y heartbeat.
void ajustarConHoraServidor(const String& json) {
  if (horaPorNtp) return;
  String iso = campoTexto(json, "servidorUtc");
  int a, mes, d, h, mi, s;
  if (sscanf(iso.c_str(), "%d-%d-%dT%d:%d:%d", &a, &mes, &d, &h, &mi, &s) != 6) return;
  DateTime servidor(a, mes, d, h, mi, s);
  long diferencia = (long)servidor.unixtime() - (long)rtc.now().unixtime();
  if (labs(diferencia) >= 2) {
    rtc.adjust(servidor);
    Serial.printf("Reloj ajustado con la hora de la API (%ld s)\n", diferencia);
  }
  horaValida = true;
}

// ============================================================
//  Lectura NFC: UUID escrito en el sticker (NDEF) o UID
// ============================================================

// Deja solo hexadecimal en mayúsculas (quita guiones, dos puntos y espacios).
// Devuelve "" si aparece cualquier otro carácter.
String normalizarHex(const String& texto) {
  String limpio;
  for (size_t i = 0; i < texto.length(); i++) {
    char c = texto[i];
    if (c == '-' || c == ':' || c == ' ') continue;
    if (!isxdigit((unsigned char)c)) return "";
    limpio += (char)toupper((unsigned char)c);
  }
  return limpio;
}

// Lee el primer registro NDEF de un sticker NTAG (texto, URI u otro).
bool leerTextoNdef(String& texto) {
  uint8_t datos[144];
  int leidos = 0;

  // Lee páginas de 4 bytes (desde la página 4) hasta tener "n" bytes.
  auto asegurar = [&](int n) -> bool {
    if (n > (int)sizeof(datos)) return false;
    while (leidos < n) {
      uint8_t pagina[4];
      if (!nfc.ntag2xx_ReadPage(4 + leidos / 4, pagina)) return false;
      memcpy(datos + leidos, pagina, 4);
      leidos += 4;
    }
    return true;
  };

  int i = 0;
  while (true) {
    if (!asegurar(i + 1)) return false;
    uint8_t tipoTlv = datos[i];
    if (tipoTlv == 0x00) { i++; continue; }  // TLV nulo
    if (tipoTlv == 0xFE) return false;        // fin, sin mensaje NDEF

    if (!asegurar(i + 2)) return false;
    int largo = datos[i + 1];
    int inicio = i + 2;
    if (largo == 0xFF) {
      if (!asegurar(i + 4)) return false;
      largo = (datos[i + 2] << 8) | datos[i + 3];
      inicio = i + 4;
    }
    if (tipoTlv != 0x03) {  // otro TLV (p. ej. control de bloqueo): saltarlo
      i = inicio + largo;
      continue;
    }
    if (largo < 3) return false;  // mensaje vacío (sticker sin datos)
    if (!asegurar(inicio + largo)) return false;

    // Registro NDEF
    int p = inicio;
    uint8_t cabecera = datos[p++];
    uint8_t largoTipo = datos[p++];
    uint32_t largoCarga;
    if (cabecera & 0x10) {
      largoCarga = datos[p++];
    } else {
      largoCarga = ((uint32_t)datos[p] << 24) | ((uint32_t)datos[p + 1] << 16) |
                   ((uint32_t)datos[p + 2] << 8) | datos[p + 3];
      p += 4;
    }
    uint8_t largoId = (cabecera & 0x08) ? datos[p++] : 0;
    if (p >= inicio + largo) return false;
    char tipo = largoTipo == 1 ? (char)datos[p] : 0;
    p += largoTipo + largoId;
    if (p + (int)largoCarga > inicio + largo) return false;

    const uint8_t* carga = datos + p;
    uint32_t desde = 0;
    if (tipo == 'T') desde = 1 + (carga[0] & 0x3F);  // salta idioma
    if (tipo == 'U') desde = 1;                      // salta prefijo de URI

    texto = "";
    for (uint32_t k = desde; k < largoCarga; k++) texto += (char)carga[k];
    return texto.length() > 0;
  }
}

// Devuelve la referencia en el formato de ai.credencial_nfc:
// hexadecimal en MAYÚSCULAS, sin separadores.
bool leerReferencia(String& ref) {
  uint8_t uid[7];
  uint8_t largoUid;
  if (!nfc.readPassiveTargetID(PN532_MIFARE_ISO14443A, uid, &largoUid, 50)) {
    return false;
  }

  // Stickers (UID de 7 bytes): si la app escribió un UUID, usar ese.
  if (largoUid == 7) {
    String texto;
    if (leerTextoNdef(texto)) {
      String limpio = normalizarHex(texto);
      if (limpio.length() == 32) {
        Serial.printf("NDEF (UUID): %s\n", limpio.c_str());
        ref = limpio;
        return true;
      }
      Serial.printf("NDEF ignorado (no es UUID): %s\n", texto.c_str());
    }
  }

  ref = "";
  for (uint8_t i = 0; i < largoUid; i++) {
    char hex[3];
    snprintf(hex, sizeof(hex), "%02X", uid[i]);
    ref += hex;
  }
  Serial.printf("UID: %s\n", ref.c_str());
  return true;
}

// ============================================================
//  API
// ============================================================

// UUID v4 aleatorio: identifica cada operación (seleccion_id en ai.evento).
String nuevoUuid() {
  uint8_t b[16];
  for (int i = 0; i < 16; i += 4) {
    uint32_t r = esp_random();
    memcpy(b + i, &r, 4);
  }
  b[6] = (b[6] & 0x0F) | 0x40;
  b[8] = (b[8] & 0x3F) | 0x80;
  char texto[37];
  snprintf(texto, sizeof(texto),
           "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",
           b[0], b[1], b[2], b[3], b[4], b[5], b[6], b[7],
           b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]);
  return String(texto);
}

Respuesta llamarApi(const char* metodo, const String& ruta, const String& cuerpo) {
  Respuesta r = {RES_SIN_RED, 0, ""};
  if (WiFi.status() != WL_CONNECTED) return r;

  String url = String(API_URL) + ruta;
  HTTPClient http;
  WiFiClientSecure clienteSeguro;
  bool iniciado;
  if (url.startsWith("https")) {
    clienteSeguro.setInsecure();  // certificado de desarrollo de la API
    iniciado = http.begin(clienteSeguro, url);
  } else {
    iniciado = http.begin(url);
  }
  if (!iniciado) return r;

  http.setConnectTimeout(MS_TIMEOUT_HTTP);
  http.setTimeout(MS_TIMEOUT_HTTP);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Estacion-Id", String(ESTACION_ID));
  http.addHeader("X-Estacion-Clave", ESTACION_CLAVE);

  int codigo = strcmp(metodo, "GET") == 0 ? http.GET() : http.POST(cuerpo);
  r.http = codigo;
  r.cuerpo = codigo > 0 ? http.getString() : "";
  http.end();

  Serial.printf("%s %s %s -> %d %s\n", metodo, ruta.c_str(), cuerpo.c_str(), codigo, r.cuerpo.c_str());

  if (codigo <= 0) r.tipo = RES_SIN_RED;
  else if (codigo >= 200 && codigo < 300) r.tipo = RES_OK;
  else if (codigo == 401 || codigo == 403) r.tipo = RES_NO_AUTORIZADO;
  else if (codigo == 409) r.tipo = RES_CONFLICTO;
  else if (codigo >= 400 && codigo < 500) r.tipo = RES_RECHAZADO;
  else r.tipo = RES_ERROR_SERVIDOR;
  return r;
}

// "clave":"valor" de una respuesta JSON simple ("" si no está o es null).
String campoTexto(const String& json, const char* clave) {
  String buscar = String("\"") + clave + "\":\"";
  int i = json.indexOf(buscar);
  if (i < 0) return "";
  i += buscar.length();
  int j = json.indexOf('"', i);
  return j < 0 ? "" : json.substring(i, j);
}

// "clave":true / false
bool campoBool(const String& json, const char* clave, bool siNoEsta) {
  String buscar = String("\"") + clave + "\":";
  int i = json.indexOf(buscar);
  if (i < 0) return siNoEsta;
  return json.startsWith("true", i + buscar.length());
}

String cuerpoLectura(const String& ref, const String& modoLectura, const String& ocurridoUtc,
                     const String& seleccion, unsigned long edadMs) {
  String cuerpo = "{\"referenciaNfc\":\"" + ref + "\",\"modo\":\"" + modoLectura + "\"";
  // Solo los registros guardados sin red llevan hora: en línea manda la del servidor.
  if (ocurridoUtc.length() > 0) cuerpo += ",\"ocurridoUtc\":\"" + ocurridoUtc + "\"";
  cuerpo += ",\"seleccionId\":\"" + seleccion + "\",\"seleccionEdadMs\":" + String(edadMs) + "}";
  return cuerpo;
}

String cuerpoMovimiento(const String& ref, const String& motivoCodigo, const String& ocurridoUtc,
                        const String& seleccion, unsigned long edadMs) {
  String cuerpo = "{\"referenciaNfc\":\"" + ref + "\"";
  if (motivoCodigo.length() > 0) cuerpo += ",\"motivoCodigo\":\"" + motivoCodigo + "\"";
  if (ocurridoUtc.length() > 0) cuerpo += ",\"ocurridoUtc\":\"" + ocurridoUtc + "\"";
  cuerpo += ",\"seleccionId\":\"" + seleccion + "\",\"seleccionEdadMs\":" + String(edadMs) + "}";
  return cuerpo;
}

void mostrarRechazo(const String& cuerpo) {
  String codigo = campoTexto(cuerpo, "codigo");
  for (const MensajeError& e : ERRORES) {
    if (codigo == e.codigo) {
      pantalla(e.linea1, e.linea2, 3000);
      respuestaError();
      return;
    }
  }
  pantalla("No registrado", codigo.length() > 0 ? codigo : String("Avise al docente"), 3000);
  respuestaError();
}

void mostrarSinPermiso() {
  pantalla("Estacion sin", "permiso: clave?", 3000);
  respuestaError();
}

// ============================================================
//  Tarjetas de personal conocidas (para decidir sin red)
//  Formato: REFERENCIA;TIPO   (PROFESOR, AUXILIAR, ADMINISTRADOR, PERSONAL)
// ============================================================
String tipoEnCache(const String& ref) {
  if (!sdLista || !SD.exists(ARCHIVO_PERSONAL)) return "";
  File f = SD.open(ARCHIVO_PERSONAL);
  if (!f) return "";
  String tipo;
  while (f.available()) {
    String linea = f.readStringUntil('\n');
    linea.trim();
    int sep = linea.indexOf(';');
    if (sep > 0 && linea.substring(0, sep) == ref) {
      tipo = linea.substring(sep + 1);
      break;
    }
  }
  f.close();
  return tipo;
}

// Guarda (o borra, si es ESTUDIANTE) el tipo de la tarjeta. Solo reescribe si cambió.
void recordarTipo(const String& ref, const String& tipo) {
  if (!sdLista || tipo.length() == 0) return;
  String actual = tipoEnCache(ref);
  bool esPersonal = tipo != "ESTUDIANTE";
  if (esPersonal ? actual == tipo : actual.length() == 0) return;

  std::vector<String> lineas;
  if (SD.exists(ARCHIVO_PERSONAL)) {
    File f = SD.open(ARCHIVO_PERSONAL);
    while (f && f.available()) {
      String linea = f.readStringUntil('\n');
      linea.trim();
      if (linea.length() > 0 && !linea.startsWith(ref + ";")) lineas.push_back(linea);
    }
    if (f) f.close();
  }
  if (esPersonal) lineas.push_back(ref + ";" + tipo);

  SD.remove(ARCHIVO_PERSONAL_TMP);
  File tmp = SD.open(ARCHIVO_PERSONAL_TMP, FILE_WRITE);
  if (!tmp) return;
  for (const String& linea : lineas) tmp.println(linea);
  tmp.close();
  SD.remove(ARCHIVO_PERSONAL);
  SD.rename(ARCHIVO_PERSONAL_TMP, ARCHIVO_PERSONAL);
}

bool esAdminDeEmergencia(const String& ref) {
  for (const char* admin : ADMIN_REFS) {
    if (ref.equalsIgnoreCase(admin)) return true;
  }
  return false;
}

// ============================================================
//  Cola offline en la microSD
//  Línea: tipo;referencia;motivo;ocurridoUtc;seleccionId;edadMs;modo
//  tipo = LECTURA (va a /api/estacion/lectura), SALIDA o REGRESO
// ============================================================
bool encolar(const char* tipo, const String& ref, const String& motivo,
             const String& seleccion, unsigned long edadMs) {
  if (!sdLista || !horaValida) return false;
  File f = SD.open(ARCHIVO_COLA, FILE_APPEND);
  if (!f) return false;
  String iso = isoUtc(rtc.now());
  f.printf("%s;%s;%s;%s;%s;%lu;%s\n", tipo, ref.c_str(), motivo.c_str(), iso.c_str(),
           seleccion.c_str(), edadMs, modo.c_str());
  f.close();
  Serial.printf("Encolado: %s %s %s %s\n", tipo, ref.c_str(), motivo.c_str(), iso.c_str());
  return true;
}

int contarCola() {
  if (!sdLista || !SD.exists(ARCHIVO_COLA)) return 0;
  File f = SD.open(ARCHIVO_COLA);
  if (!f) return 0;
  int total = 0;
  while (f.available()) {
    if (f.readStringUntil('\n').length() > 1) total++;
  }
  f.close();
  return total;
}

// Envía lo guardado. Lo aceptado se borra; lo rechazado (400) se archiva en
// rechazados.csv con su código; si no hay red o la API falla, el resto
// queda para el próximo intento (el seleccionId evita duplicados).
int procesarCola(int maximo) {
  if (!sdLista || WiFi.status() != WL_CONNECTED || !SD.exists(ARCHIVO_COLA)) return 0;

  File cola = SD.open(ARCHIVO_COLA);
  if (!cola) return 0;

  std::vector<String> pendientes;
  bool cortar = false;
  int enviados = 0;

  while (cola.available()) {
    String linea = cola.readStringUntil('\n');
    linea.trim();
    if (linea.length() == 0) continue;
    if (cortar || enviados >= maximo) {
      pendientes.push_back(linea);
      continue;
    }

    String c[7];
    int desde = 0;
    for (int k = 0; k < 7; k++) {
      int sep = linea.indexOf(';', desde);
      c[k] = sep < 0 ? linea.substring(desde) : linea.substring(desde, sep);
      desde = sep < 0 ? linea.length() : sep + 1;
    }
    String tipo = c[0], ref = c[1], motivo = c[2], iso = c[3], seleccion = c[4], modoLinea = c[6];
    unsigned long edad = strtoul(c[5].c_str(), nullptr, 10);
    if (seleccion.length() == 0) seleccion = nuevoUuid();
    if (modoLinea.length() == 0) modoLinea = "ASISTENCIA";

    Respuesta r;
    if (tipo == "SALIDA") {
      r = llamarApi("POST", RUTA_SALIDA, cuerpoMovimiento(ref, motivo, iso, seleccion, edad));
    } else if (tipo == "REGRESO") {
      r = llamarApi("POST", RUTA_REGRESO, cuerpoMovimiento(ref, "", iso, seleccion, edad));
    } else {
      r = llamarApi("POST", RUTA_LECTURA, cuerpoLectura(ref, modoLinea, iso, seleccion, edad));
    }

    if (r.tipo == RES_OK) {
      enviados++;
    } else if (r.tipo == RES_RECHAZADO) {
      File rech = SD.open(ARCHIVO_RECHAZADOS, FILE_APPEND);
      if (rech) {
        rech.printf("%s;%s\n", linea.c_str(), campoTexto(r.cuerpo, "codigo").c_str());
        rech.close();
      }
    } else {
      pendientes.push_back(linea);
      cortar = true;  // sin red, sin permiso o servidor caído: no insistir ahora
    }
  }
  cola.close();

  // Reescribir la cola con lo que falta (archivo temporal + renombrar)
  SD.remove(ARCHIVO_COLA_TMP);
  if (!pendientes.empty()) {
    File tmp = SD.open(ARCHIVO_COLA_TMP, FILE_WRITE);
    if (tmp) {
      for (const String& linea : pendientes) tmp.println(linea);
      tmp.close();
    }
  }
  SD.remove(ARCHIVO_COLA);
  if (!pendientes.empty()) SD.rename(ARCHIVO_COLA_TMP, ARCHIVO_COLA);

  if (enviados > 0) Serial.printf("Cola: %d enviados, %d pendientes\n", enviados, (int)pendientes.size());
  return enviados;
}

void guardarSinRed(const char* tipo, const String& ref, const String& motivo,
                   const String& seleccion, unsigned long edadMs) {
  if (encolar(tipo, ref, motivo, seleccion, edadMs)) {
    pantalla("Sin conexion", "Guardado OK", 2500);
    respuestaAviso();
  } else {
    pantalla("Sin conexion", sdLista ? "Sin hora: no se" : "Sin SD: no se", 1500);
    respuestaError();
    pantalla("Sin conexion", "guardo! Avise", 2500);
  }
}

// ============================================================
//  Configuración de la estación (la API es la fuente)
// ============================================================
void cargarDatosGuardados() {
  modo = prefs.getString("modo", "ASISTENCIA");
  aulaCodigo = prefs.getString("aula", "");
  etiquetaPunto = prefs.getString("etiqueta", "");
  modosServidor = prefs.getString("modos", "");
  segundosSelector = prefs.getInt("selector", 15);
}

// GET /api/estacion/configuracion: aula, modos habilitados, tiempo del
// selector y hora del servidor. Devuelve el tipo de respuesta.
TipoRespuesta descargarConfiguracion() {
  Respuesta r = llamarApi("GET", RUTA_CONFIGURACION, "");
  if (r.tipo != RES_OK) return r.tipo;

  aulaCodigo = campoTexto(r.cuerpo, "aulaCodigo");
  etiquetaPunto = campoTexto(r.cuerpo, "etiquetaPunto");

  modosServidor = "";
  if (r.cuerpo.indexOf("\"ASISTENCIA\"") >= 0) modosServidor += "ASISTENCIA";
  if (r.cuerpo.indexOf("\"MOVIMIENTOS\"") >= 0) {
    modosServidor += modosServidor.length() > 0 ? ",MOVIMIENTOS" : "MOVIMIENTOS";
  }

  int i = r.cuerpo.indexOf("\"expiracionSelectorS\":");
  if (i >= 0) {
    int valor = r.cuerpo.substring(i + 22).toInt();
    if (valor >= 3 && valor <= 60) segundosSelector = valor;
  }

  prefs.putString("aula", aulaCodigo);
  prefs.putString("etiqueta", etiquetaPunto);
  prefs.putString("modos", modosServidor);
  prefs.putInt("selector", segundosSelector);

  ajustarConHoraServidor(r.cuerpo);
  Serial.printf("Configuracion: aula %s, modos %s, selector %d s\n",
                aulaCodigo.c_str(), modosServidor.c_str(), segundosSelector);
  return RES_OK;
}

void enviarHeartbeat() {
  String cuerpo = "{\"modo\":\"" + modo + "\",\"pendientes\":" + String(contarCola()) + "}";
  Respuesta r = llamarApi("POST", RUTA_HEARTBEAT, cuerpo);
  if (r.tipo == RES_OK) ajustarConHoraServidor(r.cuerpo);
}

// ============================================================
//  Flujos
// ============================================================
void volverEspera() {
  estado = ESPERA;
  refPendiente = "";
  nombrePendiente = "";
  seleccionPendiente = "";
  motivoPendiente = -1;
  selectorSinRed = false;
  redibujar = true;
}

void cancelar(const char* motivo) {
  volverEspera();
  pantalla("Cancelado", motivo, 1500);
  sonidoInvalido();
}

void mostrarMenuAdmin() {
  pantalla("1Asist 2Salidas", "3Inf 4Cola 5Sinc");
  limiteSelector = millis() + 30000UL;
}

void prepararSelector(const String& ref, const String& nombre) {
  refPendiente = ref;
  nombrePendiente = nombre;
  seleccionPendiente = nuevoUuid();
  motivoPendiente = -1;
  lecturaMs = millis();
  limiteSelector = millis() + segundosSelector * 1000UL;
  sonidoTecla();
}

// Estudiante dentro del aula: elige motivo y confirma.
void iniciarSalida(const String& ref, const String& nombre) {
  prepararSelector(ref, nombre);
  estado = MOV_ELEGIR;
  pantalla(nombre, "Motivo 1-5  *=X");
}

// Estudiante que estaba fuera: confirma su regreso.
void iniciarRegreso(const String& ref, const String& nombre) {
  prepararSelector(ref, nombre);
  estado = MOV_CONFIRMAR;
  pantalla("Regreso " + nombre, "#=OK  *=Cancelar");
}

// Sin red no se sabe si está dentro o fuera: se le pregunta.
void iniciarSelectorSinRed(const String& ref) {
  prepararSelector(ref, "");
  selectorSinRed = true;
  estado = MOV_ELEGIR;
  pantalla("Sin red: 1-5 sal", "A=Regreso  *=X");
}

void registrarMovimiento() {
  bool esSalida = motivoPendiente >= 0;
  String motivoCodigo = esSalida ? MOTIVOS[motivoPendiente].codigo : "";
  unsigned long edadMs = millis() - lecturaMs;

  pantalla("Enviando...", "");
  Respuesta r = llamarApi("POST", esSalida ? RUTA_SALIDA : RUTA_REGRESO,
                          cuerpoMovimiento(refPendiente, motivoCodigo, "", seleccionPendiente, edadMs));
  switch (r.tipo) {
    case RES_OK:
      if (esSalida) pantalla("Salida OK", MOTIVOS[motivoPendiente].nombre, 2500);
      else pantalla("Regreso OK", "Bienvenido!", 2500);
      respuestaOk();
      break;
    case RES_RECHAZADO:
      mostrarRechazo(r.cuerpo);
      break;
    case RES_NO_AUTORIZADO:
      mostrarSinPermiso();
      break;
    default:  // sin red, servidor caído o conflicto: se guarda con el mismo seleccionId
      guardarSinRed(esSalida ? "SALIDA" : "REGRESO", refPendiente, motivoCodigo, seleccionPendiente, edadMs);
      break;
  }
  volverEspera();
}

// Lo que respondió POST /api/estacion/lectura
void atenderLectura(const String& ref, const String& json) {
  String resultado = campoTexto(json, "resultado");
  String nombre = campoTexto(json, "nombreCorto");
  recordarTipo(ref, campoTexto(json, "tipoPersona"));

  if (resultado == "ASISTENCIA_REGISTRADA") {
    String puntualidad = campoTexto(json, "puntualidad");
    pantalla(nombre, puntualidad == "TARDIA" ? "Llegada tardia" : "Asistencia OK", 2500);
    respuestaOk();
  } else if (resultado == "ASISTENCIA_YA_REGISTRADA") {
    pantalla(nombre, "Ya registrado", 2000);
    respuestaAviso();
  } else if (resultado == "LLEGADA_PERSONAL" || resultado == "SALIDA_PERSONAL") {
    bool llegada = resultado == "LLEGADA_PERSONAL";
    bool aceptado = campoBool(json, "aceptado", true);
    String clase = campoTexto(json, "clase");
    pantalla((llegada ? "Llegada " : "Salida ") + nombre,
             aceptado ? (clase.length() > 0 ? clase : String("Registrado")) : String("Sin clase aqui"), 2500);
    if (aceptado) respuestaOk();
    else respuestaAviso();
  } else if (resultado == "PERSONAL_YA_REGISTRADO") {
    bool llegada = campoTexto(json, "accion") == "LLEGADA_PERSONAL";
    pantalla(nombre, llegada ? "Llegada ya marc." : "Salida ya marc.", 2000);
    respuestaAviso();
  } else if (resultado == "ELEGIR_MOTIVO") {
    iniciarSalida(ref, nombre);
  } else if (resultado == "CONFIRMAR_REGRESO") {
    iniciarRegreso(ref, nombre);
  } else {
    pantalla("Respuesta", "desconocida", 2000);
    respuestaError();
  }
}

void manejarTarjetaEnEspera(const String& ref) {
  pantalla("Leyendo...", "");
  String seleccion = nuevoUuid();
  Respuesta r = llamarApi("POST", RUTA_LECTURA, cuerpoLectura(ref, modo, "", seleccion, 0));

  switch (r.tipo) {
    case RES_OK:
      atenderLectura(ref, r.cuerpo);
      return;
    case RES_RECHAZADO:
      mostrarRechazo(r.cuerpo);
      return;
    case RES_NO_AUTORIZADO:
      mostrarSinPermiso();
      return;
    default:
      break;
  }

  // Sin red (o API caída)
  String tipo = tipoEnCache(ref);
  bool esPersonal = tipo.length() > 0 && tipo != "ESTUDIANTE";
  if (modo == "ASISTENCIA" || esPersonal) {
    // La API decide al reenviar: asistencia del estudiante o llegada/salida del personal.
    guardarSinRed("LECTURA", ref, "", seleccion, 0);
  } else {
    iniciarSelectorSinRed(ref);
  }
}

void verificarAdmin(const String& ref) {
  pantalla("Verificando...", "");
  Respuesta r = llamarApi("GET", String(RUTA_CREDENCIAL) + ref, "");
  bool admin = false;

  if (r.tipo == RES_OK) {
    recordarTipo(ref, campoTexto(r.cuerpo, "tipoPersona"));
    admin = campoBool(r.cuerpo, "esAdministrador", false);
  } else if (r.tipo != RES_RECHAZADO) {
    // Sin red: vale lo último que dijo la API de esta tarjeta.
    admin = tipoEnCache(ref) == "ADMINISTRADOR";
  }
  admin = admin || esAdminDeEmergencia(ref);

  if (admin) {
    estado = ADMIN_MENU;
    sonidoTecla();
    mostrarMenuAdmin();
  } else {
    volverEspera();
    pantalla("No autorizado", "Solo admin", 2000);
    respuestaError();
  }
}

void manejarTarjeta(const String& ref) {
  if (estado == ADMIN_TARJETA) verificarAdmin(ref);
  else manejarTarjetaEnEspera(ref);
}

void mostrarInfo() {
  char linea[17];
  String ip = WiFi.status() == WL_CONNECTED ? WiFi.localIP().toString() : String("Sin WiFi");
  snprintf(linea, sizeof(linea), "Cola:%d SD:%s", contarCola(), sdLista ? "si" : "no");
  pantalla(ip, linea);
  delay(2500);

  snprintf(linea, sizeof(linea), "Est #%d Aula %s", ESTACION_ID, aulaCodigo.length() > 0 ? aulaCodigo.c_str() : "?");
  pantalla(linea, etiquetaPunto.length() > 0 ? etiquetaPunto : String("Sin config."));
  delay(2500);

  DateTime t = horaLocal();
  snprintf(linea, sizeof(linea), "Hora %02d:%02d %s", t.hour(), t.minute(),
           !horaValida ? "MAL" : horaPorNtp ? "NTP" : "API");
  pantalla(linea, "Modos: " + (modosServidor.length() > 0 ? modosServidor : String("?")));
  delay(2500);
}

void cambiarModo(const String& nuevo) {
  modo = nuevo;
  prefs.putString("modo", modo);
  volverEspera();
  bool habilitado = modosServidor.length() == 0 || modosServidor.indexOf(nuevo) >= 0;
  if (habilitado) {
    pantalla("Modo cambiado:", nuevo == "ASISTENCIA" ? "ASISTENCIA" : "SALIDAS", 2000);
    respuestaOk();
  } else {
    pantalla("Aviso: modo no", "activo en API", 2500);
    respuestaError();
  }
  enviarHeartbeat();  // la API guarda el modo que muestra la estación
}

void manejarTecla(char tecla) {
  switch (estado) {
    case ESPERA:
      if (tecla == 'D') {
        estado = ADMIN_TARJETA;
        limiteSelector = millis() + 15000UL;
        sonidoTecla();
        pantalla("Modo admin", "Pase credencial");
      } else {
        sonidoInvalido();
        pantalla("Primero pase", "su tarjeta", 1500);
      }
      break;

    case MOV_ELEGIR:
      if (tecla == '*') {
        cancelar("");
        return;
      }
      if (tecla == TECLA_REGRESO && selectorSinRed) {
        motivoPendiente = -1;
        estado = MOV_CONFIRMAR;
        limiteSelector = millis() + segundosSelector * 1000UL;
        sonidoTecla();
        pantalla("Regreso al aula", "#=OK  *=Cancelar");
        return;
      }
      for (int i = 0; i < TOTAL_MOTIVOS; i++) {
        if (MOTIVOS[i].tecla == tecla) {
          motivoPendiente = i;
          estado = MOV_CONFIRMAR;
          limiteSelector = millis() + segundosSelector * 1000UL;
          sonidoTecla();
          pantalla(MOTIVOS[i].nombre, "#=OK  *=Cancelar");
          return;
        }
      }
      sonidoInvalido();  // tecla sin uso
      break;

    case MOV_CONFIRMAR:
      if (tecla == '#') registrarMovimiento();
      else if (tecla == '*') cancelar("");
      else sonidoInvalido();
      break;

    case ADMIN_TARJETA:
      if (tecla == '*') cancelar("");
      else sonidoInvalido();
      break;

    case ADMIN_MENU:
      if (tecla == '1') {
        cambiarModo("ASISTENCIA");
      } else if (tecla == '2') {
        cambiarModo("MOVIMIENTOS");
      } else if (tecla == '3') {
        mostrarInfo();
        mostrarMenuAdmin();
      } else if (tecla == '4') {
        pantalla("Enviando cola...", "");
        int enviados = procesarCola(50);
        char linea2[17];
        snprintf(linea2, sizeof(linea2), "Env:%d Falta:%d", enviados, contarCola());
        pantalla("Cola procesada", linea2);
        delay(2500);
        mostrarMenuAdmin();
      } else if (tecla == '5') {
        pantalla("Sincronizando...", "");
        bool ntp = sincronizarNtp();
        TipoRespuesta conf = descargarConfiguracion();
        pantalla(conf == RES_OK ? "Config. OK" : conf == RES_NO_AUTORIZADO ? "Clave invalida" : "Config. fallo",
                 ntp ? "Hora NTP OK" : horaValida ? "Hora de la API" : "Sin hora");
        delay(2500);
        mostrarMenuAdmin();
      } else if (tecla == '*') {
        volverEspera();
      } else {
        sonidoInvalido();
      }
      break;
  }
}

// ============================================================
//  Pantalla de espera
// ============================================================
void dibujarEspera() {
  static int ultimoSegundo = -1;
  DateTime t = horaLocal();
  if (!redibujar && t.second() == ultimoSegundo) return;
  ultimoSegundo = t.second();

  char linea1[17];
  snprintf(linea1, sizeof(linea1), "%-10s %02d:%02d",
           modo == "ASISTENCIA" ? "ASISTENCIA" : "SALIDAS", t.hour(), t.minute());

  // '!' al final = sin WiFi (lo que se registre se guarda en la SD)
  char linea2[17];
  snprintf(linea2, sizeof(linea2), "Pase su tarjeta%c", WiFi.status() == WL_CONNECTED ? ' ' : '!');

  if (redibujar) lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print(linea1);
  lcd.setCursor(0, 1);
  lcd.print(linea2);
  redibujar = false;
}

// ============================================================
//  Arranque
// ============================================================
void iniciarSD() {
  SPI.begin(PIN_SD_SCK, PIN_SD_MISO, PIN_SD_MOSI, PIN_SD_CS);
  sdLista = SD.begin(PIN_SD_CS);
  Serial.println(sdLista ? "microSD lista" : "microSD no encontrada");
}

void iniciarWiFi() {
  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  pantalla("Conectando WiFi", WIFI_SSID);
  unsigned long limite = millis() + 10000UL;
  while (WiFi.status() != WL_CONNECTED && millis() < limite) delay(250);

  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("WiFi OK, IP: %s\n", WiFi.localIP().toString().c_str());
  } else {
    Serial.println("WiFi no disponible: se seguira intentando en segundo plano");
  }
}

void setup() {
  Serial.begin(115200);
  delay(300);

  pinMode(PIN_BUZZER, OUTPUT);
  pinMode(PIN_LED_VERDE, OUTPUT);
  pinMode(PIN_LED_ROJO, OUTPUT);
  apagarLeds();

  Wire.begin(21, 22);
  lcd.init();
  lcd.backlight();
  char linea2[17];
  snprintf(linea2, sizeof(linea2), "Estacion #%d", ESTACION_ID);
  pantalla("AulaInnova", linea2);

  if (!rtc.begin()) detener("Error: reloj");
  horaValida = !rtc.lostPower() && rtc.now().year() >= 2025;

  nfc.begin();
  if (!nfc.getFirmwareVersion()) detener("Error: NFC");
  nfc.SAMConfig();

  iniciarSD();

  prefs.begin("estacion", false);
  cargarDatosGuardados();

  iniciarWiFi();
  sincronizarNtp();

  // Qué aula y modos tiene en la API (si no hay red, usa lo guardado).
  TipoRespuesta conf = RES_SIN_RED;
  if (WiFi.status() == WL_CONNECTED) {
    pantalla("Pidiendo config.", "a la API...");
    conf = descargarConfiguracion();
  }

  unsigned long ahora = millis();
  proximaSyncHora = ahora + esperaSyncHora();
  proximaConfiguracion = ahora + 30UL * 60000UL;
  proximoHeartbeat = ahora + 5000UL;

  if (conf == RES_NO_AUTORIZADO) {
    pantalla("Clave estacion", "invalida (401)", 3000);
    respuestaError();
  } else if (conf == RES_RECHAZADO) {
    pantalla("Estacion sin", "configurar (API)", 3000);
    respuestaError();
  } else if (!sdLista) {
    pantalla("Aviso:", "Sin microSD", 2000);
    respuestaError();
  } else if (!horaValida) {
    pantalla("Aviso: sin hora", "Conecte WiFi", 2000);
    respuestaError();
  } else {
    digitalWrite(PIN_LED_VERDE, HIGH);
    digitalWrite(PIN_LED_ROJO, HIGH);
    tone(PIN_BUZZER, 1500, 200);
    delay(400);
    apagarLeds();
  }

  ultimaActividad = millis();
  redibujar = true;
}

// ============================================================
//  Bucle principal
// ============================================================
void loop() {
  unsigned long ahora = millis();

  // 1) Teclado
  char tecla = teclado.getKey();
  if (tecla) {
    bool estabaApagada = !pantallaEncendida;
    despertar();
    if (estabaApagada) {  // la primera tecla solo enciende la pantalla
      redibujar = true;
      return;
    }
    mensajeHasta = 0;
    apagarLeds();
    manejarTecla(tecla);
    return;
  }

  // 2) Mientras se muestra un mensaje, esperar
  if (mensajeHasta != 0) {
    if (ahora < mensajeHasta) return;
    mensajeHasta = 0;
    apagarLeds();
    redibujar = true;
  }

  // 3) Tiempo agotado en un flujo
  if (estado != ESPERA && ahora > limiteSelector) {
    cancelar("Tiempo agotado");
    return;
  }

  // 4) Tarjeta
  if (estado == ESPERA || estado == ADMIN_TARJETA) {
    String ref;
    if (leerReferencia(ref)) {
      if (ref == ultimaRef && ahora - ultimaRefMs < 5000) return;  // misma tarjeta apoyada
      ultimaRef = ref;
      ultimaRefMs = ahora;
      despertar();
      manejarTarjeta(ref);
      return;
    }
  }

  if (estado != ESPERA) return;

  // 5) Tareas de fondo (solo en espera)
  if (ahora > proximaSyncHora && WiFi.status() == WL_CONNECTED) {
    sincronizarNtp();
    proximaSyncHora = ahora + esperaSyncHora();
  }

  if (ahora > proximoIntentoCola) {
    procesarCola(10);
    proximoIntentoCola = ahora + SEGUNDOS_REINTENTO_COLA * 1000UL;
  }

  if (ahora > proximoHeartbeat) {
    enviarHeartbeat();
    proximoHeartbeat = ahora + MINUTOS_HEARTBEAT * 60000UL;
  }

  if (ahora > proximaConfiguracion) {
    descargarConfiguracion();
    proximaConfiguracion = ahora + 30UL * 60000UL;
  }

  // 6) Apagar la luz por inactividad
  if (pantallaEncendida && ahora - ultimaActividad > SEGUNDOS_PANTALLA_ACTIVA * 1000UL) {
    lcd.noBacklight();
    pantallaEncendida = false;
  }

  dibujarEspera();
}
