"""
AULAINNOVA VISIÓN - 5. ENTRENAR EL MODELO (fine-tuning)

Parte del modelo base de COCO (que ya sabe qué es una persona) y lo ajusta a
las fotos de NUESTRA cámara: el ángulo desde arriba de la pizarra, los
pupitres que tapan media persona, la luz del salón.

  python tools/05_entrenar.py                    entrena (GPU si hay, si no CPU)
  python tools/05_entrenar.py --epocas 30        más rápido, para probar
  python tools/05_entrenar.py --zip              solo empaqueta el dataset para Google Colab

Al final compara contra el modelo base con las fotos de validación y SOLO
reemplaza models/aulainnova_personas.pt si el nuevo es mejor.
También lo exporta a ONNX (para correr sin GPU).

Tiempo aproximado con ~400 fotos: GPU 10-20 min · CPU varias horas.
Sin GPU: usa --zip y entrena en Google Colab (ver README).
"""

import argparse
import shutil
import sys
from datetime import datetime
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402


def map50(modelo, datos):
    metricas = modelo.val(data=str(datos), imgsz=config.TAMANO_IMAGEN, classes=[0],
                          split="val", plots=False, verbose=False)
    return float(metricas.box.map50)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--epocas", type=int, default=80)
    parser.add_argument("--lote", type=int, default=-1, help="-1 = automático según la memoria")
    parser.add_argument("--zip", action="store_true", help="empaquetar dataset para Colab y salir")
    args = parser.parse_args()

    if not config.DATA_YAML.exists():
        print("No existe dataset/yolo/data.yaml. Corre 03_autoetiquetar.py primero.")
        return

    if args.zip:
        destino = shutil.make_archive(str(ROOT_DIR / "dataset_yolo"), "zip", config.YOLO_DIR)
        print(f"Dataset empaquetado: {destino}  (súbelo a Colab, ver README)")
        return

    n_train = len(list((config.YOLO_DIR / "images" / "train").glob("*.jpg")))
    n_val = len(list((config.YOLO_DIR / "images" / "val").glob("*.jpg")))
    print(f"Dataset: train {n_train} · val {n_val}")
    if n_train < 100 or n_val < 15:
        print("Muy pocas fotos todavía: captura más antes de entrenar (300+ recomendado).")
        return

    from ultralytics import YOLO

    print(f"\n1) Modelo base {config.MODELO_BASE} sobre nuestras fotos de validación...")
    base_map = map50(YOLO(config.MODELO_BASE), config.DATA_YAML)
    print(f"   mAP50 base: {base_map:.3f}")

    nombre = datetime.now().strftime("%Y%m%d_%H%M")
    proyecto = config.MODELS_DIR / "entrenamientos"
    print(f"\n2) Entrenando ({args.epocas} épocas)...")
    YOLO(config.MODELO_BASE).train(
        data=str(config.DATA_YAML),
        epochs=args.epocas,
        imgsz=config.TAMANO_IMAGEN,
        batch=args.lote,
        patience=20,          # para solo si deja de mejorar
        project=str(proyecto),
        name=nombre,
        exist_ok=True,
        single_cls=True,
    )
    mejor = proyecto / nombre / "weights" / "best.pt"
    if not mejor.exists():
        print("El entrenamiento no generó best.pt; revisa los mensajes de arriba.")
        return

    print("\n3) Comparando con el modelo base...")
    nuevo_map = map50(YOLO(str(mejor)), config.DATA_YAML)
    print(f"   mAP50 base {base_map:.3f}  ->  nuevo {nuevo_map:.3f}")

    if nuevo_map <= base_map:
        print("\nEl modelo nuevo NO mejora al base. No se reemplaza nada.")
        print("Sugerencias: más fotos, revisar más etiquetas (04), o más épocas.")
        print(f"(El candidato quedó en {mejor})")
        return

    config.MODELS_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copy2(mejor, config.MODELO_FINAL)
    YOLO(str(config.MODELO_FINAL)).export(format="onnx", imgsz=config.TAMANO_IMAGEN)
    print(f"\nListo: {config.MODELO_FINAL} (+ .onnx). Las demás herramientas ya lo usan solas.")


if __name__ == "__main__":
    main()
