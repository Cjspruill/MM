using System.Collections.Generic;

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;
    public float customDelay = -1f;
    public string audioClipName;
}

[System.Serializable]
public class DialogueSequence
{
    public string sequenceId;
    public List<DialogueLine> lines;
}

[System.Serializable]
public class DialogueDatabase
{
    public List<DialogueSequence> sequences;
}