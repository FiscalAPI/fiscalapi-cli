using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Output;

public static class PagedListView
{
    public static void Write<T>(
        TextWriter output, PagedList<T> page, IReadOnlyList<string> headers, Func<T, IReadOnlyList<string?>> row)
    {
        if (page.Items.Count == 0)
        {
            output.WriteLine("No data.");
        }
        else
        {
            TextTable.Write(output, headers, page.Items.Select(row));
        }

        var footer = $"Page {page.PageNumber} of {Math.Max(page.TotalPages, 1)} ({page.TotalCount} total).";
        output.WriteLine(page.HasNextPage ? $"{footer} Next: --page {page.PageNumber + 1}" : footer);
    }
}
