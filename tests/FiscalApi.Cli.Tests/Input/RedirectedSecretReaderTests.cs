using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Tests.Input;

/// <summary>Stdin redirigido: un valor por línea.</summary>
public sealed class RedirectedSecretReaderTests
{
    [Fact]
    public async Task Byte_order_mark_at_the_start_of_the_input_is_removed()
    {
        // Un archivo guardado como "UTF-8 con BOM" (p. ej. por el Bloc de notas) y luego redirigido.
        var reader = new RedirectedSecretReader(new StringReader("﻿sk_test_con-bom\ntenant\n"));

        Assert.Equal("sk_test_con-bom", await reader.ReadAsync("API key: ", CancellationToken.None));
        Assert.Equal("tenant", await reader.ReadAsync("Tenant key: ", CancellationToken.None));
    }

    [Fact]
    public async Task Only_the_first_line_can_carry_the_byte_order_mark()
    {
        var reader = new RedirectedSecretReader(new StringReader("sk_test\n﻿tenant\n"));

        await reader.ReadAsync("API key: ", CancellationToken.None);

        Assert.Equal("﻿tenant", await reader.ReadAsync("Tenant key: ", CancellationToken.None));
    }

    [Fact]
    public async Task End_of_input_reads_as_null()
    {
        var reader = new RedirectedSecretReader(new StringReader(""));

        Assert.Null(await reader.ReadAsync("API key: ", CancellationToken.None));
    }
}
