using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
namespace OS_Lab02_FAT32
{
    public partial class MainForm : Form
    {
        private Fat32Reader reader = null!;

        // Các Control Giao diện
        private TabControl tabControl = null!;
        private TextBox txtDrive = null!;
        private Button btnRead = null!;
        private DataGridView dgvFunction1 = null!;
        private DataGridView dgvFunction2 = null!;
        private Label lblFileInfo = null!;
        private DataGridView dgvFunction3 = null!;

        // Control cho Tab 4
        private Button btnRunSchedule = null!;
        private PictureBox picGantt = null!;
        private DataGridView dgvStats = null!;

        public MainForm()
        {
            this.Text = "Hệ Điều Hành - Lab 02 - Project FAT32 & Scheduling";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Panel trên cùng chứa ô nhập đĩa
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 50 };
            topPanel.Controls.Add(new Label { Text = "Nhập ổ đĩa (VD: F:)", Location = new Point(10, 15), AutoSize = true });
            txtDrive = new TextBox { Location = new Point(140, 12), Width = 50, Text = "F:" };
            btnRead = new Button { Text = "Khởi tạo && Đọc đĩa", Location = new Point(200, 10), Width = 160, Font = new Font("Arial", 9, FontStyle.Bold), BackColor = Color.LightBlue };
            btnRead.Click += BtnRead_Click;
            topPanel.Controls.Add(txtDrive);
            topPanel.Controls.Add(btnRead);
            this.Controls.Add(topPanel);

            // Tab Control chia trang
            tabControl = new TabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabControl);
            tabControl.BringToFront();

            // ================= TAB 1: FUNCTION 1 =================
            TabPage tab1 = new TabPage("Function 1: Boot Sector");
            dgvFunction1 = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                Font = new Font("Courier New", 9)
            };
            tab1.Controls.Add(dgvFunction1);
            tabControl.TabPages.Add(tab1);

            // ================= TAB 2: FUNCTION 2 =================
            TabPage tab2 = new TabPage("Function 2: Danh sách file *.txt");
            GroupBox gbFunc2 = new GroupBox { Text = "Danh sách file *.txt trên đĩa (Click vào dòng để xem chi tiết ở Tab 3)", Dock = DockStyle.Fill };
            dgvFunction2 = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Courier New", 9)
            };
            dgvFunction2.CellClick += DgvFunction2_CellClick;
            gbFunc2.Controls.Add(dgvFunction2);
            tab2.Controls.Add(gbFunc2);
            tabControl.TabPages.Add(tab2);

            // ================= TAB 3: FUNCTION 3 =================
            TabPage tab3 = new TabPage("Function 3: Chi tiết file & Tiến trình");
            GroupBox gbFunc3 = new GroupBox { Text = "Chi tiết file đã chọn & Bảng thông tin tiến trình", Dock = DockStyle.Fill };
            lblFileInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 70,
                Font = new Font("Courier New", 9, FontStyle.Bold),
                BackColor = Color.LightCyan,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6)
            };
            dgvFunction3 = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                Font = new Font("Courier New", 9)
            };
            gbFunc3.Controls.Add(dgvFunction3);
            gbFunc3.Controls.Add(lblFileInfo);
            tab3.Controls.Add(gbFunc3);
            tabControl.TabPages.Add(tab3);

            // ================= TAB 4: FUNCTION 4 (VẼ GANTT CHART) =================
            TabPage tab4 = new TabPage("Function 4: CPU Scheduling Simulator");

            Panel topTab4 = new Panel { Dock = DockStyle.Top, Height = 40 };
            btnRunSchedule = new Button { Text = "▶ Chạy Thuật Toán & Vẽ Biểu Đồ", Location = new Point(10, 5), Width = 260, Font = new Font("Arial", 9, FontStyle.Bold), BackColor = Color.LightGreen };
            btnRunSchedule.Click += BtnRunSchedule_Click;
            topTab4.Controls.Add(btnRunSchedule);
            tab4.Controls.Add(topTab4);

            SplitContainer split4 = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 220 };

            // Khu vực vẽ biểu đồ Gantt
            GroupBox gbGantt = new GroupBox { Text = "Gantt Chart – CPU SCHEDULING DIAGRAM", Dock = DockStyle.Fill };
            Panel pnlGantt = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            picGantt = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize, Location = new Point(0, 0) };
            pnlGantt.Controls.Add(picGantt);
            gbGantt.Controls.Add(pnlGantt);
            split4.Panel1.Controls.Add(gbGantt);

            // Khu vực hiển thị bảng thống kê
            GroupBox gbStats = new GroupBox { Text = "PROCESS STATISTICS – Bảng thống kê", Dock = DockStyle.Fill };
            dgvStats = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                Font = new Font("Courier New", 9)
            };
            gbStats.Controls.Add(dgvStats);
            split4.Panel2.Controls.Add(gbStats);

            tab4.Controls.Add(split4);
            tabControl.TabPages.Add(tab4);
        }

        private void BtnRead_Click(object sender, EventArgs e)
        {
            if (reader != null) reader.Close();

            string drive = txtDrive.Text.Trim();
            if (drive.Length != 2 || drive[1] != ':') { MessageBox.Show("Sai định dạng ổ đĩa!"); return; }

            reader = new Fat32Reader(drive);
            if (!reader.IsOpen)
            {
                MessageBox.Show("Không thể mở đĩa! Nhớ Run as Administrator.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // Tab 1 – Function 1: Boot Sector
                dgvFunction1.DataSource = reader.GetBootSectorInfo();
                dgvFunction1.Columns["Property"].HeaderText = "Attribute";

                // Tab 2 – Function 2: Danh sách file *.txt
                reader.ScanForTxtFiles();
                dgvFunction2.DataSource = reader.TxtFiles;
                foreach (DataGridViewColumn col in dgvFunction2.Columns)
                    col.Visible = false;
                dgvFunction2.Columns["No"].Visible = true;
                dgvFunction2.Columns["Name"].Visible = true;
                dgvFunction2.Columns["FullPath"].Visible = true;
                dgvFunction2.Columns["No"].HeaderText = "No.";
                dgvFunction2.Columns["Name"].HeaderText = "File Name";
                dgvFunction2.Columns["FullPath"].HeaderText = "Full Path";

                // Chuyển sang Tab 1 để người dùng xem Boot Sector ngay
                tabControl.SelectedIndex = 0;
                MessageBox.Show($"Đọc đĩa thành công! Tìm thấy {reader.TxtFiles.Count} file *.txt.\nHãy sang Tab 2 để chọn file.", "Thành công");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi đọc đĩa"); }
        }

        private void DgvFunction2_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || reader == null) return;
            var selectedFile = reader.TxtFiles[e.RowIndex];

            // Tab 3 – Function 3: Hiển thị thông tin file theo đúng format Lab2.cs
            lblFileInfo.Text = $"  Name         : {selectedFile.Name}\r\n" +
                               $"  Date Created : {reader.ParseDate(selectedFile.CreationDate)}    " +
                               $"Time Created : {reader.ParseTime(selectedFile.CreationTime)}\r\n" +
                               $"  Total Size   : {selectedFile.FileSize} bytes";
            try
            {
                reader.ParseTxtFile(selectedFile);

                // Gộp Process và Queue để hiển thị đúng bảng của Function 3
                var displayData = reader.ParsedProcesses.Select(p =>
                {
                    var q = reader.ParsedQueues.FirstOrDefault(queue => queue.queueID == p.queueID);
                    return new
                    {
                        ProcessID = p.processID,
                        ArrivalTime = p.arrivalTime,
                        CPUBurstTime = p.burstTime,
                        PriorityQueueID = p.queueID,
                        TimeSlice = q != null ? q.timeSlice.ToString() : "N/A",
                        Algorithm = q != null ? q.schedulingPolicy : "N/A"
                    };
                }).ToList();

                dgvFunction3.DataSource = displayData;
                dgvFunction3.Columns["ProcessID"].HeaderText = "Process ID";
                dgvFunction3.Columns["ArrivalTime"].HeaderText = "Arrival Time";
                dgvFunction3.Columns["CPUBurstTime"].HeaderText = "CPU Burst Time";
                dgvFunction3.Columns["PriorityQueueID"].HeaderText = "Priority Queue ID";
                dgvFunction3.Columns["TimeSlice"].HeaderText = "Time Slice";
                dgvFunction3.Columns["Algorithm"].HeaderText = "Scheduling Algorithm Name";

                // Tự động chuyển sang Tab 3 để xem chi tiết
                tabControl.SelectedIndex = 2;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi đọc file: " + ex.Message);
                dgvFunction3.DataSource = null;
            }
        }

        private void BtnRunSchedule_Click(object sender, EventArgs e)
        {
            if (reader == null || reader.ParsedProcesses.Count == 0)
            {
                MessageBox.Show("Vui lòng sang Tab 2 chọn file *.txt, rồi sang Tab 3 xem chi tiết trước khi chạy lập lịch!");
                return;
            }

            SchedulingResult scheduler = new SchedulingResult(reader.ParsedProcesses, reader.ParsedQueues);
            scheduler.RunScheduling();

            // Gọi 2 hàm mới để vẽ ảnh và tạo bảng
            DrawGanttChart(scheduler);
            FillStatsTable(scheduler);
        }

        // ==============================================================
        // HÀM VẼ GANTT CHART ĐỈNH CAO BẰNG SYSTEM.DRAWING
        // ==============================================================
        private void DrawGanttChart(SchedulingResult scheduler)
        {
            if (scheduler.logs.Count == 0) return;

            // 1. Tính toán kích thước bức ảnh dựa trên tổng thời gian chạy
            int maxTime = 0;
            foreach (var log in scheduler.logs)
                if (log.endTime > maxTime) maxTime = log.endTime;

            int pixelsPerTimeUnit = 30; // Mỗi 1 giây/đơn vị thời gian sẽ vẽ rộng 30 pixels
            int imgWidth = Math.Max(800, maxTime * pixelsPerTimeUnit + 100);
            int imgHeight = 150;

            // 2. Tạo một bức ảnh trống (Bitmap)
            Bitmap bmp = new Bitmap(imgWidth, imgHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Tạo từ điển lưu màu ngẫu nhiên cho từng tiến trình
                Dictionary<string, Color> processColors = new Dictionary<string, Color>();
                Random rnd = new Random(10); // Seed cố định để màu không bị giật đổi liên tục

                int startX = 20;
                int startY = 40;
                int barHeight = 60;

                // Vẽ một đường thẳng đen làm trục thời gian
                g.DrawLine(new Pen(Color.Black, 2), startX, startY + barHeight, imgWidth - 20, startY + barHeight);

                // 3. Duyệt qua từng đoạn log và vẽ các khối màu
                foreach (var log in scheduler.logs)
                {
                    if (!processColors.ContainsKey(log.processID))
                    {
                        // Random màu sáng sủa
                        processColors[log.processID] = Color.FromArgb(rnd.Next(100, 240), rnd.Next(100, 240), rnd.Next(100, 240));
                    }

                    int x = startX + log.startTime * pixelsPerTimeUnit;
                    int w = (log.endTime - log.startTime) * pixelsPerTimeUnit;

                    Rectangle rect = new Rectangle(x, startY, w, barHeight);

                    // Tô màu khối hộp
                    using (Brush b = new SolidBrush(processColors[log.processID]))
                    {
                        g.FillRectangle(b, rect);
                    }
                    // Vẽ viền đen cho khối hộp
                    g.DrawRectangle(Pens.Black, rect);

                    // Vẽ tên Process (VD: P1, P2) vào chính giữa khối hộp
                    using (Font f = new Font("Arial", 11, FontStyle.Bold))
                    {
                        SizeF textSize = g.MeasureString(log.processID, f);
                        float textX = x + (w - textSize.Width) / 2;
                        float textY = startY + (barHeight - textSize.Height) / 2;
                        g.DrawString(log.processID, f, Brushes.Black, textX, textY);
                    }

                    // Vẽ các mốc thời gian (StartTime và EndTime) ở bên dưới
                    using (Font f = new Font("Arial", 9, FontStyle.Regular))
                    {
                        g.DrawString(log.startTime.ToString(), f, Brushes.Black, x - 5, startY + barHeight + 5);
                        g.DrawString(log.endTime.ToString(), f, Brushes.Black, x + w - 5, startY + barHeight + 5);

                        // Vẽ vạch nhỏ đánh dấu trên trục thời gian
                        g.DrawLine(Pens.Black, x, startY + barHeight, x, startY + barHeight + 5);
                        g.DrawLine(Pens.Black, x + w, startY + barHeight, x + w, startY + barHeight + 5);
                    }
                }
            }

            // Gắn bức ảnh vừa vẽ xong vào PictureBox
            picGantt.Image = bmp;
        }

        // ==============================================================
        // HÀM ĐỔ DỮ LIỆU THỐNG KÊ (WT, TT) VÀO DATAGRIDVIEW
        // ==============================================================
        private void FillStatsTable(SchedulingResult scheduler)
        {
            dgvStats.Columns.Clear();
            dgvStats.Columns.Add("Process", "Process ID");
            dgvStats.Columns.Add("Arrival", "Arrival Time");
            dgvStats.Columns.Add("Burst", "Burst Time");
            dgvStats.Columns.Add("Completion", "Completion Time");
            dgvStats.Columns.Add("Turnaround", "Turnaround Time");
            dgvStats.Columns.Add("Waiting", "Waiting Time");

            double totalWT = 0, totalTT = 0;
            foreach (var p in scheduler.processes)
            {
                dgvStats.Rows.Add(p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime);
                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }

            // Tính trung bình và highlight dòng cuối cùng bằng màu vàng
            int rowIndex = dgvStats.Rows.Add("AVERAGE", "-", "-", "-", (totalTT / scheduler.processes.Count).ToString("F2"), (totalWT / scheduler.processes.Count).ToString("F2"));
            dgvStats.Rows[rowIndex].DefaultCellStyle.Font = new Font("Arial", 10, FontStyle.Bold);
            dgvStats.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightYellow;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (reader != null) reader.Close();
            base.OnFormClosing(e);
        }
    }
}