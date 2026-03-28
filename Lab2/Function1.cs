using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OS_Lab02_FAT32_Strict
{
    class Program
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

        const uint GENERIC_READ = 0x80000000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;

        static void Main(string[] args)
        {
            string driveLetter = "F:";
            string drivePath = "\\\\.\\" + driveLetter;

            SafeFileHandle handle = CreateFile(
                drivePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                Console.WriteLine("Lỗi: Không thể mở ổ đĩa (Nhớ Run as Administrator).");
                return;
            }

            byte[] bootSector = new byte[512];
            uint bytesRead = 0;

            bool result = ReadFile(
                handle,
                bootSector,
                512,
                out bytesRead,
                IntPtr.Zero);

            if (!result || bytesRead != 512)
            {
                Console.WriteLine("Lỗi: Không thể đọc Sector vật lý.");
                handle.Close();
                return;
            }

            // 1. Bytes per sector (Offset 0x0B - 11, dài 2 bytes)
            short bytesPerSector = BitConverter.ToInt16(bootSector, 11);

            // 2. Sectors per cluster (Offset 0x0D - 13, dài 1 byte)
            byte sectorsPerCluster = bootSector[13];

            // 3. Number of sectors in the Boot Sector region (Offset 0x0E - 14, dài 2 bytes)
            short reservedSectors = BitConverter.ToInt16(bootSector, 14);

            // 4. Number of FAT tables (Offset 0x10 - 16, dài 1 byte)
            byte numberOfFATs = bootSector[16];

            // Đọc số lượng Entry của RDET (Offset 0x11 - 17, dài 2 bytes). Với FAT32, giá trị này luôn bằng 0.
            short rdetEntries = BitConverter.ToInt16(bootSector, 17);

            // 5. Tính toán Number of sectors for the RDET
            // Công thức: (Số lượng Entry * 32 bytes mỗi entry) / Số bytes mỗi sector.
            // Kết quả cho FAT32 sẽ là 0.
            int rdetSectors = (rdetEntries * 32 + bytesPerSector - 1) / bytesPerSector;

            // 6. Total number of sectors on the disk (Offset 0x20 - 32, dài 4 bytes)
            int totalSectors = BitConverter.ToInt32(bootSector, 32);

            // 7. Number of sectors per FAT table (Offset 0x24 - 36, dài 4 bytes)
            int sectorsPerFAT = BitConverter.ToInt32(bootSector, 36);

            // IN RA MÀN HÌNH ĐÚNG FORMAT CỦA ĐỀ BÀI (Table/List format)
            Console.WriteLine(string.Format("{0,-45} | {1}", "Attribute", "Value"));
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Bytes per sector", bytesPerSector));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Sectors per cluster", sectorsPerCluster));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of sectors in the Boot Sector region", reservedSectors));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of FAT tables", numberOfFATs));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of sectors per FAT table", sectorsPerFAT));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Number of sectors for the RDET", rdetSectors));
            Console.WriteLine(string.Format("{0,-45} | {1}", "Total number of sectors on the disk", totalSectors));
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Nhan phim ENTER de ket thuc chuong trinh...");
            handle.Close();
            Console.ReadLine();
        }
    }
}