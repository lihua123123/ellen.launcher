Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

$exe = Join-Path $PSScriptRoot "..\..\src\Ellen.Launcher\Ellen.Launcher\bin\Release\net10.0-windows10.0.26100.0\win-x64\Ellen.Launcher.exe"
$exe = (Resolve-Path $exe).Path
Write-Host "EXE=$exe"

Get-Process Ellen.Launcher -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$p = Start-Process $exe -PassThru
$null = $p.WaitForInputIdle(30000)
Start-Sleep -Seconds 12

$p.Refresh()
$hwnd = $p.MainWindowHandle
Write-Host "HWND=$hwnd"
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "NO_WINDOW"; $p.Kill(); exit 1 }

$null = [WinCap]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 800

$rect = New-Object WinCap+RECT
$null = [WinCap]::GetWindowRect($hwnd, [ref]$rect)
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Host "WINDOW=${w}x${h}"
if ($w -le 0 -or $h -le 0) { $w = 1500; $h = 1000 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [WinCap]::PrintWindow($hwnd, $hdc, 2)
$g.ReleaseHdc($hdc)
$g.Dispose()

$out = Join-Path $PSScriptRoot "app-shot.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

# 额外裁出详情面板下半部分（原生分辨率，便于看清技能/掉落）
if ($w -gt 1200 -and $h -gt 1000) {
    $cw = [Math]::Min(1160, $w - 500)
    $ch = [Math]::Min(920, $h - 120)
    if ($cw -gt 100 -and $ch -gt 100) {
        $crop = New-Object System.Drawing.Bitmap $cw, $ch
        $g2 = [System.Drawing.Graphics]::FromImage($crop)
        $g2.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $cw, $ch), (New-Object System.Drawing.Rectangle 500, 120, $cw, $ch), [System.Drawing.GraphicsUnit]::Pixel)
        $g2.Dispose()
        $out2 = Join-Path $PSScriptRoot "app-shot-bottom.png"
        $crop.Save($out2, [System.Drawing.Imaging.ImageFormat]::Png)
        $crop.Dispose()
        Write-Host "CROP=$out2"
    }
}

$bmp.Dispose()
Write-Host "PRINTWINDOW=$ok OUT=$out"
Write-Host "ALIVE=$(-not $p.HasExited)"
