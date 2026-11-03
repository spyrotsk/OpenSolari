using System;
using System.IO;
using System.Text;

namespace OpenSol.Core
{
    /// <summary>
    /// Writes configuration and log files so that, on Linux, only the user running the server can read them
    /// (chmod 600, folders 700). They hold user code hashes and the terminals' passwords.
    /// On Windows the files keep the permissions of their folder.
    /// </summary>
    public static class SecureFile
    {
        private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        private const UnixFileMode OwnerOnlyDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        public static void WriteAllText(string path, string contents)
        {
            Write(path, contents, FileMode.Create);
        }

        public static void AppendAllText(string path, string contents)
        {
            Write(path, contents, FileMode.Append);
        }

        /// <summary>
        /// Restricts a file written by hand or by an older version, if it exists.
        /// </summary>
        public static void Restrict(string path)
        {
            if (OperatingSystem.IsWindows() || !File.Exists(path)) return;
            try { File.SetUnixFileMode(path, OwnerOnly); } catch (Exception) { }
        }

        public static void CreateDirectory(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
                return;
            }

            Directory.CreateDirectory(path, OwnerOnlyDirectory);
            try { File.SetUnixFileMode(path, OwnerOnlyDirectory); } catch (Exception) { }
        }

        private static void Write(string path, string contents, FileMode mode)
        {
            var options = new FileStreamOptions { Mode = mode, Access = FileAccess.Write, Share = FileShare.Read };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerOnly;
                Restrict(path);
            }

            using var writer = new StreamWriter(path, new UTF8Encoding(false), options);
            writer.Write(contents);
        }
    }
}
