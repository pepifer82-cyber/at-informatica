namespace ATPrinterAgent;

public sealed class MainForm : Form
{
    private readonly AgentConfig _config = ConfigStore.Load();
    private readonly AgentClient _client = new();
    private readonly TextBox _serialBox = new();
    private readonly TextBox _endpointBox = new();
    private readonly CheckBox _snmpCheck = new();
    private readonly Label _statusLabel = new();
    private readonly Label _projectLabel = new();
    private readonly Label _countLabel = new();
    private readonly DataGridView _grid = new();
    private readonly Button _connectButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _sendButton = new();
    private readonly Button _saveButton = new();
    private System.Windows.Forms.Timer? _timer;
    private List<PrinterInfo> _printers = [];

    public MainForm()
    {
        Text = "AT Printer Agent v0.2";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1120, 720);
        Font = new Font("Segoe UI", 10);

        BuildUi();
        _ = RefreshPrintersAsync();
        _projectLabel.Text = _config.ProjectName;
        UpdateStatus("No conectado", false);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 125));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            Text = "A&T INFORMÁTICA  •  AT PRINTER AGENT",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        settings.Controls.Add(new Label { Text = "Serial del proyecto", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _serialBox.Dock = DockStyle.Fill;
        _serialBox.Text = _config.Serial;
        settings.Controls.Add(_serialBox, 1, 0);

        settings.Controls.Add(new Label { Text = "Endpoint", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        _endpointBox.Dock = DockStyle.Fill;
        _endpointBox.Text = _config.Endpoint;
        settings.Controls.Add(_endpointBox, 1, 1);

        _snmpCheck.Text = "Consultar SNMP en impresoras IP (contador / tóner cuando la impresora lo permita)";
        _snmpCheck.Checked = _config.EnableSnmp;
        _snmpCheck.AutoSize = true;
        settings.Controls.Add(new Label { Text = "Monitor", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        settings.Controls.Add(_snmpCheck, 1, 2);
        root.Controls.Add(settings, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        AddButton(actions, _connectButton, "Conectar proyecto", async () => await SyncAsync(false));
        AddButton(actions, _refreshButton, "Detectar impresoras", async () => await RefreshPrintersAsync());
        AddButton(actions, _sendButton, "Enviar ahora", async () => await SyncAsync(true));
        AddButton(actions, _saveButton, "Guardar configuración", () => SaveSettings());

        _statusLabel.AutoSize = true;
        _statusLabel.Padding = new Padding(15, 7, 0, 0);
        actions.Controls.Add(_statusLabel);
        root.Controls.Add(actions, 0, 2);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _projectLabel.AutoSize = true;
        _countLabel.AutoSize = true;
        var infoPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
        infoPanel.Controls.Add(new Label { Text = "Proyecto: ", AutoSize = true });
        infoPanel.Controls.Add(_projectLabel);
        infoPanel.Controls.Add(new Label { Text = "    Impresoras: ", AutoSize = true });
        infoPanel.Controls.Add(_countLabel);
        bottom.Controls.Add(infoPanel, 0, 0);

        ConfigureGrid();
        bottom.Controls.Add(_grid, 0, 1);
        root.Controls.Add(bottom, 0, 3);

        Controls.Add(root);
        FormClosing += (_, _) => _timer?.Stop();
    }

    private static void AddButton(FlowLayoutPanel panel, Button button, string text, Func<Task> action)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Click += async (_, _) =>
        {
            button.Enabled = false;
            try { await action(); }
            finally { button.Enabled = true; }
        };
        panel.Controls.Add(button);
    }

    private static void AddButton(FlowLayoutPanel panel, Button button, string text, Action action)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Impresora", DataPropertyName = nameof(PrinterInfo.DisplayName) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fabricante", DataPropertyName = nameof(PrinterInfo.Manufacturer) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modelo / Driver", DataPropertyName = nameof(PrinterInfo.Model) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Conexión", DataPropertyName = nameof(PrinterInfo.ConnectionType) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IP", DataPropertyName = nameof(PrinterInfo.IpAddress) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estado", DataPropertyName = nameof(PrinterInfo.LastStatus) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SNMP", DataPropertyName = nameof(PrinterInfo.SnmpReachable) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Contador", DataPropertyName = nameof(PrinterInfo.PageCount) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Negro %", DataPropertyName = nameof(PrinterInfo.TonerBlackPercent) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cian %", DataPropertyName = nameof(PrinterInfo.TonerCyanPercent) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Magenta %", DataPropertyName = nameof(PrinterInfo.TonerMagentaPercent) });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amarillo %", DataPropertyName = nameof(PrinterInfo.TonerYellowPercent) });
    }

    private void SaveSettings()
    {
        _config.Serial = _serialBox.Text.Trim();
        _config.Endpoint = _endpointBox.Text.Trim();
        _config.EnableSnmp = _snmpCheck.Checked;
        ConfigStore.Save(_config);
        UpdateStatus("Configuración guardada", true);
    }

    private async Task RefreshPrintersAsync()
    {
        try
        {
            _printers = PrinterDiscovery.GetInstalledPrinters();
            if (_config.EnableSnmp)
            {
                UpdateStatus("Consultando SNMP...", false);
                await SnmpPrinterMonitor.EnrichAsync(_printers, _config);
            }
            BindPrinters();
            UpdateStatus(string.IsNullOrWhiteSpace(_config.AgentCode) ? "No conectado" : "Listo", !string.IsNullOrWhiteSpace(_config.AgentCode));
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudieron detectar las impresoras.\n\n" + ex.Message, "AT Printer Agent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BindPrinters()
    {
        _grid.DataSource = null;
        _grid.DataSource = _printers;
        _countLabel.Text = _printers.Count.ToString();
    }

    private async Task SyncAsync(bool manual)
    {
        var serial = _serialBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serial))
        {
            MessageBox.Show("Ingresá el serial del proyecto.", "AT Printer Agent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SaveSettings();
        _connectButton.Enabled = false;
        _sendButton.Enabled = false;
        UpdateStatus(manual ? "Enviando..." : "Conectando...", false);

        try
        {
            await RefreshPrintersAsync();
            var response = await _client.SyncAsync(_config, _printers);
            if (response.Ok)
            {
                _config.ProjectName = response.ProjectName ?? _config.ProjectName;
                _config.AgentCode = response.AgentCode ?? _config.AgentCode;
                ConfigStore.Save(_config);
                _projectLabel.Text = _config.ProjectName;
                UpdateStatus("Conectado ✓", true);
                _sendButton.Enabled = true;
                StartTimer();
            }
            else
            {
                UpdateStatus(response.Error ?? "No autorizado", false);
                if (!manual)
                    MessageBox.Show(response.Error ?? "No se pudo conectar.", "AT Printer Agent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus("Sin conexión", false);
            if (!manual)
                MessageBox.Show("No se pudo enviar la información.\n\n" + ex.Message, "AT Printer Agent", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _connectButton.Enabled = true;
            _sendButton.Enabled = !string.IsNullOrWhiteSpace(_config.AgentCode);
        }
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new System.Windows.Forms.Timer { Interval = Math.Clamp(_config.SyncIntervalSeconds, 30, 3600) * 1000 };
        _timer.Tick += async (_, _) => await SyncBackgroundAsync();
        _timer.Start();
    }

    private async Task SyncBackgroundAsync()
    {
        try
        {
            _printers = PrinterDiscovery.GetInstalledPrinters();
            if (_config.EnableSnmp) await SnmpPrinterMonitor.EnrichAsync(_printers, _config);
            BindPrinters();
            var response = await _client.SyncAsync(_config, _printers);
            UpdateStatus(response.Ok ? "Conectado ✓" : "Sin conexión", response.Ok);
        }
        catch { UpdateStatus("Sin conexión", false); }
    }

    private void UpdateStatus(string text, bool connected)
    {
        _statusLabel.Text = text;
        _statusLabel.Font = new Font("Segoe UI", 10, connected ? FontStyle.Bold : FontStyle.Regular);
    }
}