namespace StudioFlow.Core.DTOs.Common;

/// <summary>
/// A single page of a larger result set. Pagination is performed in the database
/// (Skip/Take); this only carries the materialised page plus the paging metadata
/// a client needs to render pager controls (StudioFlow spec §22).
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
