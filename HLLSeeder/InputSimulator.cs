using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HLLServerSeeder
{
    public class InputSimulator
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // Constants for keyboard and mouse events
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        // Virtual key codes
        public const byte VK_ESCAPE = 0x1B;
        public const byte VK_RETURN = 0x0D;
        public const byte VK_TAB = 0x09;
        public const byte VK_SPACE = 0x20;
        public const byte VK_BACK = 0x08;
        public const byte VK_UP = 0x26;
        public const byte VK_DOWN = 0x28;
        public const byte VK_LEFT = 0x25;
        public const byte VK_RIGHT = 0x27;
        public const byte VK_F1 = 0x70;
        public const byte VK_F2 = 0x71;
        public const byte VK_F3 = 0x72;
        public const byte VK_F4 = 0x73;
        public const byte VK_F5 = 0x74;
        public const byte VK_F6 = 0x75;
        public const byte VK_F7 = 0x76;

        /// <summary>
        /// Activates a window and brings it to the foreground
        /// </summary>
        /// <param name="processId">Process ID of the window</param>
        /// <returns>True if successful</returns>
        public static bool FocusWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            // Show window and bring to front
            ShowWindow(windowHandle, 9); // SW_RESTORE = 9
            return SetForegroundWindow(windowHandle);
        }

        /// <summary>
        /// Finds a window by partial title
        /// </summary>
        /// <param name="partialTitle">Part of the window title</param>
        /// <returns>Window handle or IntPtr.Zero if not found</returns>
        public static IntPtr FindWindowByPartialTitle(string partialTitle)
        {
            // This is a simplified approach. For real applications, you would
            // need to enumerate all windows and check their titles
            return FindWindow(null, partialTitle);
        }

        /// <summary>
        /// Simulates a key press (down and up)
        /// </summary>
        /// <param name="keyCode">Virtual key code</param>
        public static void PressKey(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        /// <summary>
        /// Simulates holding down a key
        /// </summary>
        /// <param name="keyCode">Virtual key code</param>
        public static void KeyDown(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
        }

        /// <summary>
        /// Simulates releasing a key
        /// </summary>
        /// <param name="keyCode">Virtual key code</param>
        public static void KeyUp(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, IntPtr.Zero);
        }

        /// <summary>
        /// Sets the cursor position
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SetMousePosition(int x, int y)
        {
            SetCursorPos(x, y);
        }

        /// <summary>
        /// Simulates a mouse click at the current position
        /// </summary>
        public static void LeftClick()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        }

        /// <summary>
        /// Simulates a right mouse click at the current position
        /// </summary>
        public static void RightClick()
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
        }

        /// <summary>
        /// Simulates a mouse click at the specified position
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void ClickAt(int x, int y)
        {
            // Save the current cursor position
            GetCursorPos(out POINT originalPos);

            // Move to the target position
            SetCursorPos(x, y);
            Thread.Sleep(50);

            // Perform the click
            LeftClick();

            // Optionally, restore the original position
            //SetCursorPos(originalPos.X, originalPos.Y);
        }

        /// <summary>
        /// Types a string by simulating keyboard input
        /// </summary>
        /// <param name="text">Text to type</param>
        public static void TypeText(string text)
        {
            foreach (char c in text)
            {
                // Convert the character to virtual key code and send it
                short vkey = VkKeyScan(c);
                byte keyCode = (byte)(vkey & 0xFF);
                bool shift = (vkey & 0x100) != 0;

                if (shift) KeyDown(0x10); // VK_SHIFT
                PressKey(keyCode);
                if (shift) KeyUp(0x10);

                Thread.Sleep(30); // Small delay between keystrokes
            }
        }

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        /// <summary>
        /// Delays execution for the specified time
        /// </summary>
        /// <param name="milliseconds">Time to wait in milliseconds</param>
        public static async Task Delay(int milliseconds)
        {
            await Task.Delay(milliseconds);
        }
    }
}