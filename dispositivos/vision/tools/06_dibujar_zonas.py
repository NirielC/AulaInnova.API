"""
AULAINNOVA VISIÓN - 6. DIBUJAR LAS ZONAS DEL AULA

Sobre la foto de referencia (01_probar_camara.py -> S) se marcan las zonas:
  DOCENTE      el área del escritorio y la pizarra
  ESTUDIANTES  donde están los pupitres
  PUERTA       la entrada (opcional; sirve para contar entradas y salidas)

  python tools/06_dibujar_zonas.py

Cómo:
  1. Elige la zona con 1 / 2 / 3.
  2. Clic izquierdo en cada esquina del área (4 o más puntos).
  3. ENTER para cerrarla.  (Si esa zona ya existía, se reemplaza.)
  Z deshacer el último punto · C borrar la zona elegida · S guardar · Q salir

IMPORTANTE: se marca el PISO (donde están los PIES), no las cabezas. La persona
se ubica por el centro de la parte de abajo de su caja.

Las coordenadas se guardan en zonas.json de 0 a 1, así sirven para cualquier
resolución. Si alguna vez se mueve la cámara, hay que volver a dibujarlas.
"""

import sys
from pathlib import Path

import cv2
import numpy as np

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import COLORES_ZONA, Zonas, guardar_json, leer_json, texto  # noqa: E402

VENTANA = "AulaInnova - Zonas"


def main():
    if not config.REFERENCIA_JPG.exists():
        print("No existe referencia.jpg. Abre 01_probar_camara.py y presiona S con la cámara ya fija.")
        return
    imagen = cv2.imread(str(config.REFERENCIA_JPG))
    alto, ancho = imagen.shape[:2]

    zonas = leer_json(config.ZONAS_JSON, [])
    elegida = config.ZONAS_DISPONIBLES[0]
    puntos = []

    def mouse(evento, x, y, flags, _):
        if evento == cv2.EVENT_LBUTTONDOWN:
            puntos.append((x / ancho, y / alto))

    cv2.namedWindow(VENTANA)
    cv2.setMouseCallback(VENTANA, mouse)

    while True:
        vista = imagen.copy()
        Zonas(zonas, ancho, alto).dibujar(vista)
        color = COLORES_ZONA.get(elegida, (255, 255, 255))
        pts = np.array([[x * ancho, y * alto] for x, y in puntos], dtype=np.int32)
        if len(pts):
            cv2.polylines(vista, [pts], False, color, 2)
            for p in pts:
                cv2.circle(vista, tuple(int(v) for v in p), 4, color, -1)

        opciones = "  ".join(f"{i + 1}={z}" for i, z in enumerate(config.ZONAS_DISPONIBLES))
        texto(vista, f"Zona: {elegida}   ({opciones})", (10, 25), color)
        texto(vista, "Clic = punto · ENTER cerrar · Z deshacer · C borrar · S guardar · Q salir",
              (10, alto - 15), (170, 170, 170), 0.5, 1)
        cv2.imshow(VENTANA, vista)

        tecla = cv2.waitKey(20) & 0xFF
        if ord("1") <= tecla <= ord(str(len(config.ZONAS_DISPONIBLES))):
            elegida = config.ZONAS_DISPONIBLES[tecla - ord("1")]
            puntos.clear()
        elif tecla == 13 and len(puntos) >= 3:  # ENTER
            zonas = [z for z in zonas if z["codigo"] != elegida]
            zonas.append({"codigo": elegida, "puntos": [[round(x, 4), round(y, 4)] for x, y in puntos]})
            # Orden de prioridad si se tocan: DOCENTE y PUERTA antes que ESTUDIANTES
            zonas.sort(key=lambda z: config.ZONAS_DISPONIBLES.index(z["codigo"])
                       if z["codigo"] != config.ZONA_ESTUDIANTES else 99)
            puntos.clear()
        elif tecla == ord("z") and puntos:
            puntos.pop()
        elif tecla == ord("c"):
            zonas = [z for z in zonas if z["codigo"] != elegida]
            puntos.clear()
        elif tecla == ord("s"):
            guardar_json(config.ZONAS_JSON, zonas)
            print(f"Guardado {config.ZONAS_JSON}: {[z['codigo'] for z in zonas]}")
        elif tecla in (ord("q"), 27):
            break

    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
