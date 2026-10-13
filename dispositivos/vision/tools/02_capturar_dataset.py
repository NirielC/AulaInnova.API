"""
AULAINNOVA VISIÓN - 2. CAPTURAR EL DATASET

Deja la cámara grabando sola durante una clase real y guarda una foto cada
INTERVALO_CAPTURA_S segundos, pero SOLO las que sirven:
  - descarta fotos borrosas (alguien movió la cámara, desenfoque),
  - descarta fotos oscuras,
  - descarta fotos casi iguales a la anterior (no aportan nada al modelo),
  - con --solo-con-personas, descarta las del salón vacío.

  python tools/02_capturar_dataset.py --minutos 90
  python tools/02_capturar_dataset.py --minutos 45 --solo-con-personas

Las fotos van a dataset/imagenes_crudas/ con la fecha y hora en el nombre, así
varias sesiones de captura (distintas clases, mañana/tarde) se suman sin pisarse.

PRIVACIDAD: son fotos de estudiantes. Se guardan SOLO en esta computadora,
solo se usan para entrenar y se borran al terminar el proyecto. Capturar con
la autorización del colegio.

Teclas:  ESPACIO pausar/reanudar · Q / ESC terminar
"""

import argparse
import sys
import time
from datetime import datetime
from pathlib import Path

import cv2
import numpy as np

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import abrir_camara, texto  # noqa: E402


def nitidez(gris):
    return cv2.Laplacian(gris, cv2.CV_64F).var()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--minutos", type=float, default=60, help="duración de la captura")
    parser.add_argument("--intervalo", type=float, default=config.INTERVALO_CAPTURA_S)
    parser.add_argument("--solo-con-personas", action="store_true",
                        help="guardar solo fotos donde el modelo base vea al menos una persona")
    parser.add_argument("--sin-ventana", action="store_true", help="no mostrar la imagen (más liviano)")
    args = parser.parse_args()

    config.CRUDAS_DIR.mkdir(parents=True, exist_ok=True)

    modelo = None
    if args.solo_con_personas:
        from ultralytics import YOLO
        modelo = YOLO(config.MODELO_BASE)

    cam = abrir_camara(config.CAMARA, config.ANCHO, config.ALTO)
    if not cam.isOpened():
        print(f"No se pudo abrir la cámara '{config.CAMARA}'.")
        return

    fin = time.monotonic() + args.minutos * 60
    proxima = time.monotonic()
    ultima_guardada = None
    pausado = False
    guardadas = 0
    descartes = {"borrosa": 0, "oscura": 0, "repetida": 0, "sin personas": 0}
    ultimo_motivo = ""

    print(f"Capturando {args.minutos:.0f} min, una foto cada {args.intervalo:.0f} s -> {config.CRUDAS_DIR}")
    while time.monotonic() < fin:
        ok, cuadro = cam.read()
        if not ok:
            print("La cámara dejó de entregar imagen; reintentando...")
            time.sleep(1)
            continue

        ahora = time.monotonic()
        if not pausado and ahora >= proxima:
            proxima = ahora + args.intervalo
            gris = cv2.cvtColor(cuadro, cv2.COLOR_BGR2GRAY)
            pequena = cv2.resize(gris, (160, 90))

            motivo = None
            if gris.mean() < config.MIN_LUZ:
                motivo = "oscura"
            elif nitidez(gris) < config.MIN_NITIDEZ:
                motivo = "borrosa"
            elif ultima_guardada is not None and \
                    np.abs(pequena.astype(np.int16) - ultima_guardada).mean() < config.MIN_CAMBIO:
                motivo = "repetida"
            elif modelo is not None:
                res = modelo.predict(cuadro, classes=[0], conf=config.CONFIANZA, verbose=False)[0]
                if len(res.boxes) == 0:
                    motivo = "sin personas"

            if motivo:
                descartes[motivo] += 1
                ultimo_motivo = f"descartada: {motivo}"
            else:
                nombre = datetime.now().strftime("%Y%m%d_%H%M%S") + ".jpg"
                cv2.imwrite(str(config.CRUDAS_DIR / nombre), cuadro, [cv2.IMWRITE_JPEG_QUALITY, 92])
                ultima_guardada = pequena.astype(np.int16)
                guardadas += 1
                ultimo_motivo = f"guardada: {nombre}"

        if not args.sin_ventana:
            vista = cuadro.copy()
            restante = max(0, int(fin - ahora))
            texto(vista, f"Guardadas: {guardadas}   Faltan {restante // 60}:{restante % 60:02d}", (10, 25))
            texto(vista, ultimo_motivo, (10, 50), (170, 170, 170), 0.5, 1)
            if pausado:
                texto(vista, "PAUSADO (ESPACIO para seguir)", (10, 80), (60, 200, 240))
            cv2.imshow("AulaInnova - Captura de dataset", vista)
            tecla = cv2.waitKey(1) & 0xFF
            if tecla in (ord("q"), 27):
                break
            if tecla == ord(" "):
                pausado = not pausado
        else:
            time.sleep(0.05)

    cam.release()
    cv2.destroyAllWindows()
    total = len(list(config.CRUDAS_DIR.glob("*.jpg")))
    print(f"\nListo. Guardadas en esta sesión: {guardadas}. Total en el dataset: {total}.")
    print("Descartadas:", ", ".join(f"{k} {v}" for k, v in descartes.items()))
    if total < 300:
        print("Sugerencia: junta al menos 300-500 fotos (varias clases y horarios) antes de entrenar.")


if __name__ == "__main__":
    main()
