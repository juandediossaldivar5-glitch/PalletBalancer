# Prompt para continuar en Claude Code

Copia todo el bloque de abajo y pégalo como primer mensaje en Claude Code, parado en la raíz de la carpeta `PalletBalancer/`.

---

Estoy retomando un proyecto .NET ya iniciado. Contexto: es un sistema interno para una empresa que cruza carga por frontera México-EE.UU. El requisito del cruce es que el peso dentro del contenedor venga **balanceado** (lateral y longitudinalmente). El flujo es: se escanea/captura cada pallet (SKU + cantidad de piezas), el sistema calcula su peso contra un catálogo de productos, y luego arma un layout de acomodo mostrando ambos lados (izquierdo/derecho) del contenedor balanceados en peso.

## Stack y arquitectura

- .NET 8, solución con 3 proyectos:
  - `src/PalletBalancer.Core` — class library (net8.0) con toda la lógica de negocio, sin dependencia de UI. Usa `Microsoft.Data.Sqlite` directo (sin ORM) para persistencia.
  - `src/PalletBalancer.App` — WinForms (net8.0-windows). Es la UI que usará el personal en planta/almacén (empresa usa Windows).
  - `tests/PalletBalancer.Core.Tests` — xUnit, prueba solo el Core.
- Nombres de dominio en español (Producto, Pallet, Contenedor, PosicionPallet), estructura/namespaces en inglés estándar .NET. Mantener esa convención.
- No usar EF Core ni frameworks adicionales sin que yo lo pida — mantenlo simple (Sqlite crudo, WinForms nativo).

## Ya está construido (Core completo)

- `Models/`: `Producto`, `Pallet`, `Contenedor`, `PosicionPallet`, `ResultadoBalanceo`, `LadoContenedor` (enum).
- `Services/CalculadoraPeso.cs`: calcula peso de un pallet = piezas × peso unitario del SKU + peso de pallet vacío. Método `ProcesarEscaneo` arma el `Pallet` completo desde un código escaneado.
- `Services/BalanceadorService.cs`: algoritmo de balanceo. Es un greedy en dos pasos:
  1. Reparte pallets entre lado izquierdo/derecho: ordena por peso descendente, en cada paso asigna el pallet más pesado restante al lado con menor peso acumulado.
  2. Dentro de cada lado, ordena las filas de modo que los pallets más pesados queden en las posiciones centrales del contenedor (longitudinalmente) y los más ligeros hacia cabina/puertas.
  - Tolerancia de diferencia entre lados configurable (default 5%). Genera advertencias si: no hay pallets, exceden las posiciones disponibles, o el peso total excede la capacidad del contenedor.
  - **Importante**: no existe una normativa pública única y exacta de "peso balanceado" para cruce fronterizo (lo investigué). Lo que la empresa pidió fue "básicamente centrado", así que implementé el criterio general de la industria del transporte (lateral 50/50 dentro de tolerancia + longitudinal centrado). Si más adelante me pasan una regla exacta (ej. % por eje, normativa específica del transportista), ese es el lugar del código a ajustar.
- `Data/`: `DbInitializer` (crea tablas SQLite: `Producto`, `Carga`, `CargaPallet`), `CatalogoRepository` (CRUD de productos), `CargaRepository` (guarda el resultado de un balanceo).
- Tests unitarios completos para `CalculadoraPeso` y `BalanceadorService` (casos: pesos iguales, desiguales, exceso de capacidad, exceso de posiciones, centrado del pallet más pesado).

**Nota**: este Core se escribió sin poder compilarlo (el entorno donde lo generé no tenía SDK de .NET disponible por restricciones de red). Antes que nada, corre `dotnet build` y `dotnet test` sobre la solución y corrige cualquier error de compilación que aparezca — es el primer paso obligatorio.

## Pendiente por construir (WinForms App)

El proyecto `PalletBalancer.App` solo tiene el `.csproj` (referencia a Core, `net8.0-windows`, `UseWindowsForms=true`). Falta todo el código. Constrúyelo así:

1. **`Program.cs`**: entry point estándar (`ApplicationConfiguration.Initialize()` + `Application.Run(new FormPrincipal())`).
2. **`FormPrincipal.cs`** (código puro, sin designer separado — así lo veníamos haciendo):
   - Panel superior: textbox para código escaneado (recibe Enter automático de lectores tipo teclado-wedge de QR/código de barras), combo de SKU, cantidad de piezas, botón "Agregar pallet", botón "Balancear carga", botón "Guardar carga", botón "Catálogo de productos".
   - `DataGridView` a la izquierda listando los pallets capturados (código, SKU, piezas, peso).
   - Label inferior con resumen: peso total, peso izquierdo, peso derecho, % diferencia, si está dentro de tolerancia.
   - Al centro/derecha: un `UserControl` custom (`Controles/ContenedorLayoutControl.cs`) que dibuja con GDI+ dos carriles horizontales (IZQUIERDO arriba, DERECHO abajo), cada uno con N celdas = `Contenedor.FilasDisponibles`, coloreadas por intensidad de peso (más pesado = más rojo), con SKU y peso dentro de cada celda ocupada. Etiquetar "← Frente (cabina)" y "Puertas (fondo) →".
   - Usa `_catalogoRepository.ObtenerPorSku` para validar el SKU antes de agregar un pallet (si no existe, avisar y no dejar agregar).
   - Conexión SQLite: `Data Source=palletbalancer.db`, inicializar con `DbInitializer.Inicializar` al arrancar el form.
3. **`FormCatalogo.cs`**: diálogo simple para dar de alta/editar productos del catálogo (SKU, nombre, peso unitario, largo/ancho/alto). Grid con los productos existentes. Se abre desde el botón "Catálogo de productos" de `FormPrincipal` y al cerrarse refresca el combo de SKU.
4. **`Controles/ContenedorLayoutControl.cs`**: el `UserControl` de dibujo descrito arriba.

## Decisiones abiertas que el usuario (dueño del proyecto) todavía no cerró — no las inventes, pregúntale antes de construir esa parte si no está en este prompt

- **Método de captura**: puede ser escaneo de QR/código de barras, o importar un PDF (packing list/factura) — el usuario no decidió aún y dijo que el PDF puede no tener formato fijo. Por ahora la UI soporta captura por escaneo/manual (ya cubierto arriba). Si te pide avanzar en importación de PDF, primero pregúntale si el formato de PDF es consistente entre proveedores antes de construir un parser, porque de eso depende si conviene un parser fijo o una revisión manual post-importación.
- **Salida del layout**: por ahora solo se construyó la vista 2D en pantalla dentro de WinForms. No se ha construido reporte PDF imprimible ni exportación a Excel — pregúntale si los quiere antes de construirlos, ya que no confirmó esa parte.
- **Regla exacta de balanceo**: el usuario no tiene una regla numérica oficial de la aduana/transportista, solo "centrado". El algoritmo actual es una interpretación razonable de práctica estándar de la industria (ver nota arriba). Si en algún momento te comparte números concretos (ej. % máximo de diferencia, reglas por eje), ajusta `BalanceadorService` y sus tests.

## Estilo de trabajo esperado (preferencias del usuario)

- Sé directo, técnico, sin explicaciones innecesarias.
- No refactorices código existente sin que se pida.
- No agregues frameworks/paquetes nuevos sin justificarlo o preguntarlo.
- Si falta información para tomar una decisión de producto (no técnica), pregunta — no asumas.
- Al terminar cada pieza, corre `dotnet build` y `dotnet test` para verificar antes de dar por hecho que algo funciona.

---
