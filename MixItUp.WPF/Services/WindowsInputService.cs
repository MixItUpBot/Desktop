using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace MixItUp.WPF.Services
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MousePoint
    {
        public int X;
        public int Y;

        public static implicit operator Point(MousePoint point)
        {
            return new Point(point.X, point.Y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct InputContainer
    {
        public uint Type;
        public GenericInput Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct GenericInput
    {
        [FieldOffset(0)]
        public HardwareInput Hardware;
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareInput
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public class WindowsInputService : IMessageFilter, IInputService, IDisposable
    {
        private const int INPUT_KEYBOARD = 1;

        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int KEYEVENTF_SCANCODE = 0x0008;

        private const int WINDOWS_MESSAGE_HOTKEY = 0x0312;

        private const int MAPVK_VK_TO_VSC_EX = 4;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint SendInput(uint numberOfInputs, InputContainer[] inputs, int sizeOfInputStructure);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetCursorPos(out MousePoint lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyA(uint uCode, uint uMapType);

        private static HashSet<VirtualKeyEnum> extendedVirtualKeys = new HashSet<VirtualKeyEnum>()
        {
            VirtualKeyEnum.UpArrow, VirtualKeyEnum.DownArrow, VirtualKeyEnum.LeftArrow, VirtualKeyEnum.RightArrow,
            VirtualKeyEnum.Insert, VirtualKeyEnum.Delete, VirtualKeyEnum.Home, VirtualKeyEnum.End,
            VirtualKeyEnum.PageUp, VirtualKeyEnum.PageDown
        };

        private static HashSet<VirtualKeyEnum> keybdEventVirtualKeys = new HashSet<VirtualKeyEnum>()
        {
            VirtualKeyEnum.VolumeDown, VirtualKeyEnum.VolumeUp, VirtualKeyEnum.VolumeMute,
            VirtualKeyEnum.MediaPrevious, VirtualKeyEnum.MediaNext, VirtualKeyEnum.MediaPlayPause, VirtualKeyEnum.MediaStop
        };

        public event EventHandler<HotKey> HotKeyPressed;

        private IntPtr windowHandle;
        private HwndSource windowSource;

        private readonly Dictionary<int, HotKeyConfiguration> registeredHotKeys = new Dictionary<int, HotKeyConfiguration>();
        private readonly HashSet<int> heldHotKeys = new HashSet<int>();

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage, Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public uint Type, Size;
            public IntPtr Device, WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawKeyboardInput
        {
            public RawInputHeader Header;
            public ushort MakeCode, Flags, Reserved, VirtualKey;
            public uint Message, ExtraInformation;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr handle, uint command, out RawKeyboardInput data, ref uint size, uint headerSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        private bool ObserveKeyboard(bool enable)
        {
            var device = new RawInputDevice { UsagePage = 1, Usage = 6, Flags = enable ? 0x100u : 1u, Target = enable ? this.windowHandle : IntPtr.Zero };
            return RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RawInputDevice>());
        }

        public void Initialize(IntPtr windowHandle)
        {
            if (this.windowSource != null) { this.ReleaseHotKeys(); }
            this.windowHandle = windowHandle;
            this.windowSource = HwndSource.FromHwnd(this.windowHandle);
            this.windowSource.AddHook(HwndHook);

            System.Windows.Forms.Application.AddMessageFilter(this);
        }

        public void KeyDown(VirtualKeyEnum key)
        {
            if (keybdEventVirtualKeys.Contains(key))
            {
                keybd_event((byte)key, (byte)this.ConvertVirtualKeyToScanCode(key), KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            }
            else
            {
                InputContainer input = new InputContainer { Type = INPUT_KEYBOARD };
                input.Data.Keyboard = new KeyboardInput() { Scan = (ushort)this.ConvertVirtualKeyToScanCode(key), Flags = KEYEVENTF_SCANCODE };
                if (extendedVirtualKeys.Contains(key))
                {
                    input.Data.Keyboard.Flags |= KEYEVENTF_EXTENDEDKEY;
                }
                InputContainer[] inputs = new InputContainer[] { input };
                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(InputContainer)));
            }
        }

        public void KeyUp(VirtualKeyEnum key)
        {
            if (keybdEventVirtualKeys.Contains(key))
            {
                keybd_event((byte)key, (byte)this.ConvertVirtualKeyToScanCode(key), KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, IntPtr.Zero);
            }
            else
            {
                InputContainer input = new InputContainer { Type = INPUT_KEYBOARD };
                input.Data.Keyboard = new KeyboardInput() { Scan = (ushort)this.ConvertVirtualKeyToScanCode(key), Flags = KEYEVENTF_KEYUP | KEYEVENTF_SCANCODE };
                if (extendedVirtualKeys.Contains(key))
                {
                    input.Data.Keyboard.Flags |= KEYEVENTF_EXTENDEDKEY;
                }
                InputContainer[] inputs = new InputContainer[] { input };
                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(InputContainer)));
            }
        }

        public async Task KeyClick(VirtualKeyEnum key)
        {
            this.KeyDown(key);
            await this.WaitForKeyToRegister();
            this.KeyUp(key);
        }

        public void MouseEvent(InputMouseEnum mouse)
        {
            mouse_event((int)mouse, 0, 0, 0, (IntPtr)0);
        }

        public async Task LeftMouseClick()
        {
            this.MouseEvent(InputMouseEnum.LeftDown);
            await this.WaitForKeyToRegister();
            this.MouseEvent(InputMouseEnum.LeftUp);
        }

        public async Task RightMouseClick()
        {
            this.MouseEvent(InputMouseEnum.RightDown);
            await this.WaitForKeyToRegister();
            this.MouseEvent(InputMouseEnum.RightUp);
        }

        public async Task MiddleMouseClick()
        {
            this.MouseEvent(InputMouseEnum.MiddleDown);
            await this.WaitForKeyToRegister();
            this.MouseEvent(InputMouseEnum.MiddleUp);
        }

        public void MoveMouse(int xDelta, int yDelta)
        {
            mouse_event((int)InputMouseEnum.Move, xDelta, yDelta, 0, (IntPtr)0);
        }

        public async Task WaitForKeyToRegister()
        {
            await Task.Delay(250);
        }

        public bool IsHotKeyRegistered(HotKey key) => this.registeredHotKeys.ContainsKey(this.GetHotKeyID(key.Modifiers, key.VirtualKey));

        public bool RegisterHotKey(HotKeyConfiguration key)
        {
            if (this.windowHandle == IntPtr.Zero || key.VirtualKey == VirtualKeyEnum.F12) { return false; }
            int id = this.GetHotKeyID(key.Modifiers, key.VirtualKey);
            this.registeredHotKeys.TryGetValue(id, out var previous);
            bool exclusive = key.Trigger == HotKeyTrigger.HotKeyPress;
            bool wasExclusive = previous?.Trigger == HotKeyTrigger.HotKeyPress;
            if (exclusive && !wasExclusive && !RegisterHotKey(this.windowHandle, id, (uint)key.Modifiers | 0x4000, (uint)key.VirtualKey)) { return false; }
            if (!exclusive && !this.registeredHotKeys.Values.Any(k => k.Trigger != HotKeyTrigger.HotKeyPress) && !this.ObserveKeyboard(true)) { return false; }
            if (!exclusive && wasExclusive && !UnregisterHotKey(this.windowHandle, id)) { return false; }
            this.registeredHotKeys[id] = key;
            this.heldHotKeys.Remove(id);
            if (exclusive && previous != null && !wasExclusive && this.registeredHotKeys.Values.All(k => k.Trigger == HotKeyTrigger.HotKeyPress))
            {
                this.ObserveKeyboard(false);
            }
            return true;
        }

        public bool UnregisterHotKey(HotKeyModifiersEnum modifiers, VirtualKeyEnum key)
        {
            int id = this.GetHotKeyID(modifiers, key);
            if (!this.registeredHotKeys.TryGetValue(id, out var binding)) { return false; }
            if (binding.Trigger == HotKeyTrigger.HotKeyPress && !UnregisterHotKey(this.windowHandle, id)) { return false; }
            this.registeredHotKeys.Remove(id);
            this.heldHotKeys.Remove(id);
            if (binding.Trigger != HotKeyTrigger.HotKeyPress && this.registeredHotKeys.Values.All(k => k.Trigger == HotKeyTrigger.HotKeyPress))
            {
                this.ObserveKeyboard(false);
            }
            return true;
        }

        private void HandleKeyTransition(IntPtr packet)
        {
            uint size = (uint)Marshal.SizeOf<RawKeyboardInput>();
            if (GetRawInputData(packet, 0x10000003, out var input, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) == uint.MaxValue || input.Header.Type != 1) { return; }
            bool released = (input.Flags & 1) != 0;
            foreach (var entry in this.registeredHotKeys.Where(pair => pair.Value.Trigger != HotKeyTrigger.HotKeyPress && (ushort)pair.Value.VirtualKey == input.VirtualKey).ToArray())
            {
                if (released)
                {
                    if (this.heldHotKeys.Remove(entry.Key) && entry.Value.Trigger == HotKeyTrigger.HotKeyUp) { this.HotKeyPressed?.Invoke(this, entry.Value); }
                }
                else
                {
                    var modifiers = HotKeyModifiersEnum.None;
                    if (GetAsyncKeyState(0x10) < 0) { modifiers |= HotKeyModifiersEnum.Shift; }
                    if (GetAsyncKeyState(0x11) < 0) { modifiers |= HotKeyModifiersEnum.Control; }
                    if (GetAsyncKeyState(0x12) < 0) { modifiers |= HotKeyModifiersEnum.Alt; }
                    if (GetAsyncKeyState(0x5B) < 0 || GetAsyncKeyState(0x5C) < 0) { modifiers |= HotKeyModifiersEnum.Windows; }
                    if (modifiers == entry.Value.Modifiers && this.heldHotKeys.Add(entry.Key) && entry.Value.Trigger == HotKeyTrigger.HotKeyDown)
                    {
                        this.HotKeyPressed?.Invoke(this, entry.Value);
                    }
                }
            }
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg == WINDOWS_MESSAGE_HOTKEY && message.HWnd == this.windowHandle)
            {
                int hotKeyID = message.WParam.ToInt32();
                if (this.registeredHotKeys.ContainsKey(hotKeyID))
                {
                    return true;
                }
            }
            return false;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (msg == 0x00FF) { this.HandleKeyTransition(lParam); }
                if (msg == WINDOWS_MESSAGE_HOTKEY && this.registeredHotKeys.TryGetValue(wParam.ToInt32(), out var binding) && binding.Trigger == HotKeyTrigger.HotKeyPress)
                {
                    this.HotKeyPressed?.Invoke(this, binding);
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return IntPtr.Zero;
        }

        private MousePoint GetMousePosition()
        {
            MousePoint point;
            GetCursorPos(out point);
            return point;
        }

        private int GetHotKeyID(HotKeyModifiersEnum modifiers, VirtualKeyEnum key) { return (((int)key) << 16) | (int)modifiers; }

        private uint ConvertVirtualKeyToScanCode(VirtualKeyEnum virtualKey) { return MapVirtualKeyA((uint)virtualKey, MAPVK_VK_TO_VSC_EX); }

        private void ReleaseHotKeys()
        {
            foreach (var key in this.registeredHotKeys.Values.ToList()) { this.UnregisterHotKey(key.Modifiers, key.VirtualKey); }
            this.registeredHotKeys.Clear();
            this.heldHotKeys.Clear();
            this.windowSource?.RemoveHook(HwndHook);
            System.Windows.Forms.Application.RemoveMessageFilter(this);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.ReleaseHotKeys();
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
