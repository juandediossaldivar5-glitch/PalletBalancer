# PalletBalancer Frontend — Auth + FDO Management Design

**Date:** 2026-08-05
**Scope:** Phase 1 frontend — authentication with roles, FDO PDF import, FDO viewing, and quantity editing.

---

## Goal

Build a web frontend (HTML/JS, no build step) served from the existing ASP.NET Core API on Railway, with role-based authentication and full FDO lifecycle: import from PDF, confirm, view, and edit quantities.

---

## Architecture

Static files live in `src/PalletBalancer.Api/wwwroot/`. ASP.NET Core serves them via `app.UseStaticFiles()` and `app.UseDefaultFiles()`. No separate deployment — same Railway service as the API.

**Frontend stack:** Alpine.js 3 + Bootstrap 5 via CDN. No build step. Two HTML files:
- `index.html` — login page
- `app.html` — full single-page app (navbar + three switchable sections: lista, detalle, importar)

**Auth flow:** `index.html` calls `POST /api/auth/login` → receives JWT → stores in `localStorage` as `pb_token` → redirects to `app.html`. Every subsequent API call sends `Authorization: Bearer <token>`. If token is absent or expired, `app.html` redirects back to `index.html`.

**State management:** Alpine.js `x-data` on `<body>` holds: `{ vista: 'lista', usuario: null, fdos: [], fdoActual: null, importado: null }`. Role-based UI uses `x-show="usuario.rol === 'AMG' || usuario.rol === 'ADM'"`.

---

## Backend — New Additions

### 1. Usuario model and migration

New table `Usuarios`:

| Column | Type | Notes |
|---|---|---|
| Id | int | PK, auto-increment |
| Username | string(50) | unique |
| PasswordHash | string | bcrypt hash |
| Rol | string(10) | OPE / MKT / SV / AMG / ADM |
| Activo | bool | default true |

Migration: `dotnet ef migrations add AgregarUsuarios`.

### 2. JWT configuration

`appsettings.json` adds:
```json
"Jwt": {
  "Key": "set-via-environment-variable",
  "Issuer": "PalletBalancer",
  "Audience": "PalletBalancer",
  "ExpiresHours": 8
}
```

Railway environment variable `JWT__Key` overrides the placeholder. `Program.cs` registers `AddAuthentication(JwtBearer)`.

### 3. AuthController

`POST /api/auth/login`
- Body: `{ "username": "...", "password": "..." }`
- Returns: `{ "token": "...", "username": "...", "rol": "..." }`
- Error 401 if credentials invalid or user inactive

### 4. Seed usuario ADM

`Seed.cs` creates one ADM user on first run if `Usuarios` table is empty:
- Username: `admin`
- Password: `Admin1234!` (bcrypt hashed)
- Rol: ADM

### 5. FDO import endpoint

`POST /api/fdos/importar` — accepts `multipart/form-data` with field `archivo` (PDF file). Requires `[Authorize]` (any role).

Uses **PdfPig** (NuGet: `UglyToad.PdfPig`) to extract text. Parser reads lines looking for:
- `FDO Slip No` → `FdoSlipNo`
- `Disbursement Date` → `DsbDate`
- `Ship Date` → `ShipDate`
- `Customer` → `Customer`
- `Consignee` → `Consignee`
- Tabular product lines → list of `{ CustomerPoNo, ModelNo, ReqQty }`

Returns `FdoImportadoDto` (not saved to DB). Fields that could not be parsed are returned as empty strings. The user corrects them on the confirmation screen before saving with `POST /api/fdos`.

### 6. PATCH quantity endpoint

`PATCH /api/fdos/{id}/lineas/{lineaId}`
- Body: `{ "reqQty": 120 }`
- Requires role AMG or ADM (`[Authorize(Roles = "AMG,ADM")]`)
- Returns 404 if FDO or línea not found
- Returns 200 with updated línea

---

## Frontend — Screens

### index.html — Login

- Centered card with Bootstrap
- Fields: Username, Password
- On submit: `POST /api/auth/login` → save token and user info to `localStorage` → redirect to `app.html`
- Error message displayed inline if 401

### app.html — Main App

**Navbar:** Logo "PalletBalancer", links (FDOs, Importar — all roles), username + rol badge, Cerrar Sesión button.

**Vista: lista**
- Table: FDO Slip No, Cliente, Consignatario, Fecha embarque, acciones
- Row click → loads FDO detail and switches to `detalle` vista
- "Importar PDF" button in top-right → switches to `importar` vista

**Vista: importar**
- File input (PDF only)
- On file select: POST to `/api/fdos/importar` (multipart)
- Shows parsed data in editable form fields for confirmation
- "Confirmar y Guardar" button → `POST /api/fdos` → on success, switch to `lista` and reload
- "Cancelar" → back to `lista`

**Vista: detalle**
- FDO header: Slip No, fechas, Cliente, Consignatario
- Lines table: CustomerPoNo, ModelNo, Descripción (from Items catalog), ReqQty
- Edit button per line — visible only to AMG and ADM (`x-show`)
- Edit opens inline input field, Save calls `PATCH /api/fdos/{id}/lineas/{lineaId}`
- "Volver" button → back to `lista`

---

## Permission Matrix

| Acción | OPE | MKT | SV | AMG | ADM |
|---|---|---|---|---|---|
| Ver lista FDOs | ✓ | ✓ | ✓ | ✓ | ✓ |
| Importar PDF | ✓ | ✓ | ✓ | ✓ | ✓ |
| Ver detalle FDO | ✓ | ✓ | ✓ | ✓ | ✓ |
| Modificar cantidades | ✗ | ✗ | ✗ | ✓ | ✓ |
| Gestión catálogo | ✗ | ✗ | ✗ | ✗ | ✓ |

---

## Error Handling

- API errors (4xx/5xx) shown as Bootstrap alert inside the current vista
- JWT expiry: `app.html` checks token expiry on load and on each fetch; if expired, clears localStorage and redirects to login
- PDF parse errors: returned as empty fields in `FdoImportadoDto`, user fills them manually on confirmation screen

---

## Out of Scope (Future Specs)

- Container stowage visualization
- Multi-destination unloading priority (MKT role)
- Item catalog management (ADM screen)
- User management screen (ADM)
- Password change flow
