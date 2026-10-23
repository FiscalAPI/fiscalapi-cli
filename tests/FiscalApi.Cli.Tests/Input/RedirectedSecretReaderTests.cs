using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Tests.Input;

public sealed class RedirectedSecretReaderTests
{
    [Fact]
    public async Task Byte_order_mark_at_the_start_of_the_input_is_removed()
    {
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
