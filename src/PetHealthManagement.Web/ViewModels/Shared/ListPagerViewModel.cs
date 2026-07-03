namespace PetHealthManagement.Web.ViewModels.Shared;

public class ListPagerViewModel
{
    public required string AriaLabel { get; init; }

    public required int Page { get; init; }

    public required int TotalPages { get; init; }

    /// <summary>前ページのURL。null なら「前へ」を無効表示にする。</summary>
    public string? PreviousUrl { get; init; }

    /// <summary>次ページのURL。null なら「次へ」を無効表示にする。</summary>
    public string? NextUrl { get; init; }
}
