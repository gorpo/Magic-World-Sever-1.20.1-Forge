using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Text;
using System.Windows.Forms;

internal static class MagicWorldServerLauncherApp
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            LogFatal("UI exception", e.Exception);
            MessageBox.Show(
                "O launcher encontrou um erro, mas vai continuar aberto. Veja logs\\server-launcher.log.",
                "Magic World Server Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
        {
            LogFatal("Fatal exception", e.ExceptionObject as Exception);
        };
        Application.Run(new LauncherForm());
    }

    private static void LogFatal(string title, Exception ex)
    {
        try
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "server-launcher.log"),
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + title + ": " + (ex == null ? "unknown" : ex.ToString()) + Environment.NewLine,
                Encoding.UTF8
            );
        }
        catch
        {
        }
    }
}

internal sealed class LauncherForm : Form
{
    private readonly string serverRoot = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
    private readonly string assetsRoot;
    private readonly string iconPath;
    private readonly string backgroundPath;
    private readonly string logoPath;
    private readonly string latestLogPath;
    private readonly string launcherLogPath;
    private readonly string forgeArgsPath;
    private readonly string jvmArgsPath;
    private readonly string worldPath;
    private readonly string backupsPath;
    private readonly string toolsRoot;
    private readonly string playitRoot;
    private readonly string playitExePath;
    private readonly string playitMsiPath;
    private readonly string playitLogPath;
    private readonly Timer timer;

    private Process serverProcess;
    private Process tunnelProcess;
    private string lastPlayitLink = "";
    private string lastPlayitAddress = "";
    private string cachedPlayitStatusText = "";
    private DateTime lastPlayitStatusRefresh = DateTime.MinValue;
    private string cachedJavaCommand = "";
    private string cachedJavaText = "";
    private Label statusLabel;
    private Label detailsLabel;
    private Label playersSummaryLabel;
    private ListBox playersListBox;
    private NumericUpDown minRam;
    private NumericUpDown maxRam;
    private RichTextBox logBox;
    private RichTextBox playitStatusBox;
    private RichTextBox playitLogBox;
    private TextBox commandBox;
    private Button sendCommandButton;
    private Button startButton;
    private Button stopButton;
    private Button restartButton;
    private long lastShownLogLength = -1;
    private long lastShownPlayitLogLength = -1;
    private DateTime lastPlayerListRequest = DateTime.MinValue;
    private bool closingServer;
    private List<ItemEntry> itemCatalog;
    private const int MaxVisibleItems = 250;

    private const int ServerPort = 25565;

    public LauncherForm()
    {
        assetsRoot = Path.Combine(serverRoot, "launcher-assets");
        iconPath = Path.Combine(assetsRoot, "magicworld.ico");
        backgroundPath = Path.Combine(assetsRoot, "title_background_static.png");
        logoPath = Path.Combine(assetsRoot, "title_logo.png");
        latestLogPath = Path.Combine(serverRoot, @"logs\latest.log");
        launcherLogPath = Path.Combine(serverRoot, @"logs\server-launcher.log");
        forgeArgsPath = Path.Combine(serverRoot, @"libraries\net\minecraftforge\forge\1.20.1-47.4.20\win_args.txt");
        jvmArgsPath = Path.Combine(serverRoot, "user_jvm_args.txt");
        worldPath = Path.Combine(serverRoot, "world");
        backupsPath = Path.Combine(serverRoot, "backups");
        toolsRoot = Path.Combine(serverRoot, "tools");
        playitRoot = Path.Combine(toolsRoot, "playit");
        playitExePath = Path.Combine(playitRoot, "playit.exe");
        playitMsiPath = Path.Combine(playitRoot, "playit-installer.msi");
        playitLogPath = Path.Combine(serverRoot, @"logs\playit.log");
        timer = new Timer { Interval = 2500 };
        timer.Tick += delegate { SafeRefreshUi(); };

        BuildWindow();
        statusLabel.Text = "Status: pronto";
        detailsLabel.Text = "Clique em Iniciar para ligar o servidor.";
        logBox.Text = "Servidor parado. Os logs ao vivo aparecem aqui quando iniciar.";
        playitStatusBox.Text = "Playit: aguardando.";
        playitLogBox.Text = "Playit parado. Os logs do tunel aparecem aqui.";
        Shown += delegate
        {
            SafeRefreshUi();
            timer.Start();
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
        StopPlayitTunnelForExit();
        StopServerForExit();
        base.OnFormClosing(e);
    }

    private void BuildWindow()
    {
        Text = "Magic World Server Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 680);
        Size = new Size(1180, 720);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(7, 18, 32);

        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        var root = new Panel { Dock = DockStyle.Fill };
        if (File.Exists(backgroundPath))
        {
            root.BackgroundImage = Image.FromFile(backgroundPath);
            root.BackgroundImageLayout = ImageLayout.Stretch;
        }
        Controls.Add(root);

        var shade = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(132, 0, 10, 20)
        };
        root.Controls.Add(shade);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(22),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shade.Controls.Add(layout);

        var leftScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(60, 3, 12, 22),
            Padding = new Padding(14)
        };
        layout.Controls.Add(leftScroll, 0, 0);

        var left = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = 285,
            BackColor = Color.Transparent
        };
        leftScroll.Controls.Add(left);

        if (File.Exists(logoPath))
        {
            left.Controls.Add(new PictureBox
            {
                Image = Image.FromFile(logoPath),
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 270,
                Height = 92,
                Margin = new Padding(0, 0, 0, 10)
            });
        }

        left.Controls.Add(Header("Painel do servidor"));

        var statusPanel = BoxPanel(270, 132);
        statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 92, 111)
        };
        detailsLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F),
            Padding = new Padding(0, 6, 0, 0)
        };
        statusPanel.Controls.Add(detailsLabel);
        statusPanel.Controls.Add(statusLabel);
        left.Controls.Add(statusPanel);

        var playersPanel = BoxPanel(270, 224);
        var playersTitle = SmallTitle("Jogadores online");
        playersTitle.Location = new Point(12, 9);
        playersPanel.Controls.Add(playersTitle);
        playersSummaryLabel = new Label
        {
            AutoSize = false,
            Location = new Point(12, 30),
            Size = new Size(244, 18),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Text = "Nenhum jogador detectado."
        };
        playersPanel.Controls.Add(playersSummaryLabel);
        playersListBox = new ListBox
        {
            Location = new Point(12, 52),
            Size = new Size(244, 60),
            BackColor = Color.FromArgb(3, 6, 12),
            ForeColor = Color.FromArgb(220, 238, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9F)
        };
        playersPanel.Controls.Add(playersListBox);
        var opButton = SmallActionButton("OP", 12, 122, delegate { SendPlayerCommand("op {0}"); });
        var kickButton = SmallActionButton("Kick", 94, 122, delegate { SendPlayerCommand("kick {0}"); });
        var banButton = SmallActionButton("Ban", 176, 122, delegate { SendPlayerCommand("ban {0}"); });
        var creativeButton = SmallActionButton("Criativo", 12, 158, delegate { SendPlayerCommand("gamemode creative {0}"); });
        var survivalButton = SmallActionButton("Survival", 94, 158, delegate { SendPlayerCommand("gamemode survival {0}"); });
        var teleportButton = SmallActionButton("Teleportar", 176, 158, delegate { TeleportSelectedPlayer(); });
        playersPanel.Controls.Add(opButton);
        playersPanel.Controls.Add(kickButton);
        playersPanel.Controls.Add(banButton);
        playersPanel.Controls.Add(creativeButton);
        playersPanel.Controls.Add(survivalButton);
        playersPanel.Controls.Add(teleportButton);
        left.Controls.Add(playersPanel);

        left.Controls.Add(SectionTitle("1. Servidor Minecraft"));

        startButton = ActionButton("Iniciar servidor", true);
        startButton.Click += delegate { StartServer(); };
        left.Controls.Add(startButton);

        stopButton = ActionButton("Parar servidor", false);
        stopButton.Click += delegate { StopServer(); };
        left.Controls.Add(stopButton);

        restartButton = ActionButton("Reiniciar servidor", false);
        restartButton.Click += delegate { RestartServer(); };
        left.Controls.Add(restartButton);

        left.Controls.Add(SectionTitle("2. Tunel externo"));
        left.Controls.Add(ActionButton("Iniciar tunel Playit", true, delegate { StartPlayitTunnel(); SafeRefreshUi(); }));
        left.Controls.Add(ActionButton("Parar tunel Playit", false, delegate { StopPlayitTunnel(); SafeRefreshUi(); }));
        left.Controls.Add(ActionButton("Copiar endereco publico", false, delegate { CopyPlayitAddressOrOpenPanel(); SafeRefreshUi(); }));
        left.Controls.Add(ActionButton("Configurar tunel", false, delegate { OpenTunnelSettings(); }));

        left.Controls.Add(SectionTitle("3. Configuracoes"));

        var ramPanel = BoxPanel(270, 78);
        var ramTitle = SmallTitle("RAM do servidor");
        ramTitle.Location = new Point(12, 9);
        ramPanel.Controls.Add(ramTitle);
        ramPanel.Controls.Add(SmallLabel("Min", 14, 43));
        ramPanel.Controls.Add(SmallLabel("Max", 105, 43));
        minRam = RamBox(48, 39);
        maxRam = RamBox(139, 39);
        ramPanel.Controls.Add(minRam);
        ramPanel.Controls.Add(maxRam);
        ramPanel.Controls.Add(SmallLabel("GB", 198, 43));
        LoadRam();
        left.Controls.Add(ramPanel);
        left.Controls.Add(ActionButton("Editar server.properties", false, delegate { OpenFile(Path.Combine(serverRoot, "server.properties")); }));
        left.Controls.Add(ActionButton("Abrir config do servidor", false, delegate { OpenPath(Path.Combine(serverRoot, "config")); }));
        left.Controls.Add(ActionButton("Config do tunel Playit", false, delegate { OpenTunnelSettings(); }));

        left.Controls.Add(SectionTitle("4. Ferramentas"));
        left.Controls.Add(ActionButton("Comandos rapidos", false, delegate { OpenQuickCommands(); }));
        left.Controls.Add(ActionButton("Backup mundo", false, delegate { BackupWorld(); }));
        left.Controls.Add(ActionButton("Abrir pasta de mods", false, delegate { OpenPath(Path.Combine(serverRoot, "mods")); }));
        left.Controls.Add(ActionButton("Abrir pasta de logs", false, delegate { OpenPath(Path.Combine(serverRoot, "logs")); }));
        left.Controls.Add(ActionButton("Pasta servidor", false, delegate { OpenPath(serverRoot); }));
        left.Controls.Add(ActionButton("Editar EULA", false, delegate { OpenFile(Path.Combine(serverRoot, "eula.txt")); }));

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 0, 0, 0)
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.Controls.Add(right, 1, 0);

        var title = new Label
        {
            Text = "Logs ao vivo",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold)
        };
        right.Controls.Add(title, 0, 0);

        var logSplit = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        logSplit.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        logSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        logSplit.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        logSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        logSplit.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        logSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        right.Controls.Add(logSplit, 0, 1);

        logSplit.Controls.Add(new Label
        {
            Text = "Servidor",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        }, 0, 0);

        logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(3, 6, 12),
            ForeColor = Color.FromArgb(220, 238, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9F),
            ReadOnly = true,
            WordWrap = false,
            HideSelection = false
        };
        logSplit.Controls.Add(logBox, 0, 1);

        logSplit.Controls.Add(new Label
        {
            Text = "Playit status",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        }, 0, 2);

        playitStatusBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(3, 6, 12),
            ForeColor = Color.FromArgb(220, 238, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9F),
            ReadOnly = true,
            WordWrap = false,
            HideSelection = false
        };
        logSplit.Controls.Add(playitStatusBox, 0, 3);

        logSplit.Controls.Add(new Label
        {
            Text = "Playit logs",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        }, 0, 4);

        playitLogBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(3, 6, 12),
            ForeColor = Color.FromArgb(220, 238, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9F),
            ReadOnly = true,
            WordWrap = false,
            HideSelection = false
        };
        logSplit.Controls.Add(playitLogBox, 0, 5);

        var commandPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 10, 0, 0)
        };
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        right.Controls.Add(commandPanel, 0, 2);

        commandBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(6, 12, 22),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10F),
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.CustomSource,
            AutoCompleteCustomSource = BuildCommandSuggestions()
        };
        commandBox.KeyDown += delegate(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendServerCommand();
            }
        };
        commandPanel.Controls.Add(commandBox, 0, 0);

        sendCommandButton = ActionButton("➤", true);
        sendCommandButton.Dock = DockStyle.Fill;
        sendCommandButton.Width = 120;
        sendCommandButton.Height = 30;
        sendCommandButton.Margin = new Padding(10, 0, 0, 0);
        sendCommandButton.Click += delegate { SendServerCommand(); };
        commandPanel.Controls.Add(sendCommandButton, 1, 0);
    }

    private Label Header(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Width = 270,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
    }

    private Label SectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Width = 270,
            Height = 25,
            ForeColor = Color.FromArgb(245, 196, 45),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 8, 0, 6)
        };
    }

    private Panel BoxPanel(int width, int height)
    {
        return new Panel
        {
            Width = width,
            Height = height,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.FromArgb(138, 4, 16, 28),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private Label SmallTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(245, 196, 45),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
    }

    private Label SmallLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
    }

    private NumericUpDown RamBox(int x, int y)
    {
        return new NumericUpDown
        {
            Minimum = 1,
            Maximum = 16,
            Value = 4,
            Location = new Point(x, y),
            Size = new Size(48, 24),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private Button ActionButton(string text, bool primary)
    {
        return ActionButton(text, primary, null);
    }

    private Button ActionButton(string text, bool primary, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            Width = 270,
            Height = 39,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(213, 168, 28);
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = primary ? Color.FromArgb(216, 166, 26) : Color.FromArgb(26, 36, 48);
        button.ForeColor = primary ? Color.White : Color.FromArgb(236, 245, 255);
        if (click != null)
        {
            button.Click += click;
        }
        return button;
    }

    private Button SmallActionButton(string text, int x, int y, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(76, 30),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            BackColor = Color.FromArgb(26, 36, 48),
            ForeColor = Color.FromArgb(236, 245, 255)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(213, 168, 28);
        button.FlatAppearance.BorderSize = 1;
        if (click != null)
        {
            button.Click += click;
        }
        return button;
    }

    private void LoadRam()
    {
        int min = 2;
        int max = 4;
        if (File.Exists(jvmArgsPath))
        {
            foreach (string line in File.ReadAllLines(jvmArgsPath))
            {
                if (line.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) && line.EndsWith("G", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(4, line.Length - 5), out min);
                }
                if (line.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) && line.EndsWith("G", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(4, line.Length - 5), out max);
                }
            }
        }
        minRam.Value = Math.Max(1, Math.Min(16, min));
        maxRam.Value = Math.Max(minRam.Value, Math.Min(16, max));
    }

    private void SaveRam()
    {
        int min = (int)minRam.Value;
        int max = Math.Max(min, (int)maxRam.Value);
        maxRam.Value = max;
        File.WriteAllLines(jvmArgsPath, new[]
        {
            "# Xmx and Xms set the maximum and minimum RAM usage.",
            "# Adjust these values if the machine has little RAM available.",
            "-Xms" + min + "G",
            "-Xmx" + max + "G"
        }, Encoding.ASCII);
    }

    private void RefreshUi()
    {
        Process process = GetServerProcess();
        bool running = process != null && !process.HasExited;
        bool portOpen = IsPortOpen(ServerPort);
        statusLabel.Text = running ? "Status: rodando" : "Status: parado";
        statusLabel.ForeColor = running ? Color.FromArgb(88, 235, 151) : Color.FromArgb(255, 92, 111);

        RequestPlayerListForStatus(running);
        List<string> players = GetOnlinePlayersFromLogs();
        UpdatePlayersPanel(running, players);

        string portText = "Porta " + ServerPort + ": " + (portOpen ? "aberta" : "fechada");
        string uptimeText = "Uptime: " + (running ? FormatUptime(process) : "0s");
        string ramText = "RAM: " + (running ? FormatMemory(process.WorkingSet64) : "0 MB");
        string playersText = "Players: " + (running ? players.Count.ToString() : "0");
        string javaText = "Java: " + ShortJavaText();
        detailsLabel.Text = portText + Environment.NewLine + uptimeText + Environment.NewLine + ramText + Environment.NewLine + playersText + Environment.NewLine + javaText;

        startButton.Enabled = !running;
        stopButton.Enabled = running;
        restartButton.Enabled = running;
        if (sendCommandButton != null)
        {
            sendCommandButton.Enabled = running && serverProcess != null && !serverProcess.HasExited;
        }
        stopButton.BackColor = running ? Color.FromArgb(26, 36, 48) : Color.FromArgb(220, 220, 220);
        restartButton.BackColor = running ? Color.FromArgb(26, 36, 48) : Color.FromArgb(220, 220, 220);
        stopButton.ForeColor = running ? Color.FromArgb(236, 245, 255) : Color.FromArgb(170, 170, 170);
        restartButton.ForeColor = running ? Color.FromArgb(236, 245, 255) : Color.FromArgb(170, 170, 170);
        if (sendCommandButton != null)
        {
            sendCommandButton.BackColor = sendCommandButton.Enabled ? Color.FromArgb(216, 166, 26) : Color.FromArgb(220, 220, 220);
            sendCommandButton.ForeColor = sendCommandButton.Enabled ? Color.White : Color.FromArgb(170, 170, 170);
        }

        RefreshLogs(running);
        RefreshPlayitPanel();
    }

    private void RequestPlayerListForStatus(bool running)
    {
        if (!running || serverProcess == null || serverProcess.HasExited)
        {
            return;
        }
        if ((DateTime.Now - lastPlayerListRequest).TotalSeconds < 15)
        {
            return;
        }

        try
        {
            serverProcess.StandardInput.WriteLine("list");
            serverProcess.StandardInput.Flush();
            lastPlayerListRequest = DateTime.Now;
        }
        catch
        {
        }
    }

    private void UpdatePlayersPanel(bool running, List<string> players)
    {
        if (playersSummaryLabel == null || playersListBox == null)
        {
            return;
        }

        playersSummaryLabel.Text = running
            ? players.Count + (players.Count == 1 ? " jogador online" : " jogadores online")
            : "Servidor parado.";

        playersListBox.BeginUpdate();
        try
        {
            playersListBox.Items.Clear();
            if (running && players.Count > 0)
            {
                foreach (string player in players)
                {
                    playersListBox.Items.Add(player);
                }
            }
            else
            {
                playersListBox.Items.Add(running ? "Aguardando jogadores..." : "Nenhum jogador.");
            }
        }
        finally
        {
            playersListBox.EndUpdate();
        }
    }

    private List<string> GetOnlinePlayersFromLogs()
    {
        var players = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in new[] { latestLogPath, launcherLogPath })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            string text = ReadTail(path, 500);
            foreach (string raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                Match listMatch = Regex.Match(line, @"There are \d+ of a max of \d+ players online:\s*(.*)$", RegexOptions.IgnoreCase);
                if (listMatch.Success)
                {
                    players.Clear();
                    string names = listMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(names))
                    {
                        foreach (string name in names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string clean = name.Trim();
                            if (IsMinecraftPlayerName(clean))
                            {
                                players.Add(clean);
                            }
                        }
                    }
                    continue;
                }

                Match joined = Regex.Match(line, @"\]:\s*([A-Za-z0-9_]{1,16}) joined the game", RegexOptions.IgnoreCase);
                if (joined.Success)
                {
                    players.Add(joined.Groups[1].Value);
                    continue;
                }

                Match left = Regex.Match(line, @"\]:\s*([A-Za-z0-9_]{1,16}) left the game", RegexOptions.IgnoreCase);
                if (left.Success)
                {
                    players.Remove(left.Groups[1].Value);
                }
            }
        }

        return players.OrderBy(player => player, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool IsMinecraftPlayerName(string value)
    {
        return Regex.IsMatch(value ?? "", @"^[A-Za-z0-9_]{1,16}$");
    }

    private string FormatUptime(Process process)
    {
        try
        {
            TimeSpan uptime = DateTime.Now - process.StartTime;
            if (uptime.TotalDays >= 1)
            {
                return ((int)uptime.TotalDays) + "d " + uptime.Hours + "h " + uptime.Minutes + "m";
            }
            if (uptime.TotalHours >= 1)
            {
                return ((int)uptime.TotalHours) + "h " + uptime.Minutes + "m";
            }
            if (uptime.TotalMinutes >= 1)
            {
                return ((int)uptime.TotalMinutes) + "m " + uptime.Seconds + "s";
            }
            return Math.Max(0, uptime.Seconds) + "s";
        }
        catch
        {
            return "desconhecido";
        }
    }

    private string FormatMemory(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return (bytes / 1024d / 1024d / 1024d).ToString("0.0") + " GB";
        }
        return Math.Max(0, bytes / 1024L / 1024L) + " MB";
    }

    private void SafeRefreshUi()
    {
        try
        {
            RefreshUi();
        }
        catch (Exception ex)
        {
            AppendLauncherLog("Refresh failed but launcher stayed open: " + ex.Message);
        }
    }

    private void RefreshLogs(bool running)
    {
        if (!running && (serverProcess == null || serverProcess.HasExited))
        {
            if (logBox.TextLength == 0 || logBox.Text.StartsWith("["))
            {
                logBox.Text = "Servidor parado. Os logs ao vivo aparecem aqui quando iniciar.";
            }
            lastShownLogLength = -1;
            return;
        }

        string source = File.Exists(latestLogPath) ? latestLogPath : launcherLogPath;
        if (!File.Exists(source))
        {
            logBox.Text = "Sem logs ainda.";
            return;
        }

        var info = new FileInfo(source);
        if (info.Length == lastShownLogLength)
        {
            return;
        }
        lastShownLogLength = info.Length;

        string text = ReadTail(source, 90);
        logBox.Text = text;
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }

    private void RefreshPlayitPanel()
    {
        RefreshPlayitStatus();
        RefreshPlayitLog();
    }

    private void RefreshPlayitStatus()
    {
        if (playitStatusBox == null)
        {
            return;
        }

        string playit = GetInstalledPlayitCli();
        if (string.IsNullOrWhiteSpace(playit) || !File.Exists(playit))
        {
            playitStatusBox.Text = "Playit nao instalado.\nUse Tunel externo > Baixar agente > Instalar agente > Login Playit.";
            return;
        }

        if ((DateTime.Now - lastPlayitStatusRefresh).TotalSeconds > 6 || string.IsNullOrWhiteSpace(cachedPlayitStatusText))
        {
            cachedPlayitStatusText = RunPlayitCommand(playit, "status", 2500);
            lastPlayitStatusRefresh = DateTime.Now;
        }
        DetectPlayitAddressFromKnownLogs();
        string address = IsValidTunnelAddress(lastPlayitAddress) ? lastPlayitAddress : "ainda nao detectado";
        playitStatusBox.Text = "Endereco Minecraft: " + address + Environment.NewLine + cachedPlayitStatusText;
        playitStatusBox.SelectionStart = playitStatusBox.TextLength;
        playitStatusBox.ScrollToCaret();
    }

    private void RefreshPlayitLog()
    {
        if (playitLogBox == null)
        {
            return;
        }

        if (!File.Exists(playitLogPath))
        {
            playitLogBox.Text = "Sem logs do Playit ainda.";
            lastShownPlayitLogLength = -1;
            return;
        }

        var info = new FileInfo(playitLogPath);
        if (info.Length == lastShownPlayitLogLength)
        {
            return;
        }
        lastShownPlayitLogLength = info.Length;

        string text = ReadTail(playitLogPath, 80);
        ExtractPlayitLinks(text);
        playitLogBox.Text = text;
        playitLogBox.SelectionStart = playitLogBox.TextLength;
        playitLogBox.ScrollToCaret();
    }

    private string ReadTail(string path, int maxLines)
    {
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                const int maxBytes = 131072;
                if (stream.Length > maxBytes)
                {
                    stream.Seek(-maxBytes, SeekOrigin.End);
                }
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string text = reader.ReadToEnd();
                    if (stream.Length > maxBytes)
                    {
                        int firstBreak = text.IndexOf('\n');
                        if (firstBreak >= 0 && firstBreak + 1 < text.Length)
                        {
                            text = text.Substring(firstBreak + 1);
                        }
                    }

                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                if (lines.Length <= maxLines)
                {
                    return string.Join(Environment.NewLine, lines);
                }
                return string.Join(Environment.NewLine, lines.Skip(lines.Length - maxLines).ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            return "Nao consegui ler o log: " + ex.Message;
        }
    }

    private void StartServer()
    {
        try
        {
            if (GetServerProcess() != null)
            {
                SafeRefreshUi();
                return;
            }
            if (!File.Exists(forgeArgsPath))
            {
                MessageBox.Show("Forge nao instalado. Rode setup-forge.bat antes.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Directory.CreateDirectory(Path.Combine(serverRoot, "logs"));
            SaveRam();
            AppendLauncherLog("Start requested from native launcher.");

            var startInfo = new ProcessStartInfo
            {
                FileName = GetJavaCommand(),
                Arguments = "@user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-47.4.20/win_args.txt nogui",
                WorkingDirectory = serverRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            serverProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                try { if (!string.IsNullOrWhiteSpace(e.Data)) AppendLauncherLog(e.Data); } catch { }
            };
            serverProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                try { if (!string.IsNullOrWhiteSpace(e.Data)) AppendLauncherLog(e.Data); } catch { }
            };
            serverProcess.Exited += delegate
            {
                try
                {
                    AppendLauncherLog("Server process exited. Launcher remains open.");
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke((Action)SafeRefreshUi);
                    }
                }
                catch
                {
                }
            };

            serverProcess.Start();
            serverProcess.BeginOutputReadLine();
            serverProcess.BeginErrorReadLine();
            SafeRefreshUi();
        }
        catch (Exception ex)
        {
            AppendLauncherLog("Start failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopServer()
    {
        try
        {
            Process process = GetServerProcess();
            if (process == null)
            {
                SafeRefreshUi();
                return;
            }

            AppendLauncherLog("Stop requested from native launcher.");
            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.StandardInput.WriteLine("stop");
                serverProcess.StandardInput.Flush();
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    "Este servidor foi iniciado fora deste launcher. Forcar encerramento agora?",
                    Text,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (result == DialogResult.Yes)
                {
                    process.Kill();
                }
            }
            SafeRefreshUi();
        }
        catch (Exception ex)
        {
            AppendLauncherLog("Stop failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopServerForExit()
    {
        if (closingServer)
        {
            return;
        }
        closingServer = true;

        try
        {
            Process process = GetServerProcess();
            if (process == null || process.HasExited)
            {
                return;
            }

            AppendLauncherLog("Window closed. Stopping server to avoid background process.");
            if (serverProcess != null && !serverProcess.HasExited)
            {
                try
                {
                    serverProcess.StandardInput.WriteLine("stop");
                    serverProcess.StandardInput.Flush();
                }
                catch
                {
                }

                if (!serverProcess.WaitForExit(20000))
                {
                    AppendLauncherLog("Server did not stop in time. Killing process.");
                    serverProcess.Kill();
                }
            }
            else
            {
                AppendLauncherLog("Server was not attached to this launcher. Killing detected server process.");
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            AppendLauncherLog("Close stop failed: " + ex);
        }
    }

    private void SendServerCommand()
    {
        string command = commandBox == null ? "" : commandBox.Text.Trim();
        if (SendCommandText(command, true) && commandBox != null)
        {
            commandBox.Clear();
        }
    }

    private string GetSelectedPlayerName()
    {
        if (playersListBox == null || playersListBox.SelectedItem == null)
        {
            MessageBox.Show("Selecione um jogador online primeiro.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return "";
        }

        string player = playersListBox.SelectedItem.ToString().Trim();
        if (!IsMinecraftPlayerName(player))
        {
            MessageBox.Show("Selecione um jogador valido da lista.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return "";
        }
        return player;
    }

    private void SendPlayerCommand(string commandFormat)
    {
        string player = GetSelectedPlayerName();
        if (string.IsNullOrWhiteSpace(player))
        {
            return;
        }

        string command = string.Format(commandFormat, player);
        if (command.StartsWith("ban ", StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith("kick ", StringComparison.OrdinalIgnoreCase))
        {
            DialogResult result = MessageBox.Show(
                "Enviar comando: /" + command + "?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        SendCommandText(command, true);
    }

    private void TeleportSelectedPlayer()
    {
        string player = GetSelectedPlayerName();
        if (string.IsNullOrWhiteSpace(player))
        {
            return;
        }

        string target = PromptText(
            "Teleportar " + player,
            "Destino: outro jogador ou coordenadas X Y Z",
            ""
        );
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        SendCommandText("tp " + player + " " + target.Trim(), true);
    }

    private string PromptText(string title, string label, string initialValue)
    {
        using (var popup = new Form())
        {
            popup.Text = title;
            popup.StartPosition = FormStartPosition.CenterParent;
            popup.Size = new Size(420, 150);
            popup.MinimumSize = new Size(420, 150);
            popup.BackColor = Color.FromArgb(7, 18, 32);
            popup.FormBorderStyle = FormBorderStyle.FixedDialog;
            popup.MaximizeBox = false;
            popup.MinimizeBox = false;
            if (File.Exists(iconPath))
            {
                popup.Icon = new Icon(iconPath);
            }

            var textLabel = new Label
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 34,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(12, 10, 12, 0)
            };
            popup.Controls.Add(textLabel);

            var input = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = initialValue ?? "",
                BackColor = Color.FromArgb(3, 6, 12),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10F),
                Margin = new Padding(12)
            };
            popup.Controls.Add(input);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                BackColor = Color.Transparent
            };
            popup.Controls.Add(buttons);

            var ok = SmallActionButton("Enviar", 0, 0, null);
            ok.Width = 88;
            ok.DialogResult = DialogResult.OK;
            var cancel = SmallActionButton("Cancelar", 0, 0, null);
            cancel.Width = 88;
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            popup.AcceptButton = ok;
            popup.CancelButton = cancel;

            return popup.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : "";
        }
    }

    private bool SendCommandText(string command, bool showEmptyWarning)
    {
        try
        {
            command = (command ?? "").Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                if (showEmptyWarning)
                {
                    MessageBox.Show("Digite um comando primeiro.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return false;
            }

            if (serverProcess == null || serverProcess.HasExited)
            {
                MessageBox.Show(
                    "Para enviar comandos, inicie o servidor por este launcher. Se ele foi aberto por outro .bat/.ps1 ou por uma janela antiga, o launcher consegue ver o processo, mas nao consegue acessar o console dele.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return false;
            }

            serverProcess.StandardInput.WriteLine(command);
            serverProcess.StandardInput.Flush();
            AppendLauncherLog("> " + command);
            SafeRefreshUi();
            return true;
        }
        catch (Exception ex)
        {
            AppendLauncherLog("Command failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void OpenQuickCommands()
    {
        using (var popup = new Form())
        {
            popup.Text = "Comandos rapidos";
            popup.StartPosition = FormStartPosition.CenterParent;
            popup.Size = new Size(680, 610);
            popup.MinimumSize = new Size(620, 520);
            popup.BackColor = Color.FromArgb(7, 18, 32);
            popup.Font = new Font("Segoe UI", 9F);
            if (File.Exists(iconPath))
            {
                popup.Icon = new Icon(iconPath);
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(16),
                BackColor = Color.FromArgb(7, 18, 32)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            popup.Controls.Add(root);

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            root.Controls.Add(top, 0, 0);

            var playerLabel = new Label
            {
                Text = "Jogador",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            top.Controls.Add(playerLabel, 0, 0);

            var playerBox = new TextBox
            {
                Text = GuessPlayerName(),
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(6, 12, 22),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10F)
            };
            top.Controls.Add(playerBox, 1, 0);

            var closeButton = ActionButton("Fechar", false);
            closeButton.Dock = DockStyle.Fill;
            closeButton.Margin = new Padding(12, 0, 0, 0);
            closeButton.Click += delegate { popup.Close(); };
            top.Controls.Add(closeButton, 2, 0);

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            root.Controls.Add(tabs, 0, 1);

            var rulesPage = AddCommandPage(tabs, "Ligar / desligar");
            AddSection(rulesPage, "Mundo");
            AddCommandButton(rulesPage, "Keep Inventory ON", delegate { SendCommandText("gamerule keepInventory true", false); });
            AddCommandButton(rulesPage, "Keep Inventory OFF", delegate { SendCommandText("gamerule keepInventory false", false); });
            AddCommandButton(rulesPage, "Ciclo do dia ON", delegate { SendCommandText("gamerule doDaylightCycle true", false); });
            AddCommandButton(rulesPage, "Ciclo do dia OFF", delegate { SendCommandText("gamerule doDaylightCycle false", false); });
            AddCommandButton(rulesPage, "Clima ON", delegate { SendCommandText("gamerule doWeatherCycle true", false); });
            AddCommandButton(rulesPage, "Clima OFF", delegate { SendCommandText("gamerule doWeatherCycle false", false); });
            AddCommandButton(rulesPage, "Fogo espalha ON", delegate { SendCommandText("gamerule doFireTick true", false); });
            AddCommandButton(rulesPage, "Fogo espalha OFF", delegate { SendCommandText("gamerule doFireTick false", false); });
            AddSection(rulesPage, "Mobs");
            AddCommandButton(rulesPage, "Mob spawning ON", delegate { SendCommandText("gamerule doMobSpawning true", false); });
            AddCommandButton(rulesPage, "Mob spawning OFF", delegate { SendCommandText("gamerule doMobSpawning false", false); });
            AddCommandButton(rulesPage, "Mob griefing ON", delegate { SendCommandText("gamerule mobGriefing true", false); });
            AddCommandButton(rulesPage, "Mob griefing OFF", delegate { SendCommandText("gamerule mobGriefing false", false); });

            var playerPage = AddCommandPage(tabs, "Jogador");
            AddCommandButton(playerPage, "OP jogador", delegate { SendCommandText("op " + CleanPlayer(playerBox.Text), false); });
            AddCommandButton(playerPage, "DeOP jogador", delegate { SendCommandText("deop " + CleanPlayer(playerBox.Text), false); });
            AddCommandButton(playerPage, "Criativo", delegate { SendCommandText("gamemode creative " + CleanPlayer(playerBox.Text), false); });
            AddCommandButton(playerPage, "Sobrevivencia", delegate { SendCommandText("gamemode survival " + CleanPlayer(playerBox.Text), false); });
            AddCommandButton(playerPage, "Curar", delegate { SendCommandText("effect give " + CleanPlayer(playerBox.Text) + " minecraft:instant_health 1 10 true", false); });
            AddCommandButton(playerPage, "Saturar comida", delegate { SendCommandText("effect give " + CleanPlayer(playerBox.Text) + " minecraft:saturation 1 10 true", false); });
            AddCommandButton(playerPage, "Limpar efeitos", delegate { SendCommandText("effect clear " + CleanPlayer(playerBox.Text), false); });

            var givePage = AddCommandPage(tabs, "Todos itens");
            BuildAllItemsBrowser(givePage, playerBox);

            var serverPage = AddCommandPage(tabs, "Servidor");
            AddCommandButton(serverPage, "Listar players", delegate { SendCommandText("list", false); });
            AddCommandButton(serverPage, "Salvar mundo", delegate { SendCommandText("save-all", false); });
            AddCommandButton(serverPage, "Dia", delegate { SendCommandText("time set day", false); });
            AddCommandButton(serverPage, "Noite", delegate { SendCommandText("time set night", false); });
            AddCommandButton(serverPage, "Clima limpo", delegate { SendCommandText("weather clear", false); });
            AddCommandButton(serverPage, "Chuva", delegate { SendCommandText("weather rain", false); });

            var hint = new Label
            {
                Text = "Dica: os botoes usam o console do servidor. Funciona quando o servidor foi iniciado por este launcher.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(218, 229, 242),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(hint, 0, 2);

            popup.ShowDialog(this);
        }
    }

    private void OpenTunnelSettings()
    {
        using (var popup = new Form())
        {
            popup.Text = "Tunel externo - playit.gg";
            popup.StartPosition = FormStartPosition.CenterParent;
            popup.Size = new Size(780, 620);
            popup.MinimumSize = new Size(700, 520);
            popup.BackColor = Color.FromArgb(7, 18, 32);
            popup.Font = new Font("Segoe UI", 9F);
            if (File.Exists(iconPath))
            {
                popup.Icon = new Icon(iconPath);
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16),
                BackColor = Color.FromArgb(7, 18, 32)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            popup.Controls.Add(root);

            var title = new Label
            {
                Text = "playit.gg - acesso fora da rede",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(title, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent
            };
            root.Controls.Add(buttons, 0, 1);

            var installButton = ActionButton("Baixar agente", false);
            var runInstallerButton = ActionButton("Instalar agente", false);
            var startButtonTunnel = ActionButton("Iniciar tunel", true);
            var stopButtonTunnel = ActionButton("Parar tunel", false);
            var loginButton = ActionButton("Login Playit", false);
            var claimButton = ActionButton("Abrir claim", false);
            var panelButton = ActionButton("Painel playit", false);
            var copyButton = ActionButton("Copiar endereco", false);
            foreach (var button in new[] { installButton, runInstallerButton, startButtonTunnel, stopButtonTunnel, loginButton, claimButton, panelButton, copyButton })
            {
                button.Width = 150;
                button.Margin = new Padding(0, 0, 10, 10);
                buttons.Controls.Add(button);
            }

            var logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(3, 6, 12),
                ForeColor = Color.FromArgb(220, 238, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                WordWrap = false
            };
            root.Controls.Add(logBox, 0, 2);

            var status = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(218, 229, 242),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(status, 0, 3);

            Action refresh = delegate
            {
                bool downloaded = IsPlayitDownloaded();
                bool installed = IsPlayitInstalled();
                bool running = IsPlayitRunning();
                installButton.Enabled = !running;
                runInstallerButton.Enabled = downloaded && !running;
                startButtonTunnel.Enabled = installed && !running;
                stopButtonTunnel.Enabled = running;
                claimButton.Enabled = installed;
                copyButton.Enabled = installed;
                status.Text = (downloaded ? "download pronto" : "download nao feito") + " | " +
                              (installed ? "instalado" : "nao instalado") + " | " +
                              (running ? "playit aberto" : "playit fechado") + " | " +
                              (string.IsNullOrWhiteSpace(lastPlayitAddress) ? "endereco ainda nao detectado" : lastPlayitAddress);
                logBox.Text = ReadTail(playitLogPath, 220);
                logBox.SelectionStart = logBox.TextLength;
                logBox.ScrollToCaret();
            };

            var refreshTimer = new Timer { Interval = 1000 };
            refreshTimer.Tick += delegate { refresh(); };
            popup.FormClosed += delegate
            {
                refreshTimer.Stop();
                refreshTimer.Dispose();
            };

            installButton.Click += delegate
            {
                DownloadPlayitAgent();
                refresh();
            };
            runInstallerButton.Click += delegate
            {
                OpenPlayitInstaller();
                refresh();
            };
            startButtonTunnel.Click += delegate
            {
                StartPlayitTunnel();
                refresh();
            };
            stopButtonTunnel.Click += delegate
            {
                StopPlayitTunnel();
                refresh();
            };
            claimButton.Click += delegate
            {
                OpenPlayitClaim();
                refresh();
            };
            loginButton.Click += delegate
            {
                OpenPlayitLogin();
                refresh();
            };
            panelButton.Click += delegate
            {
                Process.Start(new ProcessStartInfo { FileName = "https://playit.gg/account/tunnels", UseShellExecute = true });
            };
            copyButton.Click += delegate
            {
                CopyPlayitAddressOrOpenPanel();
                refresh();
            };

            refresh();
            refreshTimer.Start();
            popup.ShowDialog(this);
        }
    }

    private void EnsurePlayitAgent()
    {
        if (IsPlayitDownloaded())
        {
            return;
        }

        DownloadPlayitAgent();
    }

    private void DownloadPlayitAgent()
    {
        try
        {
            Directory.CreateDirectory(playitRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(playitLogPath));
            string exeUrl = "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-windows-x86_64-signed.exe";
            string msiUrl = "https://github.com/playit-cloud/playit-agent/releases/latest/download/playit-windows-x86_64-signed.msi";
            AppendPlayitLog("Downloading playit agent and installer from official GitHub release.");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "MagicWorldServerLauncher");
                client.DownloadFile(exeUrl, playitExePath);
                client.DownloadFile(msiUrl, playitMsiPath);
            }
            AppendPlayitLog("Downloaded playit agent: " + playitExePath);
            AppendPlayitLog("Downloaded playit installer: " + playitMsiPath);
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Download failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenPlayitInstaller()
    {
        try
        {
            if (!File.Exists(playitMsiPath))
            {
                DownloadPlayitAgent();
            }
            if (!File.Exists(playitMsiPath))
            {
                MessageBox.Show("Instalador do Playit nao encontrado. Clique em Baixar agente primeiro.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AppendPlayitLog("Opening playit installer: " + playitMsiPath);
            Process.Start(new ProcessStartInfo { FileName = playitMsiPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Open installer failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenPlayitLogin()
    {
        try
        {
            string playit = GetInstalledPlayitCli();
            if (!string.IsNullOrWhiteSpace(playit) && File.Exists(playit))
            {
                try
                {
                    string url = RunPlayitCommand(playit, "account login-url", 10000).Trim();
                    if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendPlayitLog("Login URL: " + url);
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AppendPlayitLog("CLI login URL failed, opening web login: " + ex.Message);
                }
            }

            Process.Start(new ProcessStartInfo { FileName = "https://playit.gg/login", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Open login failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenPlayitClaim()
    {
        try
        {
            string playit = GetInstalledPlayitCli();
            if (string.IsNullOrWhiteSpace(playit) || !File.Exists(playit))
            {
                MessageBox.Show(
                    "Para claim/login completo, instale o Playit pelo botao Baixar agente. Depois clique em Login Playit.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                Process.Start(new ProcessStartInfo { FileName = "https://playit.gg/account/agents", UseShellExecute = true });
                return;
            }
            if (string.IsNullOrWhiteSpace(playit) || !File.Exists(playit))
            {
                MessageBox.Show("Playit.gg nao encontrado.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string code = RunPlayitCommand(playit, "claim generate", 10000).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                Process.Start(new ProcessStartInfo { FileName = "https://playit.gg/account/agents", UseShellExecute = true });
                AppendPlayitLog("Claim code not generated. Opened playit agents page.");
                return;
            }

            string url = RunPlayitCommand(playit, "claim url --name MagicWorldServer " + code, 10000).Trim();
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://playit.gg/claim/" + code;
            }

            lastPlayitLink = url;
            AppendPlayitLog("Claim URL: " + url);
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Open claim failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyPlayitAddressOrOpenPanel()
    {
        try
        {
            if (!IsValidTunnelAddress(lastPlayitAddress))
            {
                lastPlayitAddress = "";
            }
            DetectPlayitAddressFromKnownLogs();
            string playit = GetInstalledPlayitCli();
            if (!IsValidTunnelAddress(lastPlayitAddress) && !string.IsNullOrWhiteSpace(playit) && File.Exists(playit))
            {
                AppendPlayitLog(RunPlayitCommand(playit, "status", 5000));
                EnsurePlayitLogAttach(playit);
                DetectPlayitAddressFromKnownLogs();
            }
            if (IsValidTunnelAddress(lastPlayitAddress))
            {
                Clipboard.SetText(lastPlayitAddress);
                AppendPlayitLog("Copied tunnel address: " + lastPlayitAddress);
                return;
            }

            Clipboard.SetText("Abra o painel do playit.gg e copie o endereco do tunel Minecraft Java.");
            Process.Start(new ProcessStartInfo { FileName = "https://playit.gg/account/tunnels", UseShellExecute = true });
            MessageBox.Show(
                "Ainda nao detectei o endereco automaticamente. Abri o painel do playit.gg; copie o endereco do tunel Minecraft Java por la.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Copy address failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartPlayitTunnel()
    {
        try
        {
            string playit = GetInstalledPlayitCli();
            if (string.IsNullOrWhiteSpace(playit) || !File.Exists(playit))
            {
                MessageBox.Show("Playit ainda nao esta instalado. Use Baixar agente, depois Instalar agente, depois Login Playit.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsPlayitRunning())
            {
                AppendPlayitLog("Playit service already running. Attaching hidden logs.");
                EnsurePlayitLogAttach(playit);
                cachedPlayitStatusText = RunPlayitCommand(playit, "status", 8000);
                lastPlayitStatusRefresh = DateTime.Now;
                return;
            }

            Directory.CreateDirectory(playitRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(playitLogPath));
            AppendPlayitLog("Starting playit service hidden: " + playit);
            AppendPlayitLog(RunPlayitCommand(playit, "start", 10000));
            cachedPlayitStatusText = RunPlayitCommand(playit, "status", 8000);
            lastPlayitStatusRefresh = DateTime.Now;
            AppendPlayitLog(cachedPlayitStatusText);
            EnsurePlayitLogAttach(playit);
            AppendPlayitLog("Playit roda escondido. Fechar este popup nao desliga o tunel; use Parar tunel.");
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Start failed: " + ex);
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnsurePlayitLogAttach(string playit)
    {
        try
        {
            if (tunnelProcess != null && !tunnelProcess.HasExited)
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = playit,
                Arguments = "attach --stdout",
                WorkingDirectory = Path.GetDirectoryName(playit),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            tunnelProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            tunnelProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                try { if (!string.IsNullOrWhiteSpace(e.Data)) HandlePlayitLine(e.Data); } catch { }
            };
            tunnelProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                try { if (!string.IsNullOrWhiteSpace(e.Data)) HandlePlayitLine(e.Data); } catch { }
            };
            tunnelProcess.Exited += delegate
            {
                AppendPlayitLog("playit log attach exited.");
            };
            tunnelProcess.Start();
            tunnelProcess.BeginOutputReadLine();
            tunnelProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Could not attach hidden playit logs: " + ex.Message);
        }
    }

    private void StopPlayitTunnel()
    {
        try
        {
            if (tunnelProcess != null && !tunnelProcess.HasExited)
            {
                AppendPlayitLog("Stopping hidden playit log attach.");
                tunnelProcess.Kill();
            }

            string playit = GetInstalledPlayitCli();
            if (!string.IsNullOrWhiteSpace(playit) && File.Exists(playit))
            {
                AppendPlayitLog("Stopping playit service.");
                AppendPlayitLog(RunPlayitCommand(playit, "stop", 10000));
                cachedPlayitStatusText = "";
                lastPlayitStatusRefresh = DateTime.MinValue;
            }

            foreach (Process process in Process.GetProcessesByName("playit"))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            AppendPlayitLog("Stop failed: " + ex);
        }
    }

    private void StopPlayitTunnelForExit()
    {
        if (IsPlayitRunning())
        {
            AppendPlayitLog("Launcher closed. Stopping playit to avoid hidden background tunnel.");
            StopPlayitTunnel();
        }
    }

    private bool IsPlayitRunning()
    {
        if (tunnelProcess != null && !tunnelProcess.HasExited)
        {
            return true;
        }
        try
        {
            string playit = GetInstalledPlayitCli();
            if (!string.IsNullOrWhiteSpace(playit) && File.Exists(playit))
            {
                string status = RunPlayitCommand(playit, "status", 2500);
                return status.IndexOf("Phase: running", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       status.IndexOf("Phase: waiting", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return Process.GetProcessesByName("playit").Any();
        }
        catch
        {
            return false;
        }
    }

    private bool IsPlayitInstalled()
    {
        string path = GetInstalledPlayitCli();
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private bool IsPlayitDownloaded()
    {
        return File.Exists(playitExePath) || File.Exists(playitMsiPath);
    }

    private string RunPlayitCommand(string playit, string arguments, int timeoutMs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = playit,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(playit),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
            }
            string output = (stdout + Environment.NewLine + stderr).Trim();
            ExtractPlayitLinks(output);
            return output;
        }
    }

    private void DetectPlayitAddressFromKnownLogs()
    {
        foreach (string path in new[]
        {
            playitLogPath,
            @"C:\ProgramData\playit_gg\logs\playitd.log"
        })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            string text = ReadTail(path, 300);
            ExtractPlayitLinks(text);
            if (!string.IsNullOrWhiteSpace(lastPlayitAddress))
            {
                return;
            }
        }
    }

    private string GetPlayitExecutable()
    {
        string[] candidates =
        {
            @"C:\Program Files\playit_gg\bin\playit.exe",
            @"C:\Program Files\playit_gg\bin\playitd-tray.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\playit_gg\bin\playit.exe"),
            playitExePath
        };

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }
        return "";
    }

    private string GetInstalledPlayitCli()
    {
        foreach (string candidate in new[]
        {
            @"C:\Program Files\playit_gg\bin\playit.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\playit_gg\bin\playit.exe")
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }
        return "";
    }

    private void HandlePlayitLine(string line)
    {
        ExtractPlayitLinks(line);
        AppendPlayitLog(line);
    }

    private void ExtractPlayitLinks(string line)
    {
        Match url = Regex.Match(line, @"https?://\S+");
        if (url.Success && url.Value.IndexOf("playit", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            lastPlayitLink = url.Value.TrimEnd('.', ',', ';', ')', ']');
        }

        MatchCollection addresses = Regex.Matches(line, @"\b([a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+(?:\:\d{2,5})?)\b");
        foreach (Match address in addresses)
        {
            string candidate = address.Value.Trim();
            if (IsValidTunnelAddress(candidate))
            {
                lastPlayitAddress = candidate;
                return;
            }
        }
    }

    private bool IsValidTunnelAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (value.IndexOf("\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Regex.IsMatch(value, @"\.(exe|msi|dll|toml|log|json|txt|bat|cmd|ps1)$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        Match match = Regex.Match(value, @"^([a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+)(?:\:(\d{2,5}))?$");
        if (!match.Success)
        {
            return false;
        }

        string host = match.Groups[1].Value.ToLowerInvariant();
        bool hasPort = match.Groups[2].Success;
        if (match.Groups[2].Success)
        {
            int port;
            if (!int.TryParse(match.Groups[2].Value, out port) || port < 1 || port > 65535)
            {
                return false;
            }
        }

        if (host == "playit.gg" || host == "www.playit.gg" || host == "api.playit.gg")
        {
            return false;
        }

        if (hasPort)
        {
            return host.EndsWith(".joinmc.link") ||
                   host.EndsWith(".ply.gg") ||
                   host.EndsWith(".playit.gg") ||
                   host.EndsWith(".at");
        }

        return host.EndsWith(".joinmc.link") ||
               host.EndsWith(".craft.ply.gg") ||
               host.EndsWith(".tcp.playit.gg") ||
               host.EndsWith(".ply.gg");
    }

    private void AppendPlayitLog(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(playitLogPath));
            File.AppendAllText(playitLogPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + text + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private FlowLayoutPanel AddCommandPage(TabControl tabs, string title)
    {
        var page = new TabPage(title)
        {
            BackColor = Color.FromArgb(7, 18, 32)
        };
        var scroll = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(7, 18, 32)
        };
        page.Controls.Add(scroll);
        tabs.TabPages.Add(page);
        return scroll;
    }

    private void AddSection(FlowLayoutPanel panel, string text)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Width = 584,
            Height = 30,
            ForeColor = Color.FromArgb(245, 196, 45),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 4)
        });
    }

    private void AddCommandButton(FlowLayoutPanel panel, string text, Action click)
    {
        var button = ActionButton(text, false);
        button.Width = 180;
        button.Height = 38;
        button.Margin = new Padding(0, 0, 10, 10);
        button.Click += delegate { click(); };
        panel.Controls.Add(button);
    }

    private void Give(string player, string item, int count)
    {
        SendCommandText("give " + CleanPlayer(player) + " " + item + " " + count, false);
    }

    private void BuildAllItemsBrowser(FlowLayoutPanel panel, TextBox playerBox)
    {
        panel.WrapContents = false;
        panel.FlowDirection = FlowDirection.TopDown;

        var catalog = LoadItemCatalog();
        var imageList = new ImageList
        {
            ImageSize = new Size(32, 32),
            ColorDepth = ColorDepth.Depth32Bit
        };
        imageList.Images.Add("__blank", CreateBlankIcon());

        var top = new TableLayoutPanel
        {
            Width = 584,
            Height = 42,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        panel.Controls.Add(top);

        var search = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(6, 12, 22),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10F)
        };
        top.Controls.Add(search, 0, 0);

        var qtyLabel = new Label
        {
            Text = "Qtd",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Padding = new Padding(0, 0, 8, 0)
        };
        top.Controls.Add(qtyLabel, 1, 0);

        var qty = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 6400,
            Value = 64,
            BorderStyle = BorderStyle.FixedSingle
        };
        top.Controls.Add(qty, 2, 0);

        var giveButton = ActionButton("Dar item", true);
        giveButton.Dock = DockStyle.Fill;
        giveButton.Margin = new Padding(10, 0, 0, 0);
        top.Controls.Add(giveButton, 3, 0);

        var list = new ListView
        {
            Width = 584,
            Height = 330,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            SmallImageList = imageList,
            BackColor = Color.FromArgb(3, 6, 12),
            ForeColor = Color.FromArgb(230, 240, 255),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9F),
            Margin = new Padding(0, 0, 0, 8)
        };
        list.Columns.Add("Item", 210);
        list.Columns.Add("ID", 240);
        list.Columns.Add("Fonte", 115);
        panel.Controls.Add(list);

        var info = new Label
        {
            Width = 584,
            Height = 28,
            ForeColor = Color.FromArgb(218, 229, 242),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(info);

        search.TextChanged += delegate { FillItemList(list, catalog, search.Text, info); };
        giveButton.Click += delegate { GiveSelectedItem(list, playerBox, qty); };
        list.DoubleClick += delegate { GiveSelectedItem(list, playerBox, qty); };
        FillItemList(list, catalog, "", info);
    }

    private void FillItemList(ListView list, List<ItemEntry> catalog, string filter, Label info)
    {
        string needle = (filter ?? "").Trim().ToLowerInvariant();
        IEnumerable<ItemEntry> query = catalog;
        if (!string.IsNullOrWhiteSpace(needle))
        {
            query = query.Where(item =>
                item.Id.ToLowerInvariant().Contains(needle) ||
                item.Label.ToLowerInvariant().Contains(needle) ||
                item.Namespace.ToLowerInvariant().Contains(needle) ||
                item.Source.ToLowerInvariant().Contains(needle));
        }

        ItemEntry[] visible = query.Take(MaxVisibleItems).ToArray();
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (ItemEntry item in visible)
            {
                EnsureItemIconLoaded(list.SmallImageList, item);
                var row = new ListViewItem(item.Label);
                row.SubItems.Add(item.Id);
                row.SubItems.Add(item.Source);
                row.Tag = item;
                row.ImageKey = list.SmallImageList.Images.ContainsKey(item.Id) ? item.Id : "__blank";
                list.Items.Add(row);
            }
        }
        finally
        {
            list.EndUpdate();
        }

        int totalMatches = string.IsNullOrWhiteSpace(needle) ? catalog.Count : query.Count();
        info.Text = totalMatches > visible.Length
            ? totalMatches + " itens encontrados. Mostrando os primeiros " + visible.Length + ". Refine a busca."
            : totalMatches + " itens encontrados.";
    }

    private void EnsureItemIconLoaded(ImageList imageList, ItemEntry item)
    {
        if (imageList == null || item == null || imageList.Images.ContainsKey(item.Id))
        {
            return;
        }

        string icon = ResolveServerPath(item.Icon);
        if (string.IsNullOrWhiteSpace(icon) || !File.Exists(icon))
        {
            return;
        }

        try
        {
            using (Image source = Image.FromFile(icon))
            using (var copy = new Bitmap(source))
            {
                imageList.Images.Add(item.Id, new Bitmap(copy));
            }
        }
        catch
        {
        }
    }

    private void GiveSelectedItem(ListView list, TextBox playerBox, NumericUpDown qty)
    {
        if (list.SelectedItems.Count == 0)
        {
            MessageBox.Show("Selecione um item na lista.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var item = list.SelectedItems[0].Tag as ItemEntry;
        if (item == null)
        {
            return;
        }
        Give(playerBox.Text, item.Id, (int)qty.Value);
    }

    private List<ItemEntry> LoadItemCatalog()
    {
        if (itemCatalog != null)
        {
            return itemCatalog;
        }

        itemCatalog = new List<ItemEntry>();
        string catalogPath = Path.Combine(serverRoot, @"launcher-assets\item-catalog.tsv");
        if (!File.Exists(catalogPath))
        {
            itemCatalog.Add(new ItemEntry { Id = "minecraft:diamond", Label = "Diamond", Namespace = "minecraft", Icon = "", Source = "fallback" });
            itemCatalog.Add(new ItemEntry { Id = "magicworld:varinha_magica", Label = "Varinha Magica", Namespace = "magicworld", Icon = "", Source = "fallback" });
            return itemCatalog;
        }

        foreach (string line in File.ReadAllLines(catalogPath, Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            string[] parts = line.Split('\t');
            if (parts.Length < 5)
            {
                continue;
            }
            itemCatalog.Add(new ItemEntry
            {
                Id = parts[0],
                Label = string.IsNullOrWhiteSpace(parts[1]) ? parts[0] : parts[1],
                Namespace = parts[2],
                Icon = parts[3],
                Source = parts[4]
            });
        }
        return itemCatalog;
    }

    private string ResolveServerPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        return Path.Combine(serverRoot, path);
    }

    private Bitmap CreateBlankIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            using (var brush = new SolidBrush(Color.FromArgb(42, 52, 66)))
            {
                graphics.FillRectangle(brush, 4, 4, 24, 24);
            }
            using (var pen = new Pen(Color.FromArgb(213, 168, 28)))
            {
                graphics.DrawRectangle(pen, 4, 4, 24, 24);
            }
        }
        return bitmap;
    }

    private string CleanPlayer(string player)
    {
        player = (player ?? "").Trim();
        if (string.IsNullOrWhiteSpace(player))
        {
            return "@p";
        }
        return player.Replace(" ", "");
    }

    private string GuessPlayerName()
    {
        try
        {
            if (File.Exists(latestLogPath))
            {
                string[] lines = File.ReadAllLines(latestLogPath);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string line = lines[i];
                    int joined = line.IndexOf(" joined the game", StringComparison.OrdinalIgnoreCase);
                    if (joined > 0)
                    {
                        int colon = line.LastIndexOf("]:", joined, Math.Min(joined, line.Length));
                        string name = colon >= 0 ? line.Substring(colon + 2, joined - colon - 2).Trim() : "";
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            return name;
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return "guipaluch";
    }

    private AutoCompleteStringCollection BuildCommandSuggestions()
    {
        var suggestions = new AutoCompleteStringCollection();
        suggestions.AddRange(new[]
        {
            "help",
            "list",
            "stop",
            "save-all",
            "save-on",
            "save-off",
            "op guipaluch",
            "deop guipaluch",
            "gamemode creative guipaluch",
            "gamemode survival guipaluch",
            "gamemode spectator guipaluch",
            "tp guipaluch ",
            "give guipaluch minecraft:diamond 64",
            "give guipaluch minecraft:emerald 64",
            "give guipaluch minecraft:golden_apple 16",
            "give guipaluch minecraft:enchanted_golden_apple 4",
            "give guipaluch minecraft:netherite_sword 1",
            "give guipaluch minecraft:netherite_pickaxe 1",
            "give guipaluch magicworld:varinha_magica 1",
            "give guipaluch magicworld:draconic_aether_helmet 1",
            "give guipaluch magicworld:draconic_aether_chestplate 1",
            "give guipaluch magicworld:draconic_aether_leggings 1",
            "give guipaluch magicworld:draconic_aether_boots 1",
            "effect give guipaluch minecraft:night_vision 999999 0 true",
            "effect give guipaluch minecraft:saturation 1 10 true",
            "effect clear guipaluch",
            "time set day",
            "time set night",
            "weather clear",
            "weather rain",
            "gamerule keepInventory true",
            "gamerule keepInventory false",
            "gamerule doDaylightCycle true",
            "gamerule doDaylightCycle false",
            "gamerule doWeatherCycle true",
            "gamerule doWeatherCycle false",
            "gamerule doMobSpawning true",
            "gamerule doMobSpawning false",
            "gamerule mobGriefing true",
            "gamerule mobGriefing false",
            "gamerule doFireTick true",
            "gamerule doFireTick false",
            "difficulty peaceful",
            "difficulty easy",
            "difficulty normal",
            "difficulty hard",
            "whitelist on",
            "whitelist off",
            "whitelist add guipaluch",
            "kick guipaluch",
            "ban guipaluch"
        });
        return suggestions;
    }

    private void RestartServer()
    {
        StopServer();
        timer.Stop();
        var restartTimer = new Timer { Interval = 3000 };
        restartTimer.Tick += delegate
        {
            restartTimer.Stop();
            restartTimer.Dispose();
            StartServer();
            timer.Start();
        };
        restartTimer.Start();
    }

    private Process GetServerProcess()
    {
        if (serverProcess != null && !serverProcess.HasExited)
        {
            return serverProcess;
        }

        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='java.exe'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string commandLine = Convert.ToString(obj["CommandLine"]) ?? "";
                    if (commandLine.IndexOf("win_args.txt", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        commandLine.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int pid = Convert.ToInt32(obj["ProcessId"]);
                        return Process.GetProcessById(pid);
                    }
                }
            }
        }
        catch
        {
            return null;
        }
        return null;
    }

    private bool IsPortOpen(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    private string GetJavaCommand()
    {
        if (!string.IsNullOrWhiteSpace(cachedJavaCommand) && File.Exists(cachedJavaCommand))
        {
            return cachedJavaCommand;
        }

        foreach (string candidate in JavaCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                cachedJavaCommand = candidate;
                return cachedJavaCommand;
            }
        }
        throw new FileNotFoundException("Java nao encontrado. Instale Java 17 ou abra o launcher oficial uma vez para baixar o runtime.");
    }

    private IEnumerable<string> JavaCandidates()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string launcherRuntime = Path.Combine(localAppData, @"MagicWorldLauncher\runtime\java17");
        foreach (string path in FindJavaIn(launcherRuntime, true)) yield return path;

        string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome)) yield return Path.Combine(javaHome, @"bin\java.exe");

        foreach (string path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (!string.IsNullOrWhiteSpace(path)) yield return Path.Combine(path.Trim(), "java.exe");
        }

        foreach (string path in JavaInstallDirCandidates(@"C:\Program Files\Eclipse Adoptium")) yield return path;
        foreach (string path in JavaInstallDirCandidates(@"C:\Program Files\Java")) yield return path;
    }

    private IEnumerable<string> JavaInstallDirCandidates(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root).OrderByDescending(path => path.IndexOf("17", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (string dir in dirs)
        {
            yield return Path.Combine(dir, @"bin\java.exe");
        }
    }

    private IEnumerable<string> FindJavaIn(string root, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "java.exe", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (string file in files.OrderBy(p => p))
        {
            yield return file;
        }
    }

    private string ShortJavaText()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(cachedJavaText))
            {
                return cachedJavaText;
            }
            string java = GetJavaCommand();
            string folder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(java)));
            cachedJavaText = folder;
            return cachedJavaText;
        }
        catch
        {
            return "nao encontrado";
        }
    }

    private void BackupWorld()
    {
        try
        {
            if (!Directory.Exists(worldPath))
            {
                MessageBox.Show("A pasta world ainda nao existe.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Directory.CreateDirectory(backupsPath);
            string backup = Path.Combine(backupsPath, "world-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
            ZipFile.CreateFromDirectory(worldPath, backup, CompressionLevel.Fastest, false);
            AppendLauncherLog("Backup created: " + backup);
            OpenPath(backupsPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenPath(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "");
        }
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void AppendLauncherLog(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(launcherLogPath));
            File.AppendAllText(launcherLogPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + text + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }
}

internal sealed class ItemEntry
{
    public string Id;
    public string Label;
    public string Namespace;
    public string Icon;
    public string Source;
}
