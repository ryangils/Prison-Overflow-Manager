using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace PrisonOverflowManager
{
    [FileLocation("ModsSettings/PrisonOverflowManager/PrisonOverflowManager")]
    [SettingsUIGroupOrder(kMainGroup, kScalingGroup, kReleaseGroup, kTimingGroup, kStatsGroup)]
    [SettingsUIShowGroupName(kMainGroup, kScalingGroup, kReleaseGroup, kTimingGroup, kStatsGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "Options";
        public const string kScalingGroup = "Van scaling";
        public const string kReleaseGroup = "Early release";
        public const string kTimingGroup = "Timing";
        public const string kStatsGroup = "Statistics";

        public Setting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUISection(kSection, kMainGroup)]
        public bool EnableMod { get; set; }

        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsModDisabled))]
        [SettingsUISection(kSection, kScalingGroup)]
        public bool EnableSoftScaling { get; set; }

        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsSoftScalingDisabled))]
        [SettingsUISlider(min = 1, max = 200, step = 1, scalarMultiplier = 1, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kScalingGroup)]
        public int BacklogThreshold { get; set; }

        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsSoftScalingDisabled))]
        [SettingsUISlider(min = 100, max = 500, step = 25, scalarMultiplier = 1, unit = Unit.kPercentage)]
        [SettingsUISection(kSection, kScalingGroup)]
        public int ScaleFactor { get; set; }

        private bool m_EnableEarlyRelease;

        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsEarlyReleaseLocked))]
        [SettingsUISection(kSection, kReleaseGroup)]
        public bool EnableEarlyRelease
        {
            // Locked off (always reads false) while the feature is being verified in-game — see
            // PrisonOverflowSystem.EarlyReleaseAvailable. This keeps the toggle unchecked and its
            // dependent grace-days slider disabled even if an older settings file stored it as on.
            get => PrisonOverflowSystem.EarlyReleaseAvailable && m_EnableEarlyRelease;
            set => m_EnableEarlyRelease = value;
        }

        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsEarlyReleaseDisabled))]
        [SettingsUISlider(min = 1, max = 30, step = 1, scalarMultiplier = 1, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kReleaseGroup)]
        public int ReleaseGraceDays { get; set; }

        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsModDisabled))]
        [SettingsUISlider(min = 1, max = PrisonOverflowSystem.kMaxSweepsPerDay, step = 1, scalarMultiplier = 1, unit = Unit.kInteger)]
        [SettingsUISection(kSection, kTimingGroup)]
        public int SweepsPerDay { get; set; }

        [SettingsUISection(kSection, kStatsGroup)]
        public string CurrentBacklog => PrisonOverflowSystem.CurrentBacklog.ToString();

        [SettingsUISection(kSection, kStatsGroup)]
        public string PeakBacklog => PrisonOverflowSystem.PeakBacklog.ToString();

        [SettingsUISection(kSection, kStatsGroup)]
        public string ScalingActive => PrisonOverflowSystem.ScalingActive ? "Yes" : "No";

        [SettingsUISection(kSection, kStatsGroup)]
        public string TotalReleased => PrisonOverflowSystem.TotalReleased.ToString();

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kStatsGroup)]
        public bool ResetStatistics
        {
            set { PrisonOverflowSystem.ResetStatistics(); }
        }

        public bool IsModDisabled() => !EnableMod;

        public bool IsSoftScalingDisabled() => !EnableMod || !EnableSoftScaling;

        public bool IsEarlyReleaseDisabled() => !EnableMod || !EnableEarlyRelease;

        // The early-release toggle itself is greyed out whenever the feature is locked (or the mod
        // is off). When PrisonOverflowSystem.EarlyReleaseAvailable is flipped to true this reverts
        // to behaving like every other option — enabled as long as the mod is on.
        public bool IsEarlyReleaseLocked() => !EnableMod || !PrisonOverflowSystem.EarlyReleaseAvailable;

        public sealed override void SetDefaults()
        {
            EnableMod = true;
            EnableSoftScaling = true;
            BacklogThreshold = 10;
            ScaleFactor = 200;
            EnableEarlyRelease = false;
            ReleaseGraceDays = 7;
            SweepsPerDay = 16;
        }
    }
}
