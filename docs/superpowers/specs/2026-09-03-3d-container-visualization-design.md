# 3D Container Visualization — Design Spec
**Date:** 2026-09-03
**Project:** PalletBalancer (`src/PalletBalancer.Api/wwwroot/app.html`)

## Overview

Add an interactive 3D visualization of the loaded container accessible from the existing `contenedor` view. A single "Ver 3D" button opens a fullscreen modal with a Three.js canvas. No backend changes. No new files.

---

## Architecture

All changes are contained in `app.html`:
- CDN `<script>` tags for Three.js r165 + OrbitControls (lazy-loaded on first open)
- One new HTML block: the 3D modal
- Three new Alpine.js methods: `abrirVista3D()`, `cerrarVista3D()`, `render3D()`
- One new Alpine.js data property: `modal3dAbierto: false`

---

## HTML — Modal Structure

```
[fullscreen overlay, z-index above everything]
  [header bar]
    "Visualización 3D — <fdoSlipNo>"    [X] Cerrar
  [canvas#canvas3d — fills remaining height]
  [info panel — bottom-right corner, hidden until pallet clicked]
    Modelo: K006T91071XB
    Piezas: 4
    Peso:   320 kg / 705 lb
    Destino: STANLEY
    Capa:   Piso
```

The modal uses `position:fixed; inset:0` so it covers the full viewport.
The canvas is sized to `window.innerWidth × (window.innerHeight - headerHeight)`.

---

## Button Placement

In the `contenedor` view, alongside the existing print/report buttons:

```html
<button class="btn-ghost" @click="abrirVista3D()">
  <i class="bi bi-box-seam"></i> Ver 3D
</button>
```

Only visible when `contenedor.posiciones?.length > 0`.

---

## 3D Scene

### Coordinate Mapping

| Pallet field | 3D axis | Direction |
|---|---|---|
| `fila` (1–26) | Z | fila 1 = near (cabin), fila N = far (doors) |
| `lado` | X | Izquierdo = −1 side, Derecho = +1 side |
| `capa` (1 or 2) | Y | 1 = floor, 2 = stacked on top of capa 1 |

Pallet dimensions (`anchoCm`, `largoCm`, `altoCm`) are used directly in cm as Three.js units. A scale factor of 0.01 converts cm → meters for a reasonable scene size.

### Container Box

A wireframe box (EdgesGeometry) represents the container shell. Dimensions:
- Width: `anchoCm × 2 + gapCm`
- Length: `filasDisponibles × largoCm`
- Height: `maxAltoCm × 2 + floorThickness`

Semitransparent grey walls (`MeshBasicMaterial, opacity: 0.08`) + solid dark floor plane.

### Pallets

Each entry in `contenedor.posiciones[]` becomes a `BoxGeometry(anchoCm, altoCm, largoCm)` mesh.

**Color by destino:** Up to 8 destinations, each gets a fixed accent color from a palette (red, blue, green, orange, purple, teal, yellow, pink). Same destination = same color across the scene.

**Hover:** Raycaster on `mousemove` highlights the nearest pallet (emissive white glow).

**Click:** Raycaster on `click` selects the pallet and populates the info panel.

### Camera & Controls

- Initial position: isometric — above, in front, slightly to the right
- `OrbitControls` enabled: rotate (left drag), zoom (scroll), pan (right drag)
- `autoRotate: false`
- Target: center of the container

### Animation Loop

`requestAnimationFrame` loop runs only while `modal3dAbierto === true`.
On `cerrarVista3D()`: renderer is disposed, scene cleared, loop cancelled.
On re-open: scene is rebuilt from current `contenedor.posiciones`.

---

## Alpine.js Methods

### `abrirVista3D()`
1. Set `modal3dAbierto = true`
2. If Three.js not loaded, inject `<script>` tags and await load
3. On next tick, call `render3D(this.contenedor)`

### `render3D(contenedor)`
1. Create `WebGLRenderer` attached to `#canvas3d`
2. Build scene: container box + pallet meshes
3. Set up `OrbitControls`, raycaster event listeners
4. Start animation loop

### `cerrarVista3D()`
1. Cancel animation frame
2. Dispose renderer + geometries + materials
3. Set `modal3dAbierto = false`
4. Clear `palletSeleccionado`

---

## Info Panel

Shown in the bottom-right of the modal when a pallet is clicked:

| Label | Source field |
|---|---|
| Modelo | `modelNo` |
| Piezas | `piezas` |
| Peso | `pesoKg` kg / `toLb(pesoKg)` lb |
| Destino | `destino` |
| Capa | `capa === 1 ? 'Piso' : 'Encima'` |

---

## Lazy Loading Strategy

Three.js and OrbitControls are loaded from CDN only when `abrirVista3D()` is called for the first time. A boolean flag `three3dLoaded` prevents double-loading. While loading, the modal shows a spinner.

CDN URLs:
- `https://cdn.jsdelivr.net/npm/three@0.165.0/build/three.module.js`
- `https://cdn.jsdelivr.net/npm/three@0.165.0/examples/jsm/controls/OrbitControls.js`

Loaded as ES modules via dynamic `import()`.

---

## What Is NOT in Scope

- Editing pallets from the 3D view
- Saving/exporting the 3D view as image
- Showing weight/axis data in the 3D modal (already in the 2D view)
- Animation of loading sequence
- Mobile touch optimization

---

## Files Changed

| File | Change |
|---|---|
| `src/PalletBalancer.Api/wwwroot/app.html` | Add modal HTML, button, 3 Alpine methods, lazy CDN loader |

No backend changes. No new files.
