using FiscalApi.Cli.Http;

namespace FiscalApi.Cli.Output;

/// <summary>Una página de un listado en la terminal: su tabla, o que no hay datos, y en qué página va.</summary>
public static class PagedListView
{
    /// <param name="headers">Los encabezados de la tabla.</param>
    /// <param name="row">Las celdas de cada elemento, en el orden de <paramref name="headers"/>.</param>
    public static void Write<T>(
        TextWriter output, PagedList<T> page, IReadOnlyList<string> headers, Func<T, IReadOnlyList<string?>> row)
    {
        if (page.Items.Count == 0)
        {
            output.WriteLine("Sin datos.");
        }
        else
        {
            TextTable.Write(output, headers, page.Items.Select(row));
        }

        // Una página fuera de rango llega vacía con totalPages = 0 si no hay nada: "de 0" no le dice nada a la persona.
        var footer = $"Página {page.PageNumber} de {Math.Max(page.TotalPages, 1)} ({page.TotalCount} en total).";
        output.WriteLine(page.HasNextPage ? $"{footer} Siguiente: --page {page.PageNumber + 1}" : footer);
    }
}
