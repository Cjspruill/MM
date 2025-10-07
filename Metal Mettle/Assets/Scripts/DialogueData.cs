using UnityEngine;

[CreateAssetMenu(fileName = "DialogueData", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public TextAsset jsonFile;
    private DialogueDatabase database;

    public void LoadDialogue()
    {
        if (jsonFile != null)
        {
            database = JsonUtility.FromJson<DialogueDatabase>(jsonFile.text);
        }
    }

    public DialogueSequence GetSequence(string sequenceId)
    {
        if (database == null)
        {
            LoadDialogue();
        }

        return database?.sequences?.Find(s => s.sequenceId == sequenceId);
    }
}