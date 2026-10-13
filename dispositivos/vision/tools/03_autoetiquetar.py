"""
AULAINNOVA VISIÓN - 3. ETIQUETAR AUTOMÁTICAMENTE

Etiquetar a mano cientos de fotos (dibujar una caja por persona) es lo más
lento de entrenar un modelo. Aquí lo hace un modelo GRANDE (MODELO_MAESTRO,
preentrenado en COCO): es lento pero preciso. Sus cajas son el punto de
partida; después se corrigen las fallas con 04_revisar_etiquetas.py.

  python tools/03_autoetiquetar.py
  python tools/03_autoetiquetar.py --rehacer     vuelve a etiquetar TODO (pierde correcciones)

Resultado (formato YOLO, una sola clase "persona"):
  dataset/yolo/images/train|val/*.jpg
  dataset/yolo/labels/train|val/*.txt     una línea por persona: 0 xc yc w h (0-1)
  dataset/yolo/data.yaml

Las fotos ya etiquetadas se saltan (para no perder lo corregido a mano).
"""

import argparse
import hashlib
import shutil
import sys
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402


def es_validacion(nombre):
    """Reparto fijo train/val según el nombre (siempre cae igual)."""
    h = int(hashlib.md5(nombre.encode()).hexdigest(), 16) % 1000
    return h < config.PORCENTAJE_VALIDACION * 1000


def escribir_yaml():
    config.DATA_YAML.write_text(
        f"path: {config.YOLO_DIR.as_posix()}\n"
        "train: images/train\n"
        "val: images/val\n"
        "names:\n"
        "  0: persona\n",
        encoding="utf-8",
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rehacer", action="store_true")
    parser.add_argument("--confianza", type=float, default=0.30)
    args = parser.parse_args()

    imagenes = sorted(config.CRUDAS_DIR.glob("*.jpg"))
    if not imagenes:
        print(f"No hay fotos en {config.CRUDAS_DIR}. Primero corre 02_capturar_dataset.py")
        return

    for parte in ("train", "val"):
        (config.YOLO_DIR / "images" / parte).mkdir(parents=True, exist_ok=True)
        (config.YOLO_DIR / "labels" / parte).mkdir(parents=True, exist_ok=True)

    pendientes = []
    for img in imagenes:
        parte = "val" if es_validacion(img.name) else "train"
        etiqueta = config.YOLO_DIR / "labels" / parte / (img.stem + ".txt")
        if etiqueta.exists() and not args.rehacer:
            continue
        pendientes.append((img, parte, etiqueta))

    print(f"Fotos: {len(imagenes)}  ·  por etiquetar: {len(pendientes)}")
    if not pendientes:
        escribir_yaml()
        print("Nada nuevo que etiquetar.")
        return

    from ultralytics import YOLO
    print(f"Cargando {config.MODELO_MAESTRO} (la primera vez se descarga)...")
    modelo = YOLO(config.MODELO_MAESTRO)

    total_cajas = 0
    for i, (img, parte, etiqueta) in enumerate(pendientes, 1):
        res = modelo.predict(str(img), classes=[0], conf=args.confianza, imgsz=1280, verbose=False)[0]
        lineas = []
        for xc, yc, w, h in res.boxes.xywhn.cpu().numpy():
            lineas.append(f"0 {xc:.6f} {yc:.6f} {w:.6f} {h:.6f}")
        etiqueta.write_text("\n".join(lineas) + ("\n" if lineas else ""), encoding="utf-8")
        shutil.copy2(img, config.YOLO_DIR / "images" / parte / img.name)
        total_cajas += len(lineas)
        if i % 25 == 0 or i == len(pendientes):
            print(f"  {i}/{len(pendientes)}  ({total_cajas} personas)")

    escribir_yaml()
    n_train = len(list((config.YOLO_DIR / "images" / "train").glob("*.jpg")))
    n_val = len(list((config.YOLO_DIR / "images" / "val").glob("*.jpg")))
    print(f"\nListo: train {n_train} · val {n_val} · {config.DATA_YAML}")
    print("Siguiente: revisar y corregir con tools/04_revisar_etiquetas.py")


if __name__ == "__main__":
    main()
