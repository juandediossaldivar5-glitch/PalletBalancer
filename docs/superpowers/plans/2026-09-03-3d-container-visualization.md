# 3D Container Visualization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an interactive Three.js 3D visualization of the loaded container, accessible via a "Ver 3D" button in the existing `contenedor` view of `app.html`.

**Architecture:** All changes are in a single file: `src/PalletBalancer.Api/wwwroot/app.html`. Three.js is loaded lazily via ES module dynamic `import()` using an importmap in `<head>`. No backend changes. No new files.

**Tech Stack:** Three.js r165 (ES module via CDN), Alpine.js (existing), plain HTML/CSS.

**Spec:** `docs/superpowers/specs/2026-09-03-3d-container-visualization-design.md`

## Global Constraints

- Only file modified: `src/PalletBalancer.Api/wwwroot/app.html`
- Three.js version: exactly `0.165.0` via `cdn.jsdelivr.net`
- No new files, no backend changes
- All new Alpine.js properties added inside the `return { ... }` block of `function app()`
- All new methods added inside `function app()` alongside existing methods (`palletColor`, `toLb`, etc.)
- Existing `_paleta` array (already in Alpine state) is reused for destino colors
- No automated tests — each task ends with a manual verification step

---

## File Map

Single file: `src/PalletBalancer.Api/wwwroot/app.html`

| Section | What changes |
|---|---|
| `<head>` | Add `<script type="importmap">` for Three.js |
| CSS block (before `</style>`) | Add `.modal-3d-*` styles |
| After closing `</div>` of `vista==='picking'` | Add modal HTML |
| `contenedor` header buttons (line ~902, after "Exportar PDF" `</template>`) | Add "Ver 3D" button |
| `return { ... }` in `function app()` | Add 4 new data properties |
| Methods section of `function app()` | Add `abrirVista3D()`, `cerrarVista3D()`, `render3D()` |

---

## Task 1: importmap + CSS + Modal HTML

**Files:**
- Modify: `src/PalletBalancer.Api/wwwroot/app.html`

**Interfaces:**
- Produces: `#canvas3d` element, `modal3dAbierto` toggle, `pallet3dSeleccionado` display, CSS classes `.modal-3d-overlay` / `.modal-3d-header` / `.modal-3d-info`

- [ ] **Step 1: Add importmap to `<head>`**

Find the `<head>` tag (line ~1). Insert immediately after it (before any other `<script>` tags):

```html
<script type="importmap">
{
  "imports": {
    "three": "https://cdn.jsdelivr.net/npm/three@0.165.0/build/three.module.js",
    "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.165.0/examples/jsm/"
  }
}
</script>
```

- [ ] **Step 2: Add CSS for the 3D modal**

Find the closing `</style>` tag. Insert before it:

```css
    /* ── 3D Modal ── */
    .modal-3d-overlay {
      position: fixed; inset: 0; z-index: 2000;
      background: #080d1a;
      display: flex; flex-direction: column;
    }
    .modal-3d-header {
      display: flex; align-items: center; justify-content: space-between;
      padding: 0.75rem 1.25rem;
      background: rgba(255,255,255,0.03);
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
    }
    .modal-3d-header h2 { font-size: 0.95rem; font-weight: 600; margin: 0; color: var(--text); }
    #canvas3d { flex: 1; display: block; width: 100%; }
    .modal-3d-info {
      position: absolute; bottom: 1.25rem; right: 1.25rem;
      background: rgba(13,22,41,0.95);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 0.9rem 1.1rem;
      min-width: 200px;
      font-size: 0.82rem;
      pointer-events: none;
    }
    .modal-3d-info .info-row { display: flex; justify-content: space-between; gap: 1rem; margin-bottom: 0.3rem; }
    .modal-3d-info .info-label { color: var(--muted); }
    .modal-3d-info .info-val { color: var(--text); font-weight: 600; font-family: monospace; }
```

- [ ] **Step 3: Add modal HTML**

Find the line `<!-- ============================================================ -->` just before `<!-- REPORTE IMPRESO` (near the end of the `<body>`, after the picking view closes). Insert the modal HTML before that comment:

```html
  <!-- ============================================================ -->
  <!-- MODAL: VISTA 3D                                               -->
  <!-- ============================================================ -->
  <div class="modal-3d-overlay no-print" x-show="modal3dAbierto" x-cloak style="position:fixed">
    <div class="modal-3d-header">
      <h2><i class="bi bi-box-seam"></i> Vista 3D —
        <span style="color:#ff8090;font-family:monospace"
              x-text="seleccionados.length > 1 ? seleccionados.length + ' FDOs' : 'FDO ' + (fdoActual?.fdoSlipNo ?? '')">
        </span>
      </h2>
      <button class="btn-ghost" style="padding:0.35rem 0.9rem" @click="cerrarVista3D()">
        <i class="bi bi-x-lg"></i> Cerrar
      </button>
    </div>

    <!-- Spinner mientras carga Three.js -->
    <div x-show="cargando3d" style="flex:1;display:flex;align-items:center;justify-content:center;gap:1rem;color:var(--muted)">
      <span class="spinner" style="width:28px;height:28px;border-width:3px"></span>
      Cargando visualización 3D...
    </div>

    <canvas id="canvas3d" x-show="!cargando3d"></canvas>

    <!-- Panel de info al hacer clic en pallet -->
    <div class="modal-3d-info" x-show="pallet3dSeleccionado" x-cloak>
      <div class="info-row">
        <span class="info-label">Modelo</span>
        <span class="info-val" x-text="pallet3dSeleccionado?.modelNo ?? '—'"></span>
      </div>
      <div class="info-row">
        <span class="info-label">Piezas</span>
        <span class="info-val" x-text="pallet3dSeleccionado?.piezas ?? '—'"></span>
      </div>
      <div class="info-row">
        <span class="info-label">Peso</span>
        <span class="info-val"
              x-text="(pallet3dSeleccionado?.pesoKg ?? 0).toLocaleString('es-MX') + ' kg / ' + toLb(pallet3dSeleccionado?.pesoKg).toLocaleString('es-MX') + ' lb'">
        </span>
      </div>
      <div class="info-row">
        <span class="info-label">Destino</span>
        <span class="info-val" x-text="pallet3dSeleccionado?.destino ?? '—'"></span>
      </div>
      <div class="info-row" style="margin-bottom:0">
        <span class="info-label">Capa</span>
        <span class="info-val" x-text="pallet3dSeleccionado?.capa === 1 ? 'Piso' : 'Encima'"></span>
      </div>
    </div>
  </div>
```

- [ ] **Step 4: Manual check — no syntax errors**

Open `app.html` in a browser (or just verify the HTML is well-formed). The modal should NOT appear because `modal3dAbierto` doesn't exist yet in Alpine — that's expected. No JS errors from the HTML itself.

- [ ] **Step 5: Commit**

```bash
cd ~/Desktop/PalletBalancer
git add src/PalletBalancer.Api/wwwroot/app.html
git commit -m "feat: add 3D modal HTML + CSS + Three.js importmap"
```

---

## Task 2: "Ver 3D" Button + Alpine Data Properties

**Files:**
- Modify: `src/PalletBalancer.Api/wwwroot/app.html`

**Interfaces:**
- Consumes: `modal3dAbierto`, `cargando3d`, `pallet3dSeleccionado` (added in this task)
- Produces: `abrirVista3D()` call site in HTML; data properties available to all methods

- [ ] **Step 1: Add "Ver 3D" button in contenedor header**

Find the block (around line 897–903):
```html
        <template x-if="contenedor">
          <button class="btn-ghost" style="white-space:nowrap;color:#4dd884;border-color:rgba(77,216,132,0.3)"
                  @click="exportarPDF()">
            <i class="bi bi-file-earmark-pdf"></i>
            Exportar PDF
          </button>
        </template>
```

Insert immediately AFTER the closing `</template>` of that block (before the `<template x-if="contenedor && Object.keys(mlosPorFdo).length > 0">` block):

```html
        <template x-if="contenedor && contenedor.posiciones?.length > 0">
          <button class="btn-ghost" style="white-space:nowrap;color:#b57cf0;border-color:rgba(181,124,240,0.3)"
                  @click="abrirVista3D()">
            <i class="bi bi-box-seam"></i> Ver 3D
          </button>
        </template>
```

- [ ] **Step 2: Add Alpine data properties**

Find `function app() { return {` (around line 1574). Inside the `return { ... }` block, after `guardandoItem: false,` and before `_colorCache: {}`, add:

```js
    modal3dAbierto:       false,
    cargando3d:           false,
    pallet3dSeleccionado: null,
    _three3dLoaded:       false,
    _three3dAnimFrame:    null,
    _three3dRenderer:     null,
    _three3dCleanup:      null,
    _selected3dMesh:      null,
```

- [ ] **Step 3: Manual check**

Serve the app (Railway deploy or local `dotnet run`). Navigate to a loaded container — the "Ver 3D" button should appear in purple next to "Exportar PDF". Clicking it does nothing yet (method doesn't exist), which may log an Alpine error — that's expected.

- [ ] **Step 4: Commit**

```bash
cd ~/Desktop/PalletBalancer
git add src/PalletBalancer.Api/wwwroot/app.html
git commit -m "feat: add Ver 3D button and Alpine 3D data properties"
```

---

## Task 3: abrirVista3D + cerrarVista3D

**Files:**
- Modify: `src/PalletBalancer.Api/wwwroot/app.html`

**Interfaces:**
- Consumes: `_three3dLoaded`, `modal3dAbierto`, `cargando3d`, `_three3dAnimFrame`, `_three3dRenderer`, `_three3dCleanup`, `_selected3dMesh`, `pallet3dSeleccionado`
- Produces: `abrirVista3D()`, `cerrarVista3D()` — called from HTML; `render3D()` called from `abrirVista3D()`

- [ ] **Step 1: Add abrirVista3D() and cerrarVista3D()**

Find the `palletColor(p) {` method in `function app()`. Insert the two new methods BEFORE it:

```js
    async abrirVista3D() {
      this.modal3dAbierto = true;
      this.pallet3dSeleccionado = null;
      this._selected3dMesh = null;
      this.cargando3d = true;
      await this.$nextTick();
      await this.render3D();
      this.cargando3d = false;
    },

    cerrarVista3D() {
      this.modal3dAbierto = false;
      this.pallet3dSeleccionado = null;
      this._selected3dMesh = null;
      if (this._three3dAnimFrame) {
        cancelAnimationFrame(this._three3dAnimFrame);
        this._three3dAnimFrame = null;
      }
      if (this._three3dCleanup) {
        this._three3dCleanup();
        this._three3dCleanup = null;
      }
      if (this._three3dRenderer) {
        this._three3dRenderer.dispose();
        this._three3dRenderer = null;
      }
    },

    async render3D() {
      // Placeholder — implemented in Task 4
    },
```

- [ ] **Step 2: Manual check**

In the app, open a loaded container and click "Ver 3D". The modal should open (black screen, spinner briefly then disappears). "Cerrar" button should close it cleanly. No JS errors in console.

- [ ] **Step 3: Commit**

```bash
cd ~/Desktop/PalletBalancer
git add src/PalletBalancer.Api/wwwroot/app.html
git commit -m "feat: abrirVista3D / cerrarVista3D modal lifecycle"
```

---

## Task 4: render3D — Full Three.js Scene

**Files:**
- Modify: `src/PalletBalancer.Api/wwwroot/app.html`

**Interfaces:**
- Consumes: `this.contenedor.posiciones[]`, `this.contenedor.filasDisponibles`, `this.contenedor.palletLargoCm`, `this.contenedor.palletAnchoCm`, `this._paleta`, `this.modal3dAbierto`, `this._selected3dMesh`, `this.pallet3dSeleccionado`
- Produces: live Three.js scene in `#canvas3d`, hover highlight on mousemove, info panel on click

- [ ] **Step 1: Replace the placeholder render3D() with the full implementation**

Find `async render3D() { // Placeholder — implemented in Task 4 },` and replace with:

```js
    async render3D() {
      const THREE = await import('three');
      const { OrbitControls } = await import('three/addons/controls/OrbitControls.js');

      const canvas = document.getElementById('canvas3d');
      const W = canvas.clientWidth  || window.innerWidth;
      const H = canvas.clientHeight || (window.innerHeight - 56); // 56px = header

      // ── Renderer ──────────────────────────────────────────────
      const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
      renderer.setSize(W, H);
      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
      renderer.setClearColor(0x080d1a);
      this._three3dRenderer = renderer;

      // ── Scene + Camera ─────────────────────────────────────────
      const scene  = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(50, W / H, 1, 50000);

      // ── Container dimensions ───────────────────────────────────
      const pos     = this.contenedor.posiciones ?? [];
      const largoCm = this.contenedor.palletLargoCm  ?? 120;
      const anchoCm = this.contenedor.palletAnchoCm  ?? 100;
      const filas   = this.contenedor.filasDisponibles ?? 12;

      const containerL = filas * largoCm;      // Z (depth, cabin→doors)
      const containerW = anchoCm * 2 + 8;      // X (left + gap + right)
      const containerH = 270;                  // Y (standard interior ~270 cm)

      // Camera: isometric from above-front-right, looking at center
      camera.position.set(containerW * 2.2, containerH * 1.6, -containerL * 0.25);
      camera.lookAt(0, containerH * 0.35, containerL * 0.5);

      // ── Lights ─────────────────────────────────────────────────
      scene.add(new THREE.AmbientLight(0xffffff, 0.75));
      const dir = new THREE.DirectionalLight(0xffffff, 0.6);
      dir.position.set(containerW, containerH * 2, 0);
      scene.add(dir);

      // ── Floor plane ────────────────────────────────────────────
      const floorGeo = new THREE.PlaneGeometry(containerW + 40, containerL + 40);
      const floorMat = new THREE.MeshBasicMaterial({ color: 0x050a12, side: THREE.DoubleSide });
      const floor    = new THREE.Mesh(floorGeo, floorMat);
      floor.rotation.x = -Math.PI / 2;
      floor.position.set(0, -0.5, containerL / 2);
      scene.add(floor);

      // ── Container box (wireframe + ghost walls) ─────────────────
      const boxGeo   = new THREE.BoxGeometry(containerW, containerH, containerL);
      const edges    = new THREE.EdgesGeometry(boxGeo);
      const wireMat  = new THREE.LineBasicMaterial({ color: 0x2a3f66 });
      const wire     = new THREE.LineSegments(edges, wireMat);
      wire.position.set(0, containerH / 2, containerL / 2);
      scene.add(wire);

      const ghostMat  = new THREE.MeshBasicMaterial({ color: 0x1a2a50, transparent: true, opacity: 0.07, side: THREE.BackSide });
      const ghost     = new THREE.Mesh(boxGeo, ghostMat);
      ghost.position.copy(wire.position);
      scene.add(ghost);

      // ── Destino color map (reuse existing _paleta) ──────────────
      const destinos = [...new Set(pos.map(p => p.destino))];
      const destinoColor = {};
      destinos.forEach((d, i) => { destinoColor[d] = this._paleta[i % this._paleta.length]; });

      // ── Pallet meshes ───────────────────────────────────────────
      const palletMeshes = [];
      pos.forEach(p => {
        const pw  = p.anchoCm  ?? anchoCm;
        const ph  = p.altoCm   ?? 150;
        const pd  = p.largoCm  ?? largoCm;

        // Y: capa 1 sits on floor; capa 2 sits on top of capa 1
        const capa1 = pos.find(q => q.fila === p.fila && q.lado === p.lado && q.capa === 1);
        const yBase  = p.capa === 1 ? 0 : (capa1?.altoCm ?? ph);
        const yCenter = yBase + ph / 2;

        // X: left side = negative, right side = positive, centered around 0
        const x = p.lado === 'Izquierdo' ? -(pw / 2 + 2) : (pw / 2 + 2);
        const z = (p.fila - 0.5) * largoCm;   // center of the row cell

        const geo  = new THREE.BoxGeometry(pw - 2, ph - 1, pd - 2); // 1-2cm gap between pallets
        const col  = new THREE.Color(destinoColor[p.destino] ?? '#4a9eff');
        const mat  = new THREE.MeshLambertMaterial({ color: col });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(x, yCenter, z);
        mesh.userData.pallet = p;
        mesh.userData.baseColor = col.clone();
        scene.add(mesh);
        palletMeshes.push(mesh);
      });

      // ── OrbitControls ───────────────────────────────────────────
      const controls         = new OrbitControls(camera, renderer.domElement);
      controls.target.set(0, containerH * 0.35, containerL / 2);
      controls.enableDamping  = true;
      controls.dampingFactor  = 0.08;
      controls.minDistance    = 50;
      controls.maxDistance    = containerL * 4;
      controls.update();

      // ── Raycaster ───────────────────────────────────────────────
      const raycaster   = new THREE.Raycaster();
      const mouse       = new THREE.Vector2();
      let   hoveredMesh = null;

      const onMouseMove = (e) => {
        const rect = canvas.getBoundingClientRect();
        mouse.x =  ((e.clientX - rect.left) / rect.width)  * 2 - 1;
        mouse.y = -((e.clientY - rect.top)  / rect.height) * 2 + 1;
        raycaster.setFromCamera(mouse, camera);
        const hits = raycaster.intersectObjects(palletMeshes);

        // Un-highlight previous hover (but keep selected one bright)
        if (hoveredMesh && hoveredMesh !== this._selected3dMesh) {
          hoveredMesh.material.emissive.set(0x000000);
        }
        hoveredMesh = hits.length ? hits[0].object : null;
        if (hoveredMesh && hoveredMesh !== this._selected3dMesh) {
          hoveredMesh.material.emissive.set(0x333333);
        }
        canvas.style.cursor = hoveredMesh ? 'pointer' : 'default';
      };

      const onClick = (e) => {
        const rect = canvas.getBoundingClientRect();
        mouse.x =  ((e.clientX - rect.left) / rect.width)  * 2 - 1;
        mouse.y = -((e.clientY - rect.top)  / rect.height) * 2 + 1;
        raycaster.setFromCamera(mouse, camera);
        const hits = raycaster.intersectObjects(palletMeshes);

        // Reset previously selected
        if (this._selected3dMesh) {
          this._selected3dMesh.material.emissive.set(0x000000);
        }

        if (hits.length) {
          this._selected3dMesh = hits[0].object;
          this._selected3dMesh.material.emissive.set(0x555555);
          this.pallet3dSeleccionado = hits[0].object.userData.pallet;
        } else {
          this._selected3dMesh      = null;
          this.pallet3dSeleccionado = null;
        }
      };

      canvas.addEventListener('mousemove', onMouseMove);
      canvas.addEventListener('click',     onClick);
      this._three3dCleanup = () => {
        canvas.removeEventListener('mousemove', onMouseMove);
        canvas.removeEventListener('click',     onClick);
      };

      // ── Handle window resize ────────────────────────────────────
      const onResize = () => {
        const nW = canvas.clientWidth;
        const nH = canvas.clientHeight;
        camera.aspect = nW / nH;
        camera.updateProjectionMatrix();
        renderer.setSize(nW, nH);
      };
      window.addEventListener('resize', onResize);
      const prevCleanup = this._three3dCleanup;
      this._three3dCleanup = () => {
        prevCleanup();
        window.removeEventListener('resize', onResize);
      };

      // ── Animation loop ──────────────────────────────────────────
      const animate = () => {
        if (!this.modal3dAbierto) return;
        this._three3dAnimFrame = requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
      };
      animate();
    },
```

- [ ] **Step 2: Manual verification — scene renders**

Deploy and open a loaded container (e.g., simula un FDO con pallets). Click "Ver 3D". Verify:
- Container wireframe box is visible
- Pallets appear as colored 3D boxes inside the container
- Different destinos have different colors
- Camera starts at an isometric angle showing the full load

- [ ] **Step 3: Manual verification — controls**

- Left-drag: rotates the scene ✓
- Scroll: zooms in/out ✓
- Right-drag: pans ✓

- [ ] **Step 4: Manual verification — hover and click**

- Move mouse over a pallet: cursor changes to pointer, pallet brightens ✓
- Click a pallet: info panel appears bottom-right with Modelo, Piezas, Peso, Destino, Capa ✓
- Click empty space: info panel disappears ✓
- Click "Cerrar": modal closes, no memory leaks (reopen should work fine) ✓

- [ ] **Step 5: Commit + push**

```bash
cd ~/Desktop/PalletBalancer
git add src/PalletBalancer.Api/wwwroot/app.html
git commit -m "feat: render3D — Three.js scene, pallets by destino, raycaster info panel"
git push
```
