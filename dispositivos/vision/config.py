# config.py
# Configuración del módulo de visión de AulaInnova.
# Es el único archivo que hay que editar el día de la instalación.

import os
from pathlib import Path

# ============================================================
# RUTAS
# ============================================================

ROOT_DIR = Path(__file__).resolve().parent

DATASET_DIR = ROOT_DIR / "dataset"
CRUDAS_DIR = DATASET_DIR / "imagenes_crudas"   # lo que guarda la cámara
YOLO_DIR = DATASET_DIR / "yolo"                # dataset etiquetado (formato YOLO)
DATA_YAML = YOLO_DIR / "data.yaml"

MODELS_DIR = ROOT_DIR / "models"
MODELO_FINAL = MODELS_DIR / "aulainnova_personas.pt"

REFERENCIA_JPG = ROOT_DIR / "referencia.jpg"   # foto fija del aula para dibujar zonas
ZONAS_JSON = ROOT_DIR / "zonas.json"
ESTADO_JSON = ROOT_DIR / "estado.json"         # ids devueltos por la API (sesión, indicadores)
PENDIENTES_JSONL = ROOT_DIR / "pendientes.jsonl"  # mediciones sin enviar (sin red)

# ============================================================
# CÁMARA
# ============================================================
# Número de la webcam (0 = la primera) o URL de una cámara IP (rtsp://...).
CAMARA = os.environ.get("AULA_CAMARA", "0")
ANCHO = 1280
ALTO = 720

# ============================================================
# MODELOS
# ============================================================
# Modelo base preentrenado en COCO (clase 0 = "person"). Detecta personas desde
# el primer día; el fine-tuning solo lo adapta al ángulo de NUESTRA cámara.
MODELO_BASE = "yolo11s.pt"
# Modelo grande que solo se usa para etiquetar el dataset automáticamente
# (más lento pero más preciso). Nunca se usa en el aula.
MODELO_MAESTRO = "yolo11x.pt"

# Si existe el modelo entrenado se usa; si no, el base.
def modelo_deteccion():
    return str(MODELO_FINAL) if MODELO_FINAL.exists() else MODELO_BASE

CONFIANZA = 0.35        # mínimo para aceptar una detección
TAMANO_IMAGEN = 640     # imgsz de YOLO

# ============================================================
# CAPTURA DEL DATASET
# ============================================================
INTERVALO_CAPTURA_S = 5     # una foto cada 5 s
MIN_NITIDEZ = 60            # varianza del Laplaciano; menos = borrosa
MIN_LUZ = 50                # brillo medio (0-255)
MIN_CAMBIO = 6.0            # diferencia media con la última foto guardada (0-255)
PORCENTAJE_VALIDACION = 0.15

# ============================================================
# ZONAS Y ROLES
# ============================================================
# Códigos de zona (deben coincidir con los que se crean en la API).
ZONA_DOCENTE = "DOCENTE"
ZONA_ESTUDIANTES = "ESTUDIANTES"
ZONA_PUERTA = "PUERTA"
ZONAS_DISPONIBLES = [ZONA_DOCENTE, ZONA_ESTUDIANTES, ZONA_PUERTA]

# Una persona "cambia de zona" solo si se queda ahí este tiempo
# (evita contar a alguien que solo pasa por el borde).
MIN_SEGUNDOS_EN_ZONA = 2.0

# Un rastro se marca como DOCENTE si acumula este tiempo en la zona DOCENTE
# y ese tiempo es al menos esta fracción de todo lo que lleva visto.
# Así un estudiante que pasa a dejar algo al escritorio NO se vuelve docente.
SEGUNDOS_PARA_DOCENTE = 20.0
FRACCION_PARA_DOCENTE = 0.6

# Movimiento: desplazamiento del pie (centro inferior de la caja), como
# fracción de la diagonal de la imagen, a partir del cual un estudiante
# "se movió" dentro de la ventana.
UMBRAL_MOVIMIENTO = 0.05

# ============================================================
# ENVÍO A LA API
# ============================================================
API_URL = os.environ.get("AULA_API_URL", "http://127.0.0.1:5077")

# La cámara se registra como una estación más, de tipo AULA, en el aula donde
# está (POST /api/admin/estaciones + /configuration + /secret). Con su id y
# clave pide la clase actual y envía las mediciones (esquema "Estacion").
ESTACION_ID = int(os.environ.get("AULA_ESTACION_ID", "2"))
ESTACION_CLAVE = os.environ.get("AULA_ESTACION_CLAVE", "IDENTIFICADOR.SECRETO")

# Aula de la cámara: solo la usa 07_configurar_api.py para crear las zonas.
# (El detector no la necesita: la API sabe el aula por la estación.)
AULA_ID = int(os.environ.get("AULA_AULA_ID", "1"))

VENTANA_SEGUNDOS = 60        # se manda una medición por zona e indicador cada minuto
MIN_FRACCION_VALIDA = 0.5    # si la cámara dio menos de la mitad de cuadros, la medición es inválida
VERSION_PROCESO = "aulainnova-vision-1.0"

# Indicadores que se crean en la API y a qué zonas se aplican.
INDICADORES = {
    "OCUPACION_PROMEDIO": {
        "unidad": "personas",
        "metodo": "Promedio de personas detectadas (YOLO, sin docente) cuyo pie cae en la zona durante la ventana.",
        "zonas": [ZONA_DOCENTE, ZONA_ESTUDIANTES, ZONA_PUERTA],
    },
    "INDICE_MOVIMIENTO": {
        "unidad": "proporcion 0-1",
        "metodo": "Fraccion de estudiantes rastreados en la zona cuyo desplazamiento en la ventana supera el umbral.",
        "zonas": [ZONA_ESTUDIANTES],
    },
    "TRANSICIONES": {
        "unidad": "eventos",
        "metodo": "Numero de entradas y salidas confirmadas de estudiantes en la zona durante la ventana.",
        "zonas": [ZONA_ESTUDIANTES, ZONA_PUERTA],
    },
}

# ============================================================
# DATOS LOCALES (no se suben a git)
# ============================================================
# Crea config_local.py junto a este archivo con lo de TU instalación, p. ej.:
#   API_URL = "http://192.168.1.100:5077"
#   ESTACION_ID = 2
#   ESTACION_CLAVE = "identificador.secreto"
#   AULA_ID = 1
#   CAMARA = "1"
try:
    from config_local import *  # noqa: F401,F403
except ImportError:
    pass
