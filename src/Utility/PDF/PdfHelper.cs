using FMO.Models;
using LiteDB;
using PDFiumSharp;
using PDFiumSharp.Enums;
using System.Formats.Asn1;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#if TARGET_NET9_WINDOWS
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif

namespace FMO.PDF;

public static class PdfHelper
{

    /// <summary>
    /// 获取股卡信息
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static (string text, byte[] page)[]? GetSecurityAccounts(Stream s)
    {
        List<(string text, byte[] page)> result = new();


        {
            List<string> strings = new List<string>();

            byte[] buf = new byte[s.Length];
            s.Seek(0, SeekOrigin.Begin);
#pragma warning disable CA2022 // 避免使用 "Stream.Read" 进行不准确读取
            s.Read(buf, 0, buf.Length);
#pragma warning restore CA2022 // 避免使用 "Stream.Read" 进行不准确读取
            var d = PDFium.FPDF_LoadDocument(buf);
            var cnt = PDFium.FPDF_GetPageCount(d);


            for (int i = 0; i < cnt; i++)
            {
                StringBuilder sb = new();
                var page = PDFium.FPDF_LoadPage(d, i);
                var textpage = PDFium.FPDFText_LoadPage(page);
                var charcnt = PDFium.FPDFText_CountChars(textpage);
                var rotate = PDFium.FPDFPage_GetRotation(page);

                var str = PDFium.FPDFText_GetText(textpage, 0, charcnt);

                str = Regex.Replace(str, @"(客户名称：.*?)\r\n(.*?)客户类型", "$1$2\n客户类型", RegexOptions.Singleline);
                str = Regex.Replace(str, @"(客户名称)(.*?)(客户)", match =>
                 {
                     string middle = match.Groups[2].Value;
                     string cleaned = Regex.Replace(middle, @"\s+", "");
                     return $"{ match.Groups[1].Value}{ cleaned}\n{ match.Groups[3].Value}";
                 });



                PDFium.FPDFText_ClosePage(textpage);
                PDFium.FPDF_ClosePage(page);


                var newdoc = PDFium.FPDF_CreateNewDocument();
                PDFium.FPDF_ImportPages(newdoc, d, 0, [i]);
                using var ms = new MemoryStream();
                PDFium.FPDF_SaveAsCopy(newdoc, ms, SaveFlags.None);
                PDFium.FPDF_CloseDocument(newdoc);

                //var ss = sb.ToString();
                result.Add((str, ms.ToArray()));
            }


            PDFium.FPDF_CloseDocument(d);
        }


        return result.ToArray();//doc.GetPages().Select(p => Regex.Replace( string.Join('\n', p.GetWords().Select(x => x.Text)),"\n([^:：]+\n|$)","$1" )).ToArray();
    }






    static double IntersectionXRatio(double a, double b, double c, double d)
    {
        // 找出两个区间左端点的最大值
        double leftMax = Math.Max(a, c);
        // 找出两个区间右端点的最小值
        double rightMin = Math.Min(b, d);

        // 判断是否存在交集
        if (leftMax < rightMin)
            return (rightMin - leftMax) / (Math.Max(b, d) - Math.Min(a, c));
        return 0;
    }

    /// <summary>
    /// t<b      有相交 > 0
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="b1"></param>
    /// <param name="t2"></param>
    /// <param name="b2"></param>
    /// <returns></returns>
    static double IntersectionYRatio(double t1, double b1, double t2, double b2)
    {
        // 1 包含 2
        if (t1 <= t2 && b1 >= b2) return 1;
        return (Math.Min(b1, b2) - Math.Max(t1, t2)) / (Math.Max(b1, b2) - Math.Min(t1, t2));
    }


    private static void Rotate(PageOrientations o, ref double l, ref double t, ref double r, ref double b)
    {
        switch (o)
        {
            case PageOrientations.Rotated90CW:
                {
                    var tmp = b;
                    b = r;
                    r = t;
                    t = l;
                    l = tmp;
                }
                break;
            case PageOrientations.Rotated180:
                {
                    var tmp = l;
                    l = r;
                    r = tmp;
                    tmp = t;
                    t = b;
                    b = tmp;
                }
                break;
            case PageOrientations.Rotated90CCW:
                {
                    var tmp = l;
                    l = t;
                    t = r;
                    r = b;
                    b = tmp;
                }
                break;
        }
    }




    public static MemoryStream ExportToPdf(string[] images)
    {
        var doc = PDFium.FPDF_CreateNewDocument();
        int index = 0;
        List<PDFiumSharp.Types.FPDF_PAGE> pages = new();
        foreach (var f in images)
        {
            using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var img = PDFium.FPDFPageObj_NewImageObj(doc);
            var page = PDFium.FPDFPage_New(doc, index++, 595, 841);
            pages.Add(page);
            PDFium.FPDFImageObj_LoadJpegFile(pages.ToArray(), img, fs);
            PDFium.FPDFImageObj_GetImagePixelSize(img, out uint width, out uint height);
            PDFium.FPDFImageObj_SetMatrix(img, 595, 0, 0, 841, 0, 0);
            PDFium.FPDFPage_InsertObject(page, ref img);
            PDFium.FPDFPage_GenerateContent(page);
        }
        var ms = new MemoryStream();
        PDFium.FPDF_SaveAsCopy(doc, ms, PDFiumSharp.Enums.SaveFlags.None);

        PDFium.FPDF_CloseDocument(doc);
        return ms;
    }


    public static BankAccount[]? GetAccountInfo(Stream s)
    {
        List<BankAccount> result = new();


        {
            List<string> strings = new List<string>();

            byte[] buf = new byte[s.Length];
            s.Seek(0, SeekOrigin.Begin);
#pragma warning disable CA2022 // 避免使用 "Stream.Read" 进行不准确读取
            s.Read(buf, 0, buf.Length);
#pragma warning restore CA2022 // 避免使用 "Stream.Read" 进行不准确读取
            var d = PDFium.FPDF_LoadDocument(buf);
            var cnt = PDFium.FPDF_GetPageCount(d);


            for (int i = 0; i < cnt; i++)
            {
                var page = PDFium.FPDF_LoadPage(d, i);
                var textpage = PDFium.FPDFText_LoadPage(page);
                var charcnt = PDFium.FPDFText_CountChars(textpage);
                if (charcnt == 0) continue;

                var rotate = PDFium.FPDFPage_GetRotation(page);

                var str = PDFium.FPDFText_GetText(textpage, 0, charcnt);

                // 获取字的位置，并排列 
                double[] l = new double[charcnt], r = new double[charcnt], t = new double[charcnt], b = new double[charcnt];
                Parallel.For(0, charcnt, j =>
                {
                    // pdf 坐标从下往上，交换t b
                    PDFium.FPDFText_GetCharBox(textpage, j, out l[j], out r[j], out t[j], out b[j]);
                    Rotate(rotate, ref l[j], ref b[j], ref r[j], ref t[j]);
                });

                // 分析行
                List<Rect> line = new();
                int[] lineId = new int[charcnt];
                line.Add(new Rect(l[0], t[0], r[0] - l[0], b[0] - t[0]));
                for (int j = 1; j < charcnt; j++)
                {
                    bool find = false;
                    // 找同行
                    for (int k = 0; k < line.Count; k++)
                    {
                        if (IntersectionYRatio(line[k].Top, line[k].Bottom, t[j], b[j]) > 0.6)
                        {
                            var nt = Math.Min(line[k].Top, t[j]);
                            line[k] = line[k] with { Y = nt, Height = Math.Max(line[k].Bottom, b[j]) - nt };
                            lineId[j] = k;
                            find = true;
                            break;
                        }
                    }

                    if (!find && r[j] > l[j] && b[j] > t[j])
                    {
                        line.Add(new Rect(l[j], t[j], r[j] - l[j], b[j] - t[j]));
                        lineId[j] = line.Count - 1;
                    }
                }

                // 对齐
                string[] strs = new string[line.Count];
                for (int j = 0; j < line.Count; j++)
                {
                    var sel = lineId.Index().Where(x => x.Item == j).Select(x => x.Index);

                    // 字符顺序
                    var idx = l.Index().Where(x => sel.Contains(x.Index)).OrderBy(x => x.Item).Select(x => x.Index);

                    strs[j] = new string(idx.Select(x => str[x]).ToArray());
                }

                var ac = BankAccount.FromString(string.Join('\n', strs));

                if (ac is not null)
                    result.Add(ac);

                PDFium.FPDFText_ClosePage(textpage);
                PDFium.FPDF_ClosePage(page);
            }


            PDFium.FPDF_CloseDocument(d);
        }


        return result.ToArray();
    }

    internal static MemoryStream? GenThumbnail(MemoryStream? stream)
    {
        if (stream is null) return null;
        var pdf = new PdfDocument(stream.ToArray());
        var wb = new WriteableBitmap(200, 283, 96, 96, PixelFormats.Bgr32, null);
        pdf.Pages[0].Render(wb);
        wb.Freeze();

        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(wb));
        var ms = new MemoryStream();
        encoder.Save(ms);
        return ms;
    }


    /// <summary>
    /// pdf 坐标 t > b
    /// </summary>
    /// <param name="rc"></param>
    /// <param name="l"></param>
    /// <param name="t"></param>
    /// <param name="r"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    static double IntersectionRatio(Rect rc, double l, double t, double r, double b)
    {
        // 判断是否存在交集
        if (rc.Left > r) return 0;
        if (rc.Right < l) return 0;
        if (rc.Top < b) return 0;
        if (rc.Bottom > t) return 0;

        // 存在交集 
        l = Math.Max(rc.Left, l);
        r = Math.Min(rc.Right, r);
        t = Math.Min(rc.Top, t);
        b = Math.Max(rc.Bottom, b);

        return (r - l) * (t - b) / rc.Width / rc.Height;
    }

    internal static WriteableBitmap Render(string path)
    {
        using PDFiumSharp.PdfDocument doc = new PDFiumSharp.PdfDocument(path);
        int pagecnt = doc.Pages.Count;

        var rects = doc.Pages.Select(x => new Rect(0, 0, x.Width, x.Height)).ToArray();
        for (int i = 1; i < rects.Length; i++)
            rects[i].Offset(0, rects[i - 1].Bottom);

        var wb = new WriteableBitmap((int)rects.Max(x => x.Width), (int)rects[^1].Bottom, 72, 72, PixelFormats.Bgr32, null);
        int stride = wb.Format.BitsPerPixel / 8;
        var buf = new byte[wb.PixelWidth * wb.PixelHeight * stride];
        Array.Fill<byte>(buf, 0xff);
        wb.WritePixels(new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight), buf, wb.PixelWidth * stride, 0);


        for (int i = 0; i < rects.Length; i++)
        {
            var page = doc.Pages[i];
            page.Render(wb, rects[i], PDFiumSharp.Enums.PageOrientations.Normal, PDFiumSharp.Enums.RenderingFlags.None);
        }
        return wb;
    }
    /// <summary>
    /// Renders the page to a <see cref="WriteableBitmap"/>
    /// </summary>
    /// <param name="page">The page which is to be rendered.</param>
    /// <param name="renderTarget">The bitmap to which the page is to be rendered.</param>
    /// <param name="rectDest">The destination rectangle in <paramref name="renderTarget"/>.</param>
    /// <param name="orientation">The orientation at which the page is to be rendered.</param>
    /// <param name="flags">The flags specifying how the page is to be rendered.</param>
    public static void Render(this PdfPage page, WriteableBitmap renderTarget, (int left, int top, int width, int height) rectDest, PageOrientations orientation = PageOrientations.Normal, RenderingFlags flags = RenderingFlags.None)
    {
        if (renderTarget == null)
            throw new ArgumentNullException(nameof(renderTarget));

        if (rectDest.left >= renderTarget.PixelWidth || rectDest.top >= renderTarget.PixelHeight)
            return;

        var bitmapFormat = GetBitmapFormat(renderTarget.Format);
        renderTarget.Lock();
        using (var tmpBitmap = new PDFiumBitmap(renderTarget.PixelWidth, renderTarget.PixelHeight, bitmapFormat, renderTarget.BackBuffer, renderTarget.BackBufferStride))
        {
            page.Render(tmpBitmap, rectDest, orientation, flags);
        }

        if (rectDest.left < 0)
        {
            rectDest.width += rectDest.left;
            rectDest.left = 0;
        }
        if (rectDest.top < 0)
        {
            rectDest.height += rectDest.top;
            rectDest.top = 0;
        }
        rectDest.width = Math.Min(rectDest.width, renderTarget.PixelWidth);
        rectDest.height = Math.Min(rectDest.height, renderTarget.PixelHeight);
        renderTarget.AddDirtyRect(new Int32Rect(rectDest.left, rectDest.top, rectDest.width, rectDest.height));
        renderTarget.Unlock();
    }

    /// <summary>
    /// Renders the page to a <see cref="WriteableBitmap"/>
    /// </summary>
    /// <param name="page">The page which is to be rendered.</param>
    /// <param name="renderTarget">The bitmap to which the page is to be rendered.</param>
    /// <param name="orientation">The orientation at which the page is to be rendered.</param>
    /// <param name="flags">The flags specifying how the page is to be rendered.</param>
    public static void Render(this PdfPage page, WriteableBitmap renderTarget, PageOrientations orientation = PageOrientations.Normal, RenderingFlags flags = RenderingFlags.None)
    {
        page.Render(renderTarget, (0, 0, renderTarget.PixelWidth, renderTarget.PixelHeight), orientation, flags);
    }

    public static void Render(this PdfPage page, WriteableBitmap renderTarget, Rect rect, PageOrientations orientation = PageOrientations.Normal, RenderingFlags flags = RenderingFlags.None)
    {
        page.Render(renderTarget, ((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height), orientation, flags);
    }

    static BitmapFormats GetBitmapFormat(PixelFormat pixelFormat)
    {
        if (pixelFormat == PixelFormats.Bgra32)
            return BitmapFormats.BGRA;
        if (pixelFormat == PixelFormats.Bgr32)
            return BitmapFormats.BGRx;
        if (pixelFormat == PixelFormats.Bgr24)
            return BitmapFormats.BGR;
        if (pixelFormat == PixelFormats.Gray8)
            return BitmapFormats.Gray;
        throw new NotSupportedException($"Pixel format {pixelFormat} is not supported.");
    }


    public static string[] GetTexts(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return [];

        List<string> strings = [];
        using var fs = new FileStream(file, FileMode.Open);
        return GetTexts(fs);
    }

    public static string[] GetTexts(Stream? fs)
    {
        if (fs is null) return [];
        if (!fs.CanSeek)
        {
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return GetTexts(ms);
        }

        fs.Seek(0, SeekOrigin.Begin);
        List<string> strings = [];
        byte[] buf = new byte[fs.Length];
        fs.ReadExactly(buf);
        var d = PDFium.FPDF_LoadDocument(buf);
        var cnt = PDFium.FPDF_GetPageCount(d);

        for (int i = 0; i < cnt; i++)
        {
            var page = PDFium.FPDF_LoadPage(d, i);
            var textpage = PDFium.FPDFText_LoadPage(page);
            var charcnt = PDFium.FPDFText_CountChars(textpage);
            if (charcnt == 0) continue;

            var str = PDFium.FPDFText_GetText(textpage, 0, charcnt);

            strings.Add(str);
        }

        PDFium.FPDF_CloseDocument(d);
        return strings.ToArray();
    }

    public static DateOnly? GetSignDate(string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;

        using var fs = new FileStream(file, FileMode.Open);
        return GetSignDate(fs);
    }

    public static DateOnly? GetSignDate(Stream? fs)
    {
        if (fs is null) return null;

        if (!fs.CanSeek)
        {
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return GetSignDate(ms);
        }


        fs.Seek(0, SeekOrigin.Begin);
        byte[] buf = new byte[fs.Length];
        fs.ReadExactly(buf);
        var d = PDFium.FPDF_LoadDocument(buf);

        var cnt = PDFium.FPDF_GetSignatureCount(d);
        if (cnt == 0) return null;

        var sign = PDFium.FPDF_GetSignatureObject(d, 0);

        // 准备缓冲区（必须是 char[]，不能是 StringBuilder）
        var buffer = new byte[1];


        // 调用函数
        ulong written = PDFium.FPDFSignatureObj_GetContents(sign, ref buffer[0], (ulong)buffer.Length);
        buffer = new byte[written];
        PDFium.FPDFSignatureObj_GetContents(sign, ref buffer[0], (ulong)buffer.Length);

        SignedCms cms = new SignedCms();
        cms.Decode(buffer);

        var time = GetSignTime(cms);

        PDFium.FPDF_CloseDocument(d);
        return time is null ? null : DateOnly.FromDateTime(time.Value);
    }


    private static DateTime? GetSignTime(SignedCms cms)
    {
        foreach (var signerInfo in cms.SignerInfos)
        {
            foreach (var attr in signerInfo.SignedAttributes)
            {
                if (attr.Oid?.Value == "1.2.840.113549.1.9.16.2.14") // id-aa-timeStampToken
                {
                    // 提取时间戳令牌
                    foreach (AsnEncodedData asnData in attr.Values)
                    {
                        byte[] tsToken = asnData.RawData; // 正确获取原始数据

                        SignedCms tsCms = new SignedCms();
                        tsCms.Decode(tsToken);

                        foreach (SignerInfo tsSignerInfo in tsCms.SignerInfos)
                        {
                            foreach (var sd in tsSignerInfo.SignedAttributes)
                            {
                                if (sd.Oid?.Value == "1.2.840.113549.1.9.5")
                                {
                                    var asnReader = new AsnReader(sd.Values[0].RawData, AsnEncodingRules.DER);
                                    return asnReader.ReadUtcTime().DateTime.ToLocalTime();
                                }
                            }
                        }
                    }
                }
            }
            foreach (var attr in signerInfo.UnsignedAttributes)
            {
                if (attr.Oid?.Value == "1.2.840.113549.1.9.16.2.14") // id-aa-timeStampToken
                {
                    // 提取时间戳令牌
                    foreach (AsnEncodedData asnData in attr.Values)
                    {
                        byte[] tsToken = asnData.RawData; // 正确获取原始数据

                        SignedCms tsCms = new SignedCms();
                        tsCms.Decode(tsToken);

                        foreach (SignerInfo tsSignerInfo in tsCms.SignerInfos)
                        {
                            foreach (var sd in tsSignerInfo.SignedAttributes)
                            {
                                if (sd.Oid?.Value == "1.2.840.113549.1.9.5")
                                {
                                    var asnReader = new AsnReader(sd.Values[0].RawData, AsnEncodingRules.DER);
                                    return asnReader.ReadUtcTime().DateTime.ToLocalTime();
                                }
                            }
                        }
                    }
                }
            }
        }

        return null;
    }
}