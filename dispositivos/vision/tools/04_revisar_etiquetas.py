"""
AULAINNOVA VISIÓN - 4. REVISAR Y CORREGIR ETIQUETAS

El etiquetado automático acierta casi siempre, pero en un aula hay casos
difíciles: alguien tapado por otro, un estudiante agachado, una mochila que
parece persona. Aquí se ven las cajas foto por foto y se corrigen.

  python tools/04_revisar_etiquetas.py
  python tools/04_revisar_etiquetas.py --desde 120     empezar en la foto 120

Mouse:
  arrastrar con clic IZQUIERDO   -> dibujar una caja nueva (persona que faltó)
  clic DERECHO dentro de una caja -> borrarla (no es persona / está repetida)

Teclas:
  D  siguiente (guarda)      A  anterior (guarda)
  U  deshacer en esta foto   X  descartar la foto completa (mala, no sirve)
  Q / ESC  guardar y salir

No hace falta revisar TODAS: con revisar bien 100-200 ya mejora mucho.
El avance se recuerda en dataset/yolo/revisadas.txt.
"""

import argparse
import shutil
import sys
from pathlib import Path

import cv2

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import texto  # noqa: E402

ANCHO_VENTANA = 1280
VENTANA = "AulaInnova - Revisar etiquetas"


def leer_cajas(ruta):
    cajas = []
    if ruta.exists():
        for linea in ruta.read_text(encoding="utf-8").splitlines():
            partes = linea.split()
            if len(partes) == 5:
                cajas.append(tuple(float(v) for v in partes[1:]))
    return cajas


def guardar_cajas(ruta, cajas):
    ruta.write_text("".join(f"0 {xc:.6f} {yc:.6f} {w:.6f} {h:.6f}\n" for xc, yc, w, h in cajas),
                    encoding="utf-8")


class Editor:
    def __init__(self):
        self.cajas = []
        self.historial = []
        self.inicio = None
        self.actual = None
        self.escala = 1.0
        self.ancho = self.alto = 1

    def guardar_historial(self):
        self.historial.append(list(self.cajas))

    def mouse(self, evento, x, y, flags, _):
        xr, yr = x / self.escala, y / self.escala
        if evento == cv2.EVENT_LBUTTONDOWN:
            self.inicio = (xr, yr)
            self.actual = (xr, yr)
        elif evento == cv2.EVENT_MOUSEMOVE and self.inicio is not None:
            self.actual = (xr, yr)
        elif evento == cv2.EVENT_LBUTTONUP and self.inicio is not None:
            x1, y1 = self.inicio
            x2, y2 = xr, yr
            self.inicio = None
            if abs(x2 - x1) > 8 and abs(y2 - y1) > 8:
                self.guardar_historial()
                xc = (x1 + x2) / 2 / self.ancho
                yc = (y1 + y2) / 2 / self.alto
                w = abs(x2 - x1) / self.ancho
                h = abs(y2 - y1) / self.alto
                self.cajas.append((xc, yc, w, h))
        elif evento == cv2.EVENT_RBUTTONDOWN:
            # borrar la caja más pequeña que contenga el clic
            dentro = []
            for i, (xc, yc, w, h) in enumerate(self.cajas):
                if abs(xr / self.ancho - xc) <= w / 2 and abs(yr / self.alto - yc) <= h / 2:
                    dentro.append((w * h, i))
            if dentro:
                self.guardar_historial()
                del self.cajas[min(dentro)[1]]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--desde", type=int, default=0)
    args = parser.parse_args()

    pares = []
    for parte in ("train", "val"):
        for img in sorted((config.YOLO_DIR / "images" / parte).glob("*.jpg")):
            pares.append((img, config.YOLO_DIR / "labels" / parte / (img.stem + ".txt")))
    if not pares:
        print("No hay dataset etiquetado. Corre primero 03_autoetiquetar.py")
        return

    archivo_revisadas = config.YOLO_DIR / "revisadas.txt"
    revisadas = set(archivo_revisadas.read_text().split()) if archivo_revisadas.exists() else set()
    descartadas_dir = config.DATASET_DIR / "descartadas"

    editor = Editor()
    cv2.namedWindow(VENTANA)
    cv2.setMouseCallback(VENTANA, editor.mouse)

    i = min(args.desde, len(pares) - 1)
    cargado = None
    while 0 <= i < len(pares):
        img_ruta, lbl_ruta = pares[i]
        if cargado != i:
            imagen = cv2.imread(str(img_ruta))
            editor.alto, editor.ancho = imagen.shape[:2]
            editor.escala = min(1.0, ANCHO_VENTANA / editor.ancho)
            editor.cajas = leer_cajas(lbl_ruta)
            editor.historial = []
            cargado = i

        vista = imagen.copy()
        for xc, yc, w, h in editor.cajas:
            x1, y1 = int((xc - w / 2) * editor.ancho), int((yc - h / 2) * editor.alto)
            x2, y2 = int((xc + w / 2) * editor.ancho), int((yc + h / 2) * editor.alto)
            cv2.rectangle(vista, (x1, y1), (x2, y2), (80, 200, 120), 2)
        if editor.inicio is not None and editor.actual is not None:
            p1 = tuple(int(v) for v in editor.inicio)
            p2 = tuple(int(v) for v in editor.actual)
            cv2.rectangle(vista, p1, p2, (60, 200, 240), 2)
        if editor.escala < 1.0:
            vista = cv2.resize(vista, None, fx=editor.escala, fy=editor.escala)

        marca = "revisada" if img_ruta.name in revisadas else ""
        texto(vista, f"{i + 1}/{len(pares)}  {img_ruta.name}  personas: {len(editor.cajas)}  {marca}", (10, 25))
        texto(vista, f"Revisadas: {len(revisadas)}   D sig · A ant · U deshacer · X descartar · Q salir",
              (10, vista.shape[0] - 15), (170, 170, 170), 0.5, 1)
        cv2.imshow(VENTANA, vista)

        tecla = cv2.waitKey(20) & 0xFF
        if tecla in (ord("d"), ord("a"), ord("q"), 27):
            guardar_cajas(lbl_ruta, editor.cajas)
            revisadas.add(img_ruta.name)
            archivo_revisadas.write_text("\n".join(sorted(revisadas)))
            if tecla == ord("d"):
                i += 1
            elif tecla == ord("a"):
                i = max(0, i - 1)
            else:
                break
        elif tecla == ord("u") and editor.historial:
            editor.cajas = editor.historial.pop()
        elif tecla == ord("x"):
            descartadas_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(img_ruta), descartadas_dir / img_ruta.name)
            lbl_ruta.unlink(missing_ok=True)
            del pares[i]
            cargado = None
            if i >= len(pares):
                break

    cv2.destroyAllWindows()
    print(f"Revisadas: {len(revisadas)} de {len(pares)}. Siguiente: tools/05_entrenar.py")


if __name__ == "__main__":
    main()
