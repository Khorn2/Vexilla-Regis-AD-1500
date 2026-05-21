public static class SelectedScenario
{
    public static ScenarioSelectionData CurrentScenario { get; private set; }

    public static void SetScenario(ScenarioSelectionData scenario)
    {
        CurrentScenario = scenario;
    }

    public static void Clear()
    {
        CurrentScenario = null;
    }

    public static string GetGameplaySceneName(string fallbackSceneName)
    {
        if (CurrentScenario == null)
            return fallbackSceneName;

        if (string.IsNullOrWhiteSpace(CurrentScenario.gameplaySceneName))
            return fallbackSceneName;

        return CurrentScenario.gameplaySceneName;
    }
}