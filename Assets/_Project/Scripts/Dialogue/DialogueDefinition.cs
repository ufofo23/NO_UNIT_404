using System;
using UnityEngine;
using NO404.Cases;

namespace NO404.Dialogue
{
    public enum DialogueChannel { FaceToFace = 0, Phone = 1, Interphone = 2, Radio = 3 }

    [Serializable]
    public sealed class DialogueChoice
    {
        public string choiceId;
        public string textKey;
        public string nextNodeId;
        [Tooltip("Choice is hidden unless all conditions pass.")]
        public ConditionDefinition[] conditions = new ConditionDefinition[0];
        public ConsequenceDefinition[] consequences = new ConsequenceDefinition[0];
        [Tooltip("Ends the conversation after the consequences are applied.")]
        public bool endsConversation;
    }

    [Serializable]
    public sealed class DialogueNode
    {
        public string nodeId;
        public string speakerKey;
        public string textKey;
        [Tooltip("Voice line id. Empty while the project has no VO (GDD 19.5).")]
        public string voiceId;
        [Tooltip("Auto-advance target when there are no choices. Empty = conversation ends.")]
        public string nextNodeId;
        public DialogueChoice[] choices = new DialogueChoice[0];
        public ConsequenceDefinition[] onEnter = new ConsequenceDefinition[0];
    }

    [CreateAssetMenu(menuName = "NO404/Dialogue/Conversation", fileName = "D_")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        public string conversationId;
        public DialogueChannel channel = DialogueChannel.FaceToFace;
        [Tooltip("Game time multiplier while this conversation is open (GDD 6.2).")]
        public float timeScale = 0.25f;
        [Tooltip("Seconds on the circular timer. 0 = no timer (GDD 16.9).")]
        public float choiceTimeLimit;
        public string startNodeId = "start";
        public DialogueNode[] nodes = new DialogueNode[0];

        public DialogueNode FindNode(string nodeId)
        {
            if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i] != null && nodes[i].nodeId == nodeId) return nodes[i];
            return null;
        }
    }
}
