using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DrawingReview.Engine.MuPdf;

/// <summary>
/// MuPDF.NET 3.28.2.4의 Windows 한글 깨짐 보정.
///
/// 원인: MuPDF 본체(C)가 글자를 C#으로 돌려줄 때 쓰는 SWIG 문자열 콜백(SWIGStringDelegate)에
/// 인코딩 지정이 없다. 그래서 .NET 기본값(Windows: 시스템 코드 페이지 CP949, Linux: UTF-8)으로 읽는다.
/// 본체는 UTF-8을 보내므로 Linux에서는 맞고, 한국어 Windows에서는 마크업 내용·작성자·책갈피 제목 등의
/// 한글이 "?��?"처럼 깨진다.
///
/// 보정: 같은 등록 함수(SWIGRegisterStringCallback_mupdf)로 UTF-8을 명시한 콜백을 다시 등록한다.
/// 본체는 마지막에 등록한 콜백 하나만 쓴다. 돌려주는 문자열은 CoTaskMem으로 할당한다
/// (원래 콜백의 반환 마샬링과 같고, 받는 쪽 P/Invoke가 LPUTF8Str로 해제한다).
///
/// MuPDF.NET 내부 구조에 기대므로 버전을 올리면 다시 확인한다 (테스트: MuPdfUtf8FixTests).
/// </summary>
internal static class MuPdfUtf8Fix
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr Utf8StringCallback(IntPtr utf8);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void RegisterCallback(IntPtr callback);

    // 본체가 함수 포인터를 계속 쓰므로 GC가 지우지 않도록 붙잡아 둔다
    private static Utf8StringCallback? _callback;
    private static readonly object Gate = new();
    private static int _calls;

    /// <summary>보정을 적용했는지.</summary>
    public static bool Applied { get; private set; }

    /// <summary>적용하지 못했을 때 이유 (진단용).</summary>
    public static string? Error { get; private set; }

    /// <summary>보정 콜백이 불린 횟수 (진단·테스트용).</summary>
    public static int Calls => Volatile.Read(ref _calls);

    /// <summary>한 번만 적용한다. 여러 번 불러도 안전하다.</summary>
    public static void Ensure()
    {
        if (Applied || Error != null) return;
        lock (Gate)
        {
            if (Applied || Error != null) return;
            try
            {
                var muAssembly = typeof(MuPDF.NET.Document).Assembly;
                // SWIG의 원래 콜백 등록(정적 생성자)이 먼저 끝나야 우리 등록이 마지막이 된다
                var pinvoke = muAssembly.GetType("mupdf.mupdfPINVOKE", throwOnError: true)!;
                RuntimeHelpers.RunClassConstructor(pinvoke.TypeHandle);
                var helper = muAssembly.GetType("mupdf.mupdfPINVOKE+SWIGStringHelper", throwOnError: true)!;
                RuntimeHelpers.RunClassConstructor(helper.TypeHandle);

                // MuPDF.NET이 쓰는 것과 같은 방식으로 원본 라이브러리를 찾는다
                var import = helper.GetMethod("SWIGRegisterStringCallback_mupdf", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetCustomAttribute<DllImportAttribute>()
                    ?? throw new InvalidOperationException("SWIG string callback registration was not found.");
                var library = NativeLibrary.Load(import.Value, muAssembly, null);
                var register = Marshal.GetDelegateForFunctionPointer<RegisterCallback>(
                    NativeLibrary.GetExport(library, import.EntryPoint ?? "SWIGRegisterStringCallback_mupdf"));

                _callback = CreateString;
                register(Marshal.GetFunctionPointerForDelegate(_callback));
                Applied = true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }
    }

    private static IntPtr CreateString(IntPtr utf8)
    {
        Interlocked.Increment(ref _calls);
        var text = Marshal.PtrToStringUTF8(utf8) ?? "";
        return Marshal.StringToCoTaskMemUTF8(text);
    }
}
