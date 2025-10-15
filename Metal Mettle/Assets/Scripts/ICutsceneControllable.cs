/// <summary>
/// Interface for scripts that need to be disabled/enabled during cutscenes.
/// Implement this on your player control scripts to properly handle Input System.
/// </summary>
public interface ICutsceneControllable
{
    /// <summary>
    /// Called when cutscene starts - disable your controls here
    /// </summary>
    void OnCutsceneStart();

    /// <summary>
    /// Called when cutscene ends - re-enable your controls here
    /// </summary>
    void OnCutsceneEnd();
}