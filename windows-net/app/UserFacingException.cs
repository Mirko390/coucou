namespace Coucou;

/// <summary>
/// An error whose message is meant for the person using Coucou: it travels to
/// the page as-is and is shown in the island or the settings window.
/// </summary>
sealed class UserFacingException(string message) : Exception(message);
