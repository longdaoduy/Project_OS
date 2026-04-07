using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS_Lab02_FAT32
{
    // =========================================================
    // Scheduling classes – ported from Lab 01 (dl.cs)
    // =========================================================
    public class executionLog
    {
        public int startTime { get; set; }
        public int endTime { get; set; }
        public string processID { get; set; } = "";
        public string queueID { get; set; } = "";
    }
    public class Queue
    {
        public string queueID;
        public int timeSlice;
        public string schedulingPolicy;
        public int remainingTime;
        public List<Process> readyQueue = new List<Process>();
        public Queue(string id, int ts, string sp)
        {
            queueID = id;
            timeSlice = ts;
            schedulingPolicy = sp;
            remainingTime = ts;
        }
    }

    public class Process
    {
        public string processID { get; set; }
        public int arrivalTime { get; set; }
        public int burstTime { get; set; }
        public int remainingTime { get; set; }
        public string queueID { get; set; }
        public int completionTime { get; set; }
        public int turnaroundTime { get; set; }
        public int waitingTime { get; set; }

        public Process(string pid, int at, int bt, string qid)
        {
            processID = pid;
            arrivalTime = at;
            burstTime = bt;
            remainingTime = bt;
            queueID = qid;
        }
        public void calculateMetrics()
        {
            turnaroundTime = completionTime - arrivalTime;
            waitingTime = turnaroundTime - burstTime;
        }
    }
    public class SchedulingResult
    {
        public List<Process> processes { get; set; }
        public List<Queue> queues { get; set; }
        public List<executionLog> logs = new List<executionLog>();
        public SchedulingResult(List<Process> p, List<Queue> q)
        {
            processes = p;
            queues = q;
        }
        // gộp các mốc thời gian của cùng một process (VD: [36-39] và [39-40] thành [36-40])
        private void AddLog(int start, int end, string qID, string pID)
        {
            if (start == end) return;
            if (logs.Count > 0)
            {
                var lastLog = logs[logs.Count - 1];
                if (lastLog.endTime == start && lastLog.queueID == qID && lastLog.processID == pID)
                {
                    lastLog.endTime = end;
                    return;
                }
            }
            logs.Add(new executionLog { startTime = start, endTime = end, queueID = qID, processID = pID });
        }

        public void RunScheduling()
        {
            int completedProcesses = 0;
            int currentTime = 0;
            int i = 0;
            int queueIndex = 0;
            Process? currentProcess = null;

            queues.Sort((q1,q2) => q2.timeSlice.CompareTo(q1.timeSlice)); // Sắp xếp các queue theo time slice giảm dần để ưu tiên queue có time slice lớn hơn
            processes.Sort((p1, p2) => p1.arrivalTime.CompareTo(p2.arrivalTime));

            while (completedProcesses < processes.Count)
            {
                // Thêm tiến trình vào các hàng đợi tương ứng khi đến giờ
                while (i < processes.Count && processes[i].arrivalTime <= currentTime)
                {
                    foreach (Queue q in queues)
                    {
                        if (q.queueID == processes[i].queueID)
                        {
                            q.readyQueue.Add(processes[i]);
                            
                            if (q.schedulingPolicy == "SRTN")
                            {
                                q.readyQueue.Sort((p1, p2) => p1.remainingTime.CompareTo(p2.remainingTime)); // Nếu queue policy là SRTN, sắp xếp lại theo remaining time
                            }
                            else if (q.schedulingPolicy == "SJF")
                            {
                                q.readyQueue.Sort((p1, p2) => p1.burstTime.CompareTo(p2.burstTime)); // Nếu queue policy là SJF, sắp xếp lại theo burst time
                            }
                            break;
                        }
                    }
                    i++;
                }

                Queue currentQueue = queues[queueIndex];
                if (currentQueue.remainingTime == 0)
                {
                    currentQueue.remainingTime = currentQueue.timeSlice;
                    queueIndex = (queueIndex + 1) % queues.Count;
                    currentQueue = queues[queueIndex];
                }

                // Nếu queue hiện tại rỗng, kiểm tra xem có nên tăng thời gian hay chuyển queue
                if (currentQueue.readyQueue.Count == 0)
                {
                    bool allEmpty = true;
                    foreach (var q in queues)
                    {
                        if (q.readyQueue.Count > 0) { allEmpty = false; break; }
                    }
                    if (allEmpty)
                    {
                        currentTime++; // Tất cả queue đều rỗng, tăng thời gian lên chờ process mới
                    }
                    else
                    {
                        // Chuyển sang queue tiếp theo (bỏ qua queue rỗng)
                        currentQueue.remainingTime = currentQueue.timeSlice; // Reset thời gian của queue mới
                        queueIndex = (queueIndex + 1) % queues.Count;
                    }
                    continue;
                }

                int prevTime = currentTime;
                currentProcess = currentQueue.readyQueue[0];
                int executionTime = Math.Min(currentProcess.remainingTime, currentQueue.remainingTime);
                // Xử lý dựa theo Policy của Queue
                if (currentQueue.schedulingPolicy == "SJF")
                {
                    currentTime += executionTime; // SJF nhảy cóc thời gian
                    currentProcess.remainingTime -= executionTime;
                    currentQueue.remainingTime -= executionTime;
                }
                else if (currentQueue.schedulingPolicy == "SRTN")
                {
                    int timeToNextArrival = int.MaxValue;
                    if (i < processes.Count) 
                    {
                        timeToNextArrival = processes[i].arrivalTime - currentTime;
                    }
                    
                    executionTime = Math.Min(executionTime, timeToNextArrival); 

                    currentTime += executionTime; // SRTN nhảy cóc thời gian
                    currentProcess.remainingTime -= executionTime;
                    currentQueue.remainingTime -= executionTime;
                }

                AddLog(prevTime, currentTime, currentQueue.queueID, currentProcess.processID);

                if (currentProcess != null && currentProcess.remainingTime == 0)
                {
                    currentProcess.completionTime = currentTime;
                    currentProcess.calculateMetrics();
                    currentQueue.readyQueue.Remove(currentProcess);
                    completedProcesses++;
                }
            }
        }
        public void PrintSchedulingDiagram()
        {
            Console.WriteLine("=================== CPU SCHEDULING DIAGRAM ===================\n");
            Console.WriteLine("{0,-18} {1,-10} {2,-10}", "[Start - End]", "Queue", "Process");
            Console.WriteLine("-----------------------------------------");
            foreach (var log in logs)
            {
                Console.WriteLine("{0,-18} {1,-10} {2,-10}", $"[{log.startTime} - {log.endTime}]", log.queueID, log.processID);
            }
            
            Console.WriteLine("\n");
            Console.WriteLine("===================== PROCESS STATISTICS =====================\n");
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                "Process", "Arrival", "Burst", "Completion", "Turnaround", "Waiting");
            Console.WriteLine("--------------------------------------------------------------");
            

            double totalWT = 0, totalTT = 0;
            foreach (var p in processes)
            {
                Console.WriteLine("{0,-10}{1,-10}{2,-10}{3,-12}{4,-12}{5,-10}",
                    p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime);

                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }

            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine($"Average Turnaround Time: {totalTT / processes.Count:F1}");
            Console.WriteLine($"Average Waiting Time:    {totalWT / processes.Count:F1}");
            Console.WriteLine("==============================================================");
        }
    }

    // =========================================================
    // Win32 API declarations
    // =========================================================
    static class WinAPI
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] uint dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] uint dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] uint dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        // Cần thiết để seek tới vùng FAT và RDET khi làm Function 2 và 3
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetFilePointerEx(
            SafeFileHandle hFile,
            long liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);
    }

    // =========================================================
    // Fat32Reader – chứa toàn bộ logic đọc ổ đĩa FAT32
    // =========================================================
    class Fat32Reader
    {
        // Hằng số Win32
        const uint GENERIC_READ     = 0x80000000;
        const uint FILE_SHARE_READ  = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING    = 3;
        const uint FILE_BEGIN       = 0;

        private SafeFileHandle _handle;

        // Các trường boot sector – được điền bởi Function1(), dùng lại ở Function2() và Function3()
        private short _bytesPerSector;
        private byte  _sectorsPerCluster;
        private short _reservedSectors;
        private byte  _numberOfFATs;
        private int   _sectorsPerFAT;
        private int   _totalSectors;
        private int   _rootCluster;   // cluster đầu tiên của thư mục gốc (offset 0x2C)

        // Danh sách file .txt tìm được (dùng cho Function 2 và 3)
        private struct TxtFileInfo
        {
            public string FullPath;       // đường dẫn đầy đủ, VD: \docs\input.txt
            public string Name;           // tên file có đuôi, VD: input.txt
            public ushort CreationTime;   // FAT16 time format
            public ushort CreationDate;   // FAT16 date format
            public int    FirstCluster;   // cluster đầu tiên của file
            public uint   FileSize;       // kích thước file (bytes)
        }

        private List<TxtFileInfo> _txtFiles = new List<TxtFileInfo>();
        private bool _scanned         = false;
        private bool _bootSectorReady = false;

                // Data stored by Function3 for use in Function4
        private List<Queue>?   _schedulingQueues    = null;
        private List<Process>? _schedulingProcesses = null;

        public Fat32Reader(string driveLetter)
        {
            string drivePath = "\\\\.\\" + driveLetter;
            _handle = WinAPI.CreateFile(
                drivePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);
        }

        public bool IsOpen => _handle != null && !_handle.IsInvalid;

        public void Close() => _handle?.Close();

        // ----------------------------------------------------------
        // Helper: seek tới byte offset tuyệt đối rồi đọc dữ liệu
        // ----------------------------------------------------------
        private bool ReadAt(long byteOffset, byte[] buffer, uint count)
        {
            long newPos;
            if (!WinAPI.SetFilePointerEx(_handle, byteOffset, out newPos, FILE_BEGIN))
                return false;

            uint bytesRead;
            return WinAPI.ReadFile(_handle, buffer, count, out bytesRead, IntPtr.Zero)
                   && bytesRead == count;
        }

        // ==========================================================
        // Function 1 – Đọc và hiển thị các thông số Boot Sector
        // ==========================================================
        public void Function1()
        {
            byte[] bootSector = new byte[512];
            if (!ReadAt(0, bootSector, 512))
            {
                Console.WriteLine("Lỗi: Không thể đọc Boot Sector.");
                return;
            }

            // Đọc các trường từ Boot Sector và lưu vào fields để Function2/3 dùng lại
            _bytesPerSector    = BitConverter.ToInt16(bootSector, 11);  // Offset 0x0B, 2 bytes
            _sectorsPerCluster = bootSector[13];                        // Offset 0x0D, 1 byte
            _reservedSectors   = BitConverter.ToInt16(bootSector, 14);  // Offset 0x0E, 2 bytes
            _numberOfFATs      = bootSector[16];                        // Offset 0x10, 1 byte
            _sectorsPerFAT     = BitConverter.ToInt32(bootSector, 36);  // Offset 0x24, 4 bytes
            _totalSectors      = BitConverter.ToInt32(bootSector, 32);  // Offset 0x20, 4 bytes
            _rootCluster       = BitConverter.ToInt32(bootSector, 44);  // Offset 0x2C, 4 bytes

            // Số entry RDET (FAT32 luôn = 0)
            short rdetEntries = BitConverter.ToInt16(bootSector, 17);   // Offset 0x11, 2 bytes
            int   rdetSectors = (rdetEntries * 32 + _bytesPerSector - 1) / _bytesPerSector;

            // In ra màn hình đúng format
            Console.WriteLine(string.Format("{0,-45} | {1}", "Attribute", "Value"));
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Bytes per sector",                            _bytesPerSector));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Sectors per cluster",                         _sectorsPerCluster));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of sectors in the Boot Sector region", _reservedSectors));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of FAT tables",                        _numberOfFATs));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of sectors per FAT table",             _sectorsPerFAT));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of sectors for the RDET",              rdetSectors));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Total number of sectors on the disk",         _totalSectors));
            _bootSectorReady = true;
        }

        // ==========================================================
        // Private helpers for Function 2 and 3
        // ==========================================================

        // Đảm bảo boot sector đã được đọc (để dùng _rootCluster, v.v.)

        // ==========================================================
        // Function 2 – Liệt kê tất cả file *.txt trên đĩa (dạng phẳng)
        // ==========================================================
        private long GetClusterOffset(int cluster)
        {
            long dataStart = (long)(_reservedSectors + _numberOfFATs * _sectorsPerFAT) * _bytesPerSector;
            return dataStart + (long)(cluster - 2) * _sectorsPerCluster * _bytesPerSector;
        }

        private int GetNextCluster(int currentCluster)
        {
            long fatStart = (long)_reservedSectors * _bytesPerSector;
            long entryOffset = fatStart + currentCluster * 4; // FAT32 entry là 4 bytes
            byte[] buf = new byte[4];
            if (!ReadAt(entryOffset, buf, 4)) return -1;
            return BitConverter.ToInt32(buf, 0) & 0x0FFFFFFF; // FAT32 chỉ dùng 28 bit cho cluster number
        }


        private byte[] ReadCluster(int startCluster)
        {
            int clusterSize = _sectorsPerCluster * _bytesPerSector;
            var data = new List<byte>();
            int cluster = startCluster;
            while (cluster >= 2 && cluster < 0x0FFFFFF8)
            {
                byte[] clusterData = new byte[clusterSize];
                if (!ReadAt(GetClusterOffset(cluster), clusterData, (uint)clusterSize)) break;
                data.AddRange(clusterData);
                cluster = GetNextCluster(cluster);
            }
            return data.ToArray();
        }

        private string LongFileNamePath(byte[] entry)
        {
            int[] offsets = { 1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30 };
            var nameChars = new List<char>();
            for (int i = 0; i < 13; i++)
            {
                ushort ch = BitConverter.ToUInt16(entry, offsets[i]);
                if (ch == 0x0000 || ch == 0xFFFF) break; // Kết thúc nếu gặp null terminator hoặc padding
                nameChars.Add((char)ch);
            }
            return new string(nameChars.ToArray());

        }

        private void ScanDirectory(int cluster, string path)
        {
            if (cluster < 2) return;
            byte[] dirData = ReadCluster(cluster);
            int entryCount = dirData.Length / 32;
            string LFN = "";

            for (int i = 0; i < entryCount; i++)
            {
                byte[] entry = new byte[32];

                Array.Copy(dirData, i * 32, entry, 0, 32);

                byte firstByte = entry[0];
                if (firstByte == 0x00) break; // hết entry
                if (firstByte == 0xE5) 
                {
                    LFN = ""; // BẮT BUỘC: Reset LFN để tránh file rác ám vào file sau
                    continue; 
                }

                byte attr = entry[11];
                if (attr == 0x0F)
                {
                    LFN = LongFileNamePath(entry) + LFN;
                    continue;
                }

                string name8 = Encoding.ASCII.GetString(entry, 0, 8).TrimEnd();
                string ext3  = Encoding.ASCII.GetString(entry, 8, 3).TrimEnd();

                string fullName = LFN != "" ? LFN : (ext3.Length > 0 ? $"{name8}.{ext3}" : name8);
                LFN = ""; // reset LFN sau khi đã dùng

                if ((attr & 0x08) != 0) continue;  // volume ID
                if (name8 == "." || name8 == "..") continue;

                int hiCluster = BitConverter.ToUInt16(entry, 20);
                int loCluster = BitConverter.ToUInt16(entry, 26);
                int firstCluster = (hiCluster << 16) | loCluster;

                if ((attr & 0x10) != 0) // thư mục con
                {
                    ScanDirectory(firstCluster, path + fullName + "\\");
                }
                else if (ext3.ToLower() == "txt") // file .txt
                {
                    _txtFiles.Add(new TxtFileInfo
                    {
                        FullPath = path + fullName,
                        Name = fullName,
                        CreationTime = BitConverter.ToUInt16(entry, 14),
                        CreationDate = BitConverter.ToUInt16(entry, 16),
                        FirstCluster = firstCluster,
                        FileSize = BitConverter.ToUInt32(entry, 28)
                    });
                }



            }

        }

        public void Function2()
        {
            if (!_bootSectorReady)  Function1();

            if (!_scanned)
            {
                _txtFiles.Clear();
                ScanDirectory(_rootCluster, "\\");
                _scanned = true;
                
            }

            if (_txtFiles.Count == 0)
            {
                Console.WriteLine("Không tìm thấy file .txt nào trên đĩa.");
                return;
            }

            Console.WriteLine(string.Format("{0,-5} {1,-35} {2,-55}",
                "No.", "File Name", "Full Path"));
            int listSepWidth = 60;
            Console.WriteLine(new string('-', listSepWidth));
            for (int i = 0; i < _txtFiles.Count; i++)
            {
                var f = _txtFiles[i];
                Console.WriteLine(string.Format("{0,-5} {1,-35} {2,-55}",
                    i + 1, f.Name, f.FullPath));
            }
        }


        // Chuyển FAT16 date thành chuỗi DD/MM/YYYY
        private string ParseDate(ushort date)
        {
            int day   =  date        & 0x1F;
            int month = (date >> 5)  & 0x0F;
            int year  = ((date >> 9) & 0x7F) + 1980;
            return $"{day:D2}/{month:D2}/{year:D4}";
        }

        // Chuyển FAT16 time thành chuỗi HH:MM:SS
        private string ParseTime(ushort time)
        {
            int secs  = (time & 0x1F) * 2;
            int mins  = (time >> 5)   & 0x3F;
            int hours = (time >> 11)  & 0x1F;
            return $"{hours:D2}:{mins:D2}:{secs:D2}";
        }


        // ==========================================================
        // Function 3 – Xem thông tin chi tiết của một file *.txt
        //   - Metadata từ directory entry
        //   - Bảng thông tin tiến trình (parse nội dung theo format Project 01)
        // ==========================================================
        public void Function3()
        {
            if (!_bootSectorReady)  Function1();

            if (!_scanned)
            {
                _txtFiles.Clear();
                ScanDirectory(_rootCluster, "\\");
                _scanned = true;
                
            }

            if (_txtFiles.Count == 0)
            {
                Console.WriteLine("Không tìm thấy file .txt nào trên đĩa.");
                return;
            }

            Console.WriteLine($"Nhập số thứ tự file (1 - {_txtFiles.Count}): ");
            if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > _txtFiles.Count)
            {
                Console.WriteLine("Lựa chọn không hợp lệ.");
                return;
            }

            TxtFileInfo file = _txtFiles[choice - 1];

            // --- Thông tin file ---
            Console.WriteLine();
            Console.WriteLine(new string('=', 75));
            Console.WriteLine(string.Format("  {0,-20}: {1}", "Name",         file.Name));
            Console.WriteLine(string.Format("  {0,-20}: {1}", "Date Created", ParseDate(file.CreationDate)));
            Console.WriteLine(string.Format("  {0,-20}: {1}", "Time Created", ParseTime(file.CreationTime)));
            Console.WriteLine(string.Format("  {0,-20}: {1} bytes", "Total Size",  file.FileSize));
            Console.WriteLine(new string('=', 75));

            // --- Đọc và phân tích nội dung file ---
            byte[] fileData = ReadCluster(file.FirstCluster);
            string content = Encoding.ASCII.GetString(fileData, 0, (int)file.FileSize);
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                Console.WriteLine("File rỗng hoặc không đúng định dạng.");
                return;
            }
            int i = 0;

            int qCount = int.Parse(lines[i].Trim());

            i++;
            _schedulingQueues    = new List<Queue>();
            _schedulingProcesses = new List<Process>();

            while (i < qCount + 1)
            {
                string[] queueData = lines[i].Trim().Split(' ');
                string queueID = queueData[0];
                int timeSlice = int.Parse(queueData[1]);
                string schedulingPolicy = queueData[2];
                Queue q = new Queue(queueID, timeSlice, schedulingPolicy);
                _schedulingQueues.Add(q);
                i++;
            }

            while (i < lines.Length)
            {
                string[] processData = lines[i].Trim().Split(' ');
                string processID = processData[0];
                int arrivalTime = int.Parse(processData[1]);
                int burstTime = int.Parse(processData[2]);
                string queueID = processData[3];
                Process p = new Process(processID, arrivalTime, burstTime, queueID);
                _schedulingProcesses.Add(p);
                i++;
            }

            Console.WriteLine("+------------+--------------+----------------+-------------------+------------+----------------------------+");
            Console.WriteLine(string.Format("| {0,-10} | {1,-12} | {2,-14} | {3,-17} | {4,-10} | {5,-26} |",
                "Process ID", "Arrival Time", "CPU Burst Time", "Priority Queue ID",
                "Time Slice", "Scheduling Algorithm Name"));
            Console.WriteLine("+------------+--------------+----------------+-------------------+------------+----------------------------+");

            foreach (var p in _schedulingProcesses)
            {
                var q = _schedulingQueues.Find(queue => queue.queueID == p.queueID);
                string timeSliceStr = q != null ? q.timeSlice.ToString() : "N/A";
                string policyStr = q != null ? q.schedulingPolicy : "N/A";

                Console.WriteLine(string.Format("| {0,-10} | {1,-12} | {2,-14} | {3,-17} | {4,-10} | {5,-26} |",
                    p.processID, p.arrivalTime, p.burstTime, p.queueID,
                    timeSliceStr, policyStr));
            }
            Console.WriteLine("+------------+--------------+----------------+-------------------+------------+----------------------------+");
        }


        // ==========================================================
        // Function 4 – Vẽ scheduling diagram và tính Turnaround/Waiting Time
        //   Dựa trên dữ liệu từ file *.txt đã chọn ở Function 3
        // ==========================================================
        public void Function4()
        {
            SchedulingResult scheduler = new SchedulingResult(_schedulingProcesses!, _schedulingQueues!);
            scheduler.RunScheduling();
            scheduler.PrintSchedulingDiagram();
        }
    }

    // =========================================================
    // Entry point
    // =========================================================
    class Program
    {
        static void Main(string[] args)
        {
            string driveLetter = "F:";
            Fat32Reader reader = new Fat32Reader(driveLetter);

            if (!reader.IsOpen)
            {
                Console.WriteLine("Lỗi: Không thể mở ổ đĩa (Nhớ Run as Administrator).");
                return;
            }

            Console.WriteLine("========== FUNCTION 1: Boot Sector Parameters ==========");
            reader.Function1();

            Console.WriteLine("\n========== FUNCTION 2: Danh sách file *.txt trên đĩa ==========");
            reader.Function2();

            Console.WriteLine("\n========== FUNCTION 3: Thông tin chi tiết file *.txt ==========");
            reader.Function3();

            Console.WriteLine("\n========== FUNCTION 4: CPU Scheduling Diagram ==========");
            reader.Function4();

            reader.Close();
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Nhan phim ENTER de ket thuc chuong trinh...");
            Console.ReadLine();
        }
    }
}