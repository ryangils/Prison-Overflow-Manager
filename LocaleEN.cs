using System.Collections.Generic;
using Colossal;

namespace PrisonOverflowManager
{
    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Prison Overflow Manager" },

                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMainGroup), "Options" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kScalingGroup), "Van scaling" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kReleaseGroup), "Early release" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kTimingGroup), "Timing" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatsGroup), "Statistics" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableMod)), "Enable Prison Overflow Manager" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableMod)), "Master switch. When off, prison van capacity returns to vanilla and nobody is released." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableSoftScaling)), "Scale up prison vans during a backlog" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableSoftScaling)), "When sentenced criminals pile up at police stations waiting for transport, temporarily raises the prison van capacity of every prison so more vans are dispatched to collect them. Capacity returns to normal once the backlog clears. Nobody is released." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BacklogThreshold)), "Backlog threshold" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BacklogThreshold)), "How many sentenced criminals must be waiting for a prison van before van capacity is scaled up. Capacity returns to normal once the backlog falls to half this value." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ScaleFactor)), "Scaled capacity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ScaleFactor)), "Prison van capacity while a backlog is being cleared, as a percentage of each prison's normal capacity. 100% disables scaling in practice." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableEarlyRelease)), "Release permanently stuck prisoners (last resort)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableEarlyRelease)), "Off by default. When the city is still backlogged despite scaling, frees sentenced criminals who have been waiting for a prison van longer than the grace period below. They simply stop being criminals and leave the police station, freeing its cells. Use this only for saves where prisoners never get collected — for example a city with no prison at all." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReleaseGraceDays)), "Grace period (in-game days)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ReleaseGraceDays)), "How long a sentenced criminal must be waiting for a prison van before the last-resort release frees them. Counting restarts when a save is loaded." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SweepsPerDay)), "Sweeps per in-game day" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SweepsPerDay)), "How often the mod measures the backlog and adjusts van capacity. Higher values react to a pileup sooner; lower values batch the work up." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CurrentBacklog)), "Criminals awaiting a prison van" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CurrentBacklog)), "How many sentenced criminals are waiting for transport to prison as of the last sweep." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PeakBacklog)), "Peak backlog this session" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PeakBacklog)), "The largest backlog seen since the game was started. Useful for picking a threshold." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ScalingActive)), "Scaling currently active" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ScalingActive)), "Whether prison van capacity is scaled up right now." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TotalReleased)), "Prisoners released this session" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TotalReleased)), "How many stuck criminals the last-resort release has freed since the game was started." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetStatistics)), "Reset statistics" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetStatistics)), "Set the peak backlog and released counters back to zero." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ResetStatistics)), "Reset all counters to zero?" },
            };
        }

        public void Unload()
        {
        }
    }
}
