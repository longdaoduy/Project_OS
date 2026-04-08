using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq;

namespace OS_Lab02_FAT32
{
    public partial class MainForm : Form
    {
        // ── palette ────────────────────────────────────────────────────────────
        private static readonly Color C_HEADER_BG    = Color.FromArgb(28,  40,  60);
        private static readonly Color C_HEADER_FG    = Color.White;
        private static readonly Color C_ACCENT       = Color.FromArgb(0, 120, 215);
        private static readonly Color C_ACCENT_HOVER = Color.FromArgb(0,  90, 170);
        private static readonly Color C_TAB_BG       = Color.FromArgb(240, 244, 250);
        private static readonly Color C_CARD_BG      = Color.White;
        private static readonly Color C_ROW_ALT      = Color.FromArgb(235, 242, 255);
        private static readonly Color C_ROW_SEL      = Color.FromArgb(173, 214, 255);
        private static readonly Color C_GRID_LINE    = Color.FromArgb(210, 220, 235);
        private static readonly Color C_HDR_GRID     = Color.FromArgb(28,  40,  60);
        private static readonly Color C_STATUS_BG    = Color.FromArgb(245, 247, 250);
        private static readonly Color C_OK            = Color.FromArgb(40, 167, 69);
        private static readonly Color C_ERR           = Color.FromArgb(220, 53, 69);

        private static readonly Font FONT_UI       = new Font("Segoe UI", 10);
        private static readonly Font FONT_BOLD     = new Font("Segoe UI", 10, FontStyle.Bold);
        private static readonly Font FONT_BOLD9    = new Font("Segoe UI",  9, FontStyle.Bold);
        private static readonly Font FONT_SMALL8   = new Font("Segoe UI",  8);
        private static readonly Font FONT_MONO     = new Font("Consolas",  9);
        private static readonly Font FONT_TITLE    = new Font("Segoe UI", 13, FontStyle.Bold);
        // Gantt-chart fonts (reused across every redraw)
        private static readonly Font FONT_GANTT_SM = new Font("Segoe UI", 7.5f);
        private static readonly Font FONT_GANTT_LB = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Font FONT_GANTT_AX = new Font("Segoe UI", 7.5f);

        // ── state ──────────────────────────────────────────────────────────────
        private Fat32Reader reader = null!;

        // ── controls ───────────────────────────────────────────────────────────
        private TabControl  tabControl      = null!;
        private TextBox     txtDrive        = null!;
        private Button      btnRead         = null!;
        private Label       lblStatus       = null!;
        private DataGridView dgvFunction1   = null!;
        private DataGridView dgvFunction2   = null!;
        private SplitContainer splitTab2   = null!;
        private Panel       pnlFileCard     = null!;
        private Label       lblFileName     = null!;
        private Label       lblFileDate     = null!;
        private Label       lblFileTime     = null!;
        private Label       lblFileSize     = null!;
        private DataGridView dgvFunction3   = null!;
        private Button      btnRunSchedule  = null!;
        private Button      btnRunScheduleTab2 = null!;
        private Label       lblQueueLegend  = null!;
        private Panel       pnlGanttScroll  = null!;
        private PictureBox  picGantt        = null!;
        private DataGridView dgvStats       = null!;

        // ──────────────────────────────────────────────────────────────────────
        public MainForm()
        {
            this.Text            = "OS Lab 02 — FAT32 & CPU Scheduling";
            this.Size            = new Size(1100, 750);
            this.MinimumSize     = new Size(900, 600);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = C_TAB_BG;
            this.Font            = FONT_UI;
            BuildUI();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── header bar ───────────────────────────────────────────────────
            Panel header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 64,
                BackColor = C_HEADER_BG
            };
            header.Paint += (s, e) =>
            {
                // thin accent line at bottom
                e.Graphics.FillRectangle(new SolidBrush(C_ACCENT), 0, header.Height - 3, header.Width, 3);
            };

            Label lblTitle = new Label
            {
                Text      = "  OS Lab 02 — FAT32 Reader & CPU Scheduling",
                ForeColor = C_HEADER_FG,
                Font      = FONT_TITLE,
                AutoSize  = false,
                Dock      = DockStyle.Left,
                Width     = 480,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // drive input group inside header
            Panel drivePanel = new Panel { Height = 64, Width = 430, Dock = DockStyle.Right, BackColor = Color.Transparent };
            Label lblDriveHint = new Label
            {
                Text      = "Drive:",
                ForeColor = Color.FromArgb(180, 200, 220),
                Font      = FONT_UI,
                Location  = new Point(10, 20),
                AutoSize  = true
            };
            txtDrive = new TextBox
            {
                Text      = "F:",
                Location  = new Point(65, 17),
                Width     = 50,
                Font      = FONT_BOLD,
                BackColor = Color.FromArgb(50, 65, 90),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center
            };
            btnRead = MakeButton("⚡  Read Drive", new Point(125, 13), 160, C_ACCENT);
            btnRead.Click += BtnRead_Click;

            lblStatus = new Label
            {
                Location  = new Point(295, 20),
                Width     = 130,
                AutoSize  = false,
                ForeColor = Color.FromArgb(180, 210, 180),
                Font      = FONT_SMALL8,
                TextAlign = ContentAlignment.MiddleLeft
            };

            drivePanel.Controls.AddRange(new Control[] { lblDriveHint, txtDrive, btnRead, lblStatus });
            header.Controls.AddRange(new Control[] { lblTitle, drivePanel });
            this.Controls.Add(header);

            // ── tab control ──────────────────────────────────────────────────
            tabControl = new TabControl
            {
                Dock      = DockStyle.Fill,
                Font      = FONT_BOLD9,
                Padding   = new Point(14, 5)
            };
            this.Controls.Add(tabControl);
            tabControl.BringToFront();

            tabControl.TabPages.Add(BuildTab1());
            tabControl.TabPages.Add(BuildTab2());
            tabControl.TabPages.Add(BuildTab4());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB 1 – FUNCTION 1: BOOT SECTOR
        // ══════════════════════════════════════════════════════════════════════
        private TabPage BuildTab1()
        {
            TabPage tab = new TabPage("  ① Boot Sector  ") { BackColor = C_TAB_BG };

            Panel card = MakeCard(DockStyle.Fill);

            Label lbl = new Label
            {
                Text      = "📀  Boot Sector Info (FAT32)",
                Font      = FONT_BOLD,
                ForeColor = C_ACCENT,
                Dock      = DockStyle.Top,
                Height    = 34,
                Padding   = new Padding(4, 6, 0, 0)
            };

            dgvFunction1 = MakeDgv();
            dgvFunction1.Dock = DockStyle.Fill;

            card.Controls.Add(dgvFunction1);
            card.Controls.Add(lbl);
            tab.Controls.Add(card);
            return tab;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB 2 – FUNCTION 2 + 3: FILE LIST & DETAILS
        // ══════════════════════════════════════════════════════════════════════
        private TabPage BuildTab2()
        {
            TabPage tab = new TabPage("  ② File List & Details  ") { BackColor = C_TAB_BG };

            splitTab2 = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 350,
                BackColor        = C_TAB_BG,
                Panel2Collapsed  = true
            };

            // ── Panel1: file list ────────────────────────────────────────────
            Panel listCard = MakeCard(DockStyle.Fill);

            Label lblList = new Label
            {
                Text      = "📂  *.txt Files on Disk — Click a row to view details",
                Font      = FONT_BOLD,
                ForeColor = C_ACCENT,
                Dock      = DockStyle.Top,
                Height    = 34,
                Padding   = new Padding(4, 6, 0, 0)
            };

            dgvFunction2 = MakeDgv();
            dgvFunction2.Dock          = DockStyle.Fill;
            dgvFunction2.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFunction2.CellClick    += DgvFunction2_CellClick;
            dgvFunction2.SelectionChanged += (s, e) =>
            {
                foreach (DataGridViewRow r in dgvFunction2.Rows)
                    r.DefaultCellStyle.BackColor = r.Selected ? C_ROW_SEL
                        : (r.Index % 2 == 0 ? Color.White : C_ROW_ALT);
            };

            listCard.Controls.Add(dgvFunction2);
            listCard.Controls.Add(lblList);
            splitTab2.Panel1.Controls.Add(listCard);
            splitTab2.Panel1.Padding = new Padding(6, 6, 6, 3);

            // ── Panel2: file info + process table ────────────────────────────
            SplitContainer splitDetails = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 20,
                BackColor        = C_TAB_BG
            };

            // file info card
            Panel fileCard = MakeCard(DockStyle.Fill);
            pnlFileCard = fileCard;

            Label lblHdr = new Label
            {
                Text      = "📄  Selected File Info",
                Font      = FONT_BOLD,
                ForeColor = C_ACCENT,
                Dock      = DockStyle.Top,
                Height    = 30,
                Padding   = new Padding(4, 4, 0, 0)
            };

            TableLayoutPanel tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 9, // 8 cột chứa dữ liệu + 1 cột trống (spring column) để đẩy chữ sang trái
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(6, 0, 6, 6)
            };

            // Đặt row tự điều chỉnh chiều cao theo nội dung để các control không bị lệch lên đỉnh
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Đặt 8 cột đầu tiên thành AutoSize để chúng ôm sát nội dung
            for (int i = 0; i < 8; i++)
            {
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            // Cột thứ 9 chiếm toàn bộ không gian còn lại (Percent = 100)
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Khởi tạo các label hiển thị giá trị — dùng TextAlign.MiddleLeft để ngang hàng với caption
            lblFileName = new Label { Font = FONT_MONO, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
            lblFileDate = new Label { Font = FONT_MONO, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
            lblFileTime = new Label { Font = FONT_MONO, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
            lblFileSize = new Label { Font = FONT_MONO, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

            // Khởi tạo Caption và thêm khoảng cách lề trái (Margin Left = 25) để không bị dính vào chữ phía trước
            var capName = MakeInfoCaption("File Name:");
            var capDate = MakeInfoCaption("Date Created:"); capDate.Margin = new Padding(25, 0, 0, 0);
            var capTime = MakeInfoCaption("Time Created:"); capTime.Margin = new Padding(25, 0, 0, 0);
            var capSize = MakeInfoCaption("Total Size:");   capSize.Margin = new Padding(25, 0, 0, 0);

            tbl.Controls.Add(capName,     0, 0);
            tbl.Controls.Add(lblFileName, 1, 0);
            tbl.Controls.Add(capDate,     2, 0);
            tbl.Controls.Add(lblFileDate, 3, 0);
            tbl.Controls.Add(capTime,     4, 0);
            tbl.Controls.Add(lblFileTime, 5, 0);
            tbl.Controls.Add(capSize,     6, 0);
            tbl.Controls.Add(lblFileSize, 7, 0);

            fileCard.Controls.Add(tbl);
            fileCard.Controls.Add(lblHdr);
            splitDetails.Panel1.Controls.Add(fileCard);
            splitDetails.Panel1.Padding = new Padding(6, 3, 6, 3);

            // process table + run button
            Panel procCard = MakeCard(DockStyle.Fill);

            Label lblProcHdr = new Label
            {
                Text      = "⚙  Process Information (from file)",
                Font      = FONT_BOLD,
                ForeColor = C_ACCENT,
                Dock      = DockStyle.Top,
                Height    = 30,
                Padding   = new Padding(4, 4, 0, 0)
            };

            btnRunScheduleTab2 = MakeButton("▶  Run Scheduling", new Point(0, 0), 200, C_OK);
            btnRunScheduleTab2.Dock   = DockStyle.Bottom;
            btnRunScheduleTab2.Height = 36;
            btnRunScheduleTab2.Click += BtnRunSchedule_Click;

            dgvFunction3 = MakeDgv();
            dgvFunction3.Dock = DockStyle.Fill;

            procCard.Controls.Add(dgvFunction3);
            procCard.Controls.Add(btnRunScheduleTab2);
            procCard.Controls.Add(lblProcHdr);
            splitDetails.Panel2.Controls.Add(procCard);
            splitDetails.Panel2.Padding = new Padding(6, 3, 6, 6);

            splitTab2.Panel2.Controls.Add(splitDetails);
            tab.Controls.Add(splitTab2);
            return tab;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB 3 – FUNCTION 4: GANTT CHART + STATS
        // ══════════════════════════════════════════════════════════════════════
        private TabPage BuildTab4()
        {
            TabPage tab = new TabPage("  ③ CPU Scheduling  ") { BackColor = C_TAB_BG };

            // toolbar at top
            Panel toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 50,
                BackColor = C_CARD_BG,
                Padding   = new Padding(8, 8, 8, 8)
            };
            toolbar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_GRID_LINE, 1), 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);

            btnRunSchedule = MakeButton("▶  Run Scheduling & Draw Chart", new Point(8, 8), 260, C_OK);
            btnRunSchedule.Click += BtnRunSchedule_Click;

            lblQueueLegend = new Label
            {
                Location  = new Point(278, 13),
                Width     = 700,
                AutoSize  = false,
                Font      = FONT_SMALL8,
                ForeColor = Color.FromArgb(80, 80, 80),
                TextAlign = ContentAlignment.MiddleLeft
            };

            toolbar.Controls.AddRange(new Control[] { btnRunSchedule, lblQueueLegend });
            tab.Controls.Add(toolbar);

            // vertical split: top = gantt, bottom = stats
            SplitContainer split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 240,
                BackColor        = C_TAB_BG
            };

            // Gantt panel
            Panel ganttCard = MakeCard(DockStyle.Fill);
            Label lblGantt = new Label
            {
                Text      = "📊  Gantt Chart – CPU Scheduling Diagram",
                Font      = FONT_BOLD,
                ForeColor = C_ACCENT,
                Dock      = DockStyle.Top,
                Height    = 30,
                Padding   = new Padding(4, 4, 0, 0)
            };
            pnlGanttScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            picGantt = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize, Location = new Point(0, 0) };
            pnlGanttScroll.Controls.Add(picGantt);
            ganttCard.Controls.Add(pnlGanttScroll);
            ganttCard.Controls.Add(lblGantt);
            split.Panel1.Controls.Add(ganttCard);
            split.Panel1.Padding = new Padding(6, 6, 6, 3);

            // Stats table
            Panel statsCard = MakeCard(DockStyle.Fill);
            Label lblStats = new Label
            {
                Text      = "📋  Process Statistics",
                Font      = FONT_BOLD,
                ForeColor = C_ACCENT,
                Dock      = DockStyle.Top,
                Height    = 30,
                Padding   = new Padding(4, 4, 0, 0)
            };
            dgvStats = MakeDgv();
            dgvStats.Dock = DockStyle.Fill;
            statsCard.Controls.Add(dgvStats);
            statsCard.Controls.Add(lblStats);
            split.Panel2.Controls.Add(statsCard);
            split.Panel2.Padding = new Padding(6, 3, 6, 6);

            tab.Controls.Add(split);
            return tab;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════════
        private void BtnRead_Click(object sender, EventArgs e)
        {
            if (reader != null) reader.Close();

            string drive = txtDrive.Text.Trim().ToUpper();
            if (drive.Length != 2 || drive[1] != ':')
            {
                SetStatus("Invalid format!", C_ERR);
                MessageBox.Show("Invalid drive format! Example: F:", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            reader = new Fat32Reader(drive);
            if (!reader.IsOpen)
            {
                SetStatus("Cannot open disk", C_ERR);
                MessageBox.Show("Cannot open disk!\nPlease run the program as Administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // Function 1 – Boot Sector
                var bootInfo = reader.GetBootSectorInfo();
                dgvFunction1.DataSource = bootInfo;
                StyleDgv(dgvFunction1);
                dgvFunction1.Columns["Property"].HeaderText = "Property";
                dgvFunction1.Columns["Value"].HeaderText    = "Value";

                // Function 2 – file list
                // dgvFunction2 lives in a non-active tab whose Win32 handle has not been
                // created yet.  Without a handle, DataGridView defers AutoGenerateColumns,
                // so Columns["No"] etc. would return null and throw NullReferenceException.
                // Forcing handle creation here ensures columns are generated immediately.
                if (!dgvFunction2.IsHandleCreated)
                    _ = dgvFunction2.Handle;

                reader.ScanForTxtFiles();
                dgvFunction2.DataSource = reader.TxtFiles;
                StyleDgv(dgvFunction2);
                foreach (DataGridViewColumn col in dgvFunction2.Columns) col.Visible = false;

                dgvFunction2.Columns["No"].Visible       = true;
                dgvFunction2.Columns["Name"].Visible     = true;
                dgvFunction2.Columns["FullPath"].Visible = true;
                dgvFunction2.Columns["FileSize"].Visible = true;

                dgvFunction2.Columns["No"].HeaderText       = "No.";
                dgvFunction2.Columns["Name"].HeaderText     = "File Name";
                dgvFunction2.Columns["FullPath"].HeaderText = "Full Path";
                dgvFunction2.Columns["FileSize"].HeaderText = "Size (bytes)";

                dgvFunction2.Columns["No"].Width = 50;

                ApplyAlternatingRows(dgvFunction2);

                tabControl.SelectedIndex = 0;
                SetStatus($"✓ {reader.TxtFiles.Count} .txt files", C_OK);
                MessageBox.Show($"Successfully read disk {drive}!\nFound {reader.TxtFiles.Count} *.txt files.\n\nSwitch to tab ② to select a file.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Error!", C_ERR);
                MessageBox.Show("Disk read error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvFunction2_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || reader == null || e.RowIndex >= reader.TxtFiles.Count) return;

            var f = reader.TxtFiles[e.RowIndex];

            // File info card
            lblFileName.Text = f.Name;
            lblFileDate.Text = reader.ParseDate(f.CreationDate);
            lblFileTime.Text = reader.ParseTime(f.CreationTime); // Gán giờ vào nhãn mới
            lblFileSize.Text = $"{f.FileSize:N0} bytes";

            try
            {
                reader.ParseTxtFile(f);

                var rows = reader.ParsedProcesses.Select(p =>
                {
                    var q = reader.ParsedQueues.FirstOrDefault(x => x.queueID == p.queueID);
                    return new
                    {
                        ProcessID        = p.processID,
                        ArrivalTime      = p.arrivalTime,
                        CPUBurstTime     = p.burstTime,
                        PriorityQueueID  = p.queueID,
                        TimeSlice        = q != null ? q.timeSlice.ToString() : "N/A",
                        Algorithm        = q != null ? q.schedulingPolicy : "N/A"
                    };
                }).ToList();

                dgvFunction3.DataSource = rows;
                StyleDgv(dgvFunction3);
                dgvFunction3.Columns["ProcessID"].HeaderText       = "Process ID";
                dgvFunction3.Columns["ArrivalTime"].HeaderText     = "Arrival Time";
                dgvFunction3.Columns["CPUBurstTime"].HeaderText    = "CPU Burst Time";
                dgvFunction3.Columns["PriorityQueueID"].HeaderText = "Queue ID";
                dgvFunction3.Columns["TimeSlice"].HeaderText       = "Time Slice";
                dgvFunction3.Columns["Algorithm"].HeaderText       = "Algorithm";
                ApplyAlternatingRows(dgvFunction3);

                // Show details panel
                splitTab2.Panel2Collapsed = false;
                // Guard against SplitterDistance being out of the valid range when the
                // container hasn't been fully laid out yet (would throw ArgumentOutOfRangeException).
                int half = splitTab2.Height / 2;
                int minD = splitTab2.Panel1MinSize;
                int maxD = splitTab2.Height - splitTab2.Panel2MinSize - splitTab2.SplitterWidth;
                if (half >= minD && half <= maxD)
                    splitTab2.SplitterDistance = half;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading/parsing file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                dgvFunction3.DataSource = null;
            }
        }

        private void BtnRunSchedule_Click(object sender, EventArgs e)
        {
            if (reader == null || reader.ParsedProcesses.Count == 0)
            {
                MessageBox.Show("Please select a *.txt file in tab ② before running scheduling!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Deep-copy lists so scheduling doesn't mutate the original reader data
            var procs  = reader.ParsedProcesses.Select(p => new Process(p.processID, p.arrivalTime, p.burstTime, p.queueID)).ToList();
            var queues = reader.ParsedQueues.Select(q => new Queue(q.queueID, q.timeSlice, q.schedulingPolicy)).ToList();

            SchedulingResult scheduler = new SchedulingResult(procs, queues);
            scheduler.RunScheduling();

            DrawGanttChart(scheduler);
            FillStatsTable(scheduler);
            BuildQueueLegend(scheduler);

            tabControl.SelectedIndex = 2; // switch to ③ CPU Scheduling tab
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GANTT CHART DRAWING
        // ══════════════════════════════════════════════════════════════════════
        private void DrawGanttChart(SchedulingResult scheduler)
        {
            if (scheduler.logs.Count == 0) return;

            // Collect distinct queues to draw one row per queue
            var queueIDs = scheduler.logs.Select(l => l.queueID).Distinct().OrderBy(x => x).ToList();
            int rowCount = queueIDs.Count;

            int maxTime = scheduler.logs.Max(l => l.endTime);

            const int PX   = 28;   // pixels per time unit
            const int PAD  = 50;   // left/right padding
            const int TOP  = 30;   // top padding for labels
            const int ROW_H= 52;   // height of each queue row
            const int BAR_H= 36;   // bar height inside row
            const int BAR_TOP_MARGIN = (ROW_H - BAR_H) / 2;
            const int AXIS_EXTRA = 30; // space below last row for time labels

            int imgWidth  = Math.Max(900, maxTime * PX + PAD * 2);
            int imgHeight = TOP + rowCount * ROW_H + AXIS_EXTRA + 20;

            Bitmap bmp = new Bitmap(imgWidth, imgHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // colour map: fixed palette for up to 12 processes
                Color[] palette =
                {
                    Color.FromArgb(100, 149, 237), Color.FromArgb(144, 238, 144),
                    Color.FromArgb(255, 179,  71), Color.FromArgb(240, 128, 128),
                    Color.FromArgb(147, 112, 219), Color.FromArgb( 64, 224, 208),
                    Color.FromArgb(255, 215,   0), Color.FromArgb(176, 224, 230),
                    Color.FromArgb(250, 128, 114), Color.FromArgb(152, 251, 152),
                    Color.FromArgb(221, 160, 221), Color.FromArgb(173, 216, 230)
                };
                var procList  = scheduler.logs.Select(l => l.processID).Distinct().OrderBy(x => x).ToList();
                var colorMap  = new Dictionary<string, Color>();
                for (int ci = 0; ci < procList.Count; ci++)
                    colorMap[procList[ci]] = palette[ci % palette.Length];

                using Pen  pAxis  = new Pen(Color.FromArgb(100, 100, 100), 1.5f);
                using Pen  pGrid  = new Pen(Color.FromArgb(220, 220, 220), 1) { DashStyle = DashStyle.Dot };

                // ── vertical grid lines ──────────────────────────────────────
                for (int t = 0; t <= maxTime; t++)
                {
                    int gx = PAD + t * PX;
                    g.DrawLine(pGrid, gx, TOP, gx, TOP + rowCount * ROW_H);
                }

                // ── draw per-queue row ───────────────────────────────────────
                for (int ri = 0; ri < queueIDs.Count; ri++)
                {
                    string qid = queueIDs[ri];
                    int rowY   = TOP + ri * ROW_H;

                    // row background
                    Color rowBg = ri % 2 == 0 ? Color.FromArgb(248, 250, 255) : Color.FromArgb(240, 245, 255);
                    g.FillRectangle(new SolidBrush(rowBg), PAD, rowY, maxTime * PX, ROW_H);

                    // queue label on left
                    g.DrawString(qid, FONT_GANTT_LB, new SolidBrush(C_HEADER_BG),
                        new RectangleF(2, rowY + (ROW_H - 16) / 2, PAD - 4, 16),
                        new StringFormat { Alignment = StringAlignment.Far });

                    // horizontal separator
                    g.DrawLine(pAxis, PAD, rowY + ROW_H, PAD + maxTime * PX, rowY + ROW_H);

                    // blocks for this queue
                    foreach (var log in scheduler.logs.Where(l => l.queueID == qid))
                    {
                        int bx = PAD + log.startTime * PX;
                        int bw = (log.endTime - log.startTime) * PX;
                        int by = rowY + BAR_TOP_MARGIN;

                        Rectangle rect = new Rectangle(bx, by, bw, BAR_H);

                        Color c = colorMap.TryGetValue(log.processID, out Color mc) ? mc : Color.FromArgb(200, 200, 200);

                        // gradient fill
                        using (LinearGradientBrush gb = new LinearGradientBrush(
                            rect, c, ControlPaint.Light(c, 0.4f), LinearGradientMode.Vertical))
                            g.FillRectangle(gb, rect);

                        g.DrawRectangle(new Pen(ControlPaint.Dark(c, 0.15f), 1), rect);

                        // process label centred
                        if (bw >= 16)
                        {
                            SizeF ts = g.MeasureString(log.processID, FONT_GANTT_LB);
                            if (ts.Width <= bw - 2)
                                g.DrawString(log.processID, FONT_GANTT_LB, Brushes.Black,
                                    bx + (bw - ts.Width) / 2, by + (BAR_H - ts.Height) / 2);
                        }
                    }
                }

                // ── time axis ───────────────────────────────────────────────
                int axisY = TOP + rowCount * ROW_H;
                g.DrawLine(pAxis, PAD, axisY, PAD + maxTime * PX, axisY);

                for (int t = 0; t <= maxTime; t++)
                {
                    int tx = PAD + t * PX;
                    g.DrawLine(pAxis, tx, axisY, tx, axisY + 5);
                    SizeF ts = g.MeasureString(t.ToString(), FONT_GANTT_AX);
                    g.DrawString(t.ToString(), FONT_GANTT_AX, Brushes.Black, tx - ts.Width / 2, axisY + 7);
                }
            }

            picGantt.Image = bmp;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATS TABLE
        // ══════════════════════════════════════════════════════════════════════
        private void FillStatsTable(SchedulingResult scheduler)
        {
            dgvStats.Columns.Clear();
            dgvStats.Columns.Add("Process",     "Process ID");
            dgvStats.Columns.Add("Queue",       "Queue ID");
            dgvStats.Columns.Add("Arrival",     "Arrival Time");
            dgvStats.Columns.Add("Burst",       "Burst Time");
            dgvStats.Columns.Add("Completion",  "Completion Time");
            dgvStats.Columns.Add("Turnaround",  "Turnaround Time");
            dgvStats.Columns.Add("Waiting",     "Waiting Time");

            StyleDgv(dgvStats);

            double totalWT = 0, totalTT = 0;
            foreach (var p in scheduler.processes)
            {
                int idx = dgvStats.Rows.Add(p.processID, p.queueID, p.arrivalTime, p.burstTime,
                                             p.completionTime, p.turnaroundTime, p.waitingTime);
                dgvStats.Rows[idx].DefaultCellStyle.BackColor =
                    idx % 2 == 0 ? Color.White : C_ROW_ALT;
                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }

            int cnt = scheduler.processes.Count;
            int avgRow = dgvStats.Rows.Add(
                "AVERAGE", "—", "—", "—", "—",
                (totalTT / cnt).ToString("F2"),
                (totalWT / cnt).ToString("F2"));

            dgvStats.Rows[avgRow].DefaultCellStyle.Font      = FONT_BOLD;
            dgvStats.Rows[avgRow].DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 200);
            dgvStats.Rows[avgRow].DefaultCellStyle.ForeColor = Color.FromArgb(60, 60, 60);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  QUEUE LEGEND LABEL (toolbar of tab 4)
        // ══════════════════════════════════════════════════════════════════════
        private void BuildQueueLegend(SchedulingResult scheduler)
        {
            var parts = scheduler.queues
                .OrderBy(q => q.queueID)
                .Select(q => $"{q.queueID}: {q.schedulingPolicy} (slice={q.timeSlice})");
            lblQueueLegend.Text = "Queues: " + string.Join("   |   ", parts);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private static Panel MakeCard(DockStyle dock)
        {
            return new Panel
            {
                Dock        = dock,
                BackColor   = C_CARD_BG,
                Padding     = new Padding(8),
                Margin      = new Padding(6),
            };
        }

        private static DataGridView MakeDgv()
        {
            var dgv = new DataGridView
            {
                ReadOnly              = true,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToResizeRows = false,
                BorderStyle           = BorderStyle.None,
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor             = C_GRID_LINE,
                BackgroundColor       = Color.White,
                Font                  = FONT_MONO,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor   = C_HDR_GRID,
                ForeColor   = Color.White,
                Font        = FONT_BOLD9,
                Alignment   = DataGridViewContentAlignment.MiddleLeft,
                Padding     = new Padding(4, 0, 0, 0)
            };
            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor   = Color.White,
                ForeColor   = Color.FromArgb(30, 30, 30),
                SelectionBackColor = C_ROW_SEL,
                SelectionForeColor = Color.Black,
                Padding     = new Padding(4, 0, 0, 0)
            };
            dgv.EnableHeadersVisualStyles = false;
            return dgv;
        }

        private static void StyleDgv(DataGridView dgv)
        {
            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor   = C_HDR_GRID,
                ForeColor   = Color.White,
                Font        = FONT_BOLD9,
                Alignment   = DataGridViewContentAlignment.MiddleLeft,
                Padding     = new Padding(4, 0, 0, 0)
            };
            dgv.EnableHeadersVisualStyles = false;
        }

        private static void ApplyAlternatingRows(DataGridView dgv)
        {
            for (int i = 0; i < dgv.Rows.Count; i++)
                dgv.Rows[i].DefaultCellStyle.BackColor = i % 2 == 0 ? Color.White : C_ROW_ALT;
        }

        private static Button MakeButton(string text, Point loc, int width, Color backColor)
        {
            var btn = new Button
            {
                Text      = text,
                Location  = loc,
                Width     = width,
                Height    = 32,
                Font      = FONT_BOLD9,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => ((Button)s).BackColor = ControlPaint.Dark(backColor, 0.1f);
            btn.MouseLeave += (s, e) => ((Button)s).BackColor = backColor;
            return btn;
        }

        private static Label MakeInfoCaption(string text) =>
            new Label
            {
                Text      = text,
                Font      = FONT_BOLD9,
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize  = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

        private void SetStatus(string msg, Color color)
        {
            lblStatus.Text      = msg;
            lblStatus.ForeColor = color;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (reader != null) reader.Close();
            base.OnFormClosing(e);
        }
    }
}
