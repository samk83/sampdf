using DrawingReview.Engine.MuPdf;
using DrawingReview.TestData;
var S = "/tmp/claude-0/-home-user-sampdf/56d3a45e-0a94-5bdf-a2d2-18809b498e99/scratchpad";
var bytes = VirtualDrawings.CreatePlanSet();
File.WriteAllBytes(S + "/planset.pdf", bytes);
Console.WriteLine($"size {bytes.Length}");
using var doc = new MuPdfEngine().Open(bytes);
for (int i=0;i<doc.PageCount;i++){ var info = doc.GetPageInfo(i); Console.WriteLine(info);
  File.WriteAllBytes(S + $"/p{i}.png", doc.RenderPagePng(i, 600.0/Math.Max(info.Width, info.Height)));
  foreach (var m in doc.GetMarkups(i)) Console.WriteLine("  " + m);
  Console.WriteLine("  search TRUSS: " + string.Join(", ", doc.SearchText(i, "TRUSS")));
}
var d = VirtualDrawings.CreateDenseVectorSheet();
Console.WriteLine($"dense size {d.Length}");
