namespace FrogBets.Api.Services;

public interface ISettlementService
{
    /// <summary>
    /// Settles all Active bets for the given market.
    /// If <paramref name="isVoided"/> is true, releases balance to both sides and marks bets as Voided.
    /// Otherwise, credits the winner and marks bets as Settled with the correct BetResult.
    /// Also sets the game status to Finished when all markets are Settled or Voided.
    /// </summary>
    Task SettleMarketAsync(Guid marketId, string winningOption, bool isVoided = false);
}
