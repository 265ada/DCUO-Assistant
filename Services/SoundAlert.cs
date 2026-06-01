namespace DCUOTracker.Services
{
    // I-1: NAudio removed — Console.Beep is all that was used
    public static class SoundAlert
    {
        public static void Play(string metalType)
        {
            Task.Run(() =>
            {
                try
                {
                    int freq = metalType.ToLowerInvariant() switch
                    {
                        "raw"       => 400,
                        "extracted" => 500,
                        "treated"   => 600,
                        "processed" => 700,
                        "refined"   => 900,
                        "purified"  => 1200,
                        _           => 500
                    };
                    int duration = metalType.ToLowerInvariant() is "refined" or "purified" ? 400 : 200;
                    Console.Beep(freq, duration);
                }
                catch (Exception ex)
                {
                    Logger.Error("SoundAlert.Play", ex); // C-1: log instead of swallow
                }
            });
        }
    }
}
