using FiscalApi.Cli.Input;

namespace FiscalApi.Cli.Tests.Input;

public sealed class ConsoleSecretReaderTests
{
    private readonly StringWriter _prompts = new();

    [Fact]
    public async Task Enter_ends_the_value()
    {
        var value = await ReadAsync("sk_test_abc\rresto-que-no-se-lee");

        Assert.Equal("sk_test_abc", value);
    }

    [Fact]
    public async Task Backspace_removes_the_last_character_and_does_nothing_on_an_empty_value()
    {
        var value = await ReadAsync("\b\bsk_test_abX\bc\r");

        Assert.Equal("sk_test_abc", value);
    }

    [Fact]
    public async Task Control_characters_other_than_enter_and_backspace_are_ignored()
    {
        var value = await ReadAsync("sk_\ttest\r");

        Assert.Equal("sk_test", value);
    }

    [Fact]
    public async Task Only_the_prompt_and_the_line_break_are_written_never_the_secret()
    {
        await ReadAsync("sk_test_secreto\r");

        Assert.Equal("API key: " + Environment.NewLine, _prompts.ToString());
    }

    [Fact]
    public async Task Cancellation_while_waiting_for_a_key_ends_the_read()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var reader = new ConsoleSecretReader(_prompts, new ScriptedKeys("").TryRead);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync("API key: ", cancellation.Token));
    }

    private Task<string?> ReadAsync(string typed) =>
        new ConsoleSecretReader(_prompts, new ScriptedKeys(typed).TryRead).ReadAsync("API key: ", CancellationToken.None);
}
