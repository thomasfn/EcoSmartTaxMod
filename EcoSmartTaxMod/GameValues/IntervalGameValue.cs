using System;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Shared.Localization;
    using Shared.Networking;

    using Gameplay.Civics.GameValues;
    using Gameplay.Civics.GameValues.Values;

    using Simulation.Time;

    [Eco]
    public abstract class IntervalGameValue : GameValue<float>
    {
        [Eco, LocDescription("If relative, the number of days in the past. If not, the number of days from the beginning.")] public GameValue<float> IntervalStart { get; set; } = MakeGameValue.GameValue(0.0f);
        [Eco, LocDescription("If relative, the number of days in the past. If not, the number of days from the beginning.")] public GameValue<float> IntervalEnd { get; set; } = MakeGameValue.GameValue(0.0f);
        [Eco, LocDescription("If yes, the interval period will be relative to now. If no, they will be relative to day 0.")] public GameValue<bool> IntervalRelative { get; set; } = new Yes();

        protected bool EvaluateInterval(IContextObject action, out Reports.ReportInterval successInterval, out Eval<float> failEval)
        {
            var intervalStart = this.IntervalStart?.Value(action);
            var intervalEnd = this.IntervalEnd?.Value(action);
            var intervalRelative = this.IntervalRelative?.Value(action);

            if (intervalStart?.Val == null)
            {
                successInterval = default;
                failEval = this.FailNullSafe<float, float>(intervalStart, nameof(this.IntervalStart));
                return false;
            }
            if (intervalEnd?.Val == null)
            {
                successInterval = default;
                failEval = this.FailNullSafe<float, float>(intervalEnd, nameof(this.IntervalEnd));
                return false;
            }
            if (intervalRelative?.Val == null)
            {
                successInterval = default;
                failEval = this.FailNullSafe<float, bool>(intervalRelative, nameof(this.IntervalRelative));
                return false;
            }

            failEval = null;
            if (intervalRelative.Val)
            {
                int dayIndex = (int)WorldTime.Day;
                int absStart = (int)(dayIndex - intervalStart.Val + 0.5f);
                int absEnd = (int)(dayIndex - intervalEnd.Val + 0.5f);
                successInterval = new Reports.ReportInterval(Math.Min(absStart, absEnd), Math.Max(absStart, absEnd) + 1);
            }
            else
            {
                successInterval = new Reports.ReportInterval(Math.Min((int)(intervalStart.Val + 0.5f), (int)(intervalEnd.Val + 0.5f)), Math.Max((int)(intervalStart.Val + 0.5f), (int)(intervalEnd.Val + 0.5f)) + 1);
            }
            return true;
        }

        protected LocString DescribeInterval()
        {
            if (IntervalRelative is Yes)
            {
                return DescribeRelativeInterval();
            }
            if (IntervalRelative is No)
            {
                return DescribeAbsoluteInterval();
            }
            return Localizer.Do($"if {IntervalRelative.DescribeNullSafe()} then {DescribeRelativeInterval()} otherwise {DescribeAbsoluteInterval()}");
        }

        protected LocString DescribeAbsoluteInterval()
        {
            var intervalStartDesc = DescribeAbsoluteDay(IntervalStart);
            var intervalEndDesc = DescribeAbsoluteDay(IntervalEnd);
            if (intervalStartDesc == intervalEndDesc)
            {
                return intervalStartDesc;
            }
            return Localizer.Do($"from {intervalStartDesc} to {intervalEndDesc}");
        }

        protected LocString DescribeRelativeInterval()
        {
            var intervalStartDesc = DescribeRelativeDay(IntervalStart);
            var intervalEndDesc = DescribeRelativeDay(IntervalEnd);
            if (intervalStartDesc == intervalEndDesc)
            {
                return intervalStartDesc;
            }
            return Localizer.Do($"from {intervalStartDesc} to {intervalEndDesc}");
        }

        protected LocString DescribeAbsoluteDay(GameValue<float> absoluteDay)
        {
            if (absoluteDay is GameValueWrapper<float> gameValueWrapper)
            {
                switch ((int)(gameValueWrapper.Object + 0.5f))
                {
                    case 0: return Localizer.DoStr("the 1st day");
                    case 1: return Localizer.DoStr("the 2nd day");
                    case 2: return Localizer.DoStr("the 3rd day");
                    default: return Localizer.Do($"the {gameValueWrapper.Object + 1}th day");
                }
            }
            else if (absoluteDay is WorldAgeInDays)
            {
                return Localizer.DoStr("today");
            }
            return Localizer.Do($"the ({absoluteDay.DescribeNullSafe()} + 1)'th day");
        }

        protected LocString DescribeRelativeDay(GameValue<float> relativeDay)
        {
            if (relativeDay is GameValueWrapper<float> gameValueWrapper)
            {
                switch ((int)(gameValueWrapper.Object + 0.5f))
                {
                    case 0: return Localizer.DoStr("today");
                    case 1: return Localizer.DoStr("yesterday");
                    default: return Localizer.Do($"{gameValueWrapper.Object} days ago");
                }
            }
            return Localizer.Do($"{relativeDay.DescribeNullSafe()} days ago");
        }
    }
}
