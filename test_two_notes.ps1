$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class KE2 {
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, [Out] StringBuilder s, int n);
  public const uint KEYEVENTF_KEYUP = 0x0002;
  public const byte VK_LWIN = 0x5B;
  public const byte VK_D = 0x44;
  public static string ClassOf(IntPtr h){ var s=new StringBuilder(256); GetClassNameW(h,s,256); return s.ToString(); }
}
"@

$fg1 = [KE2]::GetForegroundWindow()
Write-Output "Before Win+D: FG=0x$($fg1.ToString('X')) class=$([KE2]::ClassOf($fg1))"

# Send Win+D
[KE2]::keybd_event([KE2]::VK_LWIN, 0, 0, [IntPtr]::Zero)
[KE2]::keybd_event([KE2]::VK_D, 0, 0, [IntPtr]::Zero)
[KE2]::keybd_event([KE2]::VK_D, 0, [KE2]::KEYEVENTF_KEYUP, [IntPtr]::Zero)
[KE2]::keybd_event([KE2]::VK_LWIN, 0, [KE2]::KEYEVENTF_KEYUP, [IntPtr]::Zero)

Start-Sleep -Milliseconds 1500

$fg2 = [KE2]::GetForegroundWindow()
Write-Output "After Win+D:  FG=0x$($fg2.ToString('X')) class=$([KE2]::ClassOf($fg2))"
