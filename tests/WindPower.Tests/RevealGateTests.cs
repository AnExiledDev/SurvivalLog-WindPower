using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

/// <summary>The turbine recipes are checked when the game may have shown a recipe, not on a timer.</summary>
public class RevealGateTests
{
    [Fact]
    public void The_first_tick_checks_and_later_ticks_with_no_wake_do_not()
    {
        var gate = new RevealGate(5);

        Assert.True(gate.IsDue(0, 0, false));
        Assert.False(gate.IsDue(10, 0, false));
        Assert.False(gate.IsDue(100, 0, false));
    }

    [Fact]
    public void A_wake_checks_once()
    {
        var gate = new RevealGate(5);

        gate.IsDue(0, 0, false);

        Assert.True(gate.IsDue(1, 1, false));
        Assert.False(gate.IsDue(2, 1, false));
    }

    [Fact]
    public void A_new_list_of_recipes_checks_again_without_a_wake()
    {
        var gate = new RevealGate(5);

        gate.IsDue(0, 0, false);
        gate.Again();

        Assert.True(gate.IsDue(1, 0, false));
        Assert.False(gate.IsDue(2, 0, false));
    }

    [Fact]
    public void With_a_hook_missing_it_checks_every_few_seconds()
    {
        var gate = new RevealGate(5);

        Assert.True(gate.IsDue(0, 0, true));
        Assert.False(gate.IsDue(4, 0, true));
        Assert.True(gate.IsDue(5, 0, true));
        Assert.False(gate.IsDue(9, 0, true));
    }
}
