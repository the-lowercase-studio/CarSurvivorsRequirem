using System;
using System.Runtime.InteropServices;
using Assets.Scripts.GameWindow.Constants;
using UnityEngine;

namespace Assets.Scripts.GameWindow
{
    public class GameWindowCorrectDisplayerOnAwakeForWindows : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private void Awake()
        {
            CenterOnPrimaryMonitor();
        }

        private void CenterOnPrimaryMonitor()
        {
            IntPtr hWnd = GetActiveWindow();
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            RECT rect;
            GetWindowRect(hWnd, out rect);
            int windowWidth = rect.Right - rect.Left;
            int windowHeight = rect.Bottom - rect.Top;

            int screenWidth = Display.displays[0].systemWidth;
            int screenHeight = Display.displays[0].systemHeight;

            int x = (screenWidth - windowWidth) / 2;
            int y = (screenHeight - windowHeight) / 2;

            SetWindowPos(hWnd, IntPtr.Zero, x, y, windowWidth, windowHeight, GameWindowConstants.SWP_NOZORDER | GameWindowConstants.SWP_NOACTIVATE);
        }
#endif
    }
}