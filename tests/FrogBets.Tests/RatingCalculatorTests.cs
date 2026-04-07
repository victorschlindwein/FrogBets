using FrogBets.Api.Services;

namespace FrogBets.Tests;

public class RatingCalculatorTests
{
    // ── Fórmula HLTV 2.0 adaptada ────────────────────────────────────────────

    [Fact]
    public void Calculate_KnownValues_ReturnsExpectedRating()
    {
        // 20 kills, 15 deaths, 5 assists, 2000 damage, 25 rounds, 75% KAST
        double rating = RatingCalculator.Calculate(20, 15, 5, 2000, 25, 75);

        double kpr    = 20.0 / 25;
        double dpr    = 15.0 / 25;
        double adr    = 2000.0 / 25;
        double impact = kpr + (5.0 / 25 * 0.4);
        double expected = 0.0073 * 75 + 0.3591 * kpr + (-0.5329) * dpr
                        + 0.2372 * impact + 0.0032 * adr + 0.1587;

        Assert.Equal(expected, rating, precision: 9);
    }

    [Fact]
    public void Calculate_ZeroKills_DoesNotThrow()
    {
        var rating = RatingCalculator.Calculate(0, 10, 0, 500, 20, 50);
        Assert.True(double.IsFinite(rating));
    }

    [Fact]
    public void Calculate_HighKills_ProducesHigherRatingThanLowKills()
    {
        var high = RatingCalculator.Calculate(30, 5, 5, 3000, 20, 90);
        var low  = RatingCalculator.Calculate(5, 20, 1, 500, 20, 40);
        Assert.True(high > low);
    }

    [Fact]
    public void Calculate_SameInputs_AlwaysReturnsSameResult()
    {
        var r1 = RatingCalculator.Calculate(15, 10, 3, 1500, 20, 70);
        var r2 = RatingCalculator.Calculate(15, 10, 3, 1500, 20, 70);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void Calculate_MaxKast_ProducesFiniteResult()
    {
        var rating = RatingCalculator.Calculate(20, 5, 5, 2000, 20, 100);
        Assert.True(double.IsFinite(rating));
    }

    [Fact]
    public void Calculate_ZeroKast_ProducesFiniteResult()
    {
        var rating = RatingCalculator.Calculate(10, 10, 2, 1000, 20, 0);
        Assert.True(double.IsFinite(rating));
    }
}
