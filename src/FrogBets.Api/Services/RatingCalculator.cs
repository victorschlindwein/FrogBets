namespace FrogBets.Api.Services;

public static class RatingCalculator
{
    public static double Calculate(int kills, int deaths, int assists, double totalDamage, int rounds, double kastPercent)
    {
        double kpr    = (double)kills / rounds;
        double dpr    = (double)deaths / rounds;
        double adr    = totalDamage / rounds;
        double impact = kpr + ((double)assists / rounds * 0.4);

        return 0.0073 * kastPercent
             + 0.3591 * kpr
             + (-0.5329) * dpr
             + 0.2372 * impact
             + 0.0032 * adr
             + 0.1587;
    }
}
