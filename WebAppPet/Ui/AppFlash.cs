using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WebAppPet.Ui;

public static class AppFlash
{
    public const string ToastKey = "AppToast";
    public const string ToastKindKey = "AppToastKind";

    public static void Toast(ITempDataDictionary tempData, string message, string kind = "success")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        tempData[ToastKey] = message;
        tempData[ToastKindKey] = kind;
    }

    public static void Toast(PageModel page, string message, string kind = "success")
        => Toast(page.TempData, message, kind);
}
