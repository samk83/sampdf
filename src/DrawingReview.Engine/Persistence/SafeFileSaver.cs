namespace DrawingReview.Engine.Persistence;

/// <summary>
/// "임시 파일 작성 → 검증 → 교체" 순서로 저장해서, 저장 도중 실패해도 기존 파일을 지킨다.
/// PDF 엔진과 무관한 공통 기능이라 다른 파일 형식(프로젝트 데이터 등)에도 재사용한다.
/// </summary>
public static class SafeFileSaver
{
    /// <param name="targetPath">최종 저장 경로.</param>
    /// <param name="writeTemp">임시 파일 경로를 받아 내용을 쓰는 함수.</param>
    /// <param name="verifyTemp">임시 파일이 올바른지 검사. 문제가 있으면 예외를 던진다.</param>
    public static void Save(string targetPath, Action<string> writeTemp, Action<string> verifyTemp)
    {
        var fullTarget = Path.GetFullPath(targetPath);
        var dir = Path.GetDirectoryName(fullTarget)!;
        Directory.CreateDirectory(dir);

        // 같은 폴더에 만들어야 마지막 교체가 같은 드라이브 안의 이동(원자적 교체)이 된다.
        var temp = Path.Combine(dir, $".{Path.GetFileName(fullTarget)}.{Guid.NewGuid():N}.saving");
        try
        {
            writeTemp(temp);
            if (!File.Exists(temp) || new FileInfo(temp).Length == 0)
                throw new IOException("Temporary save file was not written.");
            verifyTemp(temp);
            File.Move(temp, fullTarget, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* 임시 파일 정리 실패는 원본에 영향이 없으므로 무시 */ }
        catch (UnauthorizedAccessException) { }
    }
}
