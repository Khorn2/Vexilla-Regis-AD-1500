using UnityEngine;

[CreateAssetMenu(menuName = "Vexilla/Scenario Selection Data")]
public class ScenarioSelectionData : ScriptableObject
{
    [Header("Identity")]
    public string scenarioId = "scenario_01";
    public string scenarioTitle = "Tytuł scenariusza";

    [Header("Presentation")]
    [TextArea(4, 12)] public string historicalDescription = "Opis historyczny scenariusza.";
    public Sprite scenarioImage;

    [Header("Scene")]
    public string gameplaySceneName = "SampleScene";
}