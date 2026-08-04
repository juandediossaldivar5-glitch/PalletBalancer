using PalletBalancer.Core.Data;
using PalletBalancer.Core.Models;

namespace PalletBalancer.App;

public class FormCatalogo : Form
{
    private readonly CatalogoRepository _catalogo;
    private List<Producto> _productos = new();

    private DataGridView _dgv = null!;
    private TextBox _txtSku = null!;
    private TextBox _txtNombre = null!;
    private NumericUpDown _nudPeso = null!;
    private NumericUpDown _nudLargo = null!;
    private NumericUpDown _nudAncho = null!;
    private NumericUpDown _nudAlto = null!;
    private Button _btnGuardar = null!;
    private Button _btnNuevo = null!;

    public FormCatalogo(string cadenaConexion)
    {
        _catalogo = new CatalogoRepository(cadenaConexion);
        InicializarComponentes();
        CargarGrid();
    }

    private void InicializarComponentes()
    {
        Text = "Catálogo de Productos";
        Size = new Size(860, 480);
        MinimumSize = new Size(720, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;

        // ---- Panel derecho con campos ----
        var pnlCampos = new Panel
        {
            Dock = DockStyle.Right,
            Width = 280,
            Padding = new Padding(10, 10, 10, 10),
            BorderStyle = BorderStyle.FixedSingle
        };

        int y = 12;
        int lblX = 10, fldX = 110, fldW = 150;

        pnlCampos.Controls.Add(MkLabel("SKU:", y, lblX));
        _txtSku = new TextBox { Top = y - 1, Left = fldX, Width = fldW };
        pnlCampos.Controls.Add(_txtSku);
        y += 30;

        pnlCampos.Controls.Add(MkLabel("Nombre:", y, lblX));
        _txtNombre = new TextBox { Top = y - 1, Left = fldX, Width = fldW };
        pnlCampos.Controls.Add(_txtNombre);
        y += 30;

        pnlCampos.Controls.Add(MkLabel("Peso (kg):", y, lblX));
        _nudPeso = MkNud(y, 0, 99999m, 3); pnlCampos.Controls.Add(_nudPeso);
        y += 30;

        pnlCampos.Controls.Add(MkLabel("Largo (cm):", y, lblX));
        _nudLargo = MkNud(y, 0, 9999m, 1); pnlCampos.Controls.Add(_nudLargo);
        y += 30;

        pnlCampos.Controls.Add(MkLabel("Ancho (cm):", y, lblX));
        _nudAncho = MkNud(y, 0, 9999m, 1); pnlCampos.Controls.Add(_nudAncho);
        y += 30;

        pnlCampos.Controls.Add(MkLabel("Alto (cm):", y, lblX));
        _nudAlto = MkNud(y, 0, 9999m, 1); pnlCampos.Controls.Add(_nudAlto);
        y += 42;

        _btnGuardar = new Button { Text = "Guardar", Top = y, Left = fldX, Width = 70, Height = 28 };
        _btnGuardar.Click += BtnGuardar_Click;
        pnlCampos.Controls.Add(_btnGuardar);

        _btnNuevo = new Button { Text = "Nuevo", Top = y, Left = fldX + 76, Width = 70, Height = 28 };
        _btnNuevo.Click += (s, e) => LimpiarCampos();
        pnlCampos.Controls.Add(_btnNuevo);

        Label MkLabel(string txt, int top, int left) =>
            new() { Text = txt, AutoSize = true, Top = top + 2, Left = left };

        NumericUpDown MkNud(int top, decimal min, decimal max, int dec) =>
            new() { Top = top - 1, Left = fldX, Width = fldW, Minimum = min, Maximum = max, DecimalPlaces = dec };

        // ---- Grid ----
        _dgv = new DataGridView
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
        _dgv.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "SKU", Name = "col_sku", FillWeight = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Nombre", Name = "col_nombre", FillWeight = 140 },
            new DataGridViewTextBoxColumn { HeaderText = "Peso kg", Name = "col_peso", FillWeight = 60 },
            new DataGridViewTextBoxColumn { HeaderText = "Largo", Name = "col_largo", FillWeight = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Ancho", Name = "col_ancho", FillWeight = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Alto", Name = "col_alto", FillWeight = 50 }
        );
        _dgv.SelectionChanged += DgvSelectionChanged;

        Controls.Add(_dgv);
        Controls.Add(pnlCampos);
    }

    private void DgvSelectionChanged(object? sender, EventArgs e)
    {
        if (_dgv.SelectedRows.Count == 0) return;
        int idx = _dgv.SelectedRows[0].Index;
        if (idx < 0 || idx >= _productos.Count) return;

        var p = _productos[idx];
        _txtSku.Text = p.Sku;
        _txtSku.ReadOnly = true;
        _txtNombre.Text = p.Nombre;
        _nudPeso.Value = (decimal)p.PesoUnitarioKg;
        _nudLargo.Value = (decimal)p.LargoCm;
        _nudAncho.Value = (decimal)p.AnchoCm;
        _nudAlto.Value = (decimal)p.AltoCm;
    }

    private void BtnGuardar_Click(object? sender, EventArgs e)
    {
        string sku = _txtSku.Text.Trim().ToUpperInvariant();
        string nombre = _txtNombre.Text.Trim();

        if (string.IsNullOrEmpty(sku) || string.IsNullOrEmpty(nombre))
        {
            MessageBox.Show("SKU y Nombre son obligatorios.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _catalogo.GuardarOActualizar(new Producto
        {
            Sku = sku,
            Nombre = nombre,
            PesoUnitarioKg = (double)_nudPeso.Value,
            LargoCm = (double)_nudLargo.Value,
            AnchoCm = (double)_nudAncho.Value,
            AltoCm = (double)_nudAlto.Value
        });

        LimpiarCampos();
        CargarGrid();
    }

    private void LimpiarCampos()
    {
        _txtSku.Text = "";
        _txtSku.ReadOnly = false;
        _txtNombre.Text = "";
        _nudPeso.Value = 0;
        _nudLargo.Value = 0;
        _nudAncho.Value = 0;
        _nudAlto.Value = 0;
        _dgv.ClearSelection();
        _txtSku.Focus();
    }

    private void CargarGrid()
    {
        _productos = _catalogo.ObtenerTodos();
        _dgv.Rows.Clear();
        foreach (var p in _productos)
        {
            _dgv.Rows.Add(
                p.Sku,
                p.Nombre,
                p.PesoUnitarioKg.ToString("F3"),
                p.LargoCm.ToString("F1"),
                p.AnchoCm.ToString("F1"),
                p.AltoCm.ToString("F1"));
        }
    }
}
