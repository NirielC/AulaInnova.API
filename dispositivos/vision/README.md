# AulaInnova · Módulo de visión

Cuenta personas por zona del aula y mide el **movimiento de los estudiantes**
con una webcam y YOLO, y lo envía a `AulaInnova.API`
(`POST /api/Visual/measurements`), siguiendo solo el horario de clases. **Nunca guarda ni envía video de la clase:
a la API solo llegan números por minuto.**

Mismo esquema que LESSAI: `config.py` + herramientas numeradas en `tools/`.

```
AULAINNOVA_VISION/
├─ config.py               ← ajustes (los datos de la instalación van en config_local.py)
├─ aulainnova_vision.py    ← lógica compartida (zonas, roles, mediciones, API)
├─ requirements.txt
└─ tools/
   ├─ 01_probar_camara.py      ver la cámara, guardar referencia.jpg
   ├─ 02_capturar_dataset.py   fotos automáticas durante una clase
   ├─ 03_autoetiquetar.py      un modelo grande pone las cajas
   ├─ 04_revisar_etiquetas.py  corregir cajas a mano
   ├─ 05_entrenar.py           fine-tuning + comparación + ONNX
   ├─ 06_dibujar_zonas.py      DOCENTE / ESTUDIANTES / PUERTA
   ├─ 07_configurar_api.py     indicadores y zonas en la API (una vez)
   └─ 08_detectar.py           el que queda corriendo todo el día
```

---

## Instalación (hacerlo EN CASA, con internet)

```bash
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
python tools/01_probar_camara.py --yolo   # descarga yolo11s.pt la primera vez
```
`03_autoetiquetar.py` descarga `yolo11x.pt` (~110 MB) la primera vez: córrelo
también en casa con alguna foto de prueba para que ya quede descargado.

---

## El día de la instalación, en orden

### 1. Cámara fija (15 min)
1. Webcam arriba de la pizarra, al centro, con la **extensión USB activa** hasta la PC.
2. `python tools/01_probar_camara.py --yolo`
   - Que se vea todo el salón, que detecte a las personas del fondo, Luz > 50.
3. Cuando quede **fija para siempre**, presiona **S** → `referencia.jpg`.

> Si la cámara se mueve después, hay que volver a hacer el paso 3 y redibujar las zonas.

### 2. Zonas (5 min)
`python tools/06_dibujar_zonas.py` → marcar sobre el **piso**:
- **1 DOCENTE**: escritorio y frente de la pizarra.
- **2 ESTUDIANTES**: el área de pupitres.
- **3 PUERTA**: la entrada (opcional, para contar entradas y salidas).

### 3. Probar sin la API
`python tools/08_detectar.py --sin-api --ver`
- Cajas **verdes** = estudiantes, **azules** = docente (tarda ~20 s en reconocerlo).
- Cada minuto imprime los valores en la consola.

**El modelo base ya funciona desde el primer día.** Entrenar solo lo mejora.

### 4. Juntar el dataset (durante una clase real)
`python tools/02_capturar_dataset.py --minutos 90`
- 1 foto cada 1.5 s, sin filtros (guarda todas). Cambiar con `--intervalo`.
- Meta: **300-500 fotos**, de varias clases y horarios.

### 5. Etiquetar, revisar y entrenar
```bash
python tools/03_autoetiquetar.py      # el modelo grande pone las cajas
python tools/04_revisar_etiquetas.py  # corregir 100-200 fotos (clic izq. agregar, der. borrar)
python tools/05_entrenar.py           # entrena y SOLO reemplaza si mejora
```
Sin GPU tarda horas. Alternativa: **Google Colab** (gratis, con GPU):
1. `python tools/05_entrenar.py --zip` → sube `dataset_yolo.zip` a Colab.
2. En Colab:
   ```python
   !pip install ultralytics
   !unzip -q dataset_yolo.zip -d yolo
   !sed -i "s|^path:.*|path: /content/yolo|" yolo/data.yaml
   from ultralytics import YOLO
   YOLO("yolo11s.pt").train(data="yolo/data.yaml", epochs=80, imgsz=640, single_cls=True)
   ```
3. Descarga `runs/detect/train/weights/best.pt` y guárdalo como `models/aulainnova_personas.pt`.

### 6. Conectar con la API (una sola vez)
1. **Registrar la cámara como estación de AULA** (como admin, ver
   `dispositivos/README.md` del repo): `POST /api/admin/estaciones`
   (`"codigoLogico": "CAM-3C-01"`), `POST /api/admin/estaciones/{id}/configuration`
   (`"tipoPunto": "AULA"`, el `aulaId` del salón, sin modos) y
   `POST /api/admin/estaciones/{id}/secret`. Crear `config_local.py` (no se
   sube a git) con `API_URL`, `ESTACION_ID`, `ESTACION_CLAVE` y `AULA_ID`
   (ejemplo al final de `config.py`).
2. Una sola vez (pide usuario admin; si da 403, correr
   `sql/10_concesiones_administrador.sql`):
   ```bash
   python tools/07_configurar_api.py indicadores
   python tools/07_configurar_api.py zonas
   python tools/07_configurar_api.py sesion     # comprobar: ¿qué clase ve la API ahora?
   ```

### 7. Uso diario: nada que hacer
```bash
python tools/08_detectar.py
```
Se deja corriendo todo el día. **Las clases cambian solas**: cada minuto el
detector pregunta a la API qué clase hay en el aula de la cámara. La API mira
el horario del día (las asignaciones que se generan del horario al crear las
jornadas), **abre la sesión** de la clase que empieza y **cierra** la que
terminó. En recreos o fuera de horario no se envía nada.

Reglas de la API para que acepte (ver `VisualService.cs`):
- Cada medición cae **dentro de su clase** (el detector recorta la ventana al
  inicio de la sesión y al fin de la clase).
- La hora de la PC no tiene que ser perfecta: el detector calcula el desfase
  con la hora del servidor y lo corrige al enviar (avisa si pasa de 5 s).
- Solo la cámara configurada en **esa aula** puede enviar mediciones (si no: 403).
- Sin red, las mediciones quedan en `pendientes.jsonl` y se reenvían solas,
  incluso después de que la clase terminó.

---

## Qué se mide (indicadores)

| Indicador | Zonas | Valor |
|---|---|---|
| `OCUPACION_PROMEDIO` | DOCENTE, ESTUDIANTES, PUERTA | Personas promedio en la zona durante el minuto |
| `INDICE_MOVIMIENTO` | ESTUDIANTES | Fracción (0-1) de estudiantes que se desplazaron más del umbral |
| `TRANSICIONES` | ESTUDIANTES, PUERTA | Entradas + salidas de estudiantes en el minuto |

Si la cámara falló más de la mitad del minuto, la medición se envía como
**inválida** (`CAMARA_SIN_IMAGEN`) en lugar de inventar un número.

## Cómo se separa al docente de los estudiantes

- A cada persona la ubica el **punto de sus pies** (centro de la parte de abajo
  de la caja), no la cabeza: así un alumno de la primera fila no "cae" en la
  zona del docente.
- **Docente** = el rastro que pasa **20 s** o más en la zona DOCENTE y es la
  mayor parte de su tiempo. Desde ahí, aunque camine entre los pupitres, **no**
  se cuenta como estudiante ni su movimiento.
- Un estudiante que **pasa** al escritorio (a entregar algo) no llega a 20 s:
  sigue siendo estudiante. Y para "cambiar de zona" hay que quedarse **2 s**, así
  quien solo pasa por un borde no genera entradas y salidas falsas.
- **Limitación:** si alguien queda tapado y el rastreador lo pierde, vuelve con
  otro número y su historial empieza de cero. Si es el docente, durante unos
  segundos puede contarse como estudiante. Mejora futura: combinarlo con el
  registro del docente en la estación NFC.

Todo se ajusta en `config.py` (`SEGUNDOS_PARA_DOCENTE`, `MIN_SEGUNDOS_EN_ZONA`,
`UMBRAL_MOVIMIENTO`, `VENTANA_SEGUNDOS`).

## Privacidad

- `08_detectar.py` **no guarda imágenes**: solo números agregados por minuto.
- Las fotos del dataset (`dataset/`) son de estudiantes: quedan **solo en la PC**,
  **no se suben a GitHub** (están en `.gitignore`), se usan solo para entrenar y
  se borran al terminar el proyecto. Capturar con autorización del colegio (la
  sesión visual de la API guarda esa referencia en `referenciaAutorizacion`).
