using System.ComponentModel;

namespace SugarTalk.Messages.Enums.Meeting;

public enum MeetingPeriodType
{
    [Description("不重复")]
    None,

    [Description("每天")]
    Daily,

    [Description("每个工作日")]
    EveryWeekday,

    [Description("每周")]
    Weekly,

    [Description("每两周")]
    BiWeekly,

    [Description("每月")]
    Monthly
}