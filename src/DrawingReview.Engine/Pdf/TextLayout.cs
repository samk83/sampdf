namespace DrawingReview.Engine.Pdf;

/// <summary>글상자 글자를 줄로 나누고(자동 줄바꿈), 줄마다 영문(Helvetica)·그 밖(CID 글꼴) 구간으로 나눈다.</summary>
internal static class TextLayout
{
    /// <summary>한 줄의 연속 구간. Latin이면 Helvetica, 아니면 한글 CID 글꼴로 쓴다.</summary>
    public sealed record Run(string Text, bool Latin)
    {
        public double Width(double fontSize) => StandardFonts.Width(Text) * fontSize;
    }

    public const double LineHeightFactor = 1.2;

    /// <summary>줄 목록. maxWidth가 0 이하면 줄바꿈 없이 줄바꿈 문자에서만 나눈다.</summary>
    public static List<List<Run>> Layout(string text, double fontSize, double maxWidth)
    {
        var lines = new List<List<Run>>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            foreach (var line in Wrap(paragraph, fontSize, maxWidth))
                lines.Add(SplitRuns(line));
        }
        return lines;
    }

    public static double Width(IEnumerable<Run> line, double fontSize) => line.Sum(r => r.Width(fontSize));

    private static IEnumerable<string> Wrap(string paragraph, double fontSize, double maxWidth)
    {
        if (maxWidth <= 0 || StandardFonts.Width(paragraph) * fontSize <= maxWidth)
        {
            yield return paragraph;
            yield break;
        }

        var current = "";
        foreach (var word in paragraph.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (StandardFonts.Width(candidate) * fontSize <= maxWidth)
            {
                current = candidate;
                continue;
            }
            if (current.Length > 0) { yield return current; current = ""; }
            // 한 단어가 한 줄보다 길면 글자 단위로 자른다
            var piece = "";
            foreach (var ch in word)
            {
                if (piece.Length > 0 && StandardFonts.Width(piece + ch) * fontSize > maxWidth)
                {
                    yield return piece;
                    piece = "";
                }
                piece += ch;
            }
            current = piece;
        }
        yield return current;
    }

    private static List<Run> SplitRuns(string line)
    {
        var runs = new List<Run>();
        if (line.Length == 0) return runs;
        var start = 0;
        var latin = StandardFonts.IsLatin(line[0]);
        for (int i = 1; i <= line.Length; i++)
        {
            if (i == line.Length || StandardFonts.IsLatin(line[i]) != latin)
            {
                runs.Add(new Run(line[start..i], latin));
                if (i < line.Length) { start = i; latin = !latin; }
            }
        }
        return runs;
    }
}
