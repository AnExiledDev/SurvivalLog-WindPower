using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class PageWatchTests
{
    private const double Frame = 0.1;

    [Fact]
    public void The_first_step_checks_the_page()
    {
        Assert.Equal(PageSend.Check, new PageWatch().Step(0, null).Send);
    }

    [Fact]
    public void Nothing_more_is_sent_while_an_answer_is_awaited()
    {
        var watch = new PageWatch();

        watch.Step(0, null);

        Assert.Equal(PageSend.Nothing, watch.Step(1, null).Send);
        Assert.Equal(PageSend.Nothing, watch.Step(PageWatch.AnswerWaitSeconds - Frame, null).Send);
    }

    [Fact]
    public void The_page_is_checked_again_two_seconds_after_it_answers()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        watch.Step(Frame, PageAnswer.Ok);

        Assert.Equal(PageSend.Nothing, watch.Step(Frame + PageWatch.CheckSeconds - Frame, null).Send);
        Assert.Equal(PageSend.Check, watch.Step(Frame + PageWatch.CheckSeconds, null).Send);
    }

    [Fact]
    public void A_missing_script_is_sent_on_the_very_next_step()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        watch.Step(Frame, PageAnswer.Missing);

        Assert.Equal(PageSend.Install, watch.Step(2 * Frame, null).Send);
    }

    [Fact]
    public void Once_the_script_is_in_the_page_the_watch_goes_back_to_checking()
    {
        var watch = InstallingWatch(out var now);

        watch.Step(now + Frame, PageAnswer.Ok);

        Assert.Equal(PageSend.Check, watch.Step(now + Frame + PageWatch.CheckSeconds, null).Send);
    }

    /// <summary>The script answering at all proves it arrived; sending it again would not bring the weather tooltip.</summary>
    [Theory]
    [InlineData(PageAnswer.NoTooltip)]
    public void An_install_answered_with_nothing_to_draw_on_is_not_sent_again(string answer)
    {
        var watch = InstallingWatch(out var now);

        var installed = watch.Step(now + Frame, answer);
        var sends = AnswerEverySend(watch, now + 2 * Frame, now + PageWatch.InstallRetrySeconds + 2 * PageWatch.CheckSeconds, answer);

        Assert.DoesNotContain("failed", installed.LogLine ?? "");
        Assert.DoesNotContain(sends, sent => sent.Send == PageSend.Install);
        Assert.Contains(sends, sent => sent.Send == PageSend.Check);
    }

    [Fact]
    public void The_same_answer_is_logged_once()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        var first = watch.Step(Frame, PageAnswer.Ok).LogLine;
        watch.Step(Frame + PageWatch.CheckSeconds, null);
        var second = watch.Step(2 * Frame + PageWatch.CheckSeconds, PageAnswer.Ok).LogLine;

        Assert.Equal("the wind row is in the weather tooltip", first);
        Assert.Null(second);
    }

    [Fact]
    public void A_failed_install_is_not_tried_again_for_thirty_seconds()
    {
        var watch = InstallingWatch(out var now);
        var failedAt = now + Frame;

        watch.Step(failedAt, "error: SyntaxError: Unexpected token");

        var sends = AnswerEverySend(watch, failedAt + Frame, failedAt + PageWatch.InstallRetrySeconds + PageWatch.CheckSeconds, PageAnswer.Missing);
        var retry = sends.First(sent => sent.Send == PageSend.Install);

        Assert.True(retry.At >= failedAt + PageWatch.InstallRetrySeconds, $"the script was sent again {retry.At - failedAt:0.0}s after it failed");
        Assert.Contains(sends, sent => sent.Send == PageSend.Check && sent.At < retry.At);
    }

    [Fact]
    public void An_install_the_web_view_could_not_take_is_sent_again_without_the_long_wait()
    {
        var watch = InstallingWatch(out var now);

        watch.NotSent(now);

        var sends = AnswerEverySend(watch, now + Frame, now + PageWatch.CheckSeconds + 1, PageAnswer.Missing);

        Assert.Contains(sends, sent => sent.Send == PageSend.Install);
    }

    /// <summary>A scene change between the check and the install takes the page away; the script did not fail, so there is no long wait.</summary>
    [Fact]
    public void An_install_that_finds_no_page_is_sent_again_as_soon_as_the_page_is_back()
    {
        var watch = InstallingWatch(out var now);
        var answeredAt = now + Frame;

        var answer = watch.Step(answeredAt, PageAnswer.NoPage);

        var sends = AnswerEverySend(watch, answeredAt + Frame, answeredAt + PageWatch.InstallRetrySeconds + PageWatch.CheckSeconds, PageAnswer.Missing);
        var retry = sends.FirstOrDefault(sent => sent.Send == PageSend.Install);

        Assert.Equal(PageSend.Install, retry.Send);
        Assert.True(retry.At < answeredAt + PageWatch.CheckSeconds + 1, $"the script was sent again {retry.At - answeredAt:0.0}s after the page went away");
        Assert.DoesNotContain("failed", answer.LogLine ?? "");
    }

    [Fact]
    public void Each_different_reason_the_web_view_refused_a_call_is_logged_once()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        var first = watch.RunFailed(0, "the view is disposed");
        watch.Step(PageWatch.CheckSeconds, null);
        var repeated = watch.RunFailed(PageWatch.CheckSeconds, "the view is disposed");
        watch.Step(2 * PageWatch.CheckSeconds, null);
        var different = watch.RunFailed(2 * PageWatch.CheckSeconds, "no main frame");

        Assert.Contains("the view is disposed", first);
        Assert.Null(repeated);
        Assert.Contains("no main frame", different);
    }

    [Fact]
    public void A_call_the_web_view_refused_is_tried_again_after_the_check_interval()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        watch.RunFailed(0, "the view is disposed");

        Assert.Equal(PageSend.Nothing, watch.Step(PageWatch.CheckSeconds - Frame, null).Send);
        Assert.Equal(PageSend.Check, watch.Step(PageWatch.CheckSeconds, null).Send);
    }

    [Fact]
    public void A_repeated_answer_is_logged_only_the_first_time()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        var first = watch.Step(Frame, PageAnswer.NoPage).LogLine;
        watch.Step(Frame + PageWatch.CheckSeconds, null);
        var second = watch.Step(2 * Frame + PageWatch.CheckSeconds, PageAnswer.NoPage).LogLine;

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void An_answer_the_watch_does_not_know_is_logged_as_it_came()
    {
        var watch = new PageWatch();

        watch.Step(0, null);

        Assert.Contains("refresh failed: boom", watch.Step(Frame, "refresh failed: boom").LogLine);
    }

    [Fact]
    public void A_call_with_no_answer_is_given_up_after_the_wait_and_checking_carries_on()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        var givenUp = watch.Step(PageWatch.AnswerWaitSeconds, null);

        Assert.NotNull(givenUp.LogLine);
        Assert.Equal(PageSend.Check, watch.Step(PageWatch.AnswerWaitSeconds + PageWatch.CheckSeconds, null).Send);
    }

    [Fact]
    public void An_answer_that_comes_after_the_wait_is_not_taken_for_the_next_call()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        watch.Step(PageWatch.AnswerWaitSeconds, null);
        watch.Step(PageWatch.AnswerWaitSeconds + Frame, PageAnswer.Missing);

        Assert.Equal(PageSend.Check, watch.Step(PageWatch.AnswerWaitSeconds + PageWatch.CheckSeconds, null).Send);
    }

    [Fact]
    public void A_check_the_web_view_could_not_take_is_tried_again_after_the_check_interval()
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        watch.NotSent(0);

        Assert.Equal(PageSend.Nothing, watch.Step(PageWatch.CheckSeconds - Frame, null).Send);
        Assert.Equal(PageSend.Check, watch.Step(PageWatch.CheckSeconds, null).Send);
    }

    /// <summary>A watch that has just sent the script, with the time it sent it.</summary>
    private static PageWatch InstallingWatch(out double now)
    {
        var watch = new PageWatch();

        watch.Step(0, null);
        watch.Step(Frame, PageAnswer.Missing);
        now = 2 * Frame;

        Assert.Equal(PageSend.Install, watch.Step(now, null).Send);

        return watch;
    }

    /// <summary>Steps frame by frame with a page that gives the same answer to every call on the frame after it; returns what was sent, and when.</summary>
    private static List<(double At, PageSend Send)> AnswerEverySend(PageWatch watch, double from, double to, string answer)
    {
        var sends = new List<(double At, PageSend Send)>();
        string? reply = null;

        for (var now = from; now <= to; now += Frame)
        {
            var turn = watch.Step(now, reply);
            reply = turn.Send == PageSend.Nothing ? null : answer;

            if (turn.Send != PageSend.Nothing) sends.Add((now, turn.Send));
        }

        return sends;
    }
}
