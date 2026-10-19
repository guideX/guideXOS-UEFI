using System;
namespace guideXOS;
/// <summary>
/// Boot Mode
/// </summary>
public enum BootMode {
    /// <summary>
    /// Minimal
    /// </summary>
    UEFI = 0,
    /// <summary>
    /// Normal
    /// </summary>
    Legacy = 1
}
/// <summary>
/// Boot Console Mode
/// </summary>
public enum BootConsoleMode {
    /// <summary>
    /// None
    /// </summary>
    None = 0,
    /// <summary>
    /// Normal
    /// </summary>
    Normal = 1,
}
/// <summary>
/// Global boot configuration.
/// Default is UltraMinimal (safe mode).
/// </summary>
public static class BootConsole {
    /// <summary>
    /// Current Mode
    /// </summary>
    public static guideXOS.BootMode CurrentMode { get; set; } = guideXOS.BootMode.UEFI;
    /// <summary>
    /// Current Console Mode
    /// </summary>
    public static guideXOS.BootConsoleMode CurrentConsoleMode { get; set; } = guideXOS.BootConsoleMode.Normal;
    /// <summary>
    /// Draw Debug Lines
    /// </summary>
    public static bool DrawDebugLines { get; set; } = false;
    /// <summary>
    /// Write
    /// </summary>
    /// <param name="str"></param>
    public static void Write(string str) {
        WriteLine(str, false);
    }
    /// <summary>
    /// Write
    /// </summary>
    /// <param name="str"></param>
    public static void Write(char str) {
        if (BootConsole.CurrentConsoleMode == BootConsoleMode.None) return;
        if (BootConsole.CurrentMode == BootMode.Legacy) {
            guideXOS.Console.Write(str);
            return;
        }
        WriteSerialChar(str);
    }

    public static unsafe void WriteDecimal(ulong value) {
        char* digits = stackalloc char[20];
        int count = 0;
        do {
            digits[count++] = (char)('0' + (int)(value % 10UL));
            value /= 10UL;
        } while (value != 0UL);
        while (count > 0) Write(digits[--count]);
    }

    public static unsafe void WriteDecimal(int value) {
        if (value < 0) {
            Write('-');
            WriteDecimal((ulong)(-(long)value));
            return;
        }
        WriteDecimal((ulong)value);
    }
    /// <summary>
    /// Write
    /// </summary>
    /// <param name="str"></param>
    public static void WriteLine(string str, bool newLine = true) {
        if (BootConsole.CurrentConsoleMode == BootConsoleMode.None) return;
        if (BootConsole.CurrentMode == BootMode.Legacy) {
            if (newLine) {
                guideXOS.Console.WriteLine(str);
            } else {
                guideXOS.Console.Write(str);
            }
            return;
        }
        if (str != null) {
            for (int i = 0; i < str.Length; i++) {
                WriteSerialChar(str[i]);
            }
        }
        if (newLine) 
            NewLine();
    }
    /// <summary>
    /// New Line
    /// </summary>
    public static void NewLine() {
        WriteSerialChar('\r');
        WriteSerialChar('\n');
    }

    private static void WriteSerialChar(char value) {
        while ((Native.In8(0x3FD) & 0x20) == 0) { }
        Native.Out8(0x3F8, (byte)(value & 0xFF));
    }
}
