# Balanceo de Ejes — MLO Consolidado + Material Faltante

## Objetivo

Extender PalletBalancer para que, dado un conjunto de FDOs y una sola MLO consolidada, el sistema:

1. Cruce automáticamente FDO vs MLO por número de parte y detecte material faltante.
2. Genere el plan de carga y la lista de picking usando las cantidades **reales** del MLO (no las esperadas del FDO).
3. Calcule W1, W2 y Wr con el peso real disponible y verifique NOM-012 y FHWA.

---

## Contexto operativo

- Un contenedor puede llevar FDOs de múltiples clientes (destinos distintos).
- El WMS genera **una sola MLO consolidada** para todo el embarque, ya ordenada por ubicación (rack → posición → nivel), que es el orden óptimo de picking y de carga.
- En producción ocurren faltantes: el MLO refleja lo que realmente está en el almacén, que puede ser menor a lo que pide el FDO.
- El sistema debe operar con lo disponible y dejar visibilidad de lo que falta.

---

## Requerimientos funcionales

### RF-01 — Carga de MLO consolidada (multi-FDO)

- El usuario sube **un solo archivo XLS** de MLO que puede contener líneas de múltiples FDOs.
- El sistema vincula cada línea del MLO al FDO correspondiente por **número de modelo (ModelNo)**.
- Si un modelo aparece en más de un FDO, se distribuye la cantidad del MLO proporcionalmente entre los FDOs que lo piden, respetando el orden de registro.
- El orden de las líneas en el MLO se preserva — es el orden de picking y de carga.

### RF-02 — Validación FDO vs MLO (detección de faltantes)

Al cruzar FDO vs MLO por número de parte, el sistema calcula para cada modelo:

```
Faltante = Qty_FDO − Qty_MLO

Si Faltante > 0  →  material faltante (parcial o total)
Si Faltante = 0  →  completo
Si Faltante < 0  →  exceso en MLO (advertencia — revisar)
```

La validación se muestra antes de generar el plan de carga.

### RF-03 — Plan de carga con faltantes

- El plan de posiciones se calcula con las **cantidades reales del MLO**.
- Los pallets faltantes se marcan como `PENDIENTE — material faltante` en el plan.
- Las posiciones `PENDIENTE` se ubican al **fondo del contenedor** por defecto (último en cargar, primero en completar cuando llegue el material).
- El usuario puede reubicar posiciones manualmente antes de confirmar.

### RF-04 — Cálculo de ejes con material faltante

- W1, W2 y Wr se calculan usando el peso real del MLO disponible, no el peso esperado del FDO.
- Las posiciones `PENDIENTE` aportan peso cero al cálculo de ejes.
- El sistema muestra dos escenarios:
  - **Carga actual** (con faltantes): W1, W2, Wr del embarque real.
  - **Carga completa estimada** (si llegara todo el faltante): proyección de W1, W2, Wr.

### RF-05 — Verificación de normas

Para ambos escenarios (actual y completo estimado):

| Eje | Límite NOM-012 | Límite FHWA |
|-----|---------------|-------------|
| W1  | 10,000 kg     | 9,072 kg    |
| W2  | 18,000 kg     | 15,422 kg   |
| Wr  | 18,000 kg     | 15,422 kg   |

Estado por eje: ✓ Cumple / ⚠ Excede.

### RF-06 — Lista de picking con faltantes

La lista de picking incluye:
- Líneas normales con CASE, ubicación, cantidad y posición en contenedor.
- Líneas `[FALTANTE]` con modelo, cantidad faltante y posición reservada.
- Las líneas faltantes aparecen al final del listado de picking (no hay nada que picar).

### RF-07 — Reporte de faltantes

Reporte independiente que muestra:

| Modelo | Descripción | FDO espera | MLO tiene | Faltante | Posición reservada |
|--------|-------------|-----------|-----------|----------|-------------------|

Para seguimiento con producción/almacén.

---

## Flujo completo

```
1. Usuario carga N FDOs
2. Usuario carga 1 MLO consolidada (multi-FDO)
3. Sistema cruza por ModelNo:
        ├── Coincide exacto   → normal
        ├── MLO < FDO         → faltante, marca PENDIENTE
        └── MLO > FDO         → advertencia exceso
4. Sistema genera plan de posiciones con cantidades reales
        ├── Posiciones normales  → CASEs reales del MLO
        └── Posiciones PENDIENTE → fondo del contenedor
5. Calcula W1 / W2 / Wr (carga actual)
6. Proyecta W1 / W2 / Wr (carga completa estimada)
7. Verifica NOM-012 y FHWA para ambos escenarios
8. Genera lista de picking en orden MLO
9. Genera reporte de faltantes
```

---

## Fórmulas de cálculo de ejes

```
Wr      = Σ(Peso_pallet_i × d_i) / D

    donde:
    d_i = distancia del kingpin al pallet i (metros)
    D   = distancia kingpin → eje trasero del remolque (metros)

F_KP    = Peso_total_contenedor − Wr
W2      = W2_vacío + F_KP × (d_5W / wheelbase_tractor)
W1      = W1_vacío + F_KP × (1 − d_5W / wheelbase_tractor)
```

> Nota: W1_vacío, W2_vacío, wheelbase_tractor y d_5W (posición del fifth wheel)
> se obtienen de la configuración del tipo de tracto seleccionado.
> El reparto 30/70 es solo una aproximación cuando no se conoce el tipo de tracto.

---

## Pendientes / fuera de alcance de este spec

- Selección de tipo de tracto con geometría real (wheelbase, posición del fifth wheel) — queda pendiente para spec siguiente.
- Redistribución automática de posiciones para equilibrar ejes — pendiente.
- Integración directa con WMS para descarga automática del MLO — pendiente.
