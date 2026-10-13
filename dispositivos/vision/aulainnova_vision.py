"""
Lógica compartida del módulo de visión de AulaInnova.

  - Zonas: polígonos dibujados sobre la imagen de la cámara (coordenadas 0-1).
  - SeguimientoRoles: a partir de los rastros de YOLO (id por persona), decide en
    qué zona está cada persona (con tolerancia para no contar a quien solo pasa)
    y marca como DOCENTE al rastro que se queda en la zona del docente.
  - Agregador: convierte lo anterior en mediciones por ventana (1 por minuto)
    con el formato exacto de POST /api/Visual/measurements.
  - ApiCliente / ColaMediciones: envío a la API y cola local si no hay red.
  - ControlSesion: pregunta a la API qué clase hay AHORA en el aula de la
    cámara (según el horario). La API abre y cierra las sesiones sola.

Nunca se guardan ni se envían imágenes: solo números agregados.
"""

import json
import math
import re
import time
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone

import cv2
import numpy as np


# ============================================================
# Utilidades
# ============================================================

def utc_ahora():
    return datetime.now(timezone.utc)


def utc_iso(momento):
    """Fecha en UTC con 'Z' (la API rechaza horas que no sean UTC)."""
    return momento.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


def parse_utc(texto):
    """Fecha de la API ("2026-10-13T16:05:00.0342456Z" o sin Z) -> datetime UTC."""
    if not texto:
        return None
    m = re.match(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d+))?", texto)
    if not m:
        return None
    fecha = datetime.strptime(m.group(1), "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc)
    if m.group(2):
        fecha += timedelta(microseconds=int(m.group(2)[:6].ljust(6, "0")))
    return fecha


def abrir_camara(fuente, ancho, alto):
    fuente = int(fuente) if str(fuente).isdigit() else fuente
    cam = cv2.VideoCapture(fuente)
    cam.set(cv2.CAP_PROP_FRAME_WIDTH, ancho)
    cam.set(cv2.CAP_PROP_FRAME_HEIGHT, alto)
    return cam


def texto(img, t, pos, color=(255, 255, 255), escala=0.6, grosor=2):
    cv2.putText(img, t, pos, cv2.FONT_HERSHEY_SIMPLEX, escala, (0, 0, 0), grosor + 3, cv2.LINE_AA)
    cv2.putText(img, t, pos, cv2.FONT_HERSHEY_SIMPLEX, escala, color, grosor, cv2.LINE_AA)


def leer_json(ruta, defecto):
    try:
        return json.loads(ruta.read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError):
        return defecto


def guardar_json(ruta, datos):
    ruta.write_text(json.dumps(datos, indent=2, ensure_ascii=False), encoding="utf-8")


# ============================================================
# Zonas
# ============================================================

COLORES_ZONA = {
    "DOCENTE": (60, 160, 255),
    "ESTUDIANTES": (80, 200, 120),
    "PUERTA": (200, 120, 240),
}


class Zonas:
    """Polígonos en coordenadas normalizadas (0-1), convertidos a píxeles."""

    def __init__(self, zonas, ancho, alto):
        self.zonas = zonas
        self.ancho = ancho
        self.alto = alto
        self.poligonos = [
            np.array([[x * ancho, y * alto] for x, y in z["puntos"]], dtype=np.float32)
            for z in zonas
        ]

    def zona_de(self, x, y):
        """Código de la zona que contiene el punto (la primera de la lista gana)."""
        for zona, poligono in zip(self.zonas, self.poligonos):
            if cv2.pointPolygonTest(poligono, (float(x), float(y)), False) >= 0:
                return zona["codigo"]
        return None

    def dibujar(self, img):
        capa = img.copy()
        for zona, poligono in zip(self.zonas, self.poligonos):
            color = COLORES_ZONA.get(zona["codigo"], (200, 200, 200))
            pts = poligono.astype(np.int32)
            cv2.fillPoly(capa, [pts], color)
            cv2.polylines(img, [pts], True, color, 2)
            x, y = pts[0]
            texto(img, zona["codigo"], (int(x) + 5, int(y) + 20), color)
        cv2.addWeighted(capa, 0.18, img, 0.82, 0, img)


# ============================================================
# Seguimiento de personas y roles
# ============================================================

@dataclass
class Rastro:
    id: int
    pie: tuple
    primera_vez: float
    ultima_vez: float
    zona_actual: str = None          # zona instantánea
    zona_confirmada: str = None      # zona tras MIN_SEGUNDOS_EN_ZONA
    zona_candidata: str = None
    desde_candidata: float = 0.0
    tiempo_por_zona: dict = field(default_factory=dict)
    es_docente: bool = False
    pie_inicio_ventana: tuple = None
    max_desplazamiento: float = 0.0  # en fracción de la diagonal, dentro de la ventana
    tiempo_en_ventana: float = 0.0


@dataclass
class Transicion:
    rastro: int
    desde: str
    hacia: str
    momento: float


class SeguimientoRoles:
    """
    Recibe en cada cuadro los rastros de YOLO (id + punto del pie) y mantiene:
      - la zona de cada persona, con histéresis (no cuenta a quien solo pasa),
      - quién es docente (por tiempo acumulado en la zona DOCENTE),
      - las transiciones entre zonas (entradas/salidas).

    Limitación conocida: si el rastreador pierde a alguien (lo tapan) y lo vuelve
    a encontrar, le asigna otro id y su historial empieza de cero.
    """

    def __init__(self, zonas, ancho, alto, config):
        self.zonas = zonas
        self.diagonal = math.hypot(ancho, alto)
        self.cfg = config
        self.rastros = {}
        self.transiciones = []

    def actualizar(self, detecciones, ahora):
        """detecciones: lista de (id_rastro, x_pie, y_pie)."""
        vistos = set()
        for id_rastro, x, y in detecciones:
            vistos.add(id_rastro)
            zona = self.zonas.zona_de(x, y)
            r = self.rastros.get(id_rastro)
            if r is None:
                r = Rastro(id=id_rastro, pie=(x, y), primera_vez=ahora, ultima_vez=ahora,
                           pie_inicio_ventana=(x, y))
                self.rastros[id_rastro] = r

            dt = min(ahora - r.ultima_vez, 1.0)  # si hubo un hueco largo, no inflar tiempos
            clave = zona or "FUERA"
            r.tiempo_por_zona[clave] = r.tiempo_por_zona.get(clave, 0.0) + dt
            r.tiempo_en_ventana += dt
            r.ultima_vez = ahora
            r.pie = (x, y)
            r.zona_actual = zona

            # Desplazamiento máximo desde el inicio de la ventana
            if r.pie_inicio_ventana is None:
                r.pie_inicio_ventana = (x, y)
            dx = x - r.pie_inicio_ventana[0]
            dy = y - r.pie_inicio_ventana[1]
            r.max_desplazamiento = max(r.max_desplazamiento, math.hypot(dx, dy) / self.diagonal)

            self._confirmar_zona(r, zona, ahora)
            self._revisar_docente(r)

        # Olvidar rastros que no se ven hace rato
        for id_rastro in [i for i, r in self.rastros.items() if ahora - r.ultima_vez > 5.0]:
            del self.rastros[id_rastro]
        return vistos

    def _confirmar_zona(self, r, zona, ahora):
        if zona == r.zona_confirmada:
            r.zona_candidata = None
            return
        if zona != r.zona_candidata:
            r.zona_candidata = zona
            r.desde_candidata = ahora
            return
        if ahora - r.desde_candidata >= self.cfg.MIN_SEGUNDOS_EN_ZONA:
            anterior = r.zona_confirmada
            r.zona_confirmada = zona
            r.zona_candidata = None
            # Solo es transición si ya tenía una zona confirmada antes
            # (un rastro que aparece de la nada no "entró").
            if anterior is not None:
                if not r.es_docente:
                    self.transiciones.append(Transicion(r.id, anterior, zona, ahora))

    def _revisar_docente(self, r):
        if r.es_docente:
            return
        en_docente = r.tiempo_por_zona.get(self.cfg.ZONA_DOCENTE, 0.0)
        total = sum(r.tiempo_por_zona.values())
        if en_docente >= self.cfg.SEGUNDOS_PARA_DOCENTE and total > 0 and \
                en_docente / total >= self.cfg.FRACCION_PARA_DOCENTE:
            r.es_docente = True

    def iniciar_ventana(self):
        for r in self.rastros.values():
            r.pie_inicio_ventana = r.pie
            r.max_desplazamiento = 0.0
            r.tiempo_en_ventana = 0.0
        self.transiciones = []


# ============================================================
# Agregación por ventana -> mediciones para la API
# ============================================================

class Agregador:
    """Acumula muestras durante VENTANA_SEGUNDOS y produce las mediciones."""

    def __init__(self, seguimiento, config):
        self.seg = seguimiento
        self.cfg = config
        self.reiniciar(utc_ahora())

    def reiniciar(self, inicio):
        self.inicio = inicio
        self.muestras_totales = 0
        self.muestras_validas = 0
        self.suma_ocupacion = {z: 0 for z in self.cfg.ZONAS_DISPONIBLES}
        self.seg.iniciar_ventana()

    def muestra(self, camara_ok):
        """Llamar una vez por cuadro procesado (o intento fallido de leer la cámara)."""
        self.muestras_totales += 1
        if not camara_ok:
            return
        self.muestras_validas += 1
        for r in self.seg.rastros.values():
            if r.zona_actual is None:
                continue
            # En la zona del docente se cuenta a todos; en las demás, sin docentes.
            if r.es_docente and r.zona_actual != self.cfg.ZONA_DOCENTE:
                continue
            if r.zona_actual in self.suma_ocupacion:
                self.suma_ocupacion[r.zona_actual] += 1

    def ventana_terminada(self, ahora):
        return (ahora - self.inicio).total_seconds() >= self.cfg.VENTANA_SEGUNDOS

    def cerrar_ventana(self, fin):
        """Devuelve [(codigo_zona, codigo_indicador, valor|None, motivo|None)] y reinicia."""
        resultados = []
        suficiente = (self.muestras_totales > 0 and
                      self.muestras_validas / self.muestras_totales >= self.cfg.MIN_FRACCION_VALIDA)

        for codigo_ind, ind in self.cfg.INDICADORES.items():
            for zona in ind["zonas"]:
                if not suficiente:
                    resultados.append((zona, codigo_ind, None, "CAMARA_SIN_IMAGEN"))
                    continue
                if codigo_ind == "OCUPACION_PROMEDIO":
                    valor = self.suma_ocupacion.get(zona, 0) / self.muestras_validas
                    resultados.append((zona, codigo_ind, round(valor, 4), None))
                elif codigo_ind == "INDICE_MOVIMIENTO":
                    resultados.append(self._movimiento(zona, codigo_ind))
                elif codigo_ind == "TRANSICIONES":
                    n = sum(1 for t in self.seg.transiciones if zona in (t.desde, t.hacia))
                    resultados.append((zona, codigo_ind, float(n), None))

        info = {
            "inicio": self.inicio,
            "fin": fin,
            "muestras_validas": self.muestras_validas,
            "muestras_totales": self.muestras_totales,
        }
        self.reiniciar(fin)
        return resultados, info

    def _movimiento(self, zona, codigo_ind):
        # Estudiantes (no docentes) que estuvieron en la zona al menos 10 s de la ventana
        candidatos = [
            r for r in self.seg.rastros.values()
            if not r.es_docente and r.zona_confirmada == zona and r.tiempo_en_ventana >= 10.0
        ]
        if not candidatos:
            return (zona, codigo_ind, None, "SIN_ESTUDIANTES_RASTREADOS")
        movidos = sum(1 for r in candidatos if r.max_desplazamiento >= self.cfg.UMBRAL_MOVIMIENTO)
        return (zona, codigo_ind, round(movidos / len(candidatos), 4), None)


# ============================================================
# API
# ============================================================

class ApiCliente:
    def __init__(self, url, estacion_id=None, estacion_clave=None, timeout=8):
        import requests  # solo hace falta en las herramientas que hablan con la API
        self.requests = requests
        self.url = url.rstrip("/")
        self.estacion_id = estacion_id
        self.estacion_clave = estacion_clave
        self.timeout = timeout
        self.token = None

    # ---------- Como usuario (JWT): configurar ----------
    def login(self, usuario, password):
        r = self.requests.post(f"{self.url}/api/Auth/login",
                               json={"usuario": usuario, "password": password}, timeout=self.timeout)
        r.raise_for_status()
        self.token = r.json()["token"]
        return self.token

    def _post_jwt(self, ruta, cuerpo=None):
        r = self.requests.post(f"{self.url}{ruta}", json=cuerpo,
                               headers={"Authorization": f"Bearer {self.token}"}, timeout=self.timeout)
        return r.status_code, (r.json() if r.content and "json" in r.headers.get("content-type", "") else r.text)

    def crear_zona(self, aula_id, codigo, revision, geometria):
        return self._post_jwt("/api/Visual/zones", {
            "aulaId": aula_id, "codigo": codigo, "revision": revision,
            "geometriaJson": json.dumps(geometria),
        })

    def crear_indicador(self, codigo, revision, unidad, metodo, configuracion, referencia):
        return self._post_jwt("/api/Visual/indicators", {
            "codigo": codigo, "revision": revision, "unidad": unidad, "metodo": metodo,
            "configuracionJson": json.dumps(configuracion), "referenciaValidacion": referencia,
        })

    def abrir_sesion(self, asignacion_id, aula_id, referencia, version, inicio):
        return self._post_jwt("/api/Visual/sessions", {
            "asignacionId": asignacion_id, "aulaId": aula_id,
            "referenciaAutorizacion": referencia, "versionProceso": version,
            "inicioUtc": utc_iso(inicio),
        })

    def cerrar_sesion(self, sesion_id):
        return self._post_jwt(f"/api/Visual/sessions/{sesion_id}/close")

    # ---------- Como estación (cámara) ----------
    def _cabeceras_estacion(self):
        return {"X-Estacion-Id": str(self.estacion_id), "X-Estacion-Clave": self.estacion_clave}

    def sesion_actual(self, version):
        """POST /api/estacion/vision/sesion -> (código HTTP | None, datos)."""
        try:
            r = self.requests.post(f"{self.url}/api/estacion/vision/sesion", json={"versionProceso": version},
                                   headers=self._cabeceras_estacion(), timeout=self.timeout)
        except self.requests.RequestException as e:
            return None, str(e)
        try:
            return r.status_code, r.json()
        except ValueError:
            return r.status_code, r.text

    def enviar_medicion(self, cuerpo):
        """Devuelve el código HTTP, o None si no hubo conexión."""
        try:
            r = self.requests.post(f"{self.url}/api/Visual/measurements", json=cuerpo, timeout=self.timeout,
                                   headers=self._cabeceras_estacion())
            return r.status_code
        except self.requests.RequestException:
            return None


class ColaMediciones:
    """Mediciones pendientes en un .jsonl: se reenvían cuando vuelve la red."""

    def __init__(self, ruta):
        self.ruta = ruta

    def agregar(self, cuerpo):
        with self.ruta.open("a", encoding="utf-8") as f:
            f.write(json.dumps(cuerpo) + "\n")

    def pendientes(self):
        if not self.ruta.exists():
            return 0
        return sum(1 for linea in self.ruta.read_text(encoding="utf-8").splitlines() if linea.strip())

    def reenviar(self, cliente):
        if not self.ruta.exists():
            return 0, 0
        lineas = [l for l in self.ruta.read_text(encoding="utf-8").splitlines() if l.strip()]
        quedan, enviadas, descartadas = [], 0, 0
        cortar = False
        for linea in lineas:
            if cortar:
                quedan.append(linea)
                continue
            codigo = cliente.enviar_medicion(json.loads(linea))
            if codigo is not None and 200 <= codigo < 300:
                enviadas += 1
            elif codigo == 400:
                descartadas += 1  # la API no la acepta (fuera de la clase, duplicada...): no insistir
            else:
                quedan.append(linea)
                cortar = True     # sin red / 401 / 403 / 5xx: probar más tarde
        if quedan:
            self.ruta.write_text("\n".join(quedan) + "\n", encoding="utf-8")
        else:
            self.ruta.unlink(missing_ok=True)
        return enviadas, descartadas


def medicion_api(sesion_id, zona_id, indicador_id, info, valor, motivo):
    """Cuerpo exacto de RecordVisualMeasurementRequest."""
    valida = motivo is None
    return {
        "sesionId": sesion_id,
        "zonaId": zona_id,
        "indicadorId": indicador_id,
        "inicioUtc": utc_iso(info["inicio"]),
        "finUtc": utc_iso(info["fin"]),
        "valor": valor if valida else None,
        "valida": valida,
        "motivoInvalidez": None if valida else motivo,
        "muestrasValidas": info["muestras_validas"],
        "muestrasTotales": info["muestras_totales"],
    }


# ============================================================
# Sesión automática según el horario
# ============================================================

@dataclass
class SesionVisual:
    """La clase que hay ahora en el aula, según la API."""
    id: int
    inicio: datetime            # la API rechaza ventanas que empiecen antes
    fin_clase: datetime         # y ventanas que terminen después del fin de la clase
    clase: str
    zonas: dict                 # {"ESTUDIANTES": 2, ...}
    indicadores: dict           # {"OCUPACION_PROMEDIO": 1, ...}


class ControlSesion:
    """
    Pregunta a la API cada `cada_s` segundos qué clase hay ahora en el aula de
    la cámara (POST /api/estacion/vision/sesion). La API mira las asignaciones
    del día (que salen del horario), cierra la sesión de la clase que terminó y
    abre la de la que empezó. Aquí solo se recuerda la respuesta.

    También calcula el desfase entre el reloj de esta PC y el del servidor, para
    mandar las horas en "hora del servidor" (la API rechaza mediciones del futuro).
    """

    def __init__(self, cliente, version, cada_s=60):
        self.cliente = cliente
        self.version = version
        self.cada_s = cada_s
        self.sesion = None
        self.desfase = timedelta(0)
        self.mensaje = "Sin consultar todavía"
        self._proxima = 0.0

    def ahora(self):
        """Hora UTC del servidor (estimada)."""
        return utc_ahora() + self.desfase

    def actualizar(self, forzar=False):
        """
        Consulta a la API si toca. Devuelve la sesión ANTERIOR si la sesión
        cambió (para cerrar su última ventana), o None si sigue igual.
        """
        if not forzar and time.monotonic() < self._proxima:
            return None
        self._proxima = time.monotonic() + self.cada_s
        anterior = self.sesion

        enviado = utc_ahora()
        codigo, datos = self.cliente.sesion_actual(self.version)
        recibido = utc_ahora()

        if codigo is None or codigo >= 500 or codigo == 409:
            # Sin red: se sigue con la sesión conocida hasta que termine su clase
            # (las mediciones quedan en la cola y se envían al volver la red).
            self.mensaje = f"API sin respuesta ({codigo or 'sin conexión'})"
            if anterior and self.ahora() >= anterior.fin_clase:
                self.sesion = None
                return anterior
            return None

        if codigo != 200:
            # 401 clave mala, 400 estación sin aula/configuración...
            detalle = datos.get("mensaje", datos) if isinstance(datos, dict) else datos
            self.mensaje = f"API respondió {codigo}: {detalle}"
            self.sesion = None
            return anterior

        servidor = parse_utc(datos.get("servidorUtc"))
        if servidor:
            self.desfase = servidor - (enviado + (recibido - enviado) / 2)

        self.mensaje = datos.get("mensaje", "")
        if datos.get("sesionId"):
            self.sesion = SesionVisual(
                id=datos["sesionId"],
                inicio=parse_utc(datos.get("sesionInicioUtc")),
                fin_clase=parse_utc(datos.get("claseFinUtc")),
                clase=datos.get("clase") or "",
                zonas=datos.get("zonas") or {},
                indicadores=datos.get("indicadores") or {},
            )
        else:
            self.sesion = None

        mismo = (anterior is None and self.sesion is None) or \
                (anterior is not None and self.sesion is not None and anterior.id == self.sesion.id)
        return None if mismo else (anterior or SESION_VACIA)


# Marca "antes no había sesión" (distinta de None = "no cambió").
SESION_VACIA = SesionVisual(0, None, None, "", {}, {})


def recortar_a_sesion(info, sesion):
    """
    Ajusta la ventana a la clase: no antes de que se abrió la sesión ni después
    del fin de la clase. Devuelve una copia de info, o None si no queda ventana.
    """
    inicio = info["inicio"]
    fin = info["fin"]
    if sesion.inicio and inicio <= sesion.inicio:
        inicio = sesion.inicio + timedelta(milliseconds=1)
    if sesion.fin_clase and fin > sesion.fin_clase:
        fin = sesion.fin_clase
    if (fin - inicio).total_seconds() < 1:
        return None
    return dict(info, inicio=inicio, fin=fin)

