using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS_Lab02_FAT32
{
    // =========================================================
    // 1. CÁC CLASS LẬP LỊCH
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
            Process? currentProcess = null;

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
    }

    // =========================================================
    // 2. CLASS ĐỌC Ổ ĐĨA FAT32
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

        // --- CÁC HÀM HELPER THEO LOGIC LAB2.CS ---
        private long GetClusterOffset(int cluster)
        {
            long dataStart = (long)(_reservedSectors + _numberOfFATs * _sectorsPerFAT) * _bytesPerSector;
            return dataStart + (long)(cluster - 2) * _sectorsPerCluster * _bytesPerSector;
        }

        private int GetNextCluster(int currentCluster)
        {
            long fatStart = (long)_reservedSectors * _bytesPerSector;
            long entryOffset = fatStart + (long)currentCluster * 4; 
            byte[] buf = new byte[4];
            if (!ReadAt(entryOffset, buf, 4)) return -1;
            return BitConverter.ToInt32(buf, 0) & 0x0FFFFFFF; 
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

        private int GetRootDirectorySectors()
        {
            int cluster = _rootCluster;
            int clusterCount = 0;

            while (cluster >= 2 && cluster < 0x0FFFFFF8)
            {
                clusterCount++;
                cluster = GetNextCluster(cluster);
            }

            return clusterCount * _sectorsPerCluster;
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

        public string ParseDate(ushort date)
        {
            int day = date & 0x1F;
            int month = (date >> 5) & 0x0F;
            int year = ((date >> 9) & 0x7F) + 1980;
            return $"{day:D2}/{month:D2}/{year:D4}";
        }

        public string ParseTime(ushort time)
        {
            int secs = (time & 0x1F) * 2;
            int mins = (time >> 5) & 0x3F;
            int hours = (time >> 11) & 0x1F;
            return $"{hours:D2}:{mins:D2}:{secs:D2}";
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

            int rdetSectors = GetRootDirectorySectors();

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
                string ext3 = Encoding.ASCII.GetString(entry, 8, 3).TrimEnd();

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
                    TxtFiles.Add(new TxtFileInfo
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

        // --- FUNCTION 3 (Parse file) ---
        public void ParseTxtFile(TxtFileInfo file)
        {
            ParsedProcesses.Clear();
            ParsedQueues.Clear();

            // Đọc và phân tích nội dung file y hệt Lab2.cs
            byte[] fileData = ReadCluster(file.FirstCluster);
            string content = Encoding.ASCII.GetString(fileData, 0, (int)file.FileSize);
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                throw new Exception("File rỗng hoặc không đúng định dạng.");
            }

            int i = 0;
            int qCount = int.Parse(lines[i].Trim());
            i++;

            while (i < qCount + 1)
            {
                string[] queueData = lines[i].Trim().Split(' ');
                string queueID = queueData[0];
                int timeSlice = int.Parse(queueData[1]);
                string schedulingPolicy = queueData[2];
                ParsedQueues.Add(new Queue(queueID, timeSlice, schedulingPolicy));
                i++;
            }

            while (i < lines.Length)
            {
                string[] processData = lines[i].Trim().Split(' ');
                string processID = processData[0];
                int arrivalTime = int.Parse(processData[1]);
                int burstTime = int.Parse(processData[2]);
                string queueID = processData[3];
                ParsedProcesses.Add(new Process(processID, arrivalTime, burstTime, queueID));
                i++;
            }
        }
    }
}