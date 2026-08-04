using PalletBalancer.App.Controles;
using PalletBalancer.Core.Data;
using PalletBalancer.Core.Models;
using PalletBalancer.Core.Services;

namespace PalletBalancer.App;

public class FormPrincipal : Form
{
    private const string CadenaConexion = "Data Source=palletbalancer.db";
    private const string RutaConfig = "configuracion.json";

    private readonly CatalogoRepository _catalogo;
    private readonly CargaRepository _cargaRepo;
    private readonly CalculadoraPeso _calculadora;
    private readonly BalanceadorService _balanceador;
    private ConfiguracionCarga _config;
    private readonly List<Pallet> _pallets = new();
    private ResultadoBalanceo? _ultimoResultado;

    private TextBox _txtCodigo = null!;
    private ComboBox _cmbSku = null!;
    private NumericUpDown _nudCantidad = null!;
    private Button _btnAgregar = null!;
    private Button _btnBalancear = null!;
    private Button _btnGuardar = null!;
    private Button _btnCatalogo = null!;
    private DataGridView _dgvPallets = null!;
    private Label _lblResumen = null!;
    private ContenedorLayoutControl _layoutControl = null!;

    public FormPrincipal()
    {
        _config = ConfiguracionService.Cargar(RutaConfig);
        _catalogo = new CatalogoRepository(CadenaConexion);
        _cargaRepo = new CargaRepository(CadenaConexion);
        _calculadora = new CalculadoraPeso();
        _balanceador = new BalanceadorService();

        DbInitializer.Inicializar(CadenaConexion);

        InicializarComponentes();
        CargarComboSku();
    }

    private void InicializarComponentes()
    {
        Text = "PalletBalancer — Carga de contenedor";
        Size = new Size(1200, 700);
        MinimumSize = new Size(1100, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // ---- Panel superior ----
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(6) };

        var lblCodigo = new Label { Text = "Código:", AutoSize = true, Top = 19, Left = 6 };
        _txtCodigo = new TextBox { Width = 155, Top = 16, Left = 58 };
        _txtCodigo.KeyDown += TxtCodigo_KeyDown;

        var lblSku = new Label { Text = "SKU:", AutoSize = true, Top = 19, Left = 224 };
        _cmbSku = new ComboBox { Width = 145, Top = 16, Left = 254, DropDownStyle = ComboBoxStyle.DropDownList };

        var lblCant = new Label { Text = "Piezas:", AutoSize = true, Top = 19, Left = 410 };
        _nudCantidad = new NumericUpDown { Width = 70, Top = 16, Left = 458, Minimum = 1, Maximum = 9999, Value = 1 };

        _btnAgregar = new Button { Text = "Agregar pallet", Top = 14, Left = 538, Width = 110, Height = 28 };
        _btnAgregar.Click += BtnAgregar_Click;

        _btnBalancear = new Button { Text = "Balancear carga", Top = 14, Left = 658, Width = 122, Height = 28 };
        _btnBalancear.Click += BtnBalancear_Click;

        _btnGuardar = new Button { Text = "Guardar carga", Top = 14, Left = 790, Width = 110, Height = 28 };
        _btnGuardar.Enabled = false;
        _btnGuardar.Click += BtnGuardar_Click;

        _btnCatalogo = new Button { Text = "Catálogo de productos", Top = 14, Left = 910, Width = 160, Height = 28 };
        _btnCatalogo.Click += BtnCatalogo_Click;

        pnlTop.Controls.AddRange(new Control[]
        {
            lblCodigo, _txtCodigo,
            lblSku, _cmbSku,
            lblCant, _nudCantidad,
            _btnAgregar, _btnBalancear, _btnGuardar, _btnCatalogo
        });

        // ---- Label resumen (bottom) ----
        _lblResumen = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(8, 0, 0, 0),
            BorderStyle = BorderStyle.FixedSingle,
            Text = "Sin carga. Agregue pallets y presione «Balancear carga»."
        };

        // ---- Panel central ----
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 280,
            Panel2MinSize = 380
        };

        _dgvPallets = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window
        };
        _dgvPallets.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "#", Name = "col_num", FillWeight = 28 },
            new DataGridViewTextBoxColumn { HeaderText = "Código", Name = "col_codigo", FillWeight = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "SKU", Name = "col_sku", FillWeight = 85 },
            new DataGridViewTextBoxColumn { HeaderText = "Piezas", Name = "col_piezas", FillWeight = 58 },
            new DataGridViewTextBoxColumn { HeaderText = "Peso (kg)", Name = "col_peso", FillWeight = 68 },
            new DataGridViewTextBoxColumn { HeaderText = "Alto (cm)", Name = "col_alto", FillWeight = 65 }
        );
        _dgvPallets.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete) EliminarPalletSeleccionado();
        };

        var menuDgv = new ContextMenuStrip();
        menuDgv.Items.Add("Eliminar pallet").Click += (s, e) => EliminarPalletSeleccionado();
        _dgvPallets.ContextMenuStrip = menuDgv;

        split.Panel1.Controls.Add(_dgvPallets);

        _layoutControl = new ContenedorLayoutControl { Dock = DockStyle.Fill };
        split.Panel2.Controls.Add(_layoutControl);

        Load += (s, e) => split.SplitterDistance = 360;

        Controls.Add(split);
        Controls.Add(_lblResumen);
        Controls.Add(pnlTop);
    }

    private void TxtCodigo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            AgregarPalletDesdeUI();
        }
    }

    private void BtnAgregar_Click(object? sender, EventArgs e) => AgregarPalletDesdeUI();

    private void AgregarPalletDesdeUI()
    {
        string codigo = _txtCodigo.Text.Trim();
        if (string.IsNullOrEmpty(codigo))
        {
            MessageBox.Show("Ingrese un código de escaneo.", "Código vacío", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtCodigo.Focus();
            return;
        }

        string? sku = _cmbSku.SelectedItem as string;
        if (string.IsNullOrEmpty(sku))
        {
            MessageBox.Show("Seleccione un SKU del catálogo.", "SKU no seleccionado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var producto = _catalogo.ObtenerPorSku(sku);
        if (producto is null)
        {
            MessageBox.Show($"El SKU '{sku}' no existe en el catálogo.", "SKU no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int piezas = (int)_nudCantidad.Value;
        var pallet = _calculadora.ProcesarEscaneo(
            codigo, producto, piezas,
            _config.PalletEstandar.PesoPalletVacioKg,
            _config.PalletEstandar.PiezasPorPalletCompleto);

        _pallets.Add(pallet);
        RefrescarDgvPallets();
        LimpiarResultado();
        _txtCodigo.Clear();
        _nudCantidad.Value = 1;
        _txtCodigo.Focus();
    }

    private void BtnBalancear_Click(object? sender, EventArgs e)
    {
        if (_pallets.Count == 0)
        {
            MessageBox.Show("No hay pallets capturados.", "Sin pallets", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _ultimoResultado = _balanceador.Balancear(_pallets, _config);
        _layoutControl.MostrarResultado(_ultimoResultado, _config.Contenedor);
        ActualizarResumen(_ultimoResultado);
        _btnGuardar.Enabled = true;

        if (_ultimoResultado.Advertencias.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, _ultimoResultado.Advertencias),
                "Advertencias",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void BtnGuardar_Click(object? sender, EventArgs e)
    {
        if (_ultimoResultado is null) return;

        try
        {
            long id = _cargaRepo.GuardarCarga(_ultimoResultado, _config.Contenedor.Tipo);
            MessageBox.Show($"Carga guardada (ID: {id}).", "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _btnGuardar.Enabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnCatalogo_Click(object? sender, EventArgs e)
    {
        using var form = new FormCatalogo(CadenaConexion);
        form.ShowDialog(this);
        CargarComboSku();
    }

    private void EliminarPalletSeleccionado()
    {
        if (_dgvPallets.SelectedRows.Count == 0) return;
        int idx = _dgvPallets.SelectedRows[0].Index;
        if (idx < 0 || idx >= _pallets.Count) return;

        _pallets.RemoveAt(idx);
        RefrescarDgvPallets();
        LimpiarResultado();
    }

    private void RefrescarDgvPallets()
    {
        _dgvPallets.Rows.Clear();
        for (int i = 0; i < _pallets.Count; i++)
        {
            var p = _pallets[i];
            _dgvPallets.Rows.Add(i + 1, p.CodigoEscaneado, p.Sku, p.CantidadPiezas,
                p.PesoTotalKg.ToString("F2"), p.AltoCm.ToString("F1"));
        }
    }

    private void CargarComboSku()
    {
        string? selAnterior = _cmbSku.SelectedItem as string;
        _cmbSku.BeginUpdate();
        _cmbSku.Items.Clear();
        foreach (var p in _catalogo.ObtenerTodos())
            _cmbSku.Items.Add(p.Sku);
        _cmbSku.EndUpdate();

        if (selAnterior != null && _cmbSku.Items.Contains(selAnterior))
            _cmbSku.SelectedItem = selAnterior;
        else if (_cmbSku.Items.Count > 0)
            _cmbSku.SelectedIndex = 0;
    }

    private void LimpiarResultado()
    {
        _ultimoResultado = null;
        _btnGuardar.Enabled = false;
        _layoutControl.LimpiarLayout();
        ActualizarResumen(null);
    }

    private void ActualizarResumen(ResultadoBalanceo? r)
    {
        if (r is null)
        {
            _lblResumen.Text = $"Pallets capturados: {_pallets.Count}. Sin balanceo calculado.";
            _lblResumen.BackColor = SystemColors.Control;
        }
        else
        {
            string estado = r.DentroDeTolerancia ? "✓ DENTRO DE TOLERANCIA" : "✗ FUERA DE TOLERANCIA";
            _lblResumen.Text =
                $"Total: {r.PesoTotalKg:F1} kg  |  " +
                $"Izquierdo: {r.PesoLadoIzquierdoKg:F1} kg  |  " +
                $"Derecho: {r.PesoLadoDerechoKg:F1} kg  |  " +
                $"Diferencia: {r.DiferenciaPorcentual:F1}%  |  {estado}";
            _lblResumen.BackColor = r.DentroDeTolerancia
                ? Color.FromArgb(198, 239, 206)
                : Color.FromArgb(255, 199, 206);
        }
    }
}
