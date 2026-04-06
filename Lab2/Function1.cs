using System;
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
        }

        // ==========================================================
        // Function 2 – Đọc và hiển thị bảng FAT
        //
        // Gợi ý:
        //   - FAT bắt đầu tại: _reservedSectors * _bytesPerSector
        //   - Mỗi entry FAT32 dài 4 bytes; mask với 0x0FFFFFFF để lấy giá trị
        //   - Giá trị đặc biệt: 0x0FFFFFF8–0x0FFFFFFF = end-of-chain, 0x00000000 = free cluster
        // ==========================================================
        public void Function2()
        {
            // TODO: implement Function 2
        }

        // ==========================================================
        // Function 3 – Đọc và hiển thị Root Directory Entry Table (RDET)
        //
        // Gợi ý:
        //   - Vùng data bắt đầu tại: (_reservedSectors + _numberOfFATs * _sectorsPerFAT) * _bytesPerSector
        //   - Địa chỉ byte của cluster N: dataStart + (N - 2) * _sectorsPerCluster * _bytesPerSector
        //   - Root cluster = _rootCluster (thường là 2)
        //   - Mỗi directory entry dài 32 bytes
        //   - Byte đầu = 0x00: hết entries; = 0xE5: entry đã xoá
        // ==========================================================
        public void Function3()
        {
            // TODO: implement Function 3
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

            // Bỏ comment khi hoàn thiện Function 2:
            // Console.WriteLine("\n========== FUNCTION 2: FAT Table ==========");
            // reader.Function2();

            // Bỏ comment khi hoàn thiện Function 3:
            // Console.WriteLine("\n========== FUNCTION 3: Root Directory Entry Table ==========");
            // reader.Function3();

            reader.Close();
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Nhan phim ENTER de ket thuc chuong trinh...");
            Console.ReadLine();
        }
    }
}