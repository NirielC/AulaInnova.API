# Caja de la estación AulaInnova (para la carpintería)

Caja rectangular de madera, **montada en la pared**, con el **frente atornillado**
para poder abrirla. Las medidas de los módulos son típicas: **confirmar con regla
el switch y la ventana de la pantalla antes de cortar**.

---

## 1. Medidas generales

| | Medida |
|---|---|
| **Exterior** (ancho × alto × fondo) | **≈ 21 × 17 × 8.5 cm** |
| Interior útil | ≈ 19 × 15 × 7 cm |
| Paredes, fondo y costados | MDF o plywood de 6-9 mm |
| **Frente (tapa)** | **MDF o plywood de 3 mm** (el NFC lee a través) |

**¿Por qué 7 cm de fondo interior?** La pantalla ocupa ~2.5 cm desde el frente, la
placa con el ESP32 y los jumpers parados ~3.5 cm desde atrás, y queda espacio para
que los jumpers (largos) se doblen sin forzarse.

---

## 2. Cómo se arma

- **Atrás (contra la pared):** fija. Ahí se pegan con silicón la **placa perforada**,
  la **batería 18650**, el **TP4056** y el **MT3608**. Lleva 2 agujeros para
  colgarla en la pared.
- **Frente:** es la **tapa que se abre**, con **4 tornillos** en las esquinas. En ella
  van pegados la pantalla, el NFC, los LEDs, el buzzer y el teclado. Al abrirla
  queda colgando de los jumpers, que son largos, así que no hace falta desconectar
  nada.
- **Costado de abajo:** el switch y los dos USB-C, para que los cables cuelguen hacia
  abajo y no se vean.

---

## 3. Frente (vista desde afuera)

```
 ┌──────────────────────────────────────────────┐
 │ ⊙                                         ⊙  │ ← tornillos
 │        ┌──────────────────────────┐          │
 │        │   VENTANA PANTALLA 72×25 │   ○ ○    │ ← LEDs (5 mm)
 │        └──────────────────────────┘          │
 │                                     ∙ ∙ ∙    │ ← buzzer (agujeritos)
 │   ┌──────────────┐     ┌────────────────┐    │
 │   │   TECLADO    │     │  ((( NFC )))   │    │
 │   │   69 × 77    │     │   PASE AQUÍ    │    │
 │   │  (pegado     │     │  (sin agujero) │    │
 │   │   encima)    │     │                │    │
 │   └──────────────┘     └────────────────┘    │
 │ ⊙         ▬ ranura cinta                  ⊙  │
 └──────────────────────────────────────────────┘
```

| Abertura | Medida | Nota |
|---|---|---|
| Ventana de la pantalla | **≈ 72 × 25 mm** | Medir la parte azul visible de tu LCD |
| 2 LEDs | Agujeros de **5 mm** | Verde y rojo |
| Buzzer | 4-6 agujeritos de **2 mm** | Para que se escuche |
| Teclado | **Sin agujero**: va pegado encima | Ranura de **25 × 3 mm** debajo para pasar la cinta |
| Zona NFC | **Sin agujero** | Solo dibujar "PASE AQUÍ". El lector va pegado por dentro |

---

## 4. Costado de abajo

```
 ┌──────────────────────────────────────────────┐
 │   [▭ switch]      [▭ USB carga]  [▭ USB prog] │
 └──────────────────────────────────────────────┘
```

| Abertura | Medida | Para qué |
|---|---|---|
| Switch ON/OFF | **Medir el cuerpo** (típico 19 × 13 mm) | Encender/apagar |
| USB-C del TP4056 | **≈ 12 × 7 mm** | Cargador (alimentación diaria) |
| USB-C del ESP32 | **≈ 12 × 7 mm** | Reprogramar sin abrir la caja |
| Ventilación | 3-4 ranuras pequeñas | Que no se caliente |

> La placa perforada se pega atrás con el **USB del ESP32 tocando el costado de
> abajo**, alineado con su agujero.

---

## 5. Antes de mandar a cortar (medir)

1. El **switch**: largo × ancho del cuerpo.
2. La **parte visible de la pantalla**.
3. El **largo de los jumpers** (para confirmar que 7 cm de fondo alcanzan).
4. Que la placa (7 × 9 cm), la batería y los módulos de carga quepan juntos en la
   pared de atrás: **19 × 15 cm** alcanzan de sobra.
