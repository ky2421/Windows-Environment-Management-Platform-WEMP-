Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @('System.Drawing.dll') -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconMaker
{
    public static void Make(string srcPath, string outPath, int[] sizes)
    {
        using (var src = Image.FromFile(srcPath))
        {
            var entries = new List<KeyValuePair<int, byte[]>>();
            foreach (var s in sizes)
            {
                using (var bmp = new Bitmap(s, s))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(src, 0, 0, s, s);
                    }
                    if (s >= 256)
                    {
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            entries.Add(new KeyValuePair<int, byte[]>(s, ms.ToArray()));
                        }
                    }
                    else
                    {
                        using (var ms = new MemoryStream())
                        {
                            using (var bw = new BinaryWriter(ms))
                            {
                                bw.Write(40);          // biSize
                                bw.Write(s);           // biWidth
                                bw.Write(s * 2);       // biHeight (includes AND mask)
                                bw.Write((short)1);    // biPlanes
                                bw.Write((short)32);   // biBitCount
                                bw.Write(0);           // biCompression
                                bw.Write(0);           // biSizeImage
                                bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
                                for (int y = s - 1; y >= 0; y--)
                                {
                                    for (int x = 0; x < s; x++)
                                    {
                                        var c = bmp.GetPixel(x, y);
                                        bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                                    }
                                }
                                int maskRow = (int)Math.Ceiling(s / 8.0);
                                maskRow = (int)Math.Ceiling(maskRow / 4.0) * 4;
                                bw.Write(new byte[maskRow * s]);
                                bw.Flush();
                                entries.Add(new KeyValuePair<int, byte[]>(s, ms.ToArray()));
                            }
                        }
                    }
                }
            }

            using (var fs = File.Create(outPath))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);                          // reserved
                bw.Write((ushort)1);                          // type: icon
                bw.Write((ushort)entries.Count);              // count
                int off = 6 + 16 * entries.Count;
                foreach (var e in entries)
                {
                    int dim = e.Key >= 256 ? 0 : e.Key;
                    bw.Write((byte)dim);
                    bw.Write((byte)dim);
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write((uint)e.Value.Length);
                    bw.Write((uint)off);
                    off += e.Value.Length;
                }
                foreach (var e in entries) bw.Write(e.Value);
            }
        }
    }
}
"@

[IconMaker]::Make('D:\WEMP\img\1318B521A2CDEC7AC817F21BFB7A29D1.png', 'D:\WEMP\src\WEMP.App\Assets\app.ico', @(256, 64, 48, 32, 16))

$info = Get-Item 'D:\WEMP\src\WEMP.App\Assets\app.ico'
Write-Host ("ICO written: {0} bytes" -f $info.Length)

Add-Type -AssemblyName System.Drawing
$ico = New-Object System.Drawing.Icon('D:\WEMP\src\WEMP.App\Assets\app.ico', 32, 32)
$bmp = $ico.ToBitmap()
Write-Host ("app.ico 32x32 loadable: {0}x{1}" -f $bmp.Width, $bmp.Height)
$ico.Dispose()
