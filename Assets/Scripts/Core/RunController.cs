using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    public sealed partial class BattleController
    {
        private RunConfigurationData runData;
        private readonly Dictionary<string, string> levelCatalog = new Dictionary<string, string>(StringComparer.Ordinal);
        private BuildLoadout build = new BuildLoadout();
        private LevelEntrySnapshot entry;
        private RewardRandom rewardRandom;
        private PassiveAbility[] rewardChoices = Array.Empty<PassiveAbility>();
        private Rect[] uiBlockingRects = Array.Empty<Rect>();
        private string feedback = "";
        private float feedbackUntil;

        public RunPhase Phase { get; private set; } = RunPhase.MainMenu;
        public bool IsPaused { get; private set; }
        public bool IsPractice { get; private set; }
        public int LevelIndex { get; private set; }
        public int LevelCount => IsPractice || legacyMode ? 1 : runData?.campaignLevels.Length ?? 0;
        public string LevelId => data == null ? "" : data.id ?? "LEGACY";
        public string Feedback => Time.unscaledTime <= feedbackUntil ? feedback : "";
        public BuildLoadout Build => build.Copy();
        public PassiveAbility[] RewardChoices => (PassiveAbility[])rewardChoices.Clone();
        public PracticeDefinition[] Practices => runData == null ? Array.Empty<PracticeDefinition>() : JsonUtility.FromJson<RunConfigurationData>(JsonUtility.ToJson(runData)).practices;
        public bool CanUseActive => Phase == RunPhase.Battle && State == BattleState.Resolving && !IsPaused && TargetsRemaining > 0 && combat != null && combat.ActiveAvailable;
        public int ActiveProjectiles => combat?.ActiveProjectiles ?? 0;
        public event Action<CombatEvent> CombatFeedback;
        public RunConfigurationData RunData => runData;
        public uint RewardRandomState => rewardRandom.State;
        internal LevelData ConfiguredLevel(string id) => LevelConfiguration.Parse(levelCatalog[id]);

        private void LoadRunCatalog()
        {
            runData = RunConfiguration.Parse(runSource.text);
            levelCatalog.Clear();
            if (levelSources == null) throw new InvalidOperationException("Run level sources are unbound.");
            foreach (var source in levelSources)
            {
                if (!source) throw new InvalidOperationException("Null run level source.");
                var level = LevelConfiguration.Parse(source.text);
                if (string.IsNullOrEmpty(level.id) || !levelCatalog.TryAdd(level.id, LevelConfiguration.Serialize(level)))
                    throw new InvalidOperationException("Missing or duplicate level id: " + source.name);
            }
            foreach (string id in runData.campaignLevels)
                if (!levelCatalog.ContainsKey(id)) throw new InvalidOperationException("Unknown campaign level: " + id);
            foreach (var practice in runData.practices)
                if (!levelCatalog.ContainsKey(practice.levelId)) throw new InvalidOperationException("Unknown practice level: " + practice.levelId);
        }
        private void InvalidateWorld()
        {
            Time.timeScale = 1;
            IsPaused = false;
            launcher?.CancelAim();
            combat?.EndShot();
            generation++;
            DamageableBody.ResetSession();
            loaded = null;
            if (world) for (int i = world.childCount - 1; i >= 0; i--)
            {
                var child = world.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            Ammunition = TargetsRemaining = 0;
            rewardChoices = Array.Empty<PassiveAbility>();
            uiBlockingRects = Array.Empty<Rect>();
            feedback = "";
            victoryStarted = -1;
        }
        public void StartCampaign()
        {
            if (runData == null) return;
            InvalidateWorld();
            legacyMode = false;
            IsPractice = false;
            LevelIndex = 0;
            build = new BuildLoadout();
            entry = null;
            rewardRandom = new RewardRandom(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0));
            Phase = RunPhase.ActiveSelection;
            ChangeState(BattleState.Preparing);
        }
        public bool SelectActive(ActiveAbility active)
        {
            if (Phase != RunPhase.ActiveSelection || (active != ActiveAbility.Split && active != ActiveAbility.Dash)) return false;
            build = new BuildLoadout { active = active };
            EnterLevel(runData.campaignLevels[0]);
            return true;
        }
        private void EnterLevel(string id)
        {
            if (!levelCatalog.TryGetValue(id, out string json)) throw new InvalidOperationException("Unknown level: " + id);
            entry = new LevelEntrySnapshot
            {
                levelJson = json, levelIndex = LevelIndex, practice = IsPractice,
                build = build.Copy(), rewardRandomState = rewardRandom.State
            };
            RestoreEntry();
        }
        private void RestoreEntry()
        {
            data = LevelConfiguration.Parse(entry.levelJson);
            LevelIndex = entry.levelIndex;
            IsPractice = entry.practice;
            build = entry.build.Copy();
            rewardRandom = new RewardRandom(entry.rewardRandomState);
            rewardChoices = Array.Empty<PassiveAbility>();
            feedback = "";
            Phase = RunPhase.Battle;
            RebuildCurrentLevel();
            Debug.Log("RUN_ENTRY: " + LevelId + " practice=" + IsPractice + " active=" + build.active + " passives=" + build.passives.Length + " rng=" + rewardRandom.State);
        }
        private void CompleteLevel()
        {
            if (IsPractice) { Phase = RunPhase.PracticeComplete; return; }
            if (LevelIndex == runData.campaignLevels.Length - 1)
            {
                rewardChoices = Array.Empty<PassiveAbility>();
                Phase = RunPhase.RunComplete;
                Debug.Log("RUN_COMPLETE: five stages, passives=" + build.passives.Length);
                return;
            }
            rewardChoices = rewardRandom.DrawThree(build.passives);
            Phase = RunPhase.RewardSelection;
            Debug.Log("RUN_REWARD: after=" + LevelId + " choices=" + string.Join(",", rewardChoices));
        }
        public bool SelectReward(PassiveAbility passive)
        {
            if (Phase != RunPhase.RewardSelection || Array.IndexOf(rewardChoices, passive) < 0 || build.Has(passive)) return false;
            var next = new PassiveAbility[build.passives.Length + 1];
            Array.Copy(build.passives, next, build.passives.Length);
            next[next.Length - 1] = passive;
            build.passives = next;
            rewardChoices = Array.Empty<PassiveAbility>();
            LevelIndex++;
            EnterLevel(runData.campaignLevels[LevelIndex]);
            return true;
        }
        public bool StartPractice(string levelId, ActiveAbility active, PassiveAbility[] passives)
        {
            if (runData == null || Phase != RunPhase.MainMenu || (active != ActiveAbility.Split && active != ActiveAbility.Dash)) return false;
            if (!Array.Exists(runData.practices, p => p.levelId == levelId)) return false;
            passives = passives ?? Array.Empty<PassiveAbility>();
            var unique = new HashSet<PassiveAbility>();
            foreach (var passive in passives)
                if (!Enum.IsDefined(typeof(PassiveAbility), passive) || !unique.Add(passive)) return false;
            InvalidateWorld();
            legacyMode = false;
            IsPractice = true;
            LevelIndex = 0;
            build = new BuildLoadout { active = active, passives = (PassiveAbility[])passives.Clone() };
            rewardRandom = new RewardRandom(1);
            EnterLevel(levelId);
            return true;
        }
        public void RetryLevel()
        {
            if (legacyMode) { RebuildCurrentLevel(); return; }
            if (entry == null || (Phase != RunPhase.Battle && Phase != RunPhase.PracticeComplete)) return;
            RestoreEntry();
        }
        public void RestartRun() => StartCampaign();
        public void SetPaused(bool paused)
        {
            if (Phase != RunPhase.Battle || (State != BattleState.Preparing && State != BattleState.Ready && State != BattleState.Resolving)) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0 : 1;
            launcher?.CancelAim();
        }
        public void ReturnToMenu()
        {
            InvalidateWorld();
            entry = null;
            build = new BuildLoadout();
            IsPractice = false;
            LevelIndex = 0;
            Phase = RunPhase.MainMenu;
            ChangeState(BattleState.Preparing);
        }
        public bool TryUseActive(Vector2 worldTarget)
        {
            if (!CanUseActive) return false;
            bool used = combat.TryUseActive(worldTarget, out string reason);
            if (!used) SetFeedback(reason ?? "当前无法发动主动能力");
            return used;
        }
        private void OnCombatEvent(CombatEvent value)
        {
            if (value.generation != generation || Phase != RunPhase.Battle) return;
            CombatFeedback?.Invoke(value);
        }
        private void SetFeedback(string value) { feedback = value; feedbackUntil = Time.unscaledTime + 3; }
        public void SetUiBlockingRects(Rect[] guiRects) { uiBlockingRects = guiRects ?? Array.Empty<Rect>(); }
        public bool IsPointerBlocked(Vector2 screenPoint)
        {
            if (Phase != RunPhase.Battle || IsPaused || (State != BattleState.Ready && State != BattleState.Resolving)) return true;
            if (screenPoint.x < 0 || screenPoint.y < 0 || screenPoint.x >= Screen.width || screenPoint.y >= Screen.height) return true;
            Vector2 guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            foreach (var rect in uiBlockingRects) if (rect.Contains(guiPoint)) return true;
            return false;
        }
        private void OnDestroy() { Time.timeScale = 1; combat?.EndShot(); }
    }
}
