namespace FrogBets.Api.Services;

public interface IBalanceService
{
    /// <summary>
    /// Reserves <paramref name="amount"/> from the user's VirtualBalance into ReservedBalance.
    /// Throws <see cref="InvalidOperationException"/> with code "INSUFFICIENT_BALANCE" if VirtualBalance &lt; amount.
    /// </summary>
    Task ReserveBalanceAsync(Guid userId, decimal amount);

    /// <summary>
    /// Releases <paramref name="amount"/> back from ReservedBalance to VirtualBalance (reverts a reservation).
    /// </summary>
    Task ReleaseBalanceAsync(Guid userId, decimal amount);

    /// <summary>
    /// Credits the winner with 2 * <paramref name="amount"/> (VirtualBalance += 2*amount)
    /// and releases the reservation (ReservedBalance -= amount).
    /// </summary>
    Task CreditWinnerAsync(Guid winnerId, decimal amount);
}
