namespace K7.Shared.Enums;

/// <summary>What the video player does when an episode ends and a next one exists.</summary>
public enum NextEpisodeBehavior
{
    /// <summary>Show the next-episode offer and start the next episode after a countdown.</summary>
    PlayAfterCountdown,

    /// <summary>Show the next-episode offer and wait until the user picks an action.</summary>
    WaitForChoice,

    /// <summary>Close the player without offering the next episode.</summary>
    ClosePlayer
}
