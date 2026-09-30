using System;
using System.Collections.Generic;
using NO404.ContentData;
using NO404.Core;

namespace NO404.Evidence
{
    public sealed class EvidenceRuntime
    {
        public readonly EvidenceDefinition Definition;
        public EvidenceSource Source;
        public int AcquiredAtGameSecond;
        /// <summary>Board position. Persisted so the board looks the same after a reload.</summary>
        public float BoardX;
        public float BoardY;
        public bool Placed;

        public EvidenceRuntime(EvidenceDefinition definition) { Definition = definition; }
        public string EvidenceId { get { return Definition != null ? Definition.evidenceId : string.Empty; } }
    }

    public sealed class EvidenceLink
    {
        public string A;
        public string B;
        public EvidenceRelation Relation;
        /// <summary>
        /// Whether the link matches an authored relation rule. GDD 16.13 forbids showing this
        /// as a right/wrong marker - the UI only shows a soft "likely related" icon.
        /// </summary>
        public bool Meaningful;

        public bool Matches(string a, string b)
        {
            return (A == a && B == b) || (A == b && B == a);
        }
    }

    public interface IEvidenceService
    {
        bool Has(string evidenceId);
        EvidenceRuntime Acquire(string evidenceId, EvidenceSource source);
        bool Link(string evidenceA, string evidenceB, EvidenceRelation relation);
    }

    /// <summary>Evidence inventory and board graph (GDD 14).</summary>
    public sealed class EvidenceService : IEvidenceService
    {
        readonly ContentDatabase _content;
        readonly Dictionary<string, EvidenceRuntime> _owned = new Dictionary<string, EvidenceRuntime>();
        readonly List<EvidenceLink> _links = new List<EvidenceLink>();

        public EvidenceService(ContentDatabase content) { _content = content; }

        public IReadOnlyDictionary<string, EvidenceRuntime> Owned { get { return _owned; } }
        public IReadOnlyList<EvidenceLink> Links { get { return _links; } }
        public int Count { get { return _owned.Count; } }

        public bool Has(string evidenceId)
        {
            return !string.IsNullOrEmpty(evidenceId) && _owned.ContainsKey(evidenceId);
        }

        public EvidenceRuntime Get(string evidenceId)
        {
            EvidenceRuntime runtime;
            return _owned.TryGetValue(evidenceId, out runtime) ? runtime : null;
        }

        public EvidenceRuntime Acquire(string evidenceId, EvidenceSource source)
        {
            if (string.IsNullOrEmpty(evidenceId)) return null;

            EvidenceRuntime existing;
            if (_owned.TryGetValue(evidenceId, out existing)) return existing;

            var definition = _content.FindEvidence(evidenceId);
            if (definition == null)
            {
                Log.Error("Evidence", "unknown evidence id: " + evidenceId);
                return null;
            }

            var runtime = new EvidenceRuntime(definition)
            {
                Source = source,
                AcquiredAtGameSecond = ServiceHub.Clock != null ? ServiceHub.Clock.GameSecond : 0
            };
            _owned[evidenceId] = runtime;

            EventBus.Publish(new EvidenceAcquiredEvent(evidenceId));
            EventBus.Publish(new NotificationEvent("ui.notify.evidence_acquired", NotificationSeverity.Info));
            ServiceHub.Analytics.Track(AnalyticsService.Events.EvidenceAcquired, evidenceId);
            Log.Info("Evidence", "acquired " + evidenceId + " from " + source);

            if (definition.archiveCritical)
                ServiceHub.State.AddStat(StatIds.ArchiveIntegrity, 10, "archive_evidence");

            ServiceHub.Cases.NotifyObjective(Cases.ObjectiveType.AcquireEvidence, evidenceId);

            // Acquiring key evidence is an autosave point (GDD 20.17).
            ServiceHub.Save.RequestAutosave(Save.SaveReason.EvidenceAcquired);

            return runtime;
        }

        public bool Link(string evidenceA, string evidenceB, EvidenceRelation relation)
        {
            if (string.IsNullOrEmpty(evidenceA) || string.IsNullOrEmpty(evidenceB) || evidenceA == evidenceB)
                return false;
            if (!Has(evidenceA) || !Has(evidenceB)) return false;

            for (int i = 0; i < _links.Count; i++)
                if (_links[i].Matches(evidenceA, evidenceB) && _links[i].Relation == relation) return false;

            var link = new EvidenceLink
            {
                A = evidenceA,
                B = evidenceB,
                Relation = relation,
                Meaningful = IsMeaningful(evidenceA, evidenceB, relation)
            };
            _links.Add(link);

            EventBus.Publish(new EvidenceLinkedEvent(evidenceA, evidenceB));
            Log.Info("Evidence", "linked " + evidenceA + " <-> " + evidenceB + " as " + relation);
            return true;
        }

        /// <summary>
        /// Removes evidence from the board. Only the night-5 confrontation does this - it is
        /// how being caught costs something without ending the run (GDD 9.6).
        /// </summary>
        public bool Remove(string evidenceId)
        {
            if (!_owned.Remove(evidenceId)) return false;

            for (int i = _links.Count - 1; i >= 0; i--)
                if (_links[i].A == evidenceId || _links[i].B == evidenceId) _links.RemoveAt(i);

            Log.Warn("Evidence", "removed " + evidenceId);
            ServiceHub.Analytics.Track(AnalyticsService.Events.EvidenceMissed, evidenceId);
            return true;
        }

        public bool Unlink(string evidenceA, string evidenceB)
        {
            for (int i = _links.Count - 1; i >= 0; i--)
            {
                if (!_links[i].Matches(evidenceA, evidenceB)) continue;
                _links.RemoveAt(i);
                return true;
            }
            return false;
        }

        bool IsMeaningful(string a, string b, EvidenceRelation relation)
        {
            return HasRule(a, b, relation) || HasRule(b, a, relation);
        }

        bool HasRule(string from, string to, EvidenceRelation relation)
        {
            var definition = _content.FindEvidence(from);
            if (definition == null || definition.validRelations == null) return false;

            for (int i = 0; i < definition.validRelations.Length; i++)
            {
                var rule = definition.validRelations[i];
                if (rule == null) continue;
                if (rule.otherEvidenceId == to && rule.relation == relation) return rule.meaningful;
            }
            return false;
        }

        /// <summary>Evidence the player owns that belongs to a case, for the board's left column.</summary>
        public List<EvidenceRuntime> ForCase(string caseId)
        {
            var result = new List<EvidenceRuntime>();
            foreach (var pair in _owned)
            {
                var def = pair.Value.Definition;
                if (def == null) continue;
                if (string.IsNullOrEmpty(caseId) || def.ownerCaseId == caseId) result.Add(pair.Value);
            }
            return result;
        }

        /// <summary>Number of meaningful links inside a case. Feeds the report grade.</summary>
        public int MeaningfulLinkCount(string caseId)
        {
            int count = 0;
            for (int i = 0; i < _links.Count; i++)
            {
                if (!_links[i].Meaningful) continue;
                var a = _content.FindEvidence(_links[i].A);
                if (a != null && (string.IsNullOrEmpty(caseId) || a.ownerCaseId == caseId)) count++;
            }
            return count;
        }

        public void SetBoardPosition(string evidenceId, float x, float y)
        {
            var runtime = Get(evidenceId);
            if (runtime == null) return;
            runtime.BoardX = x;
            runtime.BoardY = y;
            runtime.Placed = true;
        }

        public void Reset()
        {
            _owned.Clear();
            _links.Clear();
        }

        public void LoadFrom(IEnumerable<Save.EvidenceSaveEntry> owned, IEnumerable<Save.EvidenceLinkSaveEntry> links)
        {
            Reset();
            if (owned != null)
            {
                foreach (var entry in owned)
                {
                    var definition = _content.FindEvidence(entry.evidenceId);
                    if (definition == null) { Log.Warn("Evidence", "save has unknown evidence " + entry.evidenceId); continue; }
                    _owned[entry.evidenceId] = new EvidenceRuntime(definition)
                    {
                        Source = (EvidenceSource)entry.source,
                        AcquiredAtGameSecond = entry.acquiredAt,
                        BoardX = entry.boardX,
                        BoardY = entry.boardY,
                        Placed = entry.placed
                    };
                }
            }

            if (links == null) return;
            foreach (var entry in links)
            {
                if (!Has(entry.a) || !Has(entry.b)) continue;
                var relation = (EvidenceRelation)entry.relation;
                _links.Add(new EvidenceLink
                {
                    A = entry.a, B = entry.b, Relation = relation,
                    Meaningful = IsMeaningful(entry.a, entry.b, relation)
                });
            }
        }
    }
}
