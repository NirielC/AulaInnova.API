// ============================================================
//  Configuración de la estación NFC AulaInnova
//
//  1. Copia este archivo como "config.h" (en la misma carpeta).
//  2. Llena los datos de abajo.
//  config.h NO se sube a git (tiene la clave del WiFi y de la estación).
// ============================================================
#pragma once

// ---------- WiFi (2.4 GHz; el ESP32 no ve redes de 5 GHz) ----------
#define WIFI_SSID      "NOMBRE_DE_LA_RED"
#define WIFI_PASSWORD  "CLAVE_DE_LA_RED"

// ---------- API ----------
// URL de la computadora que corre AulaInnova.API, SIN "/" al final.
// La API debe escuchar en la red, no solo en localhost:
//   dotnet run --project AulaInnova.API --urls http://0.0.0.0:5077
// La IP es la de esa computadora (ipconfig en Windows).
#define API_URL        "http://192.168.1.100:5077"

// Id de ai.estacion y su clave. La clave es el valor COMPLETO que
// devuelve POST /api/admin/estaciones/{id}/secret  ->  "identificador.secreto"
#define ESTACION_ID    1
#define ESTACION_CLAVE "IDENTIFICADOR.SECRETO"

// ---------- Hora ----------
// El reloj DS3231 guarda UTC (lo que pide la API); la pantalla muestra
// hora local. El Salvador = UTC-6 (no tiene horario de verano).
#define ZONA_HORARIA_HORAS (-6)

// ---------- Administradores de emergencia ----------
// La estación pregunta a la API si una tarjeta es de un Administrador.
// Estas referencias entran al menú (tecla D) SIEMPRE, aunque no haya red:
// sirven para configurar la estación la primera vez o sin conexión.
// Formato: como la muestra el Monitor Serie (hexadecimal, sin guiones).
const char* const ADMIN_REFS[] = {
  "395C8B8E",   // llavero de prueba: cámbialo por una credencial real
};

// ---------- Comportamiento ----------
#define SEGUNDOS_PANTALLA_ACTIVA 60   // la luz de la pantalla se apaga tras este tiempo
#define SEGUNDOS_REINTENTO_COLA  30   // cada cuánto reenvía lo guardado sin red
#define MINUTOS_HEARTBEAT        5    // cada cuánto avisa a la API que sigue viva
#define MS_TIMEOUT_HTTP          5000 // espera máxima por una respuesta de la API
