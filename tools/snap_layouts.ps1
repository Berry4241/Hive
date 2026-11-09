# Turns Windows' "Snap layouts" (the split options Windows shows when you hover over a window's maximize button) off or on.
# It is the same switch as Settings > System > Multitasking > Snap windows > "Show snap layouts when I hover over a window's maximize button".
# Win+Z and dragging a window to a screen edge keep working either way. Only your own Windows user setting is changed.
param([ValidateSet("off", "on")][string]$State = "off")
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
$before = (Get-ItemProperty -Path $key -Name EnableSnapAssistFlyout -ErrorAction SilentlyContinue).EnableSnapAssistFlyout
Set-ItemProperty -Path $key -Name EnableSnapAssistFlyout -Type DWord -Value ([int]($State -eq "on"))
Add-Type -Namespace HiveSnap -Name U -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, string l, uint f, uint t, out IntPtr r);
'@
$r = [IntPtr]::Zero
[HiveSnap.U]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [IntPtr]::Zero, "TraySettings", 2, 3000, [ref]$r) | Out-Null  # tells Windows the setting changed
$after = (Get-ItemProperty -Path $key -Name EnableSnapAssistFlyout).EnableSnapAssistFlyout
"Snap layouts on hover: " + $(if ($after -eq 1) { "ON" } else { "OFF" }) + " (was " + $(if ($before -eq 0) { "off" } elseif ($before -eq 1) { "on" } else { "Windows' default: on" }) + ")"
"If the menu still shows on hover, sign out and in again (or restart Explorer) once."
