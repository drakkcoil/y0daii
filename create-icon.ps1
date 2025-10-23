# Create a simple icon for the installer
# This is a basic script to create an icon file

Add-Type -AssemblyName System.Drawing

# Create a 32x32 bitmap
$bitmap = New-Object System.Drawing.Bitmap(32, 32)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Set background color (dark blue)
$graphics.Clear([System.Drawing.Color]::FromArgb(33, 150, 243))

# Create a simple IRC-like icon (chat bubble)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 2)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

# Draw a chat bubble
$graphics.FillEllipse($brush, 4, 4, 24, 18)
$graphics.DrawEllipse($pen, 4, 4, 24, 18)

# Draw some text lines to represent chat
$font = New-Object System.Drawing.Font("Arial", 6, [System.Drawing.FontStyle]::Bold)
$graphics.DrawString("IRC", $font, [System.Drawing.Brushes]::DarkBlue, 8, 8)

# Save as ICO file
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$fileStream = [System.IO.File]::Create("icon.ico")
$icon.Save($fileStream)
$fileStream.Close()

# Cleanup
$graphics.Dispose()
$bitmap.Dispose()
$icon.Dispose()

Write-Host "Icon created: icon.ico"
