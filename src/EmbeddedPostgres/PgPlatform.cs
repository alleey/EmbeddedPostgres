using System.Runtime.InteropServices;

namespace EmbeddedPostgres;

public record PgPlatform(string Platform, string Architecture, string Distribution = "")
{
    public const string PlatformWindows = "windows";
    public const string PlatformLinux = "linux";
    public const string PlatformDarwin = "darwin";

    public const string ArchAmd64 = "amd64";
    public const string ArchArm64v8 = "arm64v8";
    public const string Archi386 = "amd64";
    public const string ArchPpc64le = "ppc64le";

    public const string DistroAlpine = "alpine";
    public const string DistroAlpineLite = "alpine-lite";

    public class Windows
    {
        public static readonly PgPlatform amd64 = new PgPlatform(PlatformWindows, ArchAmd64);
        public static readonly PgPlatform i386 = new PgPlatform(PlatformWindows, Archi386);
    }

    public class Darwin
    {
        public static readonly PgPlatform amd64 = new PgPlatform(PlatformDarwin, ArchAmd64);
        public static readonly PgPlatform amd64v8 = new PgPlatform(PlatformDarwin, ArchArm64v8);
    }

    public class Linux
    {
        public static readonly PgPlatform amd64 = new PgPlatform(PlatformLinux, ArchAmd64);
        public static readonly PgPlatform amd64Alpine = new PgPlatform(PlatformLinux, ArchAmd64, DistroAlpine);
        public static readonly PgPlatform amd64AlpineLite = new PgPlatform(PlatformLinux, ArchAmd64, DistroAlpineLite);

        public static readonly PgPlatform i386 = new PgPlatform(PlatformLinux, Archi386);
        public static readonly PgPlatform i386Alpine = new PgPlatform(PlatformLinux, Archi386, DistroAlpine);
        public static readonly PgPlatform i386AlpineLite = new PgPlatform(PlatformLinux, Archi386, DistroAlpineLite);

        public static readonly PgPlatform ppc64le = new PgPlatform(PlatformLinux, ArchPpc64le);
        public static readonly PgPlatform ppc64leAlpine = new PgPlatform(PlatformLinux, ArchPpc64le, DistroAlpine);
        public static readonly PgPlatform ppc64leAlpineLite = new PgPlatform(PlatformLinux, ArchPpc64le, DistroAlpineLite);
    }

    /// <summary>
    /// 
    /// </summary>
    public static PgPlatform Current
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DetectWindowsArchitecture();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return DetectLinuxArchitecture();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return DetectDarwinArchitecture();
            }

            throw new NotSupportedException("Unsupported operating system");
        }
    }

    public static PgPlatform DetectWindowsArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => Windows.amd64,
            System.Runtime.InteropServices.Architecture.X86 => Windows.i386,
            _ => throw new NotSupportedException("Unsupported architecture for Windows")
        };
    }

    public static PgPlatform DetectLinuxArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => Linux.amd64,
            System.Runtime.InteropServices.Architecture.X86 => Linux.i386,
            System.Runtime.InteropServices.Architecture.Ppc64le => Linux.ppc64le,
            _ => throw new NotSupportedException("Unsupported architecture for Linux")
        };
    }

    public static PgPlatform DetectDarwinArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => Darwin.amd64,
            System.Runtime.InteropServices.Architecture.Arm64 => Darwin.amd64v8,
            _ => throw new NotSupportedException("Unsupported architecture for macOS")
        };
    }
}
