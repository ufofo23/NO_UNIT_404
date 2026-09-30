using System;
using System.Collections.Generic;
using NO404.Cases;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Dialogue
{
    public sealed class DialogueLine
    {
        public string SpeakerKey;
        public string TextKey;
        public string NodeId;
        public readonly List<DialogueChoice> Choices = new List<DialogueChoice>();
        public bool HasChoices { get { return Choices.Count > 0; } }
        public float TimeLimit;
    }

    /// <summary>
    /// Node graph runner shared by phone, interphone and face-to-face conversations
    /// (GDD 20.12). Safety rules implemented here: a node without a successor ends the
    /// conversation, and the current node id is part of the save.
    /// </summary>
    public sealed class DialogueService
    {
        readonly ContentDatabase _content;

        public DialogueDefinition Current { get; private set; }
        public DialogueLine CurrentLine { get; private set; }
        public bool IsActive { get { return Current != null; } }

        /// <summary>
        /// True while a conversation important enough to defer urgent cases is open
        /// (GDD 20.16). Interphone chatter does not block; phone and face-to-face do.
        /// </summary>
        public bool IsBlocking
        {
            get { return IsActive && Current.channel != DialogueChannel.Interphone; }
        }

        public event Action<DialogueLine> OnLineChanged;
        public event Action<string> OnConversationEnded;

        public DialogueService(ContentDatabase content) { _content = content; }

        public bool Start(string conversationId)
        {
            var definition = _content.FindDialogue(conversationId);
            if (definition == null)
            {
                Log.Error("Dialogue", "unknown conversation " + conversationId);
                return false;
            }

            Current = definition;
            ServiceHub.Clock.TimeScale = definition.timeScale <= 0f ? 1f : definition.timeScale;
            Log.Info("Dialogue", "start " + conversationId);

            return GoTo(definition.startNodeId);
        }

        /// <summary>
        /// Re-enters a conversation at the exact node stored in a save. onEnter consequences
        /// are skipped so loading never re-applies stat and flag changes the player already got.
        /// </summary>
        public bool Resume(string conversationId, string nodeId)
        {
            var definition = _content.FindDialogue(conversationId);
            if (definition == null)
            {
                Log.Warn("Dialogue", "save references unknown conversation " + conversationId);
                return false;
            }

            Current = definition;
            ServiceHub.Clock.TimeScale = definition.timeScale <= 0f ? 1f : definition.timeScale;

            return GoTo(string.IsNullOrEmpty(nodeId) ? definition.startNodeId : nodeId, applyOnEnter: false);
        }

        bool GoTo(string nodeId, bool applyOnEnter = true)
        {
            if (Current == null) return false;

            var node = Current.FindNode(nodeId);
            if (node == null)
            {
                // Safety net: a dangling link ends the conversation instead of soft-locking.
                Log.Warn("Dialogue", "node not found: " + nodeId + " in " + Current.conversationId);
                End();
                return false;
            }

            if (applyOnEnter) ServiceHub.Cases.ApplyConsequences(node.onEnter);

            var line = new DialogueLine
            {
                SpeakerKey = node.speakerKey,
                TextKey = node.textKey,
                NodeId = node.nodeId,
                TimeLimit = Current.choiceTimeLimit
            };

            if (node.choices != null)
            {
                for (int i = 0; i < node.choices.Length; i++)
                {
                    var choice = node.choices[i];
                    if (choice == null) continue;

                    string reason;
                    if (!ConditionEvaluator.EvaluateAll(choice.conditions, out reason)) continue;
                    line.Choices.Add(choice);
                    if (line.Choices.Count >= 4) break; // GDD 16.9: max four choices
                }
            }

            CurrentLine = line;
            var cb = OnLineChanged;
            if (cb != null) cb(line);

            if (!line.HasChoices && string.IsNullOrEmpty(node.nextNodeId))
            {
                // Terminal node: the view shows it, then calls Advance() to close.
                return true;
            }

            return true;
        }

        /// <summary>Advance a node that has no choices.</summary>
        public void Advance()
        {
            if (Current == null || CurrentLine == null) return;
            if (CurrentLine.HasChoices) return;

            var node = Current.FindNode(CurrentLine.NodeId);
            if (node == null || string.IsNullOrEmpty(node.nextNodeId)) { End(); return; }
            GoTo(node.nextNodeId);
        }

        public void Choose(string choiceId)
        {
            if (Current == null || CurrentLine == null) return;

            DialogueChoice chosen = null;
            for (int i = 0; i < CurrentLine.Choices.Count; i++)
                if (CurrentLine.Choices[i].choiceId == choiceId) { chosen = CurrentLine.Choices[i]; break; }

            if (chosen == null) return;

            ServiceHub.Player.SelectChoice(Current.conversationId, chosen.choiceId);
            ServiceHub.Cases.ApplyConsequences(chosen.consequences);

            if (chosen.endsConversation || string.IsNullOrEmpty(chosen.nextNodeId)) { End(); return; }
            GoTo(chosen.nextNodeId);
        }

        /// <summary>Timeout resolves to "hold", never to an automatic refusal (GDD 16.9).</summary>
        public void Timeout()
        {
            if (Current == null || CurrentLine == null) return;

            for (int i = 0; i < CurrentLine.Choices.Count; i++)
            {
                var choice = CurrentLine.Choices[i];
                if (choice.choiceId != null && choice.choiceId.Contains("hold")) { Choose(choice.choiceId); return; }
            }
            End();
        }

        public void End()
        {
            if (Current == null) return;

            var id = Current.conversationId;
            Current = null;
            CurrentLine = null;
            ServiceHub.Clock.TimeScale = ServiceHub.Player.InPcMode ? 0.65f : 1f;

            // Finishing a conversation satisfies "talk to X" objectives (GDD 20.11).
            ServiceHub.Cases.NotifyObjective(ObjectiveType.CallCharacter, id);

            var cb = OnConversationEnded;
            if (cb != null) cb(id);
            Log.Info("Dialogue", "end " + id);
        }
    }
}
