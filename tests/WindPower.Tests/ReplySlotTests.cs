using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class ReplySlotTests
{
    [Fact]
    public void The_answer_to_the_call_now_out_is_handed_over_once()
    {
        var slot = new ReplySlot();
        var call = slot.Open();

        slot.Offer(call, PageAnswer.Ok);

        Assert.Equal(PageAnswer.Ok, slot.Take());
        Assert.Null(slot.Take());
    }

    /// <summary>A check given up on, then answered "missing" while the install is out, must not be read as the install failing.</summary>
    [Fact]
    public void A_late_answer_to_an_earlier_call_is_not_taken_for_the_call_now_out()
    {
        var slot = new ReplySlot();
        var givenUp = slot.Open();
        var current = slot.Open();

        slot.Offer(givenUp, PageAnswer.Missing);

        Assert.Null(slot.Take());

        slot.Offer(current, PageAnswer.Ok);

        Assert.Equal(PageAnswer.Ok, slot.Take());
    }

    [Fact]
    public void An_answer_nobody_took_is_dropped_when_the_next_call_goes_out()
    {
        var slot = new ReplySlot();

        slot.Offer(slot.Open(), PageAnswer.Missing);
        slot.Open();

        Assert.Null(slot.Take());
    }
}
