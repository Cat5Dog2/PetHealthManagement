using Microsoft.AspNetCore.Http;

namespace PetHealthManagement.Web.Infrastructure;

public static class SecurityHeaders
{
    // script-src は inline handler を site.js のイベントデリゲーションへ移行済みのため 'self' のみ。
    // style-src の 'unsafe-inline' は既存 Razor の style 属性が残っている間だけ許可する。
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; " +
        "font-src 'self' data:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public const string PermissionsPolicy = "camera=(), geolocation=(), microphone=()";
    public const string ReferrerPolicy = "strict-origin-when-cross-origin";

    public static void Apply(IHeaderDictionary headers)
    {
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Referrer-Policy"] = ReferrerPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers["X-Frame-Options"] = "DENY";
    }
}
