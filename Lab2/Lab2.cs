using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS_Lab02_FAT32
{
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
        private void EnsureBootSector()
        {
            if (!_bootSectorReady)
                Function1();
        }

        // Trả về byte offset của cluster N trong vùng Data
        private long GetClusterOffset(int cluster)
        {
            long dataStart = (long)(_reservedSectors + _numberOfFATs * _sectorsPerFAT) * _bytesPerSector;
            return dataStart + (long)(cluster - 2) * _sectorsPerCluster * _bytesPerSector;
        }

        // Đọc cluster tiếp theo trong chuỗi FAT của cluster hiện tại
        private int GetNextCluster(int cluster)
        {
            long fatStart    = (long)_reservedSectors * _bytesPerSector;
            long entryOffset = fatStart + (long)cluster * 4;
            byte[] buf = new byte[4];
            if (!ReadAt(entryOffset, buf, 4)) return -1;
            return BitConverter.ToInt32(buf, 0) & 0x0FFFFFFF;
        }

        // Giới hạn số cluster tối đa khi đọc chuỗi (tránh vòng lặp vô hạn khi FAT bị lỗi)
        private const int MAX_CLUSTER_CHAIN = 200000;

        // Đọc toàn bộ dữ liệu từ chuỗi cluster bắt đầu tại startCluster
        private byte[] ReadClusterChain(int startCluster)
        {
            int clusterSize = _sectorsPerCluster * _bytesPerSector;
            var data = new List<byte>();
            int cluster = startCluster;
            int guard   = 0;
            while (cluster >= 2 && cluster < 0x0FFFFFF8 && guard++ < MAX_CLUSTER_CHAIN)
            {
                byte[] buf = new byte[clusterSize];
                if (!ReadAt(GetClusterOffset(cluster), buf, (uint)clusterSize)) break;
                data.AddRange(buf);
                cluster = GetNextCluster(cluster);
            }
            return data.ToArray();
        }

        // Trích phần tên từ một LFN entry (UTF-16LE tại các offset cố định)
        private string ExtractLFNPart(byte[] entry)
        {
            int[] offsets = { 1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30 };
            var sb = new StringBuilder();
            foreach (int off in offsets)
            {
                ushort ch = BitConverter.ToUInt16(entry, off);
                if (ch == 0x0000 || ch == 0xFFFF) break;
                sb.Append((char)ch);
            }
            return sb.ToString();
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

        // Duyệt đệ quy thư mục tại cluster; thêm file .txt tìm được vào _txtFiles
        private void ScanDirectory(int cluster, string path)
        {
            if (cluster < 2) return;
            byte[] dirData   = ReadClusterChain(cluster);
            int    entryCount = dirData.Length / 32;
            string pendingLFN = "";

            for (int i = 0; i < entryCount; i++)
            {
                byte[] entry = new byte[32];
                Array.Copy(dirData, i * 32, entry, 0, 32);

                byte first = entry[0];
                if (first == 0x00) break;          // hết directory
                if (first == 0xE5) { pendingLFN = ""; continue; } // entry đã xoá

                byte attr = entry[11];

                if (attr == 0x0F) // LFN entry – ghép từng phần (đọc ngược từ cuối)
                {
                    pendingLFN = ExtractLFNPart(entry) + pendingLFN;
                    continue;
                }

                string name8 = Encoding.ASCII.GetString(entry, 0, 8).TrimEnd();
                string ext3  = Encoding.ASCII.GetString(entry, 8, 3).TrimEnd();
                string displayName = pendingLFN != "" ? pendingLFN
                                     : (ext3.Length > 0 ? $"{name8}.{ext3}" : name8);
                pendingLFN = "";

                if ((attr & 0x08) != 0) continue;  // volume ID
                if (name8 == "." || name8 == "..") continue;

                int hiCluster = BitConverter.ToUInt16(entry, 20);
                int loCluster = BitConverter.ToUInt16(entry, 26);
                int firstCluster = (hiCluster << 16) | loCluster;

                if ((attr & 0x10) != 0) // directory
                {
                    ScanDirectory(firstCluster, path + displayName + "\\");
                }
                else if (string.Equals(ext3, "TXT", StringComparison.OrdinalIgnoreCase)) // file .txt
                {
                    _txtFiles.Add(new TxtFileInfo
                    {
                        FullPath     = path + displayName,
                        Name         = displayName,
                        CreationTime = BitConverter.ToUInt16(entry, 14),
                        CreationDate = BitConverter.ToUInt16(entry, 16),
                        FirstCluster = firstCluster,
                        FileSize     = BitConverter.ToUInt32(entry, 28)
                    });
                }
            }
        }

        // Đảm bảo _txtFiles đã được nạp
        private void EnsureTxtFilesLoaded()
        {
            EnsureBootSector();
            if (!_scanned)
            {
                _txtFiles.Clear();
                ScanDirectory(_rootCluster, "\\");
                _scanned = true;
            }
        }

        // ==========================================================
        // Function 2 – Liệt kê tất cả file *.txt trên đĩa (dạng phẳng)
        // ==========================================================
        public void Function2()
        {
            EnsureTxtFilesLoaded();

            if (_txtFiles.Count == 0)
            {
                Console.WriteLine("Không tìm thấy file .txt nào trên đĩa.");
                return;
            }

            Console.WriteLine(string.Format("{0,-5} {1,-35} {2,-55} {3}",
                "No.", "File Name", "Full Path", "Size (bytes)"));
            int listSepWidth = 5 + 1 + 35 + 1 + 55 + 1 + 12;
            Console.WriteLine(new string('-', listSepWidth));
            for (int i = 0; i < _txtFiles.Count; i++)
            {
                var f = _txtFiles[i];
                Console.WriteLine(string.Format("{0,-5} {1,-35} {2,-55} {3}",
                    i + 1, f.Name, f.FullPath, f.FileSize));
            }
        }

        // ==========================================================
        // Function 3 – Xem thông tin chi tiết của một file *.txt
        //   - Metadata từ directory entry
        //   - Bảng thông tin tiến trình (parse nội dung theo format Project 01)
        // ==========================================================
        public void Function3()
        {
            EnsureTxtFilesLoaded();

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
            byte[] rawBytes = ReadClusterChain(file.FirstCluster);
            int    len      = (int)Math.Min(file.FileSize, (uint)rawBytes.Length);
            string text     = Encoding.UTF8.GetString(rawBytes, 0, len);
            string[] lines  = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                Console.WriteLine("File rỗng hoặc không đúng định dạng.");
                return;
            }

            int qCount;
            if (!int.TryParse(lines[0].Trim(), out qCount) || qCount <= 0 || qCount + 1 > lines.Length)
            {
                Console.WriteLine("Nội dung file không đúng định dạng lịch trình.");
                return;
            }

            // Đọc thông tin các queue
            var queues = new Dictionary<string, (int timeSlice, string algorithm)>();
            for (int i = 1; i <= qCount && i < lines.Length; i++)
            {
                string[] parts = lines[i].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;
                int ts;
                if (!int.TryParse(parts[1], out ts))
                {
                    Console.WriteLine($"Lỗi: Time slice không hợp lệ tại dòng queue {i}: '{lines[i]}'");
                    return;
                }
                queues[parts[0]] = (ts, parts[2]);
            }

            // --- Bảng thông tin tiến trình ---
            const int COL_WIDTH_PROCESS_ID    = 12;
            const int COL_WIDTH_ARRIVAL_TIME  = 14;
            const int COL_WIDTH_CPU_BURST     = 16;
            const int COL_WIDTH_QUEUE_ID      = 19;
            const int COL_WIDTH_TIME_SLICE    = 12;
            const int COL_WIDTH_ALGORITHM     = 28;

            string sep = "+" + new string('-', COL_WIDTH_PROCESS_ID)   + "+"
                             + new string('-', COL_WIDTH_ARRIVAL_TIME)  + "+"
                             + new string('-', COL_WIDTH_CPU_BURST)     + "+"
                             + new string('-', COL_WIDTH_QUEUE_ID)      + "+"
                             + new string('-', COL_WIDTH_TIME_SLICE)    + "+"
                             + new string('-', COL_WIDTH_ALGORITHM)     + "+";

            Console.WriteLine(sep);
            Console.WriteLine(string.Format("| {0,-10} | {1,-12} | {2,-14} | {3,-17} | {4,-10} | {5,-26} |",
                "Process ID", "Arrival Time", "CPU Burst Time", "Priority Queue ID",
                "Time Slice", "Scheduling Algorithm Name"));
            Console.WriteLine(sep);

            for (int i = qCount + 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                string pid       = parts[0];
                string arrival   = parts[1];
                string burst     = parts[2];
                string qid       = parts[3];
                string timeSlice = queues.ContainsKey(qid) ? queues[qid].timeSlice.ToString() : "N/A";
                string algorithm = queues.ContainsKey(qid) ? queues[qid].algorithm             : "N/A";

                Console.WriteLine(string.Format("| {0,-10} | {1,-12} | {2,-14} | {3,-17} | {4,-10} | {5,-26} |",
                    pid, arrival, burst, qid, timeSlice, algorithm));
            }

            Console.WriteLine(sep);
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

            reader.Close();
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Nhan phim ENTER de ket thuc chuong trinh...");
            Console.ReadLine();
        }
    }
}