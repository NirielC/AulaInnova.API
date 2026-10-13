"""
AULAINNOVA VISIÓN - 1. PROBAR LA CÁMARA

Para el día de la instalación: confirma que la webcam se ve bien desde su lugar
final (arriba de la pizarra) y guarda la foto de referencia para dibujar las zonas.

  python tools/01_probar_camara.py            solo la imagen
  python tools/01_probar_camara.py --yolo     además marca a las personas (modelo base COCO)

Teclas:  S guardar referencia.jpg · Q / ESC salir

Revisa en pantalla:
  - Se ve TODO el salón: el escritorio del docente, las filas y la puerta.
  - Las personas del fondo se ven de cuerpo (al menos de la cintura para arriba).
  - "Luz" mayor a 50. Si no, encender luces o mover la cámara de la ventana.
  - FPS de 10 o más.
Cuando la cámara quede FIJA (no se debe mover nunca más), guarda la referencia con S.
"""

import argparse
import sys
import time
from pathlib import Path

import cv2

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import abrir_camara, texto  # noqa: E402


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--yolo", action="store_true", help="marcar personas con el modelo base")
    args = parser.parse_args()

    modelo = None
    if args.yolo:
        from ultralytics import YOLO
        modelo = YOLO(config.modelo_deteccion())

    cam = abrir_camara(config.CAMARA, config.ANCHO, config.ALTO)
    if not cam.isOpened():
        print(f"No se pudo abrir la cámara '{config.CAMARA}'. Prueba con AULA_CAMARA=1 (u otro número).")
        return

    t_anterior = time.monotonic()
    fps = 0.0
    while True:
        ok, cuadro = cam.read()
        if not ok:
            print("La cámara no entrega imagen. Revisa el cable USB / la extensión.")
            break

        vista = cuadro.copy()
        alto, ancho = cuadro.shape[:2]
        luz = cv2.cvtColor(cuadro, cv2.COLOR_BGR2GRAY).mean()

        personas = 0
        if modelo is not None:
            res = modelo.predict(cuadro, classes=[0], conf=config.CONFIANZA,
                                 imgsz=config.TAMANO_IMAGEN, verbose=False)[0]
            for x1, y1, x2, y2 in res.boxes.xyxy.cpu().numpy().astype(int):
                cv2.rectangle(vista, (x1, y1), (x2, y2), (80, 200, 120), 2)
                personas += 1

        ahora = time.monotonic()
        fps = 0.9 * fps + 0.1 * (1.0 / max(ahora - t_anterior, 1e-3))
        t_anterior = ahora

        color_luz = (80, 200, 120) if luz >= config.MIN_LUZ else (60, 60, 230)
        texto(vista, f"{ancho}x{alto}  FPS {fps:.1f}", (10, 25))
        texto(vista, f"Luz {luz:.0f}", (10, 50), color_luz)
        if modelo is not None:
            texto(vista, f"Personas: {personas}", (10, 75))
        texto(vista, "S = guardar referencia   Q = salir", (10, alto - 15), (170, 170, 170), 0.5, 1)

        cv2.imshow("AulaInnova - Camara", vista)
        tecla = cv2.waitKey(1) & 0xFF
        if tecla in (ord("q"), 27):
            break
        if tecla == ord("s"):
            cv2.imwrite(str(config.REFERENCIA_JPG), cuadro)
            print(f"Referencia guardada en {config.REFERENCIA_JPG}")

    cam.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
