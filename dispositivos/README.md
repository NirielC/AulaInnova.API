# Dispositivos de AulaInnova: puesta en marcha

| Carpeta | Qué es | Dónde corre |
|---|---|---|
| [`estacion_nfc/`](estacion_nfc/) | Firmware de la estación NFC (ESP32) | En la puerta de cada aula |
| [`vision/`](vision/) | Detector de personas y movimiento (YOLO) | En la PC conectada a la cámara del aula |
| [`FLUJO_DE_DATOS.md`](FLUJO_DE_DATOS.md) | Cómo llega cada dato a la API y a la base | — |
| [`configurar_sistema.http`](configurar_sistema.http) | Todas las peticiones de configuración, en orden | Visual Studio / VS Code |

Todo habla con **AulaInnova.API** con la clave de cada estación (encabezados
`X-Estacion-Id` y `X-Estacion-Clave`), nunca con usuario y contraseña.

---

## Checklist del día de la instalación

### 1. Base de datos y API (en la computadora servidor)
- [ ] Base creada con `sql/01` a `04`, `dotnet ef database update`, `sql/05` a
      `09` (ver el README principal).
- [ ] Arrancar la API una vez (crea el usuario `admin`) y correr
      **`sql/10_concesiones_administrador.sql`**: da al administrador los
      permisos de `ai.concesion` (sin esto, crear zonas e indicadores de la
      cámara da 403).
- [ ] Arrancar la API **escuchando en la red** (por defecto solo escucha en
      `localhost` y el ESP32 no la ve):
      ```
      dotnet run --project AulaInnova.API --urls http://0.0.0.0:5077
      ```
- [ ] Permitir el puerto **5077** en el Firewall de Windows.
- [ ] Anotar la IP de esa computadora (`ipconfig` → Dirección IPv4).
- [ ] Hora de la computadora en automático (la API pone la hora de todo).

### 2. Datos del colegio (web o `configurar_sistema.http`, sección 2)
- [ ] Aulas, grupos, asignaturas, estudiantes, personal.
- [ ] **Usuarios con rol** para el personal: `Profesor`, `Auxiliar` o
      `Administrador`. La estación usa el rol para saber quién es.
- [ ] Matrículas y **horario semanal** (`diaSemana` 1 = lunes … 7 = domingo).
- [ ] Credenciales NFC con la referencia que muestra la estación.

### 3. Jornadas y clases (sección 3)
- [ ] `POST /api/admin/jornadas/rango` para todo el periodo (ej. hasta fin de
      año, lunes a viernes). Crea las jornadas **y las clases de cada día desde
      el horario**. Sin jornada no hay asistencia ni sesiones de cámara.
- [ ] Feriados: `PATCH /api/admin/jornadas/{id}/status?estado=CANCELADA`.

### 4. Estación NFC (sección 4 + [`estacion_nfc/README.md`](estacion_nfc/README.md))
- [ ] Crear la estación, configurarla (`AULA`, su `aulaId`,
      `permiteAsistencia` y `permiteMovimientos` en `true`) y emitir su clave.
- [ ] `config.h`: WiFi 2.4 GHz, `API_URL`, `ESTACION_ID`, `ESTACION_CLAVE`.
- [ ] Subir el firmware y hacer las pruebas de la tabla del README de la estación.

### 5. Cámara (sección 5 + [`vision/README.md`](vision/README.md))
- [ ] Crear otra estación para la cámara (`CAM-3C-01`), configurarla como
      `AULA` del mismo salón **sin modos**, emitir su clave.
- [ ] `vision/config_local.py`: `API_URL`, `ESTACION_ID`, `ESTACION_CLAVE`, `AULA_ID`.
- [ ] Una sola vez: `07_configurar_api.py indicadores` y `zonas`.
- [ ] `07_configurar_api.py sesion` → debe mostrar la clase que hay ahora.
- [ ] Dejar corriendo `08_detectar.py`: **las clases cambian solas** según el horario.

---

## Qué pasa solo, sin que nadie haga nada

| Cuándo | Qué |
|---|---|
| Al crear jornadas | Se crean las clases (asignaciones) de cada día desde el horario |
| Empieza una clase | La cámara pide la sesión y la API la abre para esa clase |
| Termina la clase | La API cierra la sesión; la cámara manda su último minuto |
| Recreo / fuera de horario | La cámara sigue encendida pero no envía nada |
| Se cae la red | Estación → microSD; cámara → `pendientes.jsonl`; se reenvía solo al volver |
| Cada 5 min | La estación avisa que sigue viva (`ultima_comunicacion_utc`) |
| Profesor pasa su tarjeta | Llegada (y luego salida) ligada a su clase → cobertura del aula |
