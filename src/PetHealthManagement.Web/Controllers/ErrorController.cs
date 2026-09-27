using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace PetHealthManagement.Web.Controllers;

// エラーページは元のリクエストの HTTP メソッドのまま再実行される。POST の後でも表示できるよう、
// 状態を変えないこのコントローラーは antiforgery の検証から外す。
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("Error")]
public class ErrorController : Controller
{
    [Route("{statusCode:int}")]
    [SkipStatusCodePages]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        // ステータスコードは引数で受けずにルート値から読む。引数にするとモデルバインドがフォームの本文を読むため、
        // 大きすぎる・壊れた本文の POST の後ではバインドに失敗し、正しいページを出せない（エラーページは本文を読まない）。
        var statusCode = int.TryParse(
            RouteData.Values["statusCode"] as string,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var routeStatusCode)
            ? routeStatusCode
            : 0;
        var resolvedStatusCode = ResolveStatusCode(statusCode);
        if (statusCode == StatusCodes.Status405MethodNotAllowed)
        {
            // 404 として返すため、メソッド不一致を示す Allow ヘッダーは外す
            Response.Headers.Remove(HeaderNames.Allow);
        }

        Response.StatusCode = resolvedStatusCode;

        return View(resolvedStatusCode.ToString());
    }

    // 画面があるのは 400 / 403 / 404 / 429 / 500。それ以外は近い画面にそろえ、4xx を 500 にはしない。
    private static int ResolveStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 or 403 or 404 or 429 or 500 => statusCode,
            // メソッド不一致は存在しない扱いにする。静的ファイルの fallback（GET/HEAD のみ）はビルド出力から起動したとき
            // （ローカル実行・テスト）だけあり、存在しない URL への POST はローカルでだけ、POST 専用の URL への GET は
            // 本番（発行したアプリ）でだけ 405 になる。どちらも同じ 404 にそろえる。
            405 => 404,
            >= 400 and < 500 => 400,
            >= 500 and < 600 => 500,
            _ => 404
        };
    }
}
