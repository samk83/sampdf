using System.Globalization;
using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace DrawingReview.TestData;

/// <summary>테스트가 PDF 파일을 직접 다룰 때 쓰는 도우미: 바이트로 쓰기, 암호 걸기, 합치기, 주석 사전 읽기.</summary>
public static class PdfTools
{
    public static byte[] ToBytes(PdfDocument doc)
    {
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    public static PdfDocument Open(byte[] pdf) => PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);

    public static PdfDocument Open(string path) => PdfReader.Open(new MemoryStream(File.ReadAllBytes(path)), PdfDocumentOpenMode.Import);

    /// <summary>PDF에 AES-256 암호를 걸어 저장한다 (사용자 암호 = 열기, 소유자 암호 = 권한).</summary>
    public static void SaveEncrypted(byte[] pdf, string path, string ownerPassword, string userPassword)
    {
        using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
        doc.SecurityHandler.SetEncryptionToV5();
        doc.SecuritySettings.UserPassword = userPassword;
        doc.SecuritySettings.OwnerPassword = ownerPassword;
        doc.Save(path);
    }

    /// <summary>여러 PDF의 모든 페이지를 순서대로 한 문서로 합친다.</summary>
    public static byte[] Merge(params byte[][] pdfs)
    {
        using var merged = new PdfDocument();
        foreach (var bytes in pdfs)
        {
            using var src = Open(bytes);
            for (int i = 0; i < src.PageCount; i++) merged.AddPage(src.Pages[i]);
        }
        return ToBytes(merged);
    }

    /// <summary>페이지의 주석 사전들 (참조는 따라간다).</summary>
    public static IReadOnlyList<PdfDictionary> Annotations(PdfDocument doc, int pageIndex)
    {
        var page = doc.Pages[pageIndex];
        if (!page.Elements.ContainsKey("/Annots") || Resolve(page.Elements["/Annots"]) is not PdfArray annots) return [];
        return annots.Elements.Items.Select(Resolve).OfType<PdfDictionary>().ToList();
    }

    public static PdfItem? Resolve(PdfItem? item) => item is PdfReference r ? r.Value : item;

    /// <summary>
    /// 사전을 사람이 읽는 PDF 문법으로 쓴다: "/Key value" 형태, 중첩 사전·배열 포함, 참조는 "n 0 R".
    /// 다른 뷰어와의 호환성 검사에서 키·값이 있는지 문자열로 확인할 때 쓴다.
    /// </summary>
    public static string Describe(PdfItem? item)
    {
        var sb = new StringBuilder();
        Write(sb, item, 0);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, PdfItem? item, int depth)
    {
        switch (item)
        {
            case null: sb.Append("null"); break;
            case PdfReference r:
                if (depth == 0) Write(sb, r.Value, depth + 1);
                else sb.Append(r.ObjectNumber).Append(" 0 R");
                break;
            case PdfDictionary d:
                sb.Append("<< ");
                foreach (var key in d.Elements.Keys)
                {
                    sb.Append(key).Append(' ');
                    Write(sb, d.Elements[key], depth + 1);
                    sb.Append(' ');
                }
                sb.Append(">>");
                if (d.Stream != null) sb.Append(" stream");
                break;
            case PdfArray a:
                sb.Append('[');
                for (int i = 0; i < a.Elements.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    Write(sb, a.Elements[i], depth + 1);
                }
                sb.Append(']');
                break;
            case PdfName n: sb.Append(n.Value); break;
            case PdfString s: sb.Append('(').Append(s.Value).Append(')'); break;
            case PdfReal real: sb.Append(real.Value.ToString("0.####", CultureInfo.InvariantCulture)); break;
            case PdfInteger i: sb.Append(i.Value); break;
            case PdfBoolean b: sb.Append(b.Value ? "true" : "false"); break;
            default: sb.Append(item.ToString()); break;
        }
    }
}
