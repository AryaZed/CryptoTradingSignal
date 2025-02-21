namespace CryptoTradingSignal.Service
{
    public class NotificationService
    {
        public async Task SendNotification(string message)
        {
            Console.WriteLine($"🔔 Notification Sent: {message}");
            await Task.CompletedTask;
        }
    }
}
