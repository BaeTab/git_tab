using FluentAssertions;
using GitTab.App.ViewModels;
using Xunit;

namespace GitTab.App.Tests;

public sealed class UndoPlanTests
{
    [Theory]
    [InlineData("commit: add feature")]
    [InlineData("commit (amend): reword")]
    public void A_commit_is_undone_with_a_soft_reset(string message)
        => UndoPlan.Args(message, "abc123").Should().Equal("reset", "--soft", "abc123");

    [Fact]
    public void A_branch_switch_is_reversed_with_checkout_dash()
        => UndoPlan.Args("checkout: moving from main to feature", "abc123")
            .Should().Equal("checkout", "-");

    [Theory]
    [InlineData("reset: moving to HEAD~1")]
    [InlineData("merge feature: Fast-forward")]
    [InlineData("pull: Fast-forward")]
    [InlineData("rebase (finish): returning to refs/heads/main")]
    [InlineData("cherry-pick: pick a commit")]
    [InlineData("revert: Revert \"oops\"")]
    public void Other_head_moves_restore_with_a_keep_reset(string message)
        => UndoPlan.Args(message, "abc123").Should().Equal("reset", "--keep", "abc123");

    [Theory]
    [InlineData("", "abc123")]
    [InlineData("commit: x", "")]
    public void Nothing_to_undo_when_message_or_previous_is_empty(string message, string previous)
        => UndoPlan.Args(message, previous).Should().BeNull();
}
