using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS_Lab02_FAT32
{
    // =========================================================
    // 1. CÁC CLASS LẬP LỊCH (Bê nguyên từ code của bạn sang)
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
        public string queueID { get; set; }
        public int timeSlice { get; set; }
        public string schedulingPolicy { get; set; }
        public int remainingTime { get; set; }
        public List<Process> readyQueue = new List<Process>();
        public Queue(string id, int ts, string sp)
        {
            queueID = id; timeSlice = ts; schedulingPolicy = sp; remainingTime = ts;
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
            processID = pid; arrivalTime = at; burstTime = bt; remainingTime = bt; queueID = qid;
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
            processes = p; queues = q;
        }

        private void AddLog(int start, int end, string qID, string pID)
        {
            if (start == end) return;
            if (logs.Count > 0)
            {
                var lastLog = logs[logs.Count - 1];
                if (lastLog.endTime == start && lastLog.queueID == qID && lastLog.processID == pID)
                {
                    lastLog.endTime = end; return;
                }
            }
            logs.Add(new executionLog { startTime = start, endTime = end, queueID = qID, processID = pID });
        }

        public void RunScheduling()
        {
            int completedProcesses = 0, currentTime = 0, i = 0, queueIndex = 0;
            Process currentProcess = null;

            queues.Sort((q1, q2) => q2.timeSlice.CompareTo(q1.timeSlice));
            processes.Sort((p1, p2) => p1.arrivalTime.CompareTo(p2.arrivalTime));

            while (completedProcesses < processes.Count)
            {
                while (i < processes.Count && processes[i].arrivalTime <= currentTime)
                {
                    foreach (Queue q in queues)
                    {
                        if (q.queueID == processes[i].queueID)
                        {
                            q.readyQueue.Add(processes[i]);
                            if (q.schedulingPolicy == "SRTN") q.readyQueue.Sort((p1, p2) => p1.remainingTime.CompareTo(p2.remainingTime));
                            else if (q.schedulingPolicy == "SJF") q.readyQueue.Sort((p1, p2) => p1.burstTime.CompareTo(p2.burstTime));
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

                if (currentQueue.readyQueue.Count == 0)
                {
                    bool allEmpty = true;
                    foreach (var q in queues) if (q.readyQueue.Count > 0) { allEmpty = false; break; }
                    if (allEmpty) currentTime++;
                    else
                    {
                        currentQueue.remainingTime = currentQueue.timeSlice;
                        queueIndex = (queueIndex + 1) % queues.Count;
                    }
                    continue;
                }

                int prevTime = currentTime;
                currentProcess = currentQueue.readyQueue[0];
                int executionTime = Math.Min(currentProcess.remainingTime, currentQueue.remainingTime);

                if (currentQueue.schedulingPolicy == "SJF")
                {
                    currentTime += executionTime;
                    currentProcess.remainingTime -= executionTime;
                    currentQueue.remainingTime -= executionTime;
                }
                else if (currentQueue.schedulingPolicy == "SRTN")
                {
                    int timeToNextArrival = int.MaxValue;
                    if (i < processes.Count) timeToNextArrival = processes[i].arrivalTime - currentTime;
                    executionTime = Math.Min(executionTime, timeToNextArrival);
                    currentTime += executionTime;
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

        // Sinh ra chuỗi kết quả để ném lên GUI
        public string GenerateDiagramString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=================== CPU SCHEDULING DIAGRAM ===================");
            sb.AppendLine(string.Format("{0,-18} | {1,-10} | {2,-10}", "[Start - End]", "Queue", "Process"));
            sb.AppendLine("--------------------------------------------------------------");
            foreach (var log in logs)
                sb.AppendLine(string.Format("{0,-18} | {1,-10} | {2,-10}", $"[{log.startTime} - {log.endTime}]", log.queueID, log.processID));

            sb.AppendLine("\n===================== PROCESS STATISTICS =====================");
            sb.AppendLine("--------------------------------------------------------------");
            sb.AppendLine(string.Format("{0,-10}| {1,-10}| {2,-10}| {3,-12}| {4,-12}| {5,-10}",
                "Process", "Arrival", "Burst", "Completion", "Turnaround", "Waiting"));
            sb.AppendLine("--------------------------------------------------------------");

            double totalWT = 0, totalTT = 0;
            foreach (var p in processes)
            {
                sb.AppendLine(string.Format("{0,-10}| {1,-10}| {2,-10}| {3,-12}| {4,-12}| {5,-10}",
                    p.processID, p.arrivalTime, p.burstTime, p.completionTime, p.turnaroundTime, p.waitingTime));
                totalWT += p.waitingTime;
                totalTT += p.turnaroundTime;
            }
            sb.AppendLine("--------------------------------------------------------------");
            sb.AppendLine($"Average Turnaround Time: {totalTT / processes.Count:F1}");
            sb.AppendLine($"Average Waiting Time:    {totalWT / processes.Count:F1}");
            return sb.ToString();
        }
    }

    // =========================================================
    // 2. CLASS ĐỌC Ổ ĐĨA FAT32 (Đã sửa để trả về dữ liệu cho GUI)
    // =========================================================
    public class TxtFileInfo
    {
        public int No { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public uint FileSize { get; set; }
        public ushort CreationTime { get; set; }
        public ushort CreationDate { get; set; }
        public int FirstCluster { get; set; }
    }

    public class BootSectorProperty
    {
        public string Property { get; set; }
        public string Value { get; set; }
    }

    public class Fat32Reader
    {
        static class WinAPI
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetFilePointerEx(SafeFileHandle hFile, long liDistanceToMove, out long lpNewFilePointer, uint dwMoveMethod);
        }

        private SafeFileHandle _handle;
        private short _bytesPerSector;
        private byte _sectorsPerCluster;
        private short _reservedSectors;
        private byte _numberOfFATs;
        private int _sectorsPerFAT;
        private int _totalSectors;
        private int _rootCluster;

        public List<TxtFileInfo> TxtFiles { get; private set; } = new List<TxtFileInfo>();
        public List<Process> ParsedProcesses { get; private set; } = new List<Process>();
        public List<Queue> ParsedQueues { get; private set; } = new List<Queue>();

        public Fat32Reader(string driveLetter)
        {
            _handle = WinAPI.CreateFile("\\\\.\\" + driveLetter, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        }

        public bool IsOpen => _handle != null && !_handle.IsInvalid;
        public void Close() => _handle?.Close();

        private bool ReadAt(long byteOffset, byte[] buffer, uint count)
        {
            if (!WinAPI.SetFilePointerEx(_handle, byteOffset, out _, 0)) return false;
            return WinAPI.ReadFile(_handle, buffer, count, out uint bytesRead, IntPtr.Zero) && bytesRead == count;
        }

        // --- FUNCTION 1 ---
        public List<BootSectorProperty> GetBootSectorInfo()
        {
            byte[] bootSector = new byte[512];
            if (!ReadAt(0, bootSector, 512)) throw new Exception("Không thể đọc Boot Sector.");

            _bytesPerSector = BitConverter.ToInt16(bootSector, 11);
            _sectorsPerCluster = bootSector[13];
            _reservedSectors = BitConverter.ToInt16(bootSector, 14);
            _numberOfFATs = bootSector[16];
            _sectorsPerFAT = BitConverter.ToInt32(bootSector, 36);
            _totalSectors = BitConverter.ToInt32(bootSector, 32);
            _rootCluster = BitConverter.ToInt32(bootSector, 44);

            short rdetEntries = BitConverter.ToInt16(bootSector, 17);
            int rdetSectors = (rdetEntries * 32 + _bytesPerSector - 1) / _bytesPerSector;

            return new List<BootSectorProperty>
            {
                new BootSectorProperty { Property = "Bytes per sector", Value = _bytesPerSector.ToString() },
                new BootSectorProperty { Property = "Sectors per cluster", Value = _sectorsPerCluster.ToString() },
                new BootSectorProperty { Property = "Number of sectors in the Boot Sector region", Value = _reservedSectors.ToString() },
                new BootSectorProperty { Property = "Number of FAT tables", Value = _numberOfFATs.ToString() },
                new BootSectorProperty { Property = "Number of sectors per FAT table", Value = _sectorsPerFAT.ToString() },
                new BootSectorProperty { Property = "Number of sectors for the RDET", Value = rdetSectors.ToString() },
                new BootSectorProperty { Property = "Total number of sectors on the disk", Value = _totalSectors.ToString() }
            };
        }

        // --- FUNCTION 2 (Tìm file) ---
        public void ScanForTxtFiles()
        {
            TxtFiles.Clear();
            ScanDirectory(_rootCluster, "\\");
            for (int i = 0; i < TxtFiles.Count; i++) TxtFiles[i].No = i + 1; // Đánh số
        }

        // --- FUNCTION 3 (Parse file) ---
        public void ParseTxtFile(TxtFileInfo file)
        {
            ParsedProcesses.Clear();
            ParsedQueues.Clear();

            byte[] rawBytes = ReadClusterChain(file.FirstCluster);
            int len = (int)Math.Min(file.FileSize, (uint)rawBytes.Length);
            string text = Encoding.UTF8.GetString(rawBytes, 0, len);
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0 || !int.TryParse(lines[0].Trim(), out int qCount)) throw new Exception("File sai format");

            var qDict = new Dictionary<string, (int ts, string alg)>();
            for (int i = 1; i <= qCount && i < lines.Length; i++)
            {
                string[] p = lines[i].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3 && int.TryParse(p[1], out int ts))
                {
                    qDict[p[0]] = (ts, p[2]);
                    ParsedQueues.Add(new Queue(p[0], ts, p[2]));
                }
            }

            for (int i = qCount + 1; i < lines.Length; i++)
            {
                string[] p = lines[i].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4 && int.TryParse(p[1], out int at) && int.TryParse(p[2], out int bt))
                {
                    ParsedProcesses.Add(new Process(p[0], at, bt, p[3]));
                }
            }
        }

        // ================= CÁC HÀM HELPER ĐỌC BYTE CŨ =================
        private long GetClusterOffset(int cluster) => ((long)_reservedSectors + _numberOfFATs * _sectorsPerFAT) * _bytesPerSector + (long)(cluster - 2) * _sectorsPerCluster * _bytesPerSector;
        private int GetNextCluster(int cluster)
        {
            byte[] buf = new byte[4];
            if (!ReadAt((long)_reservedSectors * _bytesPerSector + (long)cluster * 4, buf, 4)) return -1;
            return BitConverter.ToInt32(buf, 0) & 0x0FFFFFFF;
        }
        private byte[] ReadClusterChain(int startCluster)
        {
            var data = new List<byte>();
            int cluster = startCluster, guard = 0, size = _sectorsPerCluster * _bytesPerSector;
            while (cluster >= 2 && cluster < 0x0FFFFFF8 && guard++ < 200000)
            {
                byte[] buf = new byte[size];
                if (!ReadAt(GetClusterOffset(cluster), buf, (uint)size)) break;
                data.AddRange(buf);
                cluster = GetNextCluster(cluster);
            }
            return data.ToArray();
        }
        public string ParseDate(ushort date) => $"{date & 0x1F:D2}/{(date >> 5) & 0x0F:D2}/{((date >> 9) & 0x7F) + 1980:D4}";
        public string ParseTime(ushort time) => $"{(time >> 11) & 0x1F:D2}:{(time >> 5) & 0x3F:D2}:{(time & 0x1F) * 2:D2}";

        private void ScanDirectory(int cluster, string path)
        {
            if (cluster < 2) return;
            byte[] dirData = ReadClusterChain(cluster);
            string pendingLFN = "";
            for (int i = 0; i < dirData.Length / 32; i++)
            {
                byte[] entry = new byte[32];
                Array.Copy(dirData, i * 32, entry, 0, 32);
                if (entry[0] == 0x00) break;
                if (entry[0] == 0xE5) { pendingLFN = ""; continue; }
                if (entry[11] == 0x0F)
                {
                    var sb = new StringBuilder();
                    foreach (int off in new[] { 1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30 })
                    {
                        ushort ch = BitConverter.ToUInt16(entry, off);
                        if (ch == 0x0000 || ch == 0xFFFF) break;
                        sb.Append((char)ch);
                    }
                    pendingLFN = sb.ToString() + pendingLFN;
                    continue;
                }
                string name8 = Encoding.ASCII.GetString(entry, 0, 8).TrimEnd();
                string ext3 = Encoding.ASCII.GetString(entry, 8, 3).TrimEnd();
                string name = pendingLFN != "" ? pendingLFN : (ext3.Length > 0 ? $"{name8}.{ext3}" : name8);
                pendingLFN = "";

                if ((entry[11] & 0x08) != 0 || name8 == "." || name8 == "..") continue;
                int fc = (BitConverter.ToUInt16(entry, 20) << 16) | BitConverter.ToUInt16(entry, 26);

                if ((entry[11] & 0x10) != 0) ScanDirectory(fc, path + name + "\\");
                else if (string.Equals(ext3, "TXT", StringComparison.OrdinalIgnoreCase))
                {
                    TxtFiles.Add(new TxtFileInfo { Name = name, FullPath = path + name, FileSize = BitConverter.ToUInt32(entry, 28), CreationTime = BitConverter.ToUInt16(entry, 14), CreationDate = BitConverter.ToUInt16(entry, 16), FirstCluster = fc });
                }
            }
        }
    }
}