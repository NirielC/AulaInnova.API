"""
AULAINNOVA VISIÓN - 2. CAPTURAR EL DATASET

Deja la cámara grabando sola durante una clase real y guarda una foto cada
INTERVALO_CAPTURA_S segundos (1.5 s por defecto). Sin filtros: guarda todas.

  python tools/02_capturar_dataset.py --minutos 90
  python tools/02_capturar_dataset.py --minutos 45 --intervalo 3

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

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import abrir_camara, texto  # noqa: E402


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--minutos", type=float, default=60, help="duración de la captura")
    parser.add_argument("--intervalo", type=float, default=config.INTERVALO_CAPTURA_S,
                        help="segundos entre foto y foto")
    parser.add_argument("--sin-ventana", action="store_true", help="no mostrar la imagen (más liviano)")
    args = parser.parse_args()

    config.CRUDAS_DIR.mkdir(parents=True, exist_ok=True)

    cam = abrir_camara(config.CAMARA, config.ANCHO, config.ALTO)
    if not cam.isOpened():
        print(f"No se pudo abrir la cámara '{config.CAMARA}'.")
        return

    fin = time.monotonic() + args.minutos * 60
    proxima = time.monotonic()
    pausado = False
    guardadas = 0
    ultima = ""

    print(f"Capturando {args.minutos:.0f} min, una foto cada {args.intervalo:g} s -> {config.CRUDAS_DIR}")
    while time.monotonic() < fin:
        ok, cuadro = cam.read()
        if not ok:
            print("La cámara dejó de entregar imagen; reintentando...")
            time.sleep(1)
            continue

        ahora = time.monotonic()
        if not pausado and ahora >= proxima:
            proxima = ahora + args.intervalo
            # Con milisegundos en el nombre para que dos fotos nunca se pisen.
            nombre = datetime.now().strftime("%Y%m%d_%H%M%S_%f")[:-3] + ".jpg"
            cv2.imwrite(str(config.CRUDAS_DIR / nombre), cuadro, [cv2.IMWRITE_JPEG_QUALITY, 92])
            guardadas += 1
            ultima = f"guardada: {nombre}"

        if not args.sin_ventana:
            vista = cuadro.copy()
            restante = max(0, int(fin - ahora))
            texto(vista, f"Guardadas: {guardadas}   Faltan {restante // 60}:{restante % 60:02d}", (10, 25))
            texto(vista, ultima, (10, 50), (170, 170, 170), 0.5, 1)
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


if __name__ == "__main__":
    main()
