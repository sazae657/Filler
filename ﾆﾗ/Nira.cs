using System;
using System.IO;

namespace ﾆﾗ
{
    public delegate void IOErrorDelegaty(Exception e, FileSystemInfo path);

    public class Nira
    {
        public static long GetDriveFreeSpace(string path)
        {
            foreach (var drive in DriveInfo.GetDrives()) {
                if (drive.IsReady && path.StartsWith(drive.Name)) {
                    
                    return drive.TotalFreeSpace;
                }
            }
            return -1;
        }

        public static long GetDriveFreeSpace(string path, out long total)
        {
            total = -1;
            foreach (var drive in DriveInfo.GetDrives()) {
                if (drive.IsReady && path.StartsWith(drive.Name)) {
                    total = drive.TotalSize;
                    return drive.TotalFreeSpace;
                }
            }
            return -1;
        }

        public static readonly DateTime DefaultTimestamp = new DateTime(1999, 12, 31, 23, 59, 0);

        public static void FixTimestamp(FileSystemInfo path) => FixTimestamp(path, DefaultTimestamp, null);

        public static void FixTimestamp(FileSystemInfo path, DateTime fileTime) => FixTimestamp(path, fileTime, null);

        public static void FixTimestamp(FileSystemInfo path, IOErrorDelegaty delegaty) => FixTimestamp(path, DefaultTimestamp, delegaty);

        public static void FixTimestamp(FileSystemInfo path, DateTime fileTime, IOErrorDelegaty delegaty)
        {
            try {
                path.CreationTime = fileTime;
                path.LastAccessTime = fileTime;
                path.LastWriteTime = fileTime;
            }
            catch (Exception e) {
                delegaty?.Invoke(e, path);
            }
        }
    }
}
