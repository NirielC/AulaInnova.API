"""
AULAINNOVA VISIÓN - 8. DETECTAR EN EL AULA Y ENVIAR A LA API

El programa que queda corriendo en la PC de la cámara, todo el día:
  cámara -> YOLO (personas) -> rastreo (ByteTrack, un id por persona)
         -> zona de cada persona (por el punto de los pies)
         -> quién es docente (por tiempo en la zona DOCENTE)
         -> cada VENTANA_SEGUNDOS: una medición por zona e indicador
         -> POST /api/Visual/measurements   (solo NÚMEROS, nunca imágenes)

Las clases son AUTOMÁTICAS: cada minuto pregunta a la API qué clase hay ahora
en el aula de la cámara (POST /api/estacion/vision/sesion). La API mira el
horario del día, abre la sesión de la clase que empieza y cierra la que
terminó. Sin clase (recreo, fuera de horario) no se envía nada.

  python tools/08_detectar.py --ver            con ventana de depuración
  python tools/08_detectar.py                  sin ventana (uso normal)
  python tools/08_detectar.py --sin-api --ver  probar zonas y conteos sin enviar nada

Necesita (ver README): zonas.json (06), la cámara registrada como estación de
AULA en la API (ESTACION_ID / ESTACION_CLAVE en config.py) y, una sola vez,
indicadores y zonas creados en la API (07 indicadores / 07 zonas).

Cómo se trata al docente:
  - Un rastro que pasa SEGUNDOS_PARA_DOCENTE en la zona DOCENTE (y es la mayor
    parte de su tiempo) queda marcado como docente: aunque después camine entre
    los pupitres, no se cuenta como estudiante ni su movimiento.
  - Un estudiante que solo pasa al escritorio (unos segundos) sigue siendo estudiante.
  - Lo que importa es el movimiento de los ESTUDIANTES; la ubicación exacta del
    docente no se reporta. Su presencia ya se registra al pasar por la estación NFC.

Teclas (con --ver):  Q / ESC salir
"""

import argparse
import sys
import time
from pathlib import Path

import cv2

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import (  # noqa: E402
    SESION_VACIA, Agregador, ApiCliente, ColaMediciones, ControlSesion, SeguimientoRoles, Zonas,
    abrir_camara, leer_json, medicion_api, recortar_a_sesion, texto, utc_ahora,
)

PROCESAR_FPS = 5  # cuadros por segundo que se analizan (ahorra CPU; suficiente para personas)

AZUL = (255, 160, 60)
VERDE = (80, 200, 120)
GRIS = (170, 170, 170)


def describir(control):
    sesion = control.sesion
    if sesion is None:
        return f"Sin clase: {control.mensaje}"
    fin_local = sesion.fin_clase.astimezone().strftime("%H:%M") if sesion.fin_clase else "?"
    return f"Clase: {sesion.clase} (sesión {sesion.id}, termina {fin_local})"


def enviar_ventana(cliente, cola, sesion, resultados, info, avisados):
    """Manda las mediciones de una ventana a la sesión indicada."""
    enviadas, descartadas = cola.reenviar(cliente)
    if enviadas or descartadas:
        print(f"  cola: {enviadas} reenviadas, {descartadas} descartadas por la API")

    ventana = recortar_a_sesion(info, sesion)
    if ventana is None:
        return

    for zona, indicador, valor, motivo in resultados:
        zona_id = sesion.zonas.get(zona)
        indicador_id = sesion.indicadores.get(indicador)
        if zona_id is None or indicador_id is None:
            falta = f"{zona}/{indicador}"
            if falta not in avisados:
                avisados.add(falta)
                print(f"  AVISO: la API no tiene la zona o el indicador {falta} "
                      f"(07_configurar_api.py zonas / indicadores). No se envía.")
            continue
        cuerpo = medicion_api(sesion.id, zona_id, indicador_id, ventana, valor, motivo)
        codigo = cliente.enviar_medicion(cuerpo)
        if codigo is None or codigo >= 500 or codigo in (401, 403):
            cola.agregar(cuerpo)
        elif codigo == 400:
            print(f"  API rechazó {zona}/{indicador} (400): ventana fuera de la clase o duplicada")
    pendientes = cola.pendientes()
    if pendientes:
        print(f"  pendientes sin enviar: {pendientes}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ver", action="store_true", help="mostrar ventana de depuración")
    parser.add_argument("--sin-api", action="store_true", help="no enviar nada (pruebas)")
    args = parser.parse_args()

    zonas_json = leer_json(config.ZONAS_JSON, [])
    if not zonas_json:
        print("No hay zonas. Dibuja primero con 06_dibujar_zonas.py")
        return

    cola = ColaMediciones(config.PENDIENTES_JSONL)
    cliente = control = None
    avisados = set()
    if not args.sin_api:
        cliente = ApiCliente(config.API_URL, config.ESTACION_ID, config.ESTACION_CLAVE)
        control = ControlSesion(cliente, config.VERSION_PROCESO)
        control.actualizar(forzar=True)
        print(f"API: {config.API_URL}  ·  estación (cámara) {config.ESTACION_ID}")
        print(describir(control))
        if abs(control.desfase.total_seconds()) > 5:
            print(f"AVISO: el reloj de esta PC difiere {control.desfase.total_seconds():.0f} s del servidor "
                  f"(se corrige solo al enviar, pero conviene sincronizarlo).")
    reloj = control.ahora if control else utc_ahora

    from ultralytics import YOLO
    ruta_modelo = config.modelo_deteccion()
    print(f"Modelo: {ruta_modelo}")
    modelo = YOLO(ruta_modelo)

    cam = abrir_camara(config.CAMARA, config.ANCHO, config.ALTO)
    ok, cuadro = cam.read()
    if not ok:
        print(f"No se pudo leer la cámara '{config.CAMARA}'.")
        return
    alto, ancho = cuadro.shape[:2]

    zonas = Zonas(zonas_json, ancho, alto)
    seguimiento = SeguimientoRoles(zonas, ancho, alto, config)
    agregador = Agregador(seguimiento, config)
    agregador.reiniciar(reloj())

    periodo = 1.0 / PROCESAR_FPS
    proximo = time.monotonic()
    ultimo_reintento_camara = 0.0
    print(f"Detectando. Mediciones cada {config.VENTANA_SEGUNDOS} s. Ctrl+C para salir.")

    try:
        while True:
            # ¿Cambió la clase? (la API consulta el horario; aquí cada minuto)
            if control is not None:
                anterior = control.actualizar()
                if anterior is not None:
                    fin = reloj()
                    if anterior is not SESION_VACIA and agregador.muestras_totales > 0:
                        resultados, info = agregador.cerrar_ventana(fin)
                        enviar_ventana(cliente, cola, anterior, resultados, info, avisados)
                    agregador.reiniciar(fin)
                    print(f"[{fin.astimezone().strftime('%H:%M:%S')}] {describir(control)}")

            ahora_m = time.monotonic()
            if ahora_m < proximo:
                time.sleep(min(proximo - ahora_m, 0.05))
                continue
            proximo = ahora_m + periodo

            ok, cuadro = cam.read()
            if not ok:
                agregador.muestra(camara_ok=False)
                if ahora_m - ultimo_reintento_camara > 5:  # reconectar la cámara
                    ultimo_reintento_camara = ahora_m
                    cam.release()
                    cam = abrir_camara(config.CAMARA, config.ANCHO, config.ALTO)
            else:
                res = modelo.track(cuadro, persist=True, classes=[0], conf=config.CONFIANZA,
                                   imgsz=config.TAMANO_IMAGEN, tracker="bytetrack.yaml", verbose=False)[0]
                detecciones = []
                cajas = []
                if res.boxes.id is not None:
                    ids = res.boxes.id.int().cpu().tolist()
                    for id_rastro, (x1, y1, x2, y2) in zip(ids, res.boxes.xyxy.cpu().tolist()):
                        detecciones.append((id_rastro, (x1 + x2) / 2, y2))  # pie = centro inferior
                        cajas.append((id_rastro, int(x1), int(y1), int(x2), int(y2)))
                seguimiento.actualizar(detecciones, ahora_m)
                agregador.muestra(camara_ok=True)

                if args.ver:
                    vista = cuadro.copy()
                    zonas.dibujar(vista)
                    for id_rastro, x1, y1, x2, y2 in cajas:
                        r = seguimiento.rastros.get(id_rastro)
                        docente = r is not None and r.es_docente
                        color = AZUL if docente else VERDE
                        cv2.rectangle(vista, (x1, y1), (x2, y2), color, 2)
                        cv2.circle(vista, ((x1 + x2) // 2, y2), 4, color, -1)
                        zona = (r.zona_confirmada or "") if r is not None else ""
                        etiqueta = f"#{id_rastro} {'DOCENTE' if docente else ''} {zona}"
                        texto(vista, etiqueta, (x1, max(15, y1 - 6)), color, 0.45, 1)
                    texto(vista, f"Personas: {len(cajas)}   Ventana: {agregador.muestras_validas} muestras",
                          (10, 25))
                    texto(vista, describir(control) if control else "Sin API (--sin-api)", (10, 50), GRIS, 0.5, 1)
                    texto(vista, "Q = salir", (10, alto - 15), GRIS, 0.5, 1)
                    cv2.imshow("AulaInnova - Deteccion", vista)
                    if (cv2.waitKey(1) & 0xFF) in (ord("q"), 27):
                        break

            fin = reloj()
            if agregador.ventana_terminada(fin):
                resultados, info = agregador.cerrar_ventana(fin)
                resumen = "  ".join(
                    f"{z[:4]}/{i.split('_')[0][:5]}={'x' if v is None else v}" for z, i, v, _ in resultados)
                print(f"[{fin.astimezone().strftime('%H:%M:%S')}] {resumen}")
                if control is not None:
                    if control.sesion is not None:
                        enviar_ventana(cliente, cola, control.sesion, resultados, info, avisados)
                    else:
                        print("  (sin clase en el aula: no se envía)")
                        enviadas, _ = cola.reenviar(cliente)  # lo atrasado de la última clase
                        if enviadas:
                            print(f"  cola: {enviadas} reenviadas")
    except KeyboardInterrupt:
        pass
    finally:
        cam.release()
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
