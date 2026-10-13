"""
AULAINNOVA VISIÓN - 7. CONFIGURAR LA API (una sola vez por aula)

Registra en AulaInnova.API lo que el detector necesita:

  python tools/07_configurar_api.py indicadores   crea los 3 indicadores (una sola vez en total)
  python tools/07_configurar_api.py zonas         sube las zonas de zonas.json (una vez por aula)
  python tools/07_configurar_api.py sesion        qué clase ve la API ahora en el aula de la cámara
  python tools/07_configurar_api.py estado        muestra lo guardado

Las SESIONES son automáticas: 08_detectar.py le pregunta a la API cada minuto y
la API las abre y cierra según el horario. Solo como respaldo manual quedan:
  python tools/07_configurar_api.py abrir --asignacion 15
  python tools/07_configurar_api.py cerrar

indicadores / zonas / abrir / cerrar piden usuario y contraseña de alguien con
el permiso VISION_CONFIGURAR. Si la API responde 403, falta correr
sql/10_concesiones_administrador.sql en la base (da los permisos al admin).

Si ya existe una zona activa con el mismo código en esa aula, la API la
rechaza. No pasa nada: el detector usa la geometría LOCAL de zonas.json y le
pide los ids a la API por código.
"""

import argparse
import getpass
import sys
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT_DIR))

import config  # noqa: E402
from aulainnova_vision import ApiCliente, guardar_json, leer_json, utc_ahora  # noqa: E402


def conectar(args):
    cliente = ApiCliente(config.API_URL)
    usuario = args.usuario or input("Usuario: ")
    password = args.password or getpass.getpass("Contraseña: ")
    cliente.login(usuario, password)
    print(f"Sesión iniciada en {config.API_URL}")
    return cliente


def crear_indicadores(cliente, estado):
    estado.setdefault("indicadores", {})
    for codigo, ind in config.INDICADORES.items():
        if codigo in estado["indicadores"]:
            print(f"  {codigo}: ya registrado (id {estado['indicadores'][codigo]})")
            continue
        configuracion = {
            "modelo": config.modelo_deteccion(),
            "confianza": config.CONFIANZA,
            "ventana_s": config.VENTANA_SEGUNDOS,
            "min_s_en_zona": config.MIN_SEGUNDOS_EN_ZONA,
            "umbral_movimiento": config.UMBRAL_MOVIMIENTO,
        }
        codigo_http, respuesta = cliente.crear_indicador(
            codigo, 1, ind["unidad"], ind["metodo"], configuracion,
            "Validacion manual contra conteo humano en video de prueba del aula (pendiente).")
        if codigo_http == 200:
            estado["indicadores"][codigo] = respuesta["id"]
            print(f"  {codigo}: creado (id {respuesta['id']})")
        elif codigo_http == 403:
            print(f"  {codigo}: 403 = el usuario no tiene VISION_CONFIGURAR "
                  f"(correr sql/10_concesiones_administrador.sql)")
        else:
            print(f"  {codigo}: la API respondió {codigo_http}. Si ya existía, escribe su id a mano "
                  f"en estado.json -> indicadores -> {codigo}")


def subir_zonas(cliente, estado):
    zonas = leer_json(config.ZONAS_JSON, [])
    if not zonas:
        print("zonas.json está vacío. Dibuja las zonas con 06_dibujar_zonas.py")
        return
    for zona in zonas:
        if zona.get("id_api"):
            print(f"  {zona['codigo']}: ya registrada (id {zona['id_api']})")
            continue
        geometria = {"tipo": "poligono", "coordenadas": "normalizadas_0_1", "puntos": zona["puntos"]}
        codigo_http, respuesta = cliente.crear_zona(config.AULA_ID, zona["codigo"], 1, geometria)
        if codigo_http == 200:
            zona["id_api"] = respuesta["id"]
            print(f"  {zona['codigo']}: creada (id {respuesta['id']})")
        else:
            print(f"  {zona['codigo']}: la API respondió {codigo_http}. Si ya existía, escribe su id "
                  f"a mano en zonas.json (\"id_api\").")
    guardar_json(config.ZONAS_JSON, zonas)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("accion", choices=["indicadores", "zonas", "sesion", "abrir", "cerrar", "estado"])
    parser.add_argument("--asignacion", type=int, help="id de la asignación (clase) para abrir la sesión")
    parser.add_argument("--referencia", default="Autorizacion del colegio para el piloto AulaInnova",
                        help="referencia de la autorización (acta, permiso...)")
    parser.add_argument("--usuario")
    parser.add_argument("--password")
    args = parser.parse_args()

    estado = leer_json(config.ESTADO_JSON, {})

    if args.accion == "sesion":
        cliente = ApiCliente(config.API_URL, config.ESTACION_ID, config.ESTACION_CLAVE)
        codigo_http, datos = cliente.sesion_actual(config.VERSION_PROCESO)
        print(f"API respondió {codigo_http}")
        if isinstance(datos, dict):
            for clave, valor in datos.items():
                print(f"  {clave}: {valor}")
        else:
            print(datos)
        return

    if args.accion == "estado":
        print(f"API: {config.API_URL}  ·  aula {config.AULA_ID}  ·  estación {config.ESTACION_ID}")
        print("Indicadores:", estado.get("indicadores", {}))
        print("Zonas:", [(z["codigo"], z.get("id_api")) for z in leer_json(config.ZONAS_JSON, [])])
        print("Sesión activa:", estado.get("sesion_id"))
        return

    cliente = conectar(args)

    if args.accion == "indicadores":
        crear_indicadores(cliente, estado)
    elif args.accion == "zonas":
        subir_zonas(cliente, estado)
    elif args.accion == "abrir":
        if not args.asignacion:
            print("Falta --asignacion (id de la clase en ai.asignacion).")
            return
        codigo_http, respuesta = cliente.abrir_sesion(
            args.asignacion, config.AULA_ID, args.referencia, config.VERSION_PROCESO, utc_ahora())
        if codigo_http == 200:
            estado["sesion_id"] = respuesta["id"]
            print(f"Sesión visual abierta: id {respuesta['id']}")
        else:
            print(f"La API respondió {codigo_http}: {respuesta}")
            print("Revisa: la asignación es de tipo CLASE, está en su horario, el aula coincide "
                  "y no hay otra sesión ACTIVA en esa aula.")
    elif args.accion == "cerrar":
        sesion = estado.get("sesion_id")
        if not sesion:
            print("No hay sesión guardada.")
            return
        codigo_http, respuesta = cliente.cerrar_sesion(sesion)
        print(f"Cerrar sesión {sesion}: {codigo_http}")
        if codigo_http == 200:
            estado.pop("sesion_id", None)

    guardar_json(config.ESTADO_JSON, estado)


if __name__ == "__main__":
    main()
